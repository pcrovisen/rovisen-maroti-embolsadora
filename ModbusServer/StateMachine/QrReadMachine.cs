using log4net;
using ModbusServer.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    internal class QrReadMachine : Machine
    {
        static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public enum States
        {
            Init,
            Reading,
            Retrying,
            Completed,
            Failed,
        }
        Task<string> reader;
        int retries;

        public bool Completed
            { get { return (States)State == States.Completed; } }
        public bool Failed
            { get { return (States)State == States.Failed; } }
        public string Result
            { get; protected set; }
        public QrReadMachine() : base(States.Init)
        {
            retries = 0;
        }

        public override void Step()
        {
            switch(State)
            {
                case States.Init:
                    if (!QrReader.IsConnected)
                    {
                        Log.Error("Qr reader not connected");
                        NextState(States.Failed);
                        break;
                    }
                    reader = QrReader.Read();
                    NextState(States.Reading);
                    Log.Info("Start reading QR");
                    break;
                case States.Reading:
                    if (reader.IsCompleted)
                    {
                        if(reader.Result != string.Empty && IsValid(reader.Result))
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

        private bool IsValid(string value)
        {
            if (value[0] == 'S')
            {
                return true;
            }

            if(value.IndexOf("REEMBOLSADO") != -1)
            {
                return true;
            }
            return false;
        }
    }
}
