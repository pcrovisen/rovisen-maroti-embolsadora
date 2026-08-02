// Dummy development server for the Web HMI.
//
// Emits the same SystemStatus JSON the real ModbusServer will push (see
// HMIConnection.CreateMessage and WebHMI/README.md for the contract) and
// simulates the plant line: pallets entering via QR, routing to Bocedi 1
// directly or to Bocedi 2 through the transfer car, labeling and exit.
//
// Zero dependencies. Run:  node WebHMI/devserver/dummy_server.mjs
// Then open http://localhost:8080  (supervisor PIN: 1234)

import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const PORT = process.env.PORT || 8080;
const PIN = '1234';
const WWWROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'wwwroot');

// ---------------------------------------------------------------------------
// Enum mirrors (must match ModbusServer / TcpHMIClient definitions)
// ---------------------------------------------------------------------------

const SIGNAL_NAMES = [
  'ReadQR', 'Label1', 'Label2', 'SendingFIFOs', 'Ready',
  'Del1Valid', 'Del1Error', 'Del2Valid', 'Del2Error',
  'SendUpdate', 'SendUpdate2', 'CarWithPallet', 'CarInB1', 'CarInB2',
  'bcd1Avaliable', 'bcd2Avaliable', 'Leave1', 'Leave2',
  'WaitBocedi', 'WaitCar', 'SlaveConnected', 'WaitingPallet',
  'PLCStarting', 'PLCLabeling1', 'PLCLabeling2',
  'WaitCorrection1', 'WaitCorrection2', 'BCD2EntryError',
  'CarEntryError', 'BCD1OK', 'BCD2OK', 'Pause', 'ElevatorRequest',
  'LabelNull1', 'LabelNull2', 'WaitLabel1', 'WaitLabel2',
];
const SIG = Object.fromEntries(SIGNAL_NAMES.map((n, i) => [n, i]));

// Coils 1-20, written by the PC, sent as PcSignals.
const PC_SIGNAL_NAMES = [
  'ReadingPallet', 'SendingQR', 'Labeling1', 'Labeling2',
  'ToEmb1', 'ToEmb2', 'ReceivingFIFOs', 'Alive',
  'DelEmb1', 'DelEmb2', 'ConfirmUpdate', 'ConfirmUpdate2',
  'WeightOk1', 'WeightOk2', 'PalletLeave1', 'PalletLeave2',
  'ErrorQr', 'Waiting', 'ElevatorAuth', 'ElevatorFailedQr',
];
const PSIG = Object.fromEntries(PC_SIGNAL_NAMES.map((n, i) => [n, i]));

const PALLET_ENTRY_STATES = [
  'Waiting', 'ReadingQR', 'WaitingSetQr', 'WaitingSetEntryPallet',
  'WaitingAvailability', 'DefaultBehavior', 'AskingDB', 'SendingID',
  'WaitForBocedi1', 'WaitEnterBocedi', 'WaitUpdateFIFO1', 'UpdateFIFO1',
  'WaitForCar', 'WaitEnterCar', 'WaitUpdateCar', 'UpdateCar',
  'ReadingQrInError', 'Paused',
];
const PE = Object.fromEntries(PALLET_ENTRY_STATES.map((n, i) => [n, i]));

const CAR_POSITION = { Unknown: 0, GoingToB1: 1, InB1: 2, GoingToB2: 3, InB2: 4 };

// Sub-machines: PalletLabel1/2 each create their own PrinterMachine and
// device connections; identifiers (name suffix) are the printer id or the
// device IP, like the real server.
const PRINTER_STATES = ['Init', 'RetreivingWeightLen', 'RetreivingWeight', 'SendWeightOk',
  'WeightOk', 'RetreivingLabels', 'WaitPallet', 'WaitLabelInstruction', 'Print1', 'Print2',
  'WaitPrinter', 'WaitPLCConfirmation', 'WaitLabelLost', 'WaitApplicatorReady',
  'Reset1', 'Reset2', 'Skipped', 'Completed'];
