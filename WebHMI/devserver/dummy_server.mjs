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
];
const SIG = Object.fromEntries(SIGNAL_NAMES.map((n, i) => [n, i]));

const PALLET_ENTRY_STATES = [
  'Waiting', 'ReadingQR', 'WaitingSetQr', 'WaitingSetEntryPallet',
  'WaitingAvailability', 'DefaultBehavior', 'AskingDB', 'SendingID',
  'WaitForBocedi1', 'WaitEnterBocedi', 'WaitUpdateFIFO1', 'UpdateFIFO1',
  'WaitForCar', 'WaitEnterCar', 'WaitUpdateCar', 'UpdateCar',
  'ReadingQrInError', 'Paused',
];
const PE = Object.fromEntries(PALLET_ENTRY_STATES.map((n, i) => [n, i]));

const CAR_POSITION = { Unknown: 0, GoingToB1: 1, InB1: 2, GoingToB2: 3, InB2: 4 };

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
};

// ---------------------------------------------------------------------------
// Simulated plant state
// ---------------------------------------------------------------------------

const MAX_QUEUE = 5;

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
  entryPallet: null,
  packager1: { Queue: [], LabelPallet: null, ExitPallet: null },
  packager2: { Queue: [], LabelPallet: null, ExitPallet: null },
  car: { CarPosition: CAR_POSITION.InB1, HasPallet: false, Pallet: null },
  machineState: {
    FatekPLCCommunication: 4, // Working
    PalletEntry: PE.Waiting,
    PalletLabel1: 0,
    PalletLabel2: 0,
    CarMachine: 1, // WaitingCarInB1
    DeletePalletEmb1: 0,
    DeletePalletEmb2: 0,
    AcceptHMIs: 1, // Listening
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
    Qr: `018509WE${String(palletSeq).padStart(6, '0')}`,
    Id: String(labeling ? id + 8 : id), // +8 encodes the label flag like the PLC ids
    Recipe: `R-${(palletSeq % 9) + 1}`,
    Injector: `INY-${String((palletSeq % 12) + 1).padStart(2, '0')}`,
    Labeling: labeling,
  };
}

// --- entry simulation -------------------------------------------------------

let entryPhase = { name: 'idle', until: now() + 3000, target: null };

function now() { return Date.now(); }

