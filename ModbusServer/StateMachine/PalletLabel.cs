using log4net;
using mcOMRON;
using ModbusServer.Devices;
using System.Configuration;
using System.Reflection;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    /// <summary>
    /// Exit of one Bocedi: a pallet reaches the label position, its weight is read
    /// and its labels printed and applied (delegated to <see cref="PrinterMachine"/>),
    /// then the pallet is reported out and leaves.
    ///
    /// One instance per lane (see <see cref="PackagerBinding"/>); Name stays
    /// PalletLabel1 / PalletLabel2, which the HMI and transitions.json key on.
    /// </summary>
    internal class PalletLabel : Machine
    {
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        enum States
        {
            WaitingPallet,
            WaitUpdate,
            WaitingCorrection,
            Labeling,
            WaitUpdate2,
            WaitAck,
            WaitLeaving,
            WaitLeaveNull,
            PalletNull,
        }

        readonly PackagerBinding lane;
        readonly OmronPLC plc;
        readonly NetworkPrinter printer;
        readonly OmronConnection omronConnection;
        readonly NetworkPrinterConnection printerConnection;
        readonly PrinterMachine printerMachine;

        string currentCode;
        Task<bool> palletLeaveTask;
        Task writeTask;

        public PalletLabel(PackagerBinding lane) : base(States.WaitingPallet, lane.Number.ToString())
        {
            this.lane = lane;
            plc = new OmronPLC(TransportType.Tcp);
            printer = new NetworkPrinter(ConfigurationManager.AppSettings[lane.PrinterIpKey]);
            omronConnection = new OmronConnection(plc, ConfigurationManager.AppSettings[lane.OmronIpKey]);
            printerConnection = new NetworkPrinterConnection(printer);
            printerMachine = new PrinterMachine(plc, printer, lane);
        }

        public override void Step()
        {
            omronConnection.Step();
            printerConnection.Step();
            lane.SetLabelerConnected(printerConnection.Connected);

            if (!omronConnection.Connected || !printerConnection.Connected)
            {
                return;
            }

            switch (State)
            {
                case States.WaitingPallet:
                    lane.SetError("");
                    if (FatekPLC.ReadBit(lane.Label))
                    {
                        FatekPLC.ResetBit(lane.PalletLeave);
                        writeTask = lane.UpdateFIFO();
                        NextState(States.WaitUpdate);
                    }
                    if (FatekPLC.ReadBit(lane.PLCLabeling))
                    {
                        // Resume after a service restart: the weight was already taken.
                        if (lane.Packager.LabelPallet != null)
                        {
                            FatekPLC.ResetBit(lane.PalletLeave);
                            currentCode = lane.Packager.LabelPallet.Qr;
                            Log.InfoFormat("Continue labeling pallet {0} in bocedi{1}", currentCode, lane.Number);
                            FatekPLC.SetBit(lane.Labeling);
                            printerMachine.Reset(currentCode, lane.Packager.LabelPallet.Labeling, true);
                            lane.SetError("");
                            NextState(States.Labeling);
                        }
                        else
                        {
                            Log.Warn("Recover but no pallet yet.");
                        }
                    }
                    if (FatekPLC.ReadBit(lane.WaitCorrection))
                    {
                        _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.desorden_embolsadora, packager: lane.Number);
                        Log.InfoFormat("ID not corresponding with the machine in bocedi{0}", lane.Number);
                        NextState(States.WaitingCorrection);
                    }
                    if (FatekPLC.ReadBit(lane.LabelNull))
                    {
                        Log.Warn("Pallet in labeling, but queue is empty");
                        printerMachine.Reset("", false);
                        NextState(States.WaitLeaveNull);
                    }
                    break;
                case States.WaitUpdate:
                    if (TryComplete(ref writeTask, lane.UpdateFIFO, "Re-reading the queue at the label position"))
                    {
                        Log.InfoFormat("Fifo {0} updated", lane.Number);
                        if (lane.Packager.LabelPallet != null)
                        {
                            currentCode = lane.Packager.LabelPallet.Qr;
                            Log.InfoFormat("Start labeling pallet {0} in bocedi{1}", currentCode, lane.Number);
                            FatekPLC.SetBit(lane.Labeling);
                            printerMachine.Reset(currentCode, lane.Packager.LabelPallet.Labeling);
                            NextState(States.Labeling);
                        }
                        else
                        {
                            Log.Error("Pallet found null after fifo update");
                            lane.SetError("No se pudo recuperar la información del pallet. Sacar el pallet manualmente.");
                            NextState(States.PalletNull);
                        }
                    }
                    break;
                case States.WaitingCorrection:
                    lane.SetError("Se encontró una inconsistencia entre el ID de la cola y el ID entregado por la máquina. Corregir esto y luego presionar Start.");
                    if (!FatekPLC.ReadBit(lane.WaitCorrection))
                    {
                        Log.Info("Retring labeling");
                        NextState(States.WaitingPallet);
                    }
                    break;
                case States.Labeling:
                    printerMachine.Step();
                    if (printerMachine.WeightOk)
                    {
                        FatekPLC.SetBit(lane.WeightOk);
                    }
                    if (FatekPLC.ReadBit(lane.Leave))
                    {
                        writeTask = lane.UpdateFIFO();
                        NextState(States.WaitUpdate2);
                    }
                    if (FatekPLC.ReadBit(lane.WaitLabel) && StateTime.ElapsedMilliseconds > 1000)
                    {
                        FatekPLC.ResetBit(lane.WeightOk);
                        NextState(States.WaitingPallet);
                    }
                    break;
                case States.WaitUpdate2:
                    if (TryComplete(ref writeTask, lane.UpdateFIFO, "Re-reading the queue after the pallet left"))
                    {
                        Log.InfoFormat("Fifo {0} updated", lane.Number);
                        FatekPLC.ResetBit(lane.Labeling);
                        Log.InfoFormat("Notify pallet out {0} with code {1}", lane.PrinterIdentifier, currentCode);
                        palletLeaveTask = SqlDatabase.NotifyPalletOut(currentCode);
                        NextState(States.WaitAck);
                    }
                    break;
                case States.WaitAck:
                    FatekPLC.ResetBit(lane.WeightOk);
                    if (palletLeaveTask.IsCompleted)
                    {
                        // The pallet physically left either way; a database that did
                        // not take the notification must not hold the line.
                        if (palletLeaveTask.IsFaulted || !palletLeaveTask.Result)
                        {
                            Log.Error("Couldnt sent message to the SQL server");
                        }
                        else
                        {
                            Log.InfoFormat("Pallet {0} leave", currentCode);
                        }
                        FatekPLC.SetBit(lane.PalletLeave);
                        Log.InfoFormat("Waiting PLC{0} to leave", lane.Number);
                        NextState(States.WaitLeaving);
                    }
                    break;
                case States.WaitLeaving:
                    if (!FatekPLC.ReadBit(lane.Leave))
                    {
                        NextState(States.WaitingPallet);
                        Log.InfoFormat("Waiting pallet to label{0}", lane.Number);
                    }
                    break;
                case States.WaitLeaveNull:
                    lane.SetError("Hay un pallet en el etiquetado, pero las colas estan vacías. El pallet no será etiquetado");
                    printerMachine.Step();
                    if (!FatekPLC.ReadBit(lane.LabelNull))
                    {
                        NextState(States.WaitingPallet);
                        Log.InfoFormat("Waiting pallet to label{0}", lane.Number);
                    }
                    break;
                case States.PalletNull:
                    if (FatekPLC.ReadBit(lane.WaitLabel))
                    {
                        NextState(States.WaitingPallet);
                    }
                    break;
            }
        }
    }
}
