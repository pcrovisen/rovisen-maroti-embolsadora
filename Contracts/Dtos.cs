using System.Collections.Generic;

namespace Wenco.Contracts
{
    /// <summary>
    /// The wire contract between ModbusServer and every HMI.
    ///
    /// These types used to be declared twice — once in ModbusServer/Status.cs and
    /// again, by hand, in TcpHMIClient/HMIClient.cs — which is why CLAUDE.md had a
    /// hard rule telling you to keep them in step. Sharing the assembly makes the
    /// compiler enforce it instead.
    ///
    /// Deliberately free of dependencies (no System.Text.Json, no log4net) so both
    /// projects can reference it without pulling anything else in: it is plain
    /// data, serialized by whoever owns the transport.
    /// </summary>
    public class Pallet
    {
        public string Qr { get; set; }
        public string Id { get; set; }
        public string Recipe { get; set; }
        public string Injector { get; set; }
        public bool Labeling { get; set; }
    }

    /// <summary>One Bocedi's queue, the pallet at the labeler and the one leaving.</summary>
    public class Packager
    {
        public List<Pallet> Queue { get; set; }
        public Pallet LabelPallet { get; set; }
        public Pallet ExitPallet { get; set; }
    }

    public class Car
    {
        public enum Position
        {
            Unknown,
            GoingToB1,
            InB1,
            GoingToB2,
            InB2,
        }

        public Position CarPosition { get; set; }
        public bool HasPallet { get; set; }
        public Pallet Pallet { get; set; }
    }

    public class Connections
    {
        public bool QrReader { get; set; }
        public bool MasterPLC { get; set; }
        public bool SlavePLC { get; set; }
        public bool WencoDB { get; set; }
        public bool Packager1 { get; set; }
        public bool Packager2 { get; set; }
        public bool Labeler1 { get; set; }
        public bool Labeler2 { get; set; }
    }

    /// <summary>Operator-facing text, in Spanish. Empty means "no error".</summary>
    public class ErrorMessages
    {
        public string BDC1Error { get; set; }
        public string BDC2Error { get; set; }
        public string EntryError { get; set; }
        public string CarError { get; set; }
    }

    /// <summary>An HMI's request to drop a pallet from a queue.</summary>
    public class DeletePallet
    {
        public Pallet Pallet { get; set; }
        public int Packager { get; set; }
        public int Position { get; set; }
    }

    /// <summary>The subset of the service configuration the HMIs display.</summary>
    public class HmiConfig
    {
        public int QrRetries { get; set; }
        public bool ContinueIfNoQr { get; set; }
        public bool ContinueIfNoDB { get; set; }
        public int DefaultRecipe { get; set; }
    }

    /// <summary>
    /// One snapshot, as sent on the HMI socket and (in the Web HMI) over SSE.
    ///
    /// Packager1/2, Car and States are null when nothing changed since this
    /// client's last message — see HMIConnection.CreateMessage.
    /// </summary>
    public class SystemStatus
    {
        public HmiConfig Config { get; set; }
        public bool[] Signals { get; set; }
        public bool[] PcSignals { get; set; }
        public Connections Connections { get; set; }
        public Pallet EntryPallet { get; set; }
        public ErrorMessages ErrorMessages { get; set; }
        public Packager Packager1 { get; set; }
        public Packager Packager2 { get; set; }
        public Car Car { get; set; }

        /// <summary>Machine name -> the numeric value of its current state.</summary>
        public Dictionary<string, int> MachineState { get; set; }

        /// <summary>Machine name -> state value -> state name.</summary>
        public Dictionary<string, Dictionary<int, string>> States { get; set; }
    }
}
