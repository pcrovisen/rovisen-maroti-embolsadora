using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    internal class PrinterConnection : Machine
    {
        enum States
        {
            Connect,
            Connecting,
            Connected,
        }

        TcpClient printer;

        public PrinterConnection(TcpClient printer, string ip) : base(States.Connect, ip)
        {
            this.printer = printer;
        }

        public override void Step()
        {
            throw new NotImplementedException();
        }
    }
}