const DEV_CONN_STATES = ['Connect', 'Connecting', 'Connected'];
const QR_CONN_STATES = ['Init', 'Disconnected', 'Connected', 'Wait'];

const MACHINE_STATES = {
  FatekPLCCommunication: ['Init', 'Starting', 'WaitingMemory', 'WaitingInit', 'Working'],
  PalletEntry: PALLET_ENTRY_STATES,
  PalletLabel1: ['WaitingPallet', 'WaitUpdate', 'WaitingCorrection', 'Labeling',
    'WaitUpdate2', 'WaitAck', 'WaitLeaving', 'WaitLeaveNull', 'PalletNull'],
  PalletLabel2: ['WaitingPallet', 'WaitUpdate', 'WaitingCorrection', 'Labeling',
    'WaitUpdate2', 'WaitAck', 'WaitLeaving', 'WaitLeaveNull', 'PalletNull'],
  CarMachine: ['UnknownPosition', 'WaitingCarInB1', 'WaitingCarWithPallet',
    'WaitingCarInB2', 'WaitingCarEmpty', 'WaitingGetQr', 'WaitingGetPallet'],
  DeletePalletEmb1: ['Waiting', 'Validating', 'ValidatingPLC', 'WaitingWrite',
    'SendingFIFO', 'Completed', 'Failed'],
  DeletePalletEmb2: ['Waiting', 'Validating', 'ValidatingPLC', 'WaitingWrite',
    'SendingFIFO', 'Completed', 'Failed'],
  AcceptHMIs: ['Init', 'Listening', 'Connecting', 'Adding', 'Pause'],
  ElevatorAccess: ['WaitingRequest', 'ReadingQr', 'FailedQr', 'WaitingAuth',
    'WaitingLeave', 'Delay'],
  PrinterMachineWolrdjet1: PRINTER_STATES,
  PrinterMachineWolrdjet2: PRINTER_STATES,
  'OmronConnection192.168.6.124': DEV_CONN_STATES,
  'OmronConnection192.168.250.1': DEV_CONN_STATES,
  'NetworkPrinterConnection192.168.6.122': DEV_CONN_STATES,
  'NetworkPrinterConnection192.168.6.163': DEV_CONN_STATES,
  'QrReaderConnection192.168.6.236': QR_CONN_STATES,
  'QrReaderConnection192.168.6.241': QR_CONN_STATES,
};

// ---------------------------------------------------------------------------
// Simulated plant state
// ---------------------------------------------------------------------------

// Physical capacity of each Bocedi's stations (entry queue + processing +
// two-slot queue); Bocedi 2 has one more entry slot. The DB never routes a
// pallet to a full or unavailable machine.
const B1_MAX = 4;
const B2_MAX = 5;

// Pallets arrive faster than the packagers process them on slow cycles,
// so queues realistically build up to several pallets.
function nextArrivalGap() {
  return Math.random() < 0.5
    ? 1000 + Math.random() * 2500   // back-to-back arrivals
    : 5000 + Math.random() * 6000;  // normal lull
}

function nextBaggingTime() {
  return Math.random() < 0.35
    ? 25000 + Math.random() * 25000 // slow cycle: the queue accumulates
    : 6000 + Math.random() * 4000;  // normal cycle
}

