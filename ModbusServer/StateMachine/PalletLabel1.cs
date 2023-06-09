﻿using log4net;
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
            WaitingCorrection,
            Labeling,
            WaitAck,
            WaitLeaving,
        }

        OmronPLC plc;
        NetworkPrinter printer;
        OmronConnection omronConnection;
        NetworkPrinterConnection printerConnection;
        PrinterMachine printerMachine;
        string currentCode;
        Task<bool> palletLeaveTask;
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
                            Status.UpdateFIFO1();
                            currentCode = Status.Instance.Packager1.LabelPallet.Qr;
                            Log.InfoFormat("Start labeling pallet {0} in bocedi1", currentCode);
                            FatekPLC.SetBit(FatekPLC.Signals.Labeling1);
                            printerMachine.Reset(currentCode, Status.Instance.Packager1.LabelPallet.Labeling);
                            NextState(States.Labeling);
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
                            FatekPLC.ResetBit(FatekPLC.Signals.Labeling1);
                            Status.UpdateFIFO1();
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
                }
            }
        }
    }
}
