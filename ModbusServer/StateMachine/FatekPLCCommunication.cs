using log4net;
using ModbusServer.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Wenco.Contracts;

namespace ModbusServer.StateMachine
{
    internal class FatekPLCCommunication : Machine<FatekPLCCommunication.States>
    {
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public enum States
        {
            Init,
            Starting,
            WaitingMemory,
            WaitingInit,
            Working,
        }

        public PalletEntry palletEntry;
        readonly DeletePalletEmb deletePalletBocedi1;
        readonly DeletePalletEmb deletePalletBocedi2;
        readonly PalletLabel palletLabelBocedi1;
        readonly PalletLabel palletLabelBocedi2;
        readonly CarMachine carMachine;
        readonly ElevatorAccess elevatorMachine;

        Task initQueues;

        // Init has no ceiling: with the PLC off, sitting there is the correct
        // behaviour and the connection flags already say so. The three handshake
        // steps in between should take milliseconds, so a stall there means the
        // PLC program raised PLCStarting and then stopped talking.
        static readonly IReadOnlyDictionary<States, int> Timeouts = new Dictionary<States, int>
        {
            { States.Starting, 30000 },
            { States.WaitingMemory, 30000 },
            { States.WaitingInit, 60000 },
        };

        protected override IReadOnlyDictionary<States, int> StateTimeouts
        {
            get { return Timeouts; }
        }

        public FatekPLCCommunication() : base(States.Init)
        {
            palletEntry = new PalletEntry();
            deletePalletBocedi1 = new DeletePalletEmb(PackagerBinding.Bocedi1);
            deletePalletBocedi2 = new DeletePalletEmb(PackagerBinding.Bocedi2);
            palletLabelBocedi1 = new PalletLabel(PackagerBinding.Bocedi1);
            palletLabelBocedi2 = new PalletLabel(PackagerBinding.Bocedi2);
            carMachine = new CarMachine();
            elevatorMachine = new ElevatorAccess();
        }

        protected override void OnStep()
        {
            if (!FatekPLC.IsConnected)
            {
                NextState(States.Init);
                Status.Instance.Connections.MasterPLC = false;
                Status.Instance.Connections.SlavePLC = false;
            }
            switch (State)
            {
                case States.Init:
                    if (FatekPLC.IsConnected && FatekPLC.ReadBit(Signals.PLCStarting))
                    {
                        NextState(States.Starting);
                    }
                    break;
                case States.Starting:
                    FatekPLC.SetBit(Signals.Alive);
                    if (FatekPLC.ReadBit(Signals.SendingFIFOs))
                    {
                        NextState(States.WaitingMemory);
                    }
                    break;
                case States.WaitingMemory:
                    FatekPLC.SetBit(Signals.ReceivingFIFOs);
                    if (FatekPLC.ReadBit(Signals.Ready))
                    {
                        NextState(States.WaitingInit);
                        Log.Info("Master PLC connected");
                        palletEntry.Reset();
                        initQueues =  Status.InitQueues();
                    }
                    break;
                case States.WaitingInit:
                    if (TryComplete(ref initQueues, Status.InitQueues, "Initializing the queues"))
                    {
                        NextState(States.Working);
                        Log.Info("Queues initialized");
                    }
                    break;
                case States.Working:
                    FatekPLC.ResetBit(Signals.ReceivingFIFOs);
                    Status.Instance.Connections.MasterPLC = true;
                    Status.Instance.Connections.SlavePLC = FatekPLC.ReadBit(Signals.SlaveConnected);
                    Status.Instance.Connections.Packager1 = FatekPLC.ReadBit(Signals.BCD1OK);
                    Status.Instance.Connections.Packager2 = FatekPLC.ReadBit(Signals.BCD2OK);
                    palletEntry.Step();
                    deletePalletBocedi1.Step();
                    deletePalletBocedi2.Step();
                    palletLabelBocedi1.Step();
                    palletLabelBocedi2.Step();
                    carMachine.Step();
                    elevatorMachine.Step();
                    if (!FatekPLC.ReadBit(Signals.Ready))
                    {
                        NextState(States.Init);
                        Log.Info("Master PLC disconnected");
                        FatekPLC.ResetBit(Signals.Alive);
                        break;
                    }
                    break;
            }
        }
    }
}
