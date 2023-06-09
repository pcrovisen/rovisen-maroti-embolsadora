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
    internal class PalletLabel2 : Machine
    {
        static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        enum States
        {
            WaitingPallet,
            WaitingCorrection,
            Labeling,
            WaitLeaving,
            WaitAck,
        }

        OmronPLC plc;
        NetworkPrinter printer;
        OmronConnection omronConnection;
        NetworkPrinterConnection printerConnection;
        PrinterMachine printerMachine;
        string currentCode;
        Task<bool> palletLeaveTask;
        public PalletLabel2() : base(States.WaitingPallet)
        {
            plc = new OmronPLC(TransportType.Tcp);
            printer = new NetworkPrinter("192.168.6.163");
            omronConnection = new OmronConnection(plc, "192.168.250.1");
            printerConnection = new NetworkPrinterConnection(printer);
            printerMachine = new PrinterMachine(plc, printer, "Wolrdjet2");
        }

        public override void Step()
        {
            omronConnection.Step();
            printerConnection.Step();
            Status.Instance.Connections.Labeler2 = printerConnection.Connected;
            if (omronConnection.Connected && printerConnection.Connected)
            {
                switch (State)
                {
                    case States.WaitingPallet:
                        Status.Instance.ErrorMessages.BDC2Error = "";
                        if (FatekPLC.ReadBit(FatekPLC.Signals.Label2))
                        {
                            FatekPLC.ResetBit(FatekPLC.Signals.PalletLeave2);
                            Status.UpdateFIFO2();
                            currentCode = Status.Instance.Packager2.LabelPallet.Qr;
                            Log.InfoFormat("Start labeling pallet {0} in bocedi2", currentCode);
                            FatekPLC.SetBit(FatekPLC.Signals.Labeling2);
                            printerMachine.Reset(currentCode, Status.Instance.Packager2.LabelPallet.Labeling);
                            NextState(States.Labeling);
                        }
                        if (FatekPLC.ReadBit(FatekPLC.Signals.PLCLabeling2))
                        {
                            FatekPLC.ResetBit(FatekPLC.Signals.PalletLeave2);
                            currentCode = Status.Instance.Packager2.LabelPallet.Qr;
                            Log.InfoFormat("Continue labeling pallet {0} in bocedi2", currentCode);
                            FatekPLC.SetBit(FatekPLC.Signals.Labeling2);
                            printerMachine.Reset(currentCode, Status.Instance.Packager2.LabelPallet.Labeling, true);
                            Status.Instance.ErrorMessages.BDC2Error = "";
                            NextState(States.Labeling);
                        }
                        if (FatekPLC.ReadBit(FatekPLC.Signals.WaitCorrection2))
                        {
                            _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.desorden_embolsadora, packager: 2);
                            Log.Info("ID not corresponding with the machine in bocedi2");
                            NextState(States.WaitingCorrection);
                        }
                        break;
                    case States.WaitingCorrection:
                        Status.Instance.ErrorMessages.BDC2Error = "Se encontró una inconsistencia entre el ID de la cola y el ID entregado por la máquina. Corregir esto y luego presionar Start.";
                        if (!FatekPLC.ReadBit(FatekPLC.Signals.WaitCorrection2))
                        {
                            Log.Info("Retring labeling bocedi2");
                            NextState(States.WaitingPallet);
                        }
                        break;
                    case States.Labeling:
                        printerMachine.Step();
                        if(printerMachine.WeightOk)
                        {
                            FatekPLC.SetBit(FatekPLC.Signals.WeightOk2);
                        }
                        if (FatekPLC.ReadBit(FatekPLC.Signals.Leave2))
                        {
                            FatekPLC.ResetBit(FatekPLC.Signals.Labeling2);
                            Status.UpdateFIFO2();
                            Log.InfoFormat("Notify pallet out worldjet2 with code {0}", currentCode);
                            palletLeaveTask = SqlDatabase.NotifyPalletOut(currentCode);
                            NextState(States.WaitAck);
                        }
                        break;
                    case States.WaitAck:
                        FatekPLC.ResetBit(FatekPLC.Signals.WeightOk2);
                        if (palletLeaveTask.IsCompleted)
                        {
                            if (palletLeaveTask.Result)
                            {
                                Log.InfoFormat("Pallet #{0} leave", currentCode);
                                FatekPLC.SetBit(FatekPLC.Signals.PalletLeave2);
                            }
                            else
                            {
                                Log.Error("Couldnt sent message to the SQL server");
                                FatekPLC.SetBit(FatekPLC.Signals.PalletLeave2);
                            }
                            Log.Info("Waiting PLC to leave2");
                            NextState(States.WaitLeaving);
                        }
                        break;
                    case States.WaitLeaving:
                        if (!FatekPLC.ReadBit(FatekPLC.Signals.Leave2))
                        {
                            NextState(States.WaitingPallet);
                            Log.Info("Waiting pallet to label2");
                        }
                        break;
                    
                }
            }
        }
    }
}
