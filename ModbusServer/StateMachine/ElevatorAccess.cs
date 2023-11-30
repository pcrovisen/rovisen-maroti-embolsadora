using log4net;
using ModbusServer.Devices;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    internal class ElevatorAccess : Machine
    {
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public enum States
        {
            WaitingRequest,
            ReadingQr,
            FailedQr,
            WaitingAuth,
            WaitingLeave,
            Delay,
        }
        readonly QrReaderConnection qrReaderConnection;
        readonly QrReadMachine qrReadMachine;
        readonly QrReader qrReader;


        Task<bool> sqlRequest;
        public ElevatorAccess() : base(States.WaitingRequest)
        {
            qrReader = new QrReader(ConfigurationManager.AppSettings["ipQrElevator"]);
            qrReaderConnection = new QrReaderConnection(qrReader);
            qrReadMachine = new QrReadMachine(qrReader);
        }

        public override void Step()
        {
            qrReaderConnection.Step();

            switch(State)
            {
                case States.WaitingRequest:
                    FatekPLC.ResetBit(FatekPLC.Signals.ElevatorFailedQr);
                    if (FatekPLC.ReadBit(FatekPLC.Signals.ElevatorRequest))
                    {
                        Log.Info("Elevator reading request");
                        NextState(States.ReadingQr);
                        qrReadMachine.Reset();
                    }
                    break;
                case States.ReadingQr:
                    qrReadMachine.Step();
                    if (!FatekPLC.ReadBit(FatekPLC.Signals.ElevatorRequest))
                    {
                        NextState(States.WaitingRequest);
                        Log.Info("Pallet removed");
                        break;
                    }
                    if (qrReadMachine.Failed)
                    {
                        Log.Info("Qr failed");
                        FatekPLC.SetBit(FatekPLC.Signals.ElevatorFailedQr);
                        _ = SqlDatabase.GetAuthElevator(null, "No se pudo leer el código QR del pallet.");
                        qrReadMachine.Reset();
                    }
                    if (qrReadMachine.Completed)
                    {
                        Log.Info("Qr completed");
                        Log.InfoFormat("Qr readed: {0}", qrReadMachine.Result);
                        FatekPLC.ResetBit(FatekPLC.Signals.ElevatorFailedQr);
                        sqlRequest = SqlDatabase.GetAuthElevator(qrReadMachine.Result);
                        NextState(States.WaitingAuth);
                        Log.Info("Asking DB");
                    }
                    break;
                case States.WaitingAuth:
                    if(sqlRequest.IsCompleted)
                    {
                        if (sqlRequest.Result)
                        {
                            Log.Info("DB authorize");
                            FatekPLC.SetBit(FatekPLC.Signals.ElevatorAuth);
                            NextState(States.WaitingLeave);
                        }
                        else
                        {
                            Log.Info("DB not authorize");
                            NextState(States.Delay);
                        }
                    }
                    break;
                case States.WaitingLeave:
                    if (!FatekPLC.ReadBit(FatekPLC.Signals.ElevatorRequest))
                    {
                        Log.Info("Pallet elevated");
                        FatekPLC.ResetBit(FatekPLC.Signals.ElevatorAuth);
                        NextState(States.WaitingRequest);
                    }
                    break;
                case States.Delay:
                    if(StateTime.ElapsedMilliseconds > 2000)
                    {
                        NextState(States.WaitingRequest);
                    }
                    break;
            }
        }
    }
}
