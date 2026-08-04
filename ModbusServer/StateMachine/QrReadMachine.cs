using log4net;
using ModbusServer.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Wenco.Contracts;

namespace ModbusServer.StateMachine
{
    internal class QrReadMachine : Machine<QrReadMachine.States>
    {
        static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public enum States
        {
            Init,
            Reading,
            Retrying,
            Completed,
            [FaultState] Failed,
        }
        Task<string> reader;
        int retries;
        readonly QrReader qrReader;

        public bool Completed
            { get { return State == States.Completed; } }
        public bool Failed
            { get { return State == States.Failed; } }
        public string Result
            { get; protected set; }
        public QrReadMachine(QrReader qrReader) : base(States.Init, qrReader.Ip)
        {
            retries = 0;
            this.qrReader = qrReader;
        }

        protected override void OnStep()
        {
            switch(State)
            {
                case States.Init:
                    if (!qrReader.IsConnected)
                    {
                        Log.Error("Qr reader not connected");
                        NextState(States.Failed);
                        break;
                    }
                    reader = qrReader.Read();
                    NextState(States.Reading);
                    Log.Info("Start reading QR");
                    break;
                case States.Reading:
                    if (reader.IsCompleted)
                    {
                        // A faulted read counts as an empty one: it goes through the
                        // same retry/failure path as a reader that saw no code.
                        if(!reader.IsFaulted && reader.Result != string.Empty)
                        {
                            Result = reader.Result;
                            Log.Info($"Qr reader read {Result}");
                            NextState(States.Completed);
                            break;
                        }
                        else
                        {
                            if(retries < Config.Instance.QrRetries)
                            {
                                retries++;
                                NextState(States.Retrying);
                            }
                            else
                            {
                                Log.Error($"Qr reader failed");
                                NextState(States.Failed);
                            }
                        }
                    }
                    break;
                case States.Retrying:
                    if(StateTime.ElapsedMilliseconds > 1000)
                    {
                        Log.Error($"Qr reader retrying");
                        NextState(States.Init);
                    }
                    break;
                case States.Completed:
                    break;
                case States.Failed:
                    break;
            }
        }

        public override void Reset()
        {
            base.Reset();
            retries = 0;
        }
    }
}