function stepEntry(t) {
  const ms = state.machineState;
  switch (entryPhase.name) {
    case 'idle':
      ms.PalletEntry = PE.Waiting;
      state.entryPallet = null;
      state.signals[SIG.ReadQR] = false;
      if (t > entryPhase.until) {
        // 1 in 8 pallets arrives with an unreadable QR
        if (Math.random() < 0.125) {
          entryPhase = { name: 'qrError', until: t + 6000 };
        } else {
          entryPhase = { name: 'reading', until: t + 1500 };
        }
      }
      break;
    case 'qrError':
      ms.PalletEntry = PE.ReadingQrInError;
      state.errors.EntryError = 'No se pudo leer el código QR. Retire el palet y vuelva a intentar.';
      state.entryPallet = { Qr: '', Id: '', Recipe: '', Injector: '', Labeling: false };
      if (t > entryPhase.until) {
        state.errors.EntryError = '';
        entryPhase = { name: 'idle', until: t + 2000 };
      }
      break;
    case 'reading':
      ms.PalletEntry = PE.ReadingQR;
      state.signals[SIG.ReadQR] = true;
      if (t > entryPhase.until) {
        state.entryPallet = makePallet({ value: 0 }); // id filled after DB answer
        state.entryPallet.Id = '';
        entryPhase = { name: 'askingDb', until: t + 1200 };
      }
      break;
    case 'askingDb':
      ms.PalletEntry = PE.AskingDB;
      if (t > entryPhase.until) {
        // Mostly route to Bocedi 1 (the direct line) even if it has a
        // backlog, like the plant DB does; the car line is the overflow.
        const toB1 = state.packager1.Queue.length < MAX_QUEUE
          && (Math.random() < 0.7 || state.packager2.Queue.length >= MAX_QUEUE);
        if (toB1) {
          state.entryPallet.Id = String(nextId1);
          nextId1 = nextId1 % 7 + 1;
          entryPhase = { name: 'toBocedi1', until: t + 2500 };
        } else {
          state.entryPallet.Id = String(nextId2);
          nextId2 = nextId2 % 7 + 1;
          entryPhase = { name: 'waitCar', until: t };
        }
      }
      break;
    case 'toBocedi1':
      ms.PalletEntry = PE.WaitEnterBocedi;
      if (t > entryPhase.until) {
        if (state.packager1.Queue.length < MAX_QUEUE) {
          state.packager1.Queue.push(state.entryPallet);
          entryPhase = { name: 'idle', until: t + nextArrivalGap() };
        }
      }
      break;
    case 'waitCar':
      ms.PalletEntry = PE.WaitForCar;
      if (state.car.CarPosition === CAR_POSITION.InB1 && !state.car.HasPallet) {
        entryPhase = { name: 'enterCar', until: t + 2000 };
      }
      break;
    case 'enterCar':
      ms.PalletEntry = PE.WaitEnterCar;
      if (t > entryPhase.until) {
        state.car.HasPallet = true;
        state.car.Pallet = state.entryPallet;
        entryPhase = { name: 'idle', until: t + nextArrivalGap() };
      }
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
        if (t > carPhase.until && state.packager2.Queue.length < MAX_QUEUE) {
          state.packager2.Queue.push(car.Pallet);
          car.HasPallet = false;
          car.Pallet = null;
          carPhase.until = t + 2500;
        }
      } else if (t > carPhase.until) {
        carPhase = { name: 'toB1', until: t + 4000 };
      }
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

function makePackagerSim(pk, machineKey, labelSignal) {
  let phase = { name: 'waiting', until: 0 };
  return function step(t) {
    const ms = state.machineState;
    switch (phase.name) {
      case 'waiting':
        ms[machineKey] = 0; // WaitingPallet
        state.signals[labelSignal] = false;
        if (pk().Queue.length > 0 && !pk().LabelPallet) {
          phase = { name: 'bagging', until: t + nextBaggingTime() };
        }
        break;
      case 'bagging': // pallet being bagged before reaching the labeler
        if (t > phase.until) {
          pk().LabelPallet = pk().Queue.shift();
          phase = { name: 'labeling', until: t + 4000 };
        }
        break;
      case 'labeling':
        ms[machineKey] = 3; // Labeling
        state.signals[labelSignal] = true;
        if (t > phase.until) {
          pk().ExitPallet = pk().LabelPallet;
          pk().LabelPallet = null;
          state.signals[labelSignal] = false;
          phase = { name: 'exiting', until: t + 5000 };
        }
        break;
      case 'exiting':
        ms[machineKey] = 6; // WaitLeaving
        if (t > phase.until) {
          pk().ExitPallet = null;
          phase = { name: 'waiting', until: 0 };
        }
        break;
    }
  };
}

const stepPackager1 = makePackagerSim(() => state.packager1, 'PalletLabel1', SIG.PLCLabeling1);
const stepPackager2 = makePackagerSim(() => state.packager2, 'PalletLabel2', SIG.PLCLabeling2);

// --- random disturbances ----------------------------------------------------

function stepDisturbances(t) {
  if (Math.random() < 0.004) { // ~ every 2 min at 2 Hz
    const conns = ['WencoDB', 'Labeler1', 'Labeler2', 'QrReader'];
    const c = conns[Math.floor(Math.random() * conns.length)];
    state.connections[c] = false;
    setTimeout(() => { state.connections[c] = true; }, 5000);
  }
  if (Math.random() < 0.003) {
    state.signals[SIG.WaitCorrection1] = true;
    state.errors.BDC1Error = 'Peso fuera de rango. Corrija el último palet de la cola.';
    setTimeout(() => {
      state.signals[SIG.WaitCorrection1] = false;
      state.errors.BDC1Error = '';
    }, 9000);
  }
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
  state.machineState[machine] = 1; // Validating
  // Simulate the PLC round-trip taking a couple of seconds
  setTimeout(() => {
    const pk = del.Packager === 1 ? state.packager1 : state.packager2;
    const found = pk.Queue[del.Position];
    const ok = found && found.Qr === del.Pallet.Qr;
    if (ok) pk.Queue.splice(del.Position, 1);
    state.machineState[machine] = ok ? 5 : 6; // Completed : Failed
    setTimeout(() => { state.machineState[machine] = 0; }, 1500);
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(ok
      ? { result: 'OK' }
      : { result: 'NOK', error: 'El palet ya no está en esa posición' }));
  }, 2000);
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
  const url = new URL(req.url, `http://${req.headers.host}`);

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
