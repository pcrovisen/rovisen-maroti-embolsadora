using log4net;
using mcOMRON;
using ModbusServer.Data;
using ModbusServer.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    internal class PalletLabel1 : Machine
    {
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        enum States
        {
            WaitingPallet,
            WaitUpdate,
            WaitingCorrection,
            Labeling,
            WaitUpdate2,
            WaitAck,
            WaitLeaving,
            WaitLeaveNull,
        }

        OmronPLC plc;
        NetworkPrinter printer;
        OmronConnection omronConnection;
        NetworkPrinterConnection printerConnection;
        PrinterMachine printerMachine;
        string currentCode;
        Task<bool> palletLeaveTask;
        Task writeTask;
        public PalletLabel1() : base(States.WaitingPallet)
        {
            plc = new OmronPLC(TransportType.Tcp);
            printer = new NetworkPrinter("192.168.6.122");
            omronConnection = new OmronConnection(plc, "192.168.6.124");
            printerConnection = new NetworkPrinterConnection(printer);
            printerMachine = new PrinterMachine(plc, printer, "Wolrdjet1");
        }

        public override void Step()
        {
            omronConnection.Step();
            printerConnection.Step();
            Status.Instance.Connections.Labeler1 = printerConnection.Connected;
            if (omronConnection.Connected && printerConnection.Connected)
            {
                switch (State)
                {
                    case States.WaitingPallet:
                        Status.Instance.ErrorMessages.BDC1Error = "";
                        if (FatekPLC.ReadBit(FatekPLC.Signals.Label1))
                        {
                            FatekPLC.ResetBit(FatekPLC.Signals.PalletLeave1);
                            writeTask = Status.UpdateFIFO1();
                            NextState(States.WaitUpdate);
                        }
                        if (FatekPLC.ReadBit(FatekPLC.Signals.PLCLabeling1))
                        {
                            FatekPLC.ResetBit(FatekPLC.Signals.PalletLeave1);
                            currentCode = Status.Instance.Packager1.LabelPallet.Qr;
                            Log.InfoFormat("Continue labeling pallet {0} in bocedi1", currentCode);
                            FatekPLC.SetBit(FatekPLC.Signals.Labeling1);
                            printerMachine.Reset(currentCode, Status.Instance.Packager1.LabelPallet.Labeling, true);
                            Status.Instance.ErrorMessages.BDC1Error = "";
                            NextState(States.Labeling);
                        }
                        if (FatekPLC.ReadBit(FatekPLC.Signals.WaitCorrection1))
                        {
                            _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.desorden_embolsadora, packager: 1);
                            Log.Info("ID not corresponding with the machine in bocedi1");
                            NextState(States.WaitingCorrection);
                        }
                        if (FatekPLC.ReadBit(FatekPLC.Signals.LabelNull1))
                        {
                            Log.Warn("Pallet in labeling, but queue is empty");
                            printerMachine.Reset("", false);
                            NextState(States.WaitLeaveNull);
                        }
                        break;
                    case States.WaitUpdate:
                        if (writeTask.IsFaulted)
                        {
                            Log.Error("Could not write fifo 1");
                            writeTask = Status.UpdateFIFO1();
                        }
                        if (writeTask.IsCompleted)
                        {
                            Log.Info("Fifo 1 updated");
                            currentCode = Status.Instance.Packager1.LabelPallet.Qr;
                            Log.InfoFormat("Start labeling pallet {0} in bocedi1", currentCode);
                            FatekPLC.SetBit(FatekPLC.Signals.Labeling1);
                            printerMachine.Reset(currentCode, Status.Instance.Packager1.LabelPallet.Labeling);
                            NextState(States.Labeling);
                        }
                        break;
                    case States.WaitingCorrection:
                        Status.Instance.ErrorMessages.BDC1Error = "Se encontró una inconsistencia entre el ID de la cola y el ID entregado por la máquina. Corregir esto y luego presionar Start.";
                        if (!FatekPLC.ReadBit(FatekPLC.Signals.WaitCorrection1))
                        {
                            Log.Info("Retring labeling");
                            NextState(States.WaitingPallet);
                        }
                        break;
                    case States.Labeling:
                        printerMachine.Step();
                        if (printerMachine.WeightOk)
                        {
                            FatekPLC.SetBit(FatekPLC.Signals.WeightOk1);
                        }
                        if (FatekPLC.ReadBit(FatekPLC.Signals.Leave1))
                        {
                            writeTask = Status.UpdateFIFO1();
                            NextState(States.WaitUpdate2);
                        }
                        break;
                    case States.WaitUpdate2:
                        if (writeTask.IsFaulted)
                        {
                            Log.Error("Could not update fifo 1");
                            writeTask = Status.UpdateFIFO1();
                        }
                        if (writeTask.IsCompleted)
                        {
                            Log.Info("Fifo 1 updated");
                            FatekPLC.ResetBit(FatekPLC.Signals.Labeling1);
                            Log.InfoFormat("Notify pallet out worldjet1 with code {0}", currentCode);
                            palletLeaveTask = SqlDatabase.NotifyPalletOut(currentCode);
                            NextState(States.WaitAck);
                        }
                        break;
                    case States.WaitAck:
                        FatekPLC.ResetBit(FatekPLC.Signals.WeightOk1);
                        if (palletLeaveTask.IsCompleted)
                        {
                            if (palletLeaveTask.Result)
                            {
                                Log.InfoFormat("Pallet {0} leave", currentCode);
                                FatekPLC.SetBit(FatekPLC.Signals.PalletLeave1);
                            }
                            else
                            {
                                Log.Error("Couldnt sent message to the SQL server");
                                FatekPLC.SetBit(FatekPLC.Signals.PalletLeave1);
                            }
                            Log.Info("Waiting PLC1 to leave");
                            NextState(States.WaitLeaving);
                        }
                        break;
                    case States.WaitLeaving:
                        if (!FatekPLC.ReadBit(FatekPLC.Signals.Leave1))
                        {
                            NextState(States.WaitingPallet);
                            Log.Info("Waiting pallet to label1");
                        }
                        break;
                    case States.WaitLeaveNull:
                        Status.Instance.ErrorMessages.BDC1Error = "Hay un pallet en el etiquetado, pero las colas estan vacías. El pallet no será etiquetado";
                        printerMachine.Step();
                        if (!FatekPLC.ReadBit(FatekPLC.Signals.LabelNull1))
                        {
                            NextState(States.WaitingPallet);
                            Log.Info("Waiting pallet to label1");
                        }
                        break;
                }
            }
        }
    }
}
