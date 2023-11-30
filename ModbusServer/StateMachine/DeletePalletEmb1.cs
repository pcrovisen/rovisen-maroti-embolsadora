using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using log4net;
using ModbusServer.Devices;

namespace ModbusServer.StateMachine
{
    internal class DeletePalletEmb1 : Machine
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public enum States
        {
            Waiting,
            Validating,
            ValidatingPLC,
            WaitingWrite,
            SendingFIFO,
            Completed,
            Failed,
        }

        Task queueWrite;

        public static DeletePalletEmb1 Instance { get; set; }
        public bool needDel = false;
        public bool Completed
        {
            get { return (States)State == States.Completed; }
        }

        public bool Failed
        {
            get { return (States)State == States.Failed; }
        }

        public DeletePalletEmb1() : base(States.Waiting)
        {
            Instance = this;
        }

        public override void Step()
        {
            switch (State)
            {
                case States.Waiting:
                    if (needDel)
                    {
                        needDel = false;
                        NextState(States.Validating);
                    }
                    break;
                case States.Validating:
                    if (Validate())
                    {
                        Log.Info("Valid in PC side");
                        NextState(States.ValidatingPLC);
                    }
                    else
                    {
                        Log.Info("Deletion failed in PC");
                        NextState(States.Failed);
                    }
                    break;
                case States.ValidatingPLC:
                    FatekPLC.SetBit(FatekPLC.Signals.DelEmb1);
                    if (FatekPLC.ReadBit(FatekPLC.Signals.Del1Error))
                    {
                        Log.Info("Deletion failed in PLC");
                        NextState(States.Failed);
                        break;
                    }
                    if (FatekPLC.ReadBit(FatekPLC.Signals.Del1Valid))
                    {
                        Log.Info("Valid in PLC side");
                        DeletePallet();
                        queueWrite = Status.UpdateFIFO1();
                        NextState(States.WaitingWrite);
                        break;
                    }
                    break;
                case States.WaitingWrite:
                    if(queueWrite.IsFaulted)
                    {
                        queueWrite = Status.UpdateFIFO1();
                        Log.Error("Could not write the queue 1");
                    }
                    if (queueWrite.IsCompleted)
                    {
                        NextState(States.SendingFIFO);
                        Log.Info("Queue writen");
                    }
                    break;
                case States.SendingFIFO:
                    FatekPLC.ResetBit(FatekPLC.Signals.DelEmb1);
                    NextState(States.Completed);
                    Log.Info("Deletion success");
                    break;
                case States.Completed:
                    break;
                case States.Failed:
                    FatekPLC.ResetBit(FatekPLC.Signals.DelEmb1);
                    break;
            }
        }

        private bool Validate()
        {
            int index = (int)FatekPLC.Memory.FIFO11a + FatekPLC.ReadMemory(FatekPLC.Memory.DEL1Pos1);
            int index2 = (int)FatekPLC.Memory.FIFO21 + FatekPLC.ReadMemory(FatekPLC.Memory.DEL1Pos2);
            Log.InfoFormat("Comparing Qrs: {0}{1} -> {2}{3}",
                FatekPLC.ReadMemory(FatekPLC.Memory.DEL1b),
                FatekPLC.ReadMemory(FatekPLC.Memory.DEL1a),
                FatekPLC.ReadMemory((FatekPLC.Memory)(index + 1)),
                FatekPLC.ReadMemory((FatekPLC.Memory)index));
            Log.InfoFormat("Comparing Ids: {0} -> {1}",
                FatekPLC.ReadMemory(FatekPLC.Memory.DEL1ID),
                FatekPLC.ReadMemory((FatekPLC.Memory)index2));
            return FatekPLC.ReadMemory(FatekPLC.Memory.DEL1a) == FatekPLC.ReadMemory((FatekPLC.Memory)index) &&
                FatekPLC.ReadMemory(FatekPLC.Memory.DEL1b) == FatekPLC.ReadMemory((FatekPLC.Memory)(index + 1)) &&
                FatekPLC.ReadMemory(FatekPLC.Memory.DEL1ID) == FatekPLC.ReadMemory((FatekPLC.Memory)index2);
                
        }

        private void DeletePallet()
        {
            int index1 = (int)FatekPLC.Memory.FIFO11a + FatekPLC.ReadMemory(FatekPLC.Memory.DEL1Pos1);
            int len1 = FatekPLC.ReadMemory(FatekPLC.Memory.FIFO1Len);
            int end1 = ((int)FatekPLC.Memory.FIFO11a - 2) + 2 * len1;
            for (int i = index1; i < end1; i++)
            {
                FatekPLC.SetMemory((FatekPLC.Memory)i, FatekPLC.ReadMemory((FatekPLC.Memory)i + 2));
            }

            for (int i = end1; i < (int)FatekPLC.Memory.FIFO11a + 2 * len1; i++)
            {
                FatekPLC.SetMemory((FatekPLC.Memory)i, 0);
            }

            FatekPLC.SetMemory(FatekPLC.Memory.FIFO1Len, (short)(len1 - 1)); 

            int index2 = (int)FatekPLC.Memory.FIFO21 + FatekPLC.ReadMemory(FatekPLC.Memory.DEL1Pos2);
            int len2 = FatekPLC.ReadMemory(FatekPLC.Memory.FIFO2Len);
            int end2 = (int)FatekPLC.Memory.FIFO21 - 1 + len2;
            for (int i = index2; i < end2; i++)
            {
                FatekPLC.SetMemory((FatekPLC.Memory)i, FatekPLC.ReadMemory((FatekPLC.Memory)i + 1));
            }

            for (int i = end2; i < (int)FatekPLC.Memory.FIFO21 + len2; i++)
            {
                FatekPLC.SetMemory((FatekPLC.Memory)i, 0);
            }

            FatekPLC.SetMemory(FatekPLC.Memory.FIFO2Len, (short)(len2 - 1));
        }

        public async Task<bool> StartDelete(DeletePallet pallet)
        {
            Log.Info($"Start to delete pallet {pallet.Pallet.Qr}, in position {pallet.Position} from packager {pallet.Packager}");
            NextState(States.Validating);
            FatekPLC.SetMemory(FatekPLC.Memory.DEL1Pos1, (short)(2 * pallet.Position));
            FatekPLC.SetMemory(FatekPLC.Memory.DEL1Pos2, (short)pallet.Position);
            return await FatekPLC.SetQrAndId(FatekPLC.Memory.DEL1a, FatekPLC.Memory.DEL1ID, pallet.Pallet);        
        }
    }
}
