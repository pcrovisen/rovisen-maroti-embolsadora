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
            WaitingWrite,
            SendingFIFO,
            Completed,
            Failed,
        }

        // A result nobody collects would wedge the machine — and with it every
        // later deletion — because only the requesting HMIConnection calls Reset().
        const int AbandonedResultMs = 10000;

        // The PLC answers DelEmb2 with Del2Valid/Del2Error within a scan.
        const int PlcAnswerTimeoutMs = 10000;

        Task queueWrite;

        // Set by StartDelete once every DEL* register is written, read by Step()
        // on the step thread: this is the handshake that publishes a request.
        bool needDel = false;

        // Claimed synchronously by StartDelete, before its first await, so two
        // HMIs cannot both pass the "is it free?" check in the same cycle.
        bool requestInFlight = false;

        public static DeletePalletEmb2 Instance { get; set; }
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
                        queueWrite = Status.UpdateFIFO2();
                        NextState(States.WaitingWrite);
                        break;
                    }
                    if (StateTime.ElapsedMilliseconds > PlcAnswerTimeoutMs)
                    {
                        // The PLC answers Del2Valid/Del2Error within a scan. Without
                        // this the machine waits forever when the PLC is stopped or
                        // the link drops mid-deletion.
                        Log.Error("The PLC did not answer the deletion request");
                        NextState(States.Failed);
                    }
                    break;
                case States.WaitingWrite:
                    if(queueWrite.IsCompleted)
                    {
                        if (queueWrite.IsFaulted)
                        {
                            if(StateTime.ElapsedMilliseconds > 100)
                            {
                                queueWrite = Status.UpdateFIFO1();
                                Log.Error("Could not write the queue 2");
                                NextState(States.WaitingWrite);
                            }
                        }
                        else
                        {
                            NextState(States.SendingFIFO);
                            Log.Info("Queue writen");
                        }
                    }
                    break;
                case States.Failed:
                    FatekPLC.ResetBit(FatekPLC.Signals.DelEmb2);
                    ResetIfAbandoned();
                    break;
                case States.SendingFIFO:
                    FatekPLC.ResetBit(FatekPLC.Signals.DelEmb2);
                    NextState(States.Completed);
                    Log.Info("Deletion success");
                    break;
                case States.Completed:
                    ResetIfAbandoned();
                    break;
            }
        }

        public override void Reset()
        {
            base.Reset();
            needDel = false;
            requestInFlight = false;
        }

        // The requester reads Completed/Failed on its next step (100 ms later) and
        // resets the machine. If it disconnected before that, nothing else would,
        // and every later deletion would be refused.
        private void ResetIfAbandoned()
        {
            if (StateTime.ElapsedMilliseconds > AbandonedResultMs)
            {
                Log.Warn("Deletion result was never collected. Returning to idle");
                Reset();
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

        // Called from HMIConnection.Step(). Only the part before the first await
        // runs on the step thread: SetQrAndId awaits a SQL round trip (QR string ->
        // id) and resumes on a thread-pool thread. So the machine must not be moved
        // to Validating here — it used to be, and Step() then ran Validate() against
        // DEL* registers that had not been written yet. The registers are written
        // first and the request is published last, with needDel.
        //
        // Returns false when the request could not be started; the caller decides
        // whether to retry or to answer NOK.
        public async Task<bool> StartDelete(DeletePallet pallet)
        {
            if (requestInFlight || (States)State != States.Waiting)
            {
                return false;
            }
            requestInFlight = true;

            Log.Info($"Start to delete pallet {pallet.Pallet.Qr}, in position {pallet.Position} from packager {pallet.Packager}");
            FatekPLC.SetMemory(FatekPLC.Memory.DEL2Pos1, (short)(2 * pallet.Position));
            FatekPLC.SetMemory(FatekPLC.Memory.DEL2Pos2, (short)pallet.Position);

            if (!await FatekPLC.SetQrAndId(FatekPLC.Memory.DEL2a, FatekPLC.Memory.DEL2ID, pallet.Pallet))
            {
                Log.Error("Could not write the pallet to delete");
                requestInFlight = false;
                return false;
            }

            needDel = true;
            return true;
        }
    }
}
