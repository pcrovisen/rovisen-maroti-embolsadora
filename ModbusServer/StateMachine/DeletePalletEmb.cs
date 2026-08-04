using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using log4net;
using ModbusServer.Devices;

namespace ModbusServer.StateMachine
{
    /// <summary>
    /// HMI-requested removal of a pallet from one lane's FIFO queues, validated on
    /// both sides: the PC checks the scratch registers against the queue contents
    /// at that position, then the PLC is asked to agree.
    ///
    /// One instance per lane (see <see cref="PackagerBinding"/>); Name stays
    /// DeletePalletEmb1 / DeletePalletEmb2, which the HMI and transitions.json
    /// key on.
    /// </summary>
    internal class DeletePalletEmb : Machine<DeletePalletEmb.States>
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

        // The PLC answers DelEmb with DelValid/DelError within a scan.
        const int PlcAnswerTimeoutMs = 10000;

        static readonly Dictionary<int, DeletePalletEmb> instances = new Dictionary<int, DeletePalletEmb>();

        readonly PackagerBinding lane;
        Task queueWrite;

        // Set by StartDelete once every DEL* register is written, read by Step()
        // on the step thread: this is the handshake that publishes a request.
        bool needDel = false;

        // Claimed synchronously by StartDelete, before its first await, so two
        // HMIs cannot both pass the "is it free?" check in the same cycle.
        bool requestInFlight = false;

        public static DeletePalletEmb Instance(int packager)
        {
            return instances[packager];
        }

        public bool Completed
        {
            get { return State == States.Completed; }
        }

        public bool Failed
        {
            get { return State == States.Failed; }
        }

        public DeletePalletEmb(PackagerBinding lane) : base(States.Waiting, lane.Number.ToString())
        {
            this.lane = lane;
            instances[lane.Number] = this;
        }

        protected override void OnStep()
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
                    FatekPLC.SetBit(lane.DelEmb);
                    if (FatekPLC.ReadBit(lane.DelError))
                    {
                        Log.Info("Deletion failed in PLC");
                        NextState(States.Failed);
                        break;
                    }
                    if (FatekPLC.ReadBit(lane.DelValid))
                    {
                        Log.Info("Valid in PLC side");
                        DeletePallet();
                        queueWrite = lane.UpdateFIFO();
                        NextState(States.WaitingWrite);
                        break;
                    }
                    if (StateTime.ElapsedMilliseconds > PlcAnswerTimeoutMs)
                    {
                        // Without this the machine waits forever when the PLC is
                        // stopped or the link drops mid-deletion.
                        Log.Error("The PLC did not answer the deletion request");
                        NextState(States.Failed);
                    }
                    break;
                case States.WaitingWrite:
                    if (TryComplete(ref queueWrite, lane.UpdateFIFO, "Re-reading the queue after deletion"))
                    {
                        NextState(States.SendingFIFO);
                        Log.Info("Queue writen");
                    }
                    break;
                case States.SendingFIFO:
                    FatekPLC.ResetBit(lane.DelEmb);
                    NextState(States.Completed);
                    Log.Info("Deletion success");
                    break;
                case States.Completed:
                    ResetIfAbandoned();
                    break;
                case States.Failed:
                    FatekPLC.ResetBit(lane.DelEmb);
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

        // The pallet the HMI named must still be where it said it was: compare the
        // scratch registers with the queue contents at that position.
        private bool Validate()
        {
            int qrIndex = (int)lane.QrFifo + FatekPLC.ReadMemory(lane.DelQrPos);
            int idIndex = (int)lane.IdFifo + FatekPLC.ReadMemory(lane.DelIdPos);

            Log.InfoFormat("Comparing Qrs: {0}{1} -> {2}{3}",
                FatekPLC.ReadMemory(lane.DelQr + 1),
                FatekPLC.ReadMemory(lane.DelQr),
                FatekPLC.ReadMemory((FatekPLC.Memory)(qrIndex + 1)),
                FatekPLC.ReadMemory((FatekPLC.Memory)qrIndex));
            Log.InfoFormat("Comparing Ids: {0} -> {1}",
                FatekPLC.ReadMemory(lane.DelId),
                FatekPLC.ReadMemory((FatekPLC.Memory)idIndex));

            return FatekPLC.ReadMemory(lane.DelQr) == FatekPLC.ReadMemory((FatekPLC.Memory)qrIndex) &&
                FatekPLC.ReadMemory(lane.DelQr + 1) == FatekPLC.ReadMemory((FatekPLC.Memory)(qrIndex + 1)) &&
                FatekPLC.ReadMemory(lane.DelId) == FatekPLC.ReadMemory((FatekPLC.Memory)idIndex);
        }

        // Compacts both FIFOs in place over the removed slot and shortens them.
        // QRs occupy two registers per pallet, ids one.
        private void DeletePallet()
        {
            int qrStart = (int)lane.QrFifo + FatekPLC.ReadMemory(lane.DelQrPos);
            int qrLen = FatekPLC.ReadMemory(lane.QrFifoLen);
            int qrEnd = ((int)lane.QrFifo - 2) + 2 * qrLen;
            for (int i = qrStart; i < qrEnd; i++)
            {
                FatekPLC.SetMemory((FatekPLC.Memory)i, FatekPLC.ReadMemory((FatekPLC.Memory)i + 2));
            }

            for (int i = qrEnd; i < (int)lane.QrFifo + 2 * qrLen; i++)
            {
                FatekPLC.SetMemory((FatekPLC.Memory)i, 0);
            }

            FatekPLC.SetMemory(lane.QrFifoLen, (short)(qrLen - 1));

            int idStart = (int)lane.IdFifo + FatekPLC.ReadMemory(lane.DelIdPos);
            int idLen = FatekPLC.ReadMemory(lane.IdFifoLen);
            int idEnd = (int)lane.IdFifo - 1 + idLen;
            for (int i = idStart; i < idEnd; i++)
            {
                FatekPLC.SetMemory((FatekPLC.Memory)i, FatekPLC.ReadMemory((FatekPLC.Memory)i + 1));
            }

            for (int i = idEnd; i < (int)lane.IdFifo + idLen; i++)
            {
                FatekPLC.SetMemory((FatekPLC.Memory)i, 0);
            }

            FatekPLC.SetMemory(lane.IdFifoLen, (short)(idLen - 1));
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
            if (requestInFlight || State != States.Waiting)
            {
                return false;
            }
            requestInFlight = true;

            Log.Info($"Start to delete pallet {pallet.Pallet.Qr}, in position {pallet.Position} from packager {pallet.Packager}");
            FatekPLC.SetMemory(lane.DelQrPos, (short)(2 * pallet.Position));
            FatekPLC.SetMemory(lane.DelIdPos, (short)pallet.Position);

            if (!await FatekPLC.SetQrAndId(lane.DelQr, lane.DelId, pallet.Pallet))
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