const state = {
  config: { QrRetries: 1, ContinueIfNoQr: false, ContinueIfNoDB: false, DefaultRecipe: 1 },
  connections: {
    QrReader: true, MasterPLC: true, SlavePLC: true, WencoDB: true,
    Packager1: true, Packager2: true, Labeler1: true, Labeler2: true,
  },
  errors: { BDC1Error: '', BDC2Error: '', EntryError: '', CarError: '' },
  signals: SIGNAL_NAMES.map(() => false),
  pcSignals: PC_SIGNAL_NAMES.map(() => false),
  entryPallet: null,
  packager1: { Queue: [], LabelPallet: null, ExitPallet: null },
  packager2: { Queue: [], LabelPallet: null, ExitPallet: null },
  car: { CarPosition: CAR_POSITION.InB1, HasPallet: false, Pallet: null },
  up1: true, // Bocedi 1 machine available (occasionally drops)
  up2: true,
  machineState: {
    FatekPLCCommunication: 4, // Working
    PalletEntry: PE.Waiting,
    PalletLabel1: 0,
    PalletLabel2: 0,
    CarMachine: 1, // WaitingCarInB1
    DeletePalletEmb1: 0,
    DeletePalletEmb2: 0,
    AcceptHMIs: 1, // Listening
    ElevatorAccess: 0, // WaitingRequest
    PrinterMachineWolrdjet1: 6, // WaitPallet
    PrinterMachineWolrdjet2: 6,
    'OmronConnection192.168.6.124': 2, // Connected
    'OmronConnection192.168.250.1': 2,
    'NetworkPrinterConnection192.168.6.122': 2,
    'NetworkPrinterConnection192.168.6.163': 2,
    'QrReaderConnection192.168.6.236': 2,
    'QrReaderConnection192.168.6.241': 2,
  },
};

let nextId1 = 1;
let nextId2 = 1;
let palletSeq = 137;

function makePallet(idCounter) {
  const labeling = Math.random() < 0.5; // quirk: true means the labeler is skipped
  const id = idCounter.value;
  idCounter.value = idCounter.value % 7 + 1; // queue ids cycle 1..7
  palletSeq++;
  return {
    // Short code like the real reads, e.g. S1232F23
    Qr: `S${1000 + ((palletSeq * 37) % 9000)}F${10 + (palletSeq % 90)}`,
    Id: String(labeling ? id + 8 : id), // +8 encodes the label flag like the PLC ids
    Recipe: `R-${(palletSeq % 9) + 1}`,
    Injector: `INY-${String((palletSeq % 12) + 1).padStart(2, '0')}`,
    Labeling: labeling,
  };
}

// --- entry simulation -------------------------------------------------------

// The entry walks the same state chain the real PalletEntry machine declares
// (see transitions.json): intermediate states get short dwells so the
// diagnostics graphs light up along the declared paths.
let entryPhase = { name: 'idle', until: now() + 3000 };

function now() { return Date.now(); }

