using log4net;
using ModbusServer.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    internal class CarConnection : Machine
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        enum States
        {
            Init,
            Connected,
            Disconnected,
        }

        TcpListener carListener;
        Task<TcpClient> connectionTask;
        CarCommunication carCommunication;
        public CarConnection() : base(States.Init)
        {
        }

        public override void Reset()
        {
            base.Reset();
        }

        public override void Step()
        {
            switch(State)
            {
                case States.Init:
                    carListener = TcpListener.Create(51401);
                    carListener.Start();
                    connectionTask = carListener.AcceptTcpClientAsync();
                    NextState(States.Disconnected);
                    break;
                case States.Disconnected:
                    if(connectionTask.IsCompleted)
                    {
                        if(connectionTask.IsFaulted)
                        {
                            connectionTask = carListener.AcceptTcpClientAsync();
                        }
                        else
                        {
                            carCommunication = new CarCommunication(new TcpDevice(connectionTask.Result));
                            NextState(States.Connected);
                        }
                    }
                    break;
                case States.Connected:
                    if(carCommunication.Terminated)
                    {
                        carCommunication = null;
                        connectionTask = carListener.AcceptTcpClientAsync();
                        NextState(States.Disconnected);
                    }
                    else
                    {
                        carCommunication.Step();
                    }
                    break;
            }
        }
    }
}
