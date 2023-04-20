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
    internal class DeletePalletEmb2 : Machine
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public enum States
        {
            Waiting,
            Validating,
            ValidatingPLC,
            SendingFIFO,
            Completed,
            Failed,
        }

        public static DeletePalletEmb2 Instance { get; set; }
        public bool needDel = false;
        public bool Completed
        {
            get { return (States)State == States.Completed; }
        }

        public bool Failed
        {
            get { return (States)State == States.Failed; }
        }

        public DeletePalletEmb2() : base(States.Waiting)
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
                    FatekPLC.SetBit(FatekPLC.Signals.DelEmb2);
                    if (FatekPLC.ReadBit(FatekPLC.Signals.Del2Error))
                    {
                        NextState(States.Failed);
                        Log.Info("Deletion failed in PLC");
                        break;
                    }
                    if (FatekPLC.ReadBit(FatekPLC.Signals.Del2Valid))
                    {
                        Log.Info("Valid in PLC side");
                        DeletePallet();
                        Status.UpdateFIFO2();
                        NextState(States.SendingFIFO);
                        break;
                    }
                    break;
                case States.Failed:
                    FatekPLC.ResetBit(FatekPLC.Signals.DelEmb2);
                    break;
                case States.SendingFIFO:
                    FatekPLC.ResetBit(FatekPLC.Signals.DelEmb2);
                    NextState(States.Completed);
                    Log.Info("Deletion success");
                    break;
                case States.Completed:
                    break;
            }
        }
        private bool Validate()
        {
            int index = (int)FatekPLC.Memory.FIFO31a + FatekPLC.ReadMemory(FatekPLC.Memory.DEL2Pos1);
            int index2 = (int)FatekPLC.Memory.FIFO41 + FatekPLC.ReadMemory(FatekPLC.Memory.DEL2Pos2);
            Log.InfoFormat("Comparing Qrs: {0}{1} -> {2}{3}",
                FatekPLC.ReadMemory(FatekPLC.Memory.DEL2b),
                FatekPLC.ReadMemory(FatekPLC.Memory.DEL2a),
                FatekPLC.ReadMemory((FatekPLC.Memory)(index + 1)),
                FatekPLC.ReadMemory((FatekPLC.Memory)index));
            Log.InfoFormat("Comparing Ids: {0} -> {1}",
                FatekPLC.ReadMemory(FatekPLC.Memory.DEL2ID),
                FatekPLC.ReadMemory((FatekPLC.Memory)index2));
            return FatekPLC.ReadMemory(FatekPLC.Memory.DEL2a) == FatekPLC.ReadMemory((FatekPLC.Memory)index) &&
                FatekPLC.ReadMemory(FatekPLC.Memory.DEL2b) == FatekPLC.ReadMemory((FatekPLC.Memory)(index + 1)) &&
                FatekPLC.ReadMemory(FatekPLC.Memory.DEL2ID) == FatekPLC.ReadMemory((FatekPLC.Memory)index2);

        }

        private void DeletePallet()
        {
            int index1 = (int)FatekPLC.Memory.FIFO31a + FatekPLC.ReadMemory(FatekPLC.Memory.DEL2Pos1);
            int len1 = FatekPLC.ReadMemory(FatekPLC.Memory.FIFO3Len);
            int end1 = ((int)FatekPLC.Memory.FIFO31a - 2) + 2 * len1;
            for (int i = index1; i < end1; i++)
            {
                FatekPLC.SetMemory((FatekPLC.Memory)i, FatekPLC.ReadMemory((FatekPLC.Memory)i + 2));
            }

            for (int i = end1; i < (int)FatekPLC.Memory.FIFO31a + 2 * len1; i++)
            {
                FatekPLC.SetMemory((FatekPLC.Memory)i, 0);
            }

            FatekPLC.SetMemory(FatekPLC.Memory.FIFO3Len, (short)(len1 - 1));

            int index2 = (int)FatekPLC.Memory.FIFO41 + FatekPLC.ReadMemory(FatekPLC.Memory.DEL2Pos2);
            int len2 = FatekPLC.ReadMemory(FatekPLC.Memory.FIFO4Len);
            int end2 = (int)FatekPLC.Memory.FIFO41 - 1 + len2;
            for (int i = index2; i < end2; i++)
            {
                FatekPLC.SetMemory((FatekPLC.Memory)i, FatekPLC.ReadMemory((FatekPLC.Memory)i + 1));
            }

            for (int i = end2; i < (int)FatekPLC.Memory.FIFO41 + len2; i++)
            {
                FatekPLC.SetMemory((FatekPLC.Memory)i, 0);
            }

            FatekPLC.SetMemory(FatekPLC.Memory.FIFO4Len, (short)(len2 - 1));
        }

        public void StartDelete(DeletePallet pallet)
        {
            Log.Info($"Start to delete pallet {pallet.Pallet.Qr}, in position {pallet.Position} from packager {pallet.Packager}");
            NextState(States.Validating);
            FatekPLC.SetMemory(FatekPLC.Memory.DEL2Pos1, (short)(2 * pallet.Position));
            FatekPLC.SetMemory(FatekPLC.Memory.DEL2Pos2, (short)pallet.Position);

            FatekPLC.SetQrAndId(FatekPLC.Memory.DEL2a, FatekPLC.Memory.DEL2ID, pallet.Pallet);
        }
    }
}