function stepEntry(t) {
  const ms = state.machineState;
  const go = (name, dwell, extra) => { entryPhase = { name, until: t + dwell, ...extra }; };
  switch (entryPhase.name) {
    case 'idle': {
      ms.PalletEntry = PE.Waiting;
      state.entryPallet = null;
      state.signals[SIG.ReadQR] = false;
      // The elevator stages the next pallet: reads its QR, waits for the
      // authorization and holds it (WaitingLeave) until the conveyor is free.
      // Advance at most one declared step per tick (0→1→3→4).
      const rem = entryPhase.until - t;
      const target = rem > 4200 ? 0 : rem > 3200 ? 1 : rem > 1600 ? 3 : 4;
      const ladder = [0, 1, 3, 4];
      const cur = ladder.indexOf(ms.ElevatorAccess);
      ms.ElevatorAccess = ladder[Math.min(cur + 1, ladder.indexOf(target))] ?? 0;
      if (t > entryPhase.until) go('reading', 1500);
      break;
    }
    case 'reading':
      ms.PalletEntry = PE.ReadingQR;
      ms.ElevatorAccess = 0; // WaitingRequest
      state.signals[SIG.ReadQR] = true;
      if (t > entryPhase.until) {
        // 1 in 8 pallets arrives with an unreadable QR
        if (Math.random() < 0.125) {
          state.entryPallet = { Qr: '', Id: '', Recipe: '', Injector: '', Labeling: false };
          go('qrError', 6000);
        } else {
          state.entryPallet = makePallet({ value: 0 }); // id filled after DB answer
          state.entryPallet.Id = '';
          go('setQr', 400);
        }
      }
      break;
    case 'qrError':
      ms.PalletEntry = PE.ReadingQrInError;
      state.errors.EntryError = 'No se pudo leer el código QR. Retire el palet y vuelva a intentar.';
      if (t > entryPhase.until) {
        state.errors.EntryError = '';
        go('idle', 2000);
      }
      break;
    case 'setQr':
      ms.PalletEntry = PE.WaitingSetQr;
      if (t > entryPhase.until) go('setEntryPallet', 400);
      break;
    case 'setEntryPallet':
      ms.PalletEntry = PE.WaitingSetEntryPallet;
      if (t > entryPhase.until) go('availability', 500);
      break;
    case 'availability':
      ms.PalletEntry = PE.WaitingAvailability;
      // The pallet waits at the reader until some Bocedi is available; when
      // none is, the QR is effectively re-read until the DB can route it.
      if (t > entryPhase.until
          && (state.signals[SIG.bcd1Avaliable] || state.signals[SIG.bcd2Avaliable])) {
        go('askingDb', 1200);
      }
      break;
    case 'askingDb': {
      ms.PalletEntry = PE.AskingDB;
      if (t > entryPhase.until) {
        const b1ok = state.signals[SIG.bcd1Avaliable];
        const b2ok = state.signals[SIG.bcd2Avaliable];
        if (!b1ok && !b2ok) { go('availability', 800); break; }
        // Prefer Bocedi 1 (the direct line) when it's available.
        const toB1 = b1ok && (Math.random() < 0.7 || !b2ok);
        if (toB1) {
          state.entryPallet.Id = String(nextId1);
          nextId1 = nextId1 % 7 + 1;
        } else {
          state.entryPallet.Id = String(nextId2);
          nextId2 = nextId2 % 7 + 1;
        }
        go('sendingId', 500, { toB1 });
      }
      break;
    }
    case 'sendingId':
      ms.PalletEntry = PE.SendingID;
      if (t > entryPhase.until) go(entryPhase.toB1 ? 'waitBocedi1' : 'waitCar', 600);
      break;
    case 'waitBocedi1':
      ms.PalletEntry = PE.WaitForBocedi1;
      if (t > entryPhase.until && state.packager1.Queue.length < B1_MAX) {
        go('enterBocedi', 2500);
      }
      break;
    case 'enterBocedi':
      ms.PalletEntry = PE.WaitEnterBocedi;
      if (t > entryPhase.until) {
        state.packager1.Queue.push(state.entryPallet);
        go('waitUpdateFifo', 500);
      }
      break;
    case 'waitUpdateFifo':
      ms.PalletEntry = PE.WaitUpdateFIFO1;
      if (t > entryPhase.until) go('updateFifo', 300);
      break;
    case 'updateFifo':
      ms.PalletEntry = PE.UpdateFIFO1;
      if (t > entryPhase.until) go('idle', nextArrivalGap());
      break;
    case 'waitCar':
      ms.PalletEntry = PE.WaitForCar;
      if (state.car.CarPosition === CAR_POSITION.InB1 && !state.car.HasPallet) {
        go('enterCar', 2000);
      }
      break;
    case 'enterCar':
      ms.PalletEntry = PE.WaitEnterCar;
      if (t > entryPhase.until) {
        state.car.HasPallet = true;
        state.car.Pallet = state.entryPallet;
        go('waitUpdateCar', 500);
      }
      break;
    case 'waitUpdateCar':
      ms.PalletEntry = PE.WaitUpdateCar;
      if (t > entryPhase.until) go('updateCar', 300);
      break;
    case 'updateCar':
      ms.PalletEntry = PE.UpdateCar;
      if (t > entryPhase.until) go('idle', nextArrivalGap());
      break;
  }
}

// --- car simulation ---------------------------------------------------------

let carPhase = { name: 'atB1', until: 0 };

