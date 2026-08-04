namespace Wenco.Contracts
{
    /// <summary>
    /// Modbus coils on the Fatek master PLC link, and therefore a contract with
    /// the ladder programs in PLCs/*.pdw as well as with the HMIs — do not
    /// renumber without changing the PLC program too.
    ///
    /// Convention: 1-20 are written by the PC, 21+ by the PLC. Both ranges are
    /// sent to the HMIs, as PcSignals and Signals respectively.
    /// </summary>
    public enum Signals
    {
        // Written by the PC.
        ReadingPallet = 1,
        SendingQR,
        Labeling1,
        Labeling2,
        ToEmb1,
        ToEmb2,
        ReceivingFIFOs,
        Alive,
        DelEmb1,
        DelEmb2,
        ConfirmUpdate,
        ConfirmUpdate2,
        WeightOk1,
        WeightOk2,
        PalletLeave1,
        PalletLeave2,
        ErrorQr,
        Waiting,
        ElevatorAuth,
        ElevatorFailedQr,

        // Written by the PLC.
        ReadQR = 21,
        Label1,
        Label2,
        SendingFIFOs,
        Ready,
        Del1Valid,
        Del1Error,
        Del2Valid,
        Del2Error,
        SendUpdate,
        SendUpdate2,
        CarWithPallet,
        CarInB1,
        CarInB2,
        bcd1Avaliable,
        bcd2Avaliable,
        Leave1,
        Leave2,
        WaitBocedi,
        WaitCar,
        SlaveConnected,
        WaitingPallet,
        PLCStarting,
        PLCLabeling1,
        PLCLabeling2,
        WaitCorrection1,
        WaitCorrection2,
        BCD2EntryError,
        CarEntryError,
        BCD1OK,
        BCD2OK,
        Pause,
        ElevatorRequest,
        LabelNull1,
        LabelNull2,
        WaitLabel1,
        WaitLabel2,
    }

    /// <summary>
    /// Positions inside the two bool arrays an HMI receives. The snapshot sends
    /// each range from its own base, so a coil number is not an array index —
    /// this is the only place that conversion should be written.
    /// </summary>
    public static class SignalIndex
    {
        public const int PcFirst = (int)Signals.ReadingPallet;
        public const int PcLast = (int)Signals.ElevatorFailedQr;
        public const int PlcFirst = (int)Signals.ReadQR;
        public const int PlcLast = (int)Signals.WaitLabel2;

        public const int PcCount = PcLast - PcFirst + 1;
        public const int PlcCount = PlcLast - PlcFirst + 1;

        /// <summary>Index into SystemStatus.Signals (the coils the PLC writes).</summary>
        public static int Plc(Signals signal)
        {
            return (int)signal - PlcFirst;
        }

        /// <summary>Index into SystemStatus.PcSignals (the coils the PC writes).</summary>
        public static int Pc(Signals signal)
        {
            return (int)signal - PcFirst;
        }
    }
}
