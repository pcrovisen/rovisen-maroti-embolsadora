// GENERATED FILE — do not edit.
// Mirrors the C# enums; regenerate with:
//   node WebHMI/devserver/generate_enums.mjs

const SIGNAL_NAMES = [
  "ReadQR",
  "Label1",
  "Label2",
  "SendingFIFOs",
  "Ready",
  "Del1Valid",
  "Del1Error",
  "Del2Valid",
  "Del2Error",
  "SendUpdate",
  "SendUpdate2",
  "CarWithPallet",
  "CarInB1",
  "CarInB2",
  "bcd1Avaliable",
  "bcd2Avaliable",
  "Leave1",
  "Leave2",
  "WaitBocedi",
  "WaitCar",
  "SlaveConnected",
  "WaitingPallet",
  "PLCStarting",
  "PLCLabeling1",
  "PLCLabeling2",
  "WaitCorrection1",
  "WaitCorrection2",
  "BCD2EntryError",
  "CarEntryError",
  "BCD1OK",
  "BCD2OK",
  "Pause",
  "ElevatorRequest",
  "LabelNull1",
  "LabelNull2",
  "WaitLabel1",
  "WaitLabel2"
];
const SIG = Object.fromEntries(SIGNAL_NAMES.map((n, i) => [n, i]));

// Coils 1-20, written by the PC, sent as PcSignals.
const PC_SIGNAL_NAMES = [
  "ReadingPallet",
  "SendingQR",
  "Labeling1",
  "Labeling2",
  "ToEmb1",
  "ToEmb2",
  "ReceivingFIFOs",
  "Alive",
  "DelEmb1",
  "DelEmb2",
  "ConfirmUpdate",
  "ConfirmUpdate2",
  "WeightOk1",
  "WeightOk2",
  "PalletLeave1",
  "PalletLeave2",
  "ErrorQr",
  "Waiting",
  "ElevatorAuth",
  "ElevatorFailedQr"
];
const PSIG = Object.fromEntries(PC_SIGNAL_NAMES.map((n, i) => [n, i]));

const CAR_POSITION = {
  "Unknown": 0,
  "GoingToB1": 1,
  "InB1": 2,
  "GoingToB2": 3,
  "InB2": 4
};

// Machine class -> state names, in ordinal order.
const STATES_BY_CLASS = {
  "AcceptHMIs": [
    "Init",
    "Listening",
    "Connecting",
    "Adding",
    "Pause"
  ],
  "CarMachine": [
    "UnknownPosition",
    "WaitingCarInB1",
    "WaitingCarWithPallet",
    "WaitingCarInB2",
    "WaitingCarEmpty",
    "WaitingGetQr",
    "WaitingGetPallet"
  ],
  "DeletePalletEmb": [
    "Waiting",
    "Validating",
    "ValidatingPLC",
    "WaitingWrite",
    "SendingFIFO",
    "Completed",
    "Failed"
  ],
  "ElevatorAccess": [
    "WaitingRequest",
    "ReadingQr",
    "WaitingAuth",
    "WaitingLeave",
    "Delay"
  ],
  "FatekPLCCommunication": [
    "Init",
    "Starting",
    "WaitingMemory",
    "WaitingInit",
    "Working"
  ],
  "HMIConnection": [
    "Init",
    "Waiting",
    "WaitingDelStart",
    "WaitingDeletion",
    "Responding",
    "Terminated"
  ],
  "NetworkPrinterConnection": [
    "Connect",
    "Connecting",
    "Connected"
  ],
  "OmronConnection": [
    "Connect",
    "Connecting",
    "Connected"
  ],
  "PalletEntry": [
    "Waiting",
    "ReadingQR",
    "WaitingSetQr",
    "WaitingSetEntryPallet",
    "WaitingAvailability",
    "DefaultBehavior",
    "AskingDB",
    "SendingID",
    "WaitForBocedi1",
    "WaitEnterBocedi",
    "WaitUpdateFIFO1",
    "UpdateFIFO1",
    "WaitForCar",
    "WaitEnterCar",
    "WaitUpdateCar",
    "UpdateCar",
    "ReadingQrInError",
    "Paused"
  ],
  "PalletLabel": [
    "WaitingPallet",
    "WaitUpdate",
    "WaitingCorrection",
    "Labeling",
    "WaitUpdate2",
    "WaitAck",
    "WaitLeaving",
    "WaitLeaveNull",
    "PalletNull"
  ],
  "PrinterMachine": [
    "Init",
    "RetreivingWeightLen",
    "RetreivingWeight",
    "SendWeightOk",
    "WeightOk",
    "RetreivingLabels",
    "WaitPallet",
    "WaitLabelInstruction",
    "Print1",
    "Print2",
    "WaitPrinter",
    "WaitPLCConfirmation",
    "WaitLabelLost",
    "WaitApplicatorReady",
    "Reset1",
    "Reset2",
    "Skipped",
    "Completed"
  ],
  "QrReadMachine": [
    "Init",
    "Reading",
    "Retrying",
    "Completed",
    "Failed"
  ],
  "QrReaderConnection": [
    "Init",
    "Disconnected",
    "Connected",
    "Wait"
  ]
};

// A machine's runtime Name is its class name plus an identifier, so match the
// longest class name that prefixes it (PalletLabel1 -> PalletLabel).
const MACHINE_CLASSES = Object.keys(STATES_BY_CLASS).sort((a, b) => b.length - a.length);
function statesForMachine(name) {
  const cls = MACHINE_CLASSES.find((c) => name.startsWith(c));
  return cls ? STATES_BY_CLASS[cls] : [];
}

// Ordinal maps for the machines whose states the pages test by name.
const PE = Object.fromEntries(STATES_BY_CLASS.PalletEntry.map((n, i) => [n, i]));
const EA = Object.fromEntries(STATES_BY_CLASS.ElevatorAccess.map((n, i) => [n, i]));