function stepCar(t) {
  const ms = state.machineState;
  const car = state.car;
  switch (carPhase.name) {
    case 'atB1':
      car.CarPosition = CAR_POSITION.InB1;
      ms.CarMachine = car.HasPallet ? 3 : 2; // WaitingCarInB2 : WaitingCarWithPallet
      if (car.HasPallet) carPhase = { name: 'toB2', until: t + 4000 };
      break;
    case 'toB2':
      car.CarPosition = CAR_POSITION.GoingToB2;
      ms.CarMachine = 3;
      if (t > carPhase.until) carPhase = { name: 'atB2', until: t + 3000 };
      break;
    case 'atB2':
      // The car stays visibly in B2 during the whole WaitingCarEmpty phase:
      // a dwell before unloading and another one after, before departing.
      car.CarPosition = CAR_POSITION.InB2;
      ms.CarMachine = 4; // WaitingCarEmpty
      if (car.HasPallet) {
        if (t > carPhase.until && state.packager2.Queue.length < B2_MAX) {
          state.packager2.Queue.push(car.Pallet);
          car.HasPallet = false;
          car.Pallet = null;
          carPhase.until = t + 2500;
        }
      } else if (t > carPhase.until) {
        carPhase = { name: 'getPallet', until: t + 800 };
      }
      break;
    case 'getPallet':
      // Declared step between leaving B2 and heading back to B1.
      car.CarPosition = CAR_POSITION.InB2;
      ms.CarMachine = 6; // WaitingGetPallet
      if (t > carPhase.until) carPhase = { name: 'toB1', until: t + 4000 };
      break;
    case 'toB1':
      car.CarPosition = CAR_POSITION.GoingToB1;
      ms.CarMachine = 1;
      if (t > carPhase.until) carPhase = { name: 'atB1', until: 0 };
      break;
  }
  state.signals[SIG.CarWithPallet] = car.HasPallet;
  state.signals[SIG.CarInB1] = car.CarPosition === CAR_POSITION.InB1;
  state.signals[SIG.CarInB2] = car.CarPosition === CAR_POSITION.InB2;
}

// --- packager simulation ----------------------------------------------------

// Walks the declared PalletLabel chain: WaitingPallet → WaitUpdate →
// Labeling → WaitUpdate2 → WaitAck → WaitLeaving → WaitingPallet.
// Its PrinterMachine follows the declared label loop while labeling:
// WaitLabelInstruction → Print1 → Reset1 → Reset2 → Print2 → WaitPrinter
// → WaitPLCConfirmation.
const PRINT_SEQUENCE = [7, 8, 14, 15, 9, 10, 11];

function makePackagerSim(pk, machineKey, labelSignal, printerKey) {
  let phase = { name: 'waiting', until: 0 };
  const go = (name, dwell, t) => { phase = { name, until: t + dwell }; };
  return function step(t) {
    const ms = state.machineState;
    ms[printerKey] = 6; // WaitPallet unless printing below
    switch (phase.name) {
      case 'waiting':
        ms[machineKey] = 0; // WaitingPallet
        state.signals[labelSignal] = false;
        if (pk().Queue.length > 0 && !pk().LabelPallet) {
          go('bagging', nextBaggingTime(), t);
        }
        break;
      case 'bagging': // pallet being bagged before reaching the labeler
        if (t > phase.until) go('waitUpdate', 500, t);
        break;
      case 'waitUpdate':
        ms[machineKey] = 1; // WaitUpdate
        if (t > phase.until) {
          pk().LabelPallet = pk().Queue.shift();
          go('labeling', 4000, t);
        }
        break;
      case 'labeling': {
        ms[machineKey] = 3; // Labeling
        state.signals[labelSignal] = true;
        const progress = Math.min(Math.max(1 - (phase.until - t) / 4000, 0), 0.999);
        ms[printerKey] = PRINT_SEQUENCE[Math.floor(progress * PRINT_SEQUENCE.length)];
        if (t > phase.until) {
          state.signals[labelSignal] = false;
          go('waitUpdate2', 400, t);
        }
        break;
      }
      case 'waitUpdate2':
        ms[machineKey] = 4; // WaitUpdate2
        ms[printerKey] = 11; // WaitPLCConfirmation
        if (t > phase.until) go('waitAck', 600, t);
        break;
      case 'waitAck':
        ms[machineKey] = 5; // WaitAck
        ms[printerKey] = 11;
        if (t > phase.until) {
          pk().ExitPallet = pk().LabelPallet;
          pk().LabelPallet = null;
          go('exiting', 5000, t);
        }
        break;
      case 'exiting':
        ms[machineKey] = 6; // WaitLeaving
        if (t > phase.until) {
          pk().ExitPallet = null;
          go('waiting', 0, t);
        }
        break;
    }
  };
}

