using log4net;
using ModbusServer.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    internal class FatekPLCCommunication : Machine
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
        public DeletePalletEmb1 delletePalletEmb1;
        public DeletePalletEmb2 delletePalletEmb2;
        readonly PalletLabel1 palletLabelBocedi1;
        readonly PalletLabel2 palletLabelBocedi2;
        readonly CarMachine carMachine;
        readonly ElevatorAccess elevatorMachine;

        Task initQueues;

        public FatekPLCCommunication() : base(States.Init)
        {
            palletEntry = new PalletEntry();
            delletePalletEmb1 = new DeletePalletEmb1();
            delletePalletEmb2 = new DeletePalletEmb2();
            palletLabelBocedi1 = new PalletLabel1();
            palletLabelBocedi2 = new PalletLabel2();
            carMachine = new CarMachine();
            elevatorMachine = new ElevatorAccess();
        }

        public override void Step()
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
                    if (FatekPLC.IsConnected && FatekPLC.ReadBit(FatekPLC.Signals.PLCStarting))
                    {
                        NextState(States.Starting);
                    }
                    break;
                case States.Starting:
                    FatekPLC.SetBit(FatekPLC.Signals.Alive);
                    if (FatekPLC.ReadBit(FatekPLC.Signals.SendingFIFOs))
                    {
                        NextState(States.WaitingMemory);
                    }
                    break;
                case States.WaitingMemory:
                    FatekPLC.SetBit(FatekPLC.Signals.ReceivingFIFOs);
                    if (FatekPLC.ReadBit(FatekPLC.Signals.Ready))
                    {
                        NextState(States.WaitingInit);
                        Log.Info("Master PLC connected");
                        palletEntry.Reset();
                        initQueues =  Status.InitQueues();
                    }
                    break;
                case States.WaitingInit:
                    if (initQueues.IsFaulted)
                    {
                        Log.Error("Could not init queues");
                        initQueues = Status.InitQueues();
                    }
                    if (initQueues.IsCompleted)
                    {
                        NextState(States.Working);
                        Log.Info("Queues initialized");
                    }
                    break;
                case States.Working:
                    FatekPLC.ResetBit(FatekPLC.Signals.ReceivingFIFOs);
                    Status.Instance.Connections.MasterPLC = true;
                    Status.Instance.Connections.SlavePLC = FatekPLC.ReadBit(FatekPLC.Signals.SlaveConnected);
                    Status.Instance.Connections.Packager1 = FatekPLC.ReadBit(FatekPLC.Signals.BCD1OK);
                    Status.Instance.Connections.Packager2 = FatekPLC.ReadBit(FatekPLC.Signals.BCD2OK);
                    palletEntry.Step();
                    delletePalletEmb1.Step();
                    delletePalletEmb2.Step();
                    palletLabelBocedi1.Step();
                    palletLabelBocedi2.Step();
                    carMachine.Step();
                    elevatorMachine.Step();
                    if (!FatekPLC.ReadBit(FatekPLC.Signals.Ready))
                    {
                        NextState(States.Init);
                        Log.Info("Master PLC disconnected");
                        FatekPLC.ResetBit(FatekPLC.Signals.Alive);
                        break;
                    }
                    break;
            }
        }
    }
}
