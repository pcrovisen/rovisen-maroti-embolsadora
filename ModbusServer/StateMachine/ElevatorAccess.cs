using ModbusServer.Devices;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    internal class ElevatorAccess : Machine
    {
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
                    if (FatekPLC.ReadBit(FatekPLC.Signals.ElevatorRequest))
                    {
                        NextState(States.ReadingQr);
                        qrReadMachine.Reset();
                    }
                    break;
                case States.ReadingQr:
                    qrReadMachine.Step();
                    if (qrReadMachine.Completed)
                    {
                        if (qrReadMachine.Failed)
                        {
                            FatekPLC.SetBit(FatekPLC.Signals.ElevatorFailedQr);
                            qrReadMachine.Reset();
                        }
                        else
                        {
                            sqlRequest = SqlDatabase.GetAuthElevator(qrReadMachine.Result);
                            NextState(States.WaitingAuth);
                        }
                    }
                    break;
                case States.WaitingAuth:
                    if(sqlRequest.IsCompleted)
                    {
                        if (sqlRequest.Result)
                        {
                            NextState(States.WaitingLeave);
                        }
                        else
                        {
                            NextState(States.Delay);
                        }
                    }
                    break;
                case States.WaitingLeave:
                    if (!FatekPLC.ReadBit(FatekPLC.Signals.ElevatorRequest))
                    {
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
