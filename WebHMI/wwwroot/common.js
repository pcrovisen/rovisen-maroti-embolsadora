// Shared constants for the Web HMI pages (loaded before app.js / diag.js).
// Enum mirrors: same order as ModbusServer / FatekPLC.Signals coils 21+.

'use strict';

const SIGNAL_NAMES = [
  'ReadQR', 'Label1', 'Label2', 'SendingFIFOs', 'Ready',
  'Del1Valid', 'Del1Error', 'Del2Valid', 'Del2Error',
  'SendUpdate', 'SendUpdate2', 'CarWithPallet', 'CarInB1', 'CarInB2',
  'bcd1Avaliable', 'bcd2Avaliable', 'Leave1', 'Leave2',
  'WaitBocedi', 'WaitCar', 'SlaveConnected', 'WaitingPallet',
  'PLCStarting', 'PLCLabeling1', 'PLCLabeling2',
  'WaitCorrection1', 'WaitCorrection2', 'BCD2EntryError',
];
const SIG = Object.fromEntries(SIGNAL_NAMES.map((n, i) => [n, i]));

const PE = {
  Waiting: 0, ReadingQR: 1, WaitingSetQr: 2, WaitingSetEntryPallet: 3,
  WaitingAvailability: 4, DefaultBehavior: 5, AskingDB: 6, SendingID: 7,
  WaitForBocedi1: 8, WaitEnterBocedi: 9, WaitUpdateFIFO1: 10, UpdateFIFO1: 11,
  WaitForCar: 12, WaitEnterCar: 13, WaitUpdateCar: 14, UpdateCar: 15,
  ReadingQrInError: 16, Paused: 17,
};

const CAR_POS_TEXT = {
  0: 'Posición desconocida',
  1: 'Hacia Bocedi 1',
  2: 'En Bocedi 1',
  3: 'Hacia Bocedi 2',
  4: 'En Bocedi 2',
};

const CONN_LABELS = {
  MasterPLC: 'PLC Maestro',
  SlavePLC: 'PLC Esclavo',
  WencoDB: 'BD Wenco',
  QrReader: 'Lector QR',
  Packager1: 'Embolsadora 1',
  Packager2: 'Embolsadora 2',
  Labeler1: 'Etiquetadora 1',
  Labeler2: 'Etiquetadora 2',
};

function escapeHtml(s) {
  return String(s ?? '').replace(/[&<>"']/g, (c) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
  }[c]));
}