const stepPackager1 = makePackagerSim(() => state.packager1, 'PalletLabel1', SIG.PLCLabeling1, 'PrinterMachineWolrdjet1');
const stepPackager2 = makePackagerSim(() => state.packager2, 'PalletLabel2', SIG.PLCLabeling2, 'PrinterMachineWolrdjet2');

// --- random disturbances ----------------------------------------------------

function stepDisturbances(t) {
  // Machine availability: occasionally a Bocedi goes down for a while, and a
  // full machine is not available either. The DB routes accordingly.
  if (state.up1 && Math.random() < 0.002) {
    state.up1 = false;
    setTimeout(() => { state.up1 = true; }, 12000 + Math.random() * 18000);
  }
  if (state.up2 && Math.random() < 0.002) {
    state.up2 = false;
    setTimeout(() => { state.up2 = true; }, 12000 + Math.random() * 18000);
  }
  state.signals[SIG.bcd1Avaliable] = state.up1 && state.packager1.Queue.length < B1_MAX;
  state.signals[SIG.bcd2Avaliable] = state.up2 && state.packager2.Queue.length < B2_MAX;
  state.signals[SIG.BCD1OK] = state.up1;
  state.signals[SIG.BCD2OK] = state.up2;

  if (Math.random() < 0.004) { // ~ every 2 min at 2 Hz
    const conns = ['WencoDB', 'Labeler1', 'Labeler2', 'QrReader', 'Packager1', 'Packager2'];
    const c = conns[Math.floor(Math.random() * conns.length)];
    state.connections[c] = false;
    setTimeout(() => { state.connections[c] = true; }, 5000);
  }

  // Connection sub-machines mirror the connection flags, walking the
  // declared ramp: Connect → Connecting → Connected (and back to Connect
  // when the device drops).
  const ms = state.machineState;
  const ramp = (key, up) => {
    ms[key] = up ? Math.min((ms[key] ?? 0) + 1, 2) : 0;
  };
  ramp('OmronConnection192.168.6.124', state.connections.Packager1);
  ramp('OmronConnection192.168.250.1', state.connections.Packager2);
  ramp('NetworkPrinterConnection192.168.6.122', state.connections.Labeler1);
  ramp('NetworkPrinterConnection192.168.6.163', state.connections.Labeler2);
  // QrReaderConnection states: Init(0) Disconnected(1) Connected(2) Wait(3)
  ms['QrReaderConnection192.168.6.236'] = state.connections.QrReader ? 2 : 1;
  ms['QrReaderConnection192.168.6.241'] = 2; // elevator reader: always up in the sim
  if (Math.random() < 0.003) {
    state.signals[SIG.WaitCorrection1] = true;
    state.errors.BDC1Error = 'Peso fuera de rango. Corrija el último palet de la cola.';
    setTimeout(() => {
      state.signals[SIG.WaitCorrection1] = false;
      state.errors.BDC1Error = '';
    }, 9000);
  }

  stepPcSignals();
}

// --- PC-written coils ---------------------------------------------------------

