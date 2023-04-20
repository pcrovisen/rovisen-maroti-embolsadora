using ModbusServer.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    internal class CarCommunication : Machine
    {
        enum States
        {
            Init,
            Waiting,
            Responding,
            Terminated,
        }

        readonly TcpDevice car;
        Task<string> receiveTask;
        Task<bool> sendTask;
        CancellationTokenSource cts;
        public CancellationTokenSource Cts { get => cts; set => cts = value; }
        public bool Terminated
        {
            get
            {
                return (States)State == States.Terminated;
            }
        }
        public CarCommunication(TcpDevice car) : base(States.Init, car.Name)
        {
            this.car = car;
            Cts = new CancellationTokenSource();
        }

        public override void Reset()
        {
            base.Reset();
        }

        public override void Step()
        {
            switch (State)
            {
                case States.Init:
                    receiveTask = car.Receive(Cts.Token);
                    NextState(States.Waiting);
                    break;
                case States.Waiting:
                    if (receiveTask.IsCompleted)
                    {
                        if (receiveTask.Result != null)
                        {
                            Console.WriteLine(receiveTask.Result);
                            NextState(States.Responding);
                            sendTask = car.Send(receiveTask.Result, Cts.Token);
                        }
                        else
                        {
                            NextState(States.Terminated);
                        }
                    }
                    break;
                case States.Responding:
                    if (sendTask.IsCompleted)
                    {
                        if (sendTask.Result)
                        {
                            NextState(States.Waiting);
                            receiveTask = car.Receive(Cts.Token);
                        }
                        else
                        {
                            NextState(States.Terminated);
                        }
                    }
                    break;
                case States.Terminated:
                    break;
            }
        }
    }
}
