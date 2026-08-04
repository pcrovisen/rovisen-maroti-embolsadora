namespace Wenco.Contracts
{
    /// <summary>
    /// States of the entry station's machine (ModbusServer PalletEntry).
    ///
    /// Shared because the HMIs drive their entry animation off it, so the order
    /// is part of the wire contract: the snapshot carries the numeric value.
    /// Every other machine's states stay private to the server — the HMIs only
    /// display those by name, from the States table in the snapshot.
    /// </summary>
    public enum PalletEntryState
    {
        Waiting,
        ReadingQR,
        WaitingSetQr,
        WaitingSetEntryPallet,
        WaitingAvailability,
        DefaultBehavior,
        AskingDB,
        SendingID,
        WaitForBocedi1,
        WaitEnterBocedi,
        WaitUpdateFIFO1,
        UpdateFIFO1,
        WaitForCar,
        WaitEnterCar,
        WaitUpdateCar,
        UpdateCar,
        [FaultState] ReadingQrInError,
        Paused,
    }
}
