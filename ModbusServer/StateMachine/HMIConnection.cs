using log4net;
using ModbusServer.Devices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    internal class HMIConnection : Machine
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public enum States
        {
            Init,
            Waiting,
            WaitingDeletion1,
            WaitingDeletion2,
            Responding,
            Terminated,
        }

        readonly TcpDevice tcpHMI;
        Task<string> receiveTask;
        Task<bool> sendTask;
        CancellationTokenSource cts;

        bool UpdateQueueEmb1 = false;
        bool UpdateQueueEmb2 = false;
        bool UpdatedCar = false;
        bool UpdateMachineStates = false;

        public bool Terminated => (States)State == States.Terminated;

        public CancellationTokenSource Cts { get => cts; set => cts = value; }

        public HMIConnection(TcpDevice tcpHMI) : base(States.Init, tcpHMI.Name)
        {
            this.tcpHMI = tcpHMI;
            Cts = new CancellationTokenSource();
        }

        public override void Step()
        {
            if(Status.Instance.Packager1.Updated)
                UpdateQueueEmb1 = true;
            if (Status.Instance.Packager2.Updated)
                UpdateQueueEmb2 = true;
            if (Status.Instance.Car.Updated)
                UpdatedCar = true;
            if(Status.Instance.StateMachine.Updated)
                UpdateMachineStates = true;

            switch (State)
            {
                case States.Init:
                    receiveTask = tcpHMI.Receive(Cts.Token);
                    NextState(States.Waiting);
                    break;
                case States.Waiting:
                    if (StateTime.ElapsedMilliseconds > 10000)
                    {
                        Log.WarnFormat("HMI {0} not responding", Name);
                        NextState(States.Terminated);
                        break;
                    }
                    if (receiveTask.IsCompleted)
                    {
                        if (receiveTask.Result != null)
                        {
                            if(receiveTask.Result == "init")
                            {
                                NextState(States.Responding);
                                sendTask = tcpHMI.Send(CreateMessage(true), Cts.Token);
                            }
                            else if(receiveTask.Result.Substring(0,3) == "del")
                            {
                                DeletePallet pallet = JsonSerializer.Deserialize<DeletePallet>(receiveTask.Result.Substring(3));
                                if(pallet.Packager == 1)
                                {
                                    DeletePalletEmb1.Instance.StartDelete(pallet);
                                    NextState(States.WaitingDeletion1);
                                }
                                else
                                {
                                    DeletePalletEmb2.Instance.StartDelete(pallet);
                                    NextState(States.WaitingDeletion2);
                                }
                                
                            }
                            else if(receiveTask.Result == "terminate")
                            {
                                Log.InfoFormat("HMI {0} closed", Name);
                                NextState(States.Terminated);
                            }
                            else
                            {
                                NextState(States.Responding);
                                sendTask = tcpHMI.Send(CreateMessage(), Cts.Token);
                            }
                            
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
                            receiveTask = tcpHMI.Receive(Cts.Token);
                        }
                        else
                        {
                            NextState(States.Terminated);
                        }
                    }
                    break;
                case States.WaitingDeletion1:
                    if(DeletePalletEmb1.Instance.Completed)
                    {
                        sendTask = tcpHMI.Send("OK", Cts.Token);
                        NextState(States.Responding);
                        DeletePalletEmb1.Instance.Reset();
                        break;
                    }
                    if (DeletePalletEmb1.Instance.Failed)
                    {
                        sendTask = tcpHMI.Send("NOK", Cts.Token);
                        NextState(States.Responding);
                        DeletePalletEmb1.Instance.Reset();
                        break;
                    }
                    break;
                case States.WaitingDeletion2:
                    if (DeletePalletEmb2.Instance.Completed)
                    {
                        sendTask = tcpHMI.Send("OK", Cts.Token);
                        NextState(States.Responding);
                        DeletePalletEmb2.Instance.Reset();
                        break;
                    }
                    if (DeletePalletEmb2.Instance.Failed)
                    {
                        sendTask = tcpHMI.Send("NOK", Cts.Token);
                        NextState(States.Responding);
                        DeletePalletEmb2.Instance.Reset();
                        break;
                    }
                    break;
                case States.Terminated:
                    break;
            }
        }

        ~HMIConnection()
        {
            Cts.Cancel();
            receiveTask?.Wait();
            sendTask?.Wait();
        }

        private string CreateMessage(bool force = false)
        {
            var message = new Dictionary<string, object>
            {
                ["Config"] = Config.Instance,
                ["Signals"] = FatekPLC.ReadSignals(FatekPLC.Signals.ReadQR, 28),
                ["Connections"] = Status.Instance.Connections,
                ["EntryPallet"] = Status.Instance.EntryPallet,
                ["ErrorMessages"] = Status.Instance.ErrorMessages,
        };

            if (UpdateQueueEmb1 || force)
            {
                UpdateQueueEmb1 = false;
                message["Packager1"] = Status.Instance.Packager1;
            }
            else
            {
                message["Packager1"] = null;
            }

            if (UpdateQueueEmb2 || force)
            {
                UpdateQueueEmb2 = false;
                message["Packager2"] = Status.Instance.Packager2;
            }
            else
            {
                message["Packager2"] = null;
            }

            if (UpdatedCar || force)
            {
                UpdatedCar = false;
                message["Car"] = Status.Instance.Car;
            }
            else
            {
                message["Car"] = null;
            }

            message["MachineState"] = Status.Instance.StateMachine.Machines;
            if (UpdateMachineStates || force)
            {
                UpdateMachineStates = false;
                message["States"] = Status.Instance.StateMachine.MachinesStates;
            }
            else
            {
                message["States"] = null;
            }

            return JsonSerializer.Serialize(message);
        }
    }
}
