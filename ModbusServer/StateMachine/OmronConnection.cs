using System.Net;
using System.Threading.Tasks;
using mcOMRON;

namespace ModbusServer.StateMachine
{
    internal class OmronConnection : Machine
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

        OmronPLC plc;
        Task<bool> connectingTask;
        public OmronConnection(OmronPLC plc, string ip) : base(States.Connect, ip)
        {
            this.plc = plc;
            tcpFINSCommand command = (tcpFINSCommand)this.plc.FinsCommand;
            command.SetTCPParams(IPAddress.Parse(ip), 9600);
        }

        public override void Step()
        {
            switch (State)
            {
                case States.Connect:
                    connectingTask = plc.Connect();
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
                    if (!plc.Connected)
                    {
                        NextState(States.Connect);
                    }
                    break;
            }
        }

        public async Task<bool> Connect()
        {
            return await Task.Run(() =>
            {
                try
                {
                    plc.Connect();
                    return plc.Connected;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