// Derived every tick from the machine states the other steps just set;
// called from stepDisturbances because that's the last step of the tick,
// both here and in the demo builds (which splice this file's sim section).
function stepPcSignals() {
  const ms = state.machineState;
  const ps = state.pcSignals;
  const ph = entryPhase.name;
  ps.fill(false);

  ps[PSIG.Alive] = Math.floor(now() / 1000) % 2 === 0; // watchdog heartbeat
  ps[PSIG.Waiting] = ph === 'idle';
  ps[PSIG.ReadingPallet] = ph === 'reading';
  ps[PSIG.ErrorQr] = ph === 'qrError';
  ps[PSIG.SendingQR] = ph === 'setQr' || ph === 'setEntryPallet' || ph === 'sendingId';
  ps[PSIG.ToEmb1] = (ph === 'sendingId' && entryPhase.toB1)
    || ph === 'waitBocedi1' || ph === 'enterBocedi';
  ps[PSIG.ToEmb2] = (ph === 'sendingId' && !entryPhase.toB1)
    || ph === 'waitCar' || ph === 'enterCar';
  ps[PSIG.ReceivingFIFOs] = ['waitUpdateFifo', 'updateFifo', 'waitUpdateCar', 'updateCar'].includes(ph);
  ps[PSIG.ElevatorAuth] = ms.ElevatorAccess === 4; // WaitingLeave: auth granted

  // PalletLabel states: 1 WaitUpdate, 3 Labeling, 4 WaitUpdate2, 6 WaitLeaving
  const lane = (mk, labeling, weightOk, confirm, leave) => {
    const s = ms[mk];
    ps[labeling] = s === 3;
    ps[weightOk] = s === 1 || s === 3;
    ps[confirm] = s === 1 || s === 4;
    ps[leave] = s === 6;
  };
  lane('PalletLabel1', PSIG.Labeling1, PSIG.WeightOk1, PSIG.ConfirmUpdate, PSIG.PalletLeave1);
  lane('PalletLabel2', PSIG.Labeling2, PSIG.WeightOk2, PSIG.ConfirmUpdate2, PSIG.PalletLeave2);

  // DeletePalletEmb states 1-4: Validating .. SendingFIFO
  ps[PSIG.DelEmb1] = ms.DeletePalletEmb1 >= 1 && ms.DeletePalletEmb1 <= 4;
  ps[PSIG.DelEmb2] = ms.DeletePalletEmb2 >= 1 && ms.DeletePalletEmb2 <= 4;

  // Newer PLC coils that also follow directly from the simulated states.
  state.signals[SIG.ElevatorRequest] = ms.ElevatorAccess === 1 || ms.ElevatorAccess === 3;
  state.signals[SIG.WaitLabel1] = ms.PrinterMachineWolrdjet1 === 7; // WaitLabelInstruction
  state.signals[SIG.WaitLabel2] = ms.PrinterMachineWolrdjet2 === 7;
}

// ---------------------------------------------------------------------------
// Status snapshot (the wire contract — mirror of HMIConnection.CreateMessage)
// ---------------------------------------------------------------------------

function buildStatus() {
  const states = {};
  for (const [name, list] of Object.entries(MACHINE_STATES)) {
    states[name] = Object.fromEntries(list.map((s, i) => [String(i), s]));
  }
  return {
    Config: state.config,
    Signals: state.signals,
    PcSignals: state.pcSignals,
    Connections: state.connections,
    EntryPallet: state.entryPallet,
    ErrorMessages: state.errors,
    Packager1: state.packager1,
    Packager2: state.packager2,
    Car: state.car,
    MachineState: state.machineState,
    States: states,
  };
}

// ---------------------------------------------------------------------------
// Auth + delete simulation
// ---------------------------------------------------------------------------

const sessions = new Map(); // token -> expiry epoch ms
const SESSION_TTL = 15 * 60 * 1000;

function isAuthorized(req) {
  const auth = req.headers.authorization || '';
  const token = auth.startsWith('Bearer ') ? auth.slice(7) : null;
  const expiry = token && sessions.get(token);
  if (!expiry || expiry < now()) return false;
  sessions.set(token, now() + SESSION_TTL); // sliding expiry
  return true;
}

function handleLogin(body, res) {
  let pin;
  try { pin = JSON.parse(body).pin; } catch { /* fall through */ }
  if (pin !== PIN) {
    res.writeHead(401, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ error: 'PIN incorrecto' }));
    return;
  }
  const token = crypto.randomBytes(24).toString('hex');
  sessions.set(token, now() + SESSION_TTL);
  res.writeHead(200, { 'Content-Type': 'application/json' });
  res.end(JSON.stringify({ token }));
}

