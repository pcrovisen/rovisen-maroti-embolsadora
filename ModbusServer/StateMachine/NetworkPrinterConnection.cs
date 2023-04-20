using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using mcOMRON;
using ModbusServer.Devices;

namespace ModbusServer.StateMachine
{
    internal class NetworkPrinterConnection : Machine
    {
        enum States
        {
            Connect,
            Connecting,
            Connected,
        }

        public bool Connected
        {
            get
            {
                return (States)State == States.Connected;
            }
        }

        NetworkPrinter printer;
        Task<bool> connectingTask;
        public NetworkPrinterConnection(NetworkPrinter printer) : base(States.Connect, printer.ip)
        {
            this.printer = printer;
        }

        public override void Step()
        {
            switch (State)
            {
                case States.Connect:
                    connectingTask = printer.Connect();
                    NextState(States.Connecting);
                    break;
                case States.Connecting:
                    if (connectingTask.IsCompleted)
                    {
                        if (connectingTask.Result)
                        {
                            NextState(States.Connected);
                        }
                        else
                        {
                            NextState(States.Connect);
                        }
                    }
                    break;
                case States.Connected:
                    if (!printer.Connected)
                    {
                        NextState(States.Connect);
                    }
                    break;
            }
        }
    }
}
