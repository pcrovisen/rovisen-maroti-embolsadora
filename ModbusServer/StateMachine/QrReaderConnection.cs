using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using log4net;
using ModbusServer.Devices;

namespace ModbusServer.StateMachine
{
    internal class QrReaderConnection : Machine
    {
        static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public enum States
        {
            Init,
            Disconnected,
            Connected,
            Wait,
        }

        Task<bool> qrConnection;
        int retries;

        public QrReaderConnection() : base(States.Init)
        {
            this.State = States.Init;
            retries = 0;
        }

        public override void Step()
        {
            switch (State)
            {
                case States.Init:
                    qrConnection = QrReader.Connect();
                    NextState(States.Disconnected);
                    break;
                case States.Disconnected:
                    if (qrConnection.IsCompleted)
                    {
                        if(qrConnection.Result)
                        {
                            NextState(States.Connected);
                            Status.Instance.Connections.QrReader = true;
                            Log.Info("QR reader connected");
                        }
                        else
                        {
                            NextState(States.Wait);
                            retries++;
                            retries = retries > 5 ? 5 : retries;
                        }
                    }
                    break;
                case States.Connected:
                    retries = 0;
                    if (!QrReader.IsConnected)
                    {
                        Status.Instance.Connections.QrReader = false;
                        qrConnection = QrReader.Connect();
                        Log.Info("QR reader disconnected");
                        NextState(States.Disconnected);
                    }
                    break ;
                case States.Wait:
                    if (StateTime.ElapsedMilliseconds > 1000 * retries)
                    {
                        NextState(States.Init);
                    }
                    break; 
            }
        }
    }
}