function handleDelete(body, res) {
  let del;
  try { del = JSON.parse(body); } catch { /* fall through */ }
  if (!del || !del.Pallet || (del.Packager !== 1 && del.Packager !== 2)) {
    res.writeHead(400, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ result: 'NOK', error: 'Solicitud inválida' }));
    return;
  }
  const machine = del.Packager === 1 ? 'DeletePalletEmb1' : 'DeletePalletEmb2';
  // Walk the declared chain (Validating → ValidatingPLC → WaitingWrite →
  // SendingFIFO → Completed/Failed) while the PLC round-trip runs.
  const walk = (states, step) =>
    states.forEach((s, i) => setTimeout(() => { state.machineState[machine] = s; }, i * step));
  walk([1, 2], 500); // Validating, ValidatingPLC
  setTimeout(() => {
    const pk = del.Packager === 1 ? state.packager1 : state.packager2;
    const found = pk.Queue[del.Position];
    const ok = found && found.Qr === del.Pallet.Qr;
    if (ok) {
      pk.Queue.splice(del.Position, 1);
      walk([3, 4, 5], 500); // WaitingWrite, SendingFIFO, Completed
    } else {
      state.machineState[machine] = 6; // Failed
    }
    setTimeout(() => { state.machineState[machine] = 0; }, 2500);
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(ok
      ? { result: 'OK' }
      : { result: 'NOK', error: 'El palet ya no está en esa posición' }));
  }, 1500);
}

// ---------------------------------------------------------------------------
// HTTP server: static files + SSE + API
// ---------------------------------------------------------------------------

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
};

const sseClients = new Set();

const server = http.createServer((req, res) => {
  // A malformed request target ("//", a bare authority…) must not take the
  // whole dev server down with an unhandled ERR_INVALID_URL.
  let url;
  try {
    url = new URL(req.url, `http://${req.headers.host}`);
  } catch {
    res.writeHead(400).end('Bad Request');
    return;
  }

  if (url.pathname === '/api/events') {
    res.writeHead(200, {
      'Content-Type': 'text/event-stream',
      'Cache-Control': 'no-cache',
      Connection: 'keep-alive',
    });
    res.write(`event: status\ndata: ${JSON.stringify(buildStatus())}\n\n`);
    sseClients.add(res);
    req.on('close', () => sseClients.delete(res));
    return;
  }

  if (req.method === 'POST' && (url.pathname === '/api/login' || url.pathname === '/api/delete')) {
    let body = '';
    req.on('data', (c) => { body += c; });
    req.on('end', () => {
      if (url.pathname === '/api/login') return handleLogin(body, res);
      if (!isAuthorized(req)) {
        res.writeHead(401, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ result: 'NOK', error: 'Sesión expirada. Ingrese nuevamente.' }));
        return;
      }
      return handleDelete(body, res);
    });
    return;
  }

  // Static files from wwwroot
  let filePath = path.normalize(path.join(WWWROOT, url.pathname === '/' ? 'index.html' : url.pathname));
  if (!filePath.startsWith(WWWROOT)) {
    res.writeHead(403); res.end(); return;
  }
  fs.readFile(filePath, (err, data) => {
    if (err) { res.writeHead(404); res.end('Not found'); return; }
    res.writeHead(200, { 'Content-Type': MIME[path.extname(filePath)] || 'application/octet-stream' });
    res.end(data);
  });
});

// Simulation at 2 Hz; broadcast to all SSE clients on every tick.
setInterval(() => {
  const t = now();
  stepEntry(t);
  stepCar(t);
  stepPackager1(t);
  stepPackager2(t);
  stepDisturbances(t);
  const payload = `event: status\ndata: ${JSON.stringify(buildStatus())}\n\n`;
  for (const client of sseClients) client.write(payload);
}, 500);

server.listen(PORT, () => {
  console.log(`Dummy HMI server running at http://localhost:${PORT}`);
  console.log(`Supervisor PIN for deletions: ${PIN}`);
});
