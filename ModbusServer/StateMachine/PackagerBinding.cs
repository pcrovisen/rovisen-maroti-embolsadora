using ModbusServer.Devices;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    /// <summary>
    /// Everything that differs between the two Bocedi lanes.
    ///
    /// `PalletLabel` and `DeletePalletEmb` are written once and instantiated
    /// twice, one binding each. They used to be four files — PalletLabel1/2 and
    /// DeletePalletEmb1/2 — that differed only by the digit in signal names,
    /// memory bases, App.config keys and error fields, and had already started to
    /// drift apart (a state sitting at a different position in one copy's enum,
    /// log messages fixed on one side only).
    /// </summary>
    internal sealed class PackagerBinding
    {
        public static readonly PackagerBinding Bocedi1 = new PackagerBinding
        {
            Number = 1,

            // Labeling handshake (PalletLabel).
            Label = FatekPLC.Signals.Label1,
            Labeling = FatekPLC.Signals.Labeling1,
            PLCLabeling = FatekPLC.Signals.PLCLabeling1,
            WaitCorrection = FatekPLC.Signals.WaitCorrection1,
            LabelNull = FatekPLC.Signals.LabelNull1,
            WeightOk = FatekPLC.Signals.WeightOk1,
            Leave = FatekPLC.Signals.Leave1,
            WaitLabel = FatekPLC.Signals.WaitLabel1,
            PalletLeave = FatekPLC.Signals.PalletLeave1,

            // Deletion handshake (DeletePalletEmb).
            DelEmb = FatekPLC.Signals.DelEmb1,
            DelValid = FatekPLC.Signals.Del1Valid,
            DelError = FatekPLC.Signals.Del1Error,

            // Deletion scratch registers. DelQr is the low word; the high word is
            // always the next register (DEL1b).
            DelQr = FatekPLC.Memory.DEL1a,
            DelId = FatekPLC.Memory.DEL1ID,
            DelQrPos = FatekPLC.Memory.DEL1Pos1,
            DelIdPos = FatekPLC.Memory.DEL1Pos2,

            // Queue registers: QRs take two registers per pallet, ids one.
            QrFifo = FatekPLC.Memory.FIFO11a,
            QrFifoLen = FatekPLC.Memory.FIFO1Len,
            IdFifo = FatekPLC.Memory.FIFO21,
            IdFifoLen = FatekPLC.Memory.FIFO2Len,

            // Devices. "Wolrdjet" (sic) is load-bearing: it is the registry key
            // under HKLM\SOFTWARE\WencoInfo where the last weight is cached, and
            // PrinterMachine derives the packager number from it.
            PrinterIpKey = "ipPrinter1",
            OmronIpKey = "ipOmron1",
            PrinterIdentifier = "Wolrdjet1",
        };

        public static readonly PackagerBinding Bocedi2 = new PackagerBinding
        {
            Number = 2,

            Label = FatekPLC.Signals.Label2,
            Labeling = FatekPLC.Signals.Labeling2,
            PLCLabeling = FatekPLC.Signals.PLCLabeling2,
            WaitCorrection = FatekPLC.Signals.WaitCorrection2,
            LabelNull = FatekPLC.Signals.LabelNull2,
            WeightOk = FatekPLC.Signals.WeightOk2,
            Leave = FatekPLC.Signals.Leave2,
            WaitLabel = FatekPLC.Signals.WaitLabel2,
            PalletLeave = FatekPLC.Signals.PalletLeave2,

            DelEmb = FatekPLC.Signals.DelEmb2,
            DelValid = FatekPLC.Signals.Del2Valid,
            DelError = FatekPLC.Signals.Del2Error,

            DelQr = FatekPLC.Memory.DEL2a,
            DelId = FatekPLC.Memory.DEL2ID,
            DelQrPos = FatekPLC.Memory.DEL2Pos1,
            DelIdPos = FatekPLC.Memory.DEL2Pos2,

            QrFifo = FatekPLC.Memory.FIFO31a,
            QrFifoLen = FatekPLC.Memory.FIFO3Len,
            IdFifo = FatekPLC.Memory.FIFO41,
            IdFifoLen = FatekPLC.Memory.FIFO4Len,

            PrinterIpKey = "ipPrinter2",
            OmronIpKey = "ipOmron2",
            PrinterIdentifier = "Wolrdjet2",
        };

        private PackagerBinding() { }

        public int Number { get; private set; }

        public FatekPLC.Signals Label { get; private set; }
        public FatekPLC.Signals Labeling { get; private set; }
        public FatekPLC.Signals PLCLabeling { get; private set; }
        public FatekPLC.Signals WaitCorrection { get; private set; }
        public FatekPLC.Signals LabelNull { get; private set; }
        public FatekPLC.Signals WeightOk { get; private set; }
        public FatekPLC.Signals Leave { get; private set; }
        public FatekPLC.Signals WaitLabel { get; private set; }
        public FatekPLC.Signals PalletLeave { get; private set; }

        public FatekPLC.Signals DelEmb { get; private set; }
        public FatekPLC.Signals DelValid { get; private set; }
        public FatekPLC.Signals DelError { get; private set; }

        public FatekPLC.Memory DelQr { get; private set; }
        public FatekPLC.Memory DelId { get; private set; }
        public FatekPLC.Memory DelQrPos { get; private set; }
        public FatekPLC.Memory DelIdPos { get; private set; }

        public FatekPLC.Memory QrFifo { get; private set; }
        public FatekPLC.Memory QrFifoLen { get; private set; }
        public FatekPLC.Memory IdFifo { get; private set; }
        public FatekPLC.Memory IdFifoLen { get; private set; }

        public string PrinterIpKey { get; private set; }
        public string OmronIpKey { get; private set; }
        public string PrinterIdentifier { get; private set; }

        /// <summary>This lane's slice of the shared status model.</summary>
        public Packager Packager
        {
            get { return Number == 1 ? Status.Instance.Packager1 : Status.Instance.Packager2; }
        }

        /// <summary>Re-reads this lane's queue from PLC memory (serialized by Status).</summary>
        public Task UpdateFIFO()
        {
            return Number == 1 ? Status.UpdateFIFO1() : Status.UpdateFIFO2();
        }

        /// <summary>Operator-facing message for this Bocedi (Spanish). Empty clears it.</summary>
        public void SetError(string message)
        {
            if (Number == 1)
            {
                Status.Instance.ErrorMessages.BDC1Error = message;
            }
            else
            {
                Status.Instance.ErrorMessages.BDC2Error = message;
            }
        }

        public void SetLabelerConnected(bool connected)
        {
            if (Number == 1)
            {
                Status.Instance.Connections.Labeler1 = connected;
            }
            else
            {
                Status.Instance.Connections.Labeler2 = connected;
            }
        }
    }
}
