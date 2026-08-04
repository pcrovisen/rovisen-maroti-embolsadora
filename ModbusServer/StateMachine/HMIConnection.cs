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
    internal class HMIConnection : Machine<HMIConnection.States>, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public enum States
        {
            Init,
            Waiting,
            WaitingDelStart,
            WaitingDeletion,
            Responding,
            Terminated,
        }

        // The deletion machines are singletons shared by every connection, so a
        // request can have to wait for another operator's deletion — but not
        // forever, and not without the operator being told.
        const int DeleteStartTimeoutMs = 15000;
        const int DeleteTimeoutMs = 30000;

        readonly TcpDevice tcpHMI;
        Task<string> receiveTask;
        Task<bool> sendTask;
        Task<bool> startDel;
        CancellationTokenSource cts;
        DeletePallet pallet;

        bool UpdateQueueEmb1 = false;
        bool UpdateQueueEmb2 = false;
        bool UpdatedCar = false;
        bool UpdateMachineStates = false;


        public bool Terminated => State == States.Terminated;

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
                        if (!receiveTask.IsFaulted && receiveTask.Result != null)
                        {
                            if(receiveTask.Result == "init")
                            {
                                NextState(States.Responding);
                                sendTask = tcpHMI.Send(CreateMessage(true), Cts.Token);
                            }
                            else if(receiveTask.Result.StartsWith("del"))
                            {
                                if (TryParseDelete(receiveTask.Result.Substring(3), out pallet))
                                {
                                    startDel = StartDelete(pallet);
                                    NextState(States.WaitingDelStart);
                                }
                                else
                                {
                                    // A bad request must not take the connection (nor, through
                                    // the blanket handler in AcceptHMIs, every other HMI) down.
                                    NextState(States.Responding);
                                    sendTask = tcpHMI.Send("NOK", Cts.Token);
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
                case States.WaitingDelStart:
                    if (startDel.IsCompleted)
                    {
                        if (!startDel.IsFaulted && startDel.Result)
                        {
                            NextState(States.WaitingDeletion);
                        }
                        else if (StateTime.ElapsedMilliseconds > DeleteStartTimeoutMs)
                        {
                            Log.WarnFormat("HMI {0}: the deletion could not be started", Name);
                            NextState(States.Responding);
                            sendTask = tcpHMI.Send("NOK", Cts.Token);
                        }
                        else
                        {
                            // Retry without a transition, so StateTime keeps measuring
                            // the whole wait instead of restarting on every attempt.
                            startDel = StartDelete(pallet);
                        }
                    }
                    break;
                case States.Responding:
                    if (sendTask.IsCompleted)
                    {
                        if (!sendTask.IsFaulted && sendTask.Result)
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
                case States.WaitingDeletion:
                    var deletion = DeletePalletEmb.Instance(pallet.Packager);
                    if (deletion.Completed || deletion.Failed)
                    {
                        sendTask = tcpHMI.Send(deletion.Completed ? "OK" : "NOK", Cts.Token);
                        NextState(States.Responding);
                        deletion.Reset();
                        break;
                    }
                    if (StateTime.ElapsedMilliseconds > DeleteTimeoutMs)
                    {
                        // Leave the machine alone — it recovers on its own — but stop
                        // holding the operator on a dialog that will never close.
                        Log.WarnFormat("HMI {0}: timeout waiting for the deletion in packager {1}", Name, pallet.Packager);
                        sendTask = tcpHMI.Send("NOK", Cts.Token);
                        NextState(States.Responding);
                    }
                    break;
                case States.Terminated:
                    break;
            }
        }

        // Replaces a finalizer that cancelled the token and then blocked on
        // receiveTask.Wait() / sendTask.Wait(): a blocking wait on the finalizer
        // thread, which stalls every other finalizer in the process and deadlocks
        // outright if cancelling does not unblock the read. Cancelling the token
        // and closing the socket is enough to let both tasks finish on their own —
        // their results are simply never read.
        public void Dispose()
        {
            Cts.Cancel();
            tcpHMI.Dispose();
            Cts.Dispose();
        }

        // Parses the payload of a `del…` request. Everything past the framing is
        // attacker-controlled as far as this process is concerned (any host on the
        // plant subnet can connect to :8153), so a bad request has to end as a NOK
        // and never as an exception out of Step().
        private bool TryParseDelete(string json, out DeletePallet result)
        {
            result = null;
            DeletePallet parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<DeletePallet>(json);
            }
            catch (Exception ex)
            {
                Log.WarnFormat("HMI {0} sent a malformed deletion request: {1}", Name, ex.Message);
                return false;
            }

            if (parsed?.Pallet == null || string.IsNullOrEmpty(parsed.Pallet.Qr))
            {
                Log.WarnFormat("HMI {0} sent a deletion request without a pallet", Name);
                return false;
            }

            // Anything that is not packager 1 used to fall through to packager 2.
            if (parsed.Packager != 1 && parsed.Packager != 2)
            {
                Log.WarnFormat("HMI {0} sent a deletion request for packager {1}", Name, parsed.Packager);
                return false;
            }

            var queue = (parsed.Packager == 1 ? Status.Instance.Packager1 : Status.Instance.Packager2).Queue;
            if (parsed.Position < 0 || queue == null || parsed.Position >= queue.Count)
            {
                Log.WarnFormat("HMI {0} sent a deletion request for position {1}, outside the queue of packager {2}",
                    Name, parsed.Position, parsed.Packager);
                return false;
            }

            result = parsed;
            return true;
        }

        private static Task<bool> StartDelete(DeletePallet pallet)
        {
            return DeletePalletEmb.Instance(pallet.Packager).StartDelete(pallet);
        }

        private string CreateMessage(bool force = false)
        {
            var message = new Dictionary<string, object>
            {
                ["Config"] = Config.Instance,
                ["Signals"] = FatekPLC.ReadSignals(FatekPLC.Signals.ReadQR, FatekPLC.Signals.WaitLabel2 - FatekPLC.Signals.ReadQR + 1),
                ["PcSignals"] = FatekPLC.ReadSignals(FatekPLC.Signals.ReadingPallet, FatekPLC.Signals.ElevatorFailedQr - FatekPLC.Signals.ReadingPallet + 1),
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
