// Generates the JavaScript mirrors of the C# enums the Web HMI depends on.
//
// These were hand-copied in four places (wwwroot/common.js,
// devserver/dummy_server.mjs and the three demo/*.template.html), which is
// CLAUDE.md rule 3 in a third language: the wire format is positional, so a
// member inserted or removed in C# silently shifts everything after it. That
// has already bitten once — removing ElevatorAccess.FailedQr would have made
// the plant page blink red while waiting for the database.
//
// Written to two files with the same content, because the consumers differ:
//   wwwroot/enums.js     classic script, globals — loaded by the pages and
//                        inlined into the single-file demos
//   devserver/enums.mjs  ES module — imported by the Node dummy server
//
// Run after changing FatekPLC.Signals, Car.Position or any machine's States:
//   node WebHMI/devserver/generate_enums.mjs

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const SERVER = path.join(here, '..', '..', 'ModbusServer');

const read = (...p) => fs.readFileSync(path.join(SERVER, ...p), 'utf8');

// Members of a C# enum in declaration order, honouring explicit `= n` values
// and the auto-increment between them. Attributes ([FaultState]) are ignored.
function parseEnum(src, name, optional = false) {
  const m = src.match(new RegExp(`enum\\s+${name}\\s*\\{([\\s\\S]*?)\\}`));
  if (!m) {
    if (optional) return null;
    throw new Error(`enum ${name} not found`);
  }
  const out = [];
  let next = 0;
  for (const raw of m[1].replace(/\/\/[^\n]*/g, '').split(',')) {
    const e = raw.trim().match(/^(?:\[\w+\]\s*)*(\w+)\s*(?:=\s*(\d+))?$/);
    if (!e) continue;
    const value = e[2] !== undefined ? Number(e[2]) : next;
    out.push({ name: e[1], value });
    next = value + 1;
  }
  return out;
}

// --- FatekPLC.Signals -------------------------------------------------------
// Coils 1-20 are written by the PC and sent as PcSignals; 21+ are written by
// the PLC and sent as Signals. Both arrays are indexed from their own base, as
// HMIConnection.CreateMessage slices them.
const signals = parseEnum(read('Devices', 'FatekPLC.cs'), 'Signals');
const PC_BASE = 1;
const PLC_BASE = 21;

const slice = (base) => {
  const members = signals.filter((s) => s.value >= base && (base !== PC_BASE || s.value < PLC_BASE));
  const arr = [];
  for (const s of members) arr[s.value - base] = s.name;
  const hole = arr.findIndex((v) => v === undefined);
  if (hole >= 0) throw new Error(`gap in the coil map at index ${hole} of base ${base}`);
  return arr;
};

const pcSignalNames = slice(PC_BASE);
const signalNames = slice(PLC_BASE);

// --- Car.Position -----------------------------------------------------------
const carPositions = parseEnum(read('Status.cs'), 'Position');

// --- Machine states ---------------------------------------------------------
// Keyed by class. Runtime names carry an identifier (PalletLabel + "1",
// PrinterMachine + "Wolrdjet1", OmronConnection + an IP), so consumers resolve
// them by longest matching prefix — see statesForMachine below.
const smDir = path.join(SERVER, 'StateMachine');
const statesByClass = {};
for (const file of fs.readdirSync(smDir).filter((f) => f.endsWith('.cs'))) {
  const src = fs.readFileSync(path.join(smDir, file), 'utf8');
  // Machine.cs itself matches the class pattern inside a doc-comment example,
  // and has no States of its own — same reason generate_transitions.mjs
  // requires both before accepting a file.
  const cls = src.match(/class\s+(\w+)\s*:\s*Machine\b/);
  const states = cls && parseEnum(src, 'States', true);
  if (!states) continue;
  statesByClass[cls[1]] = states.map((s) => s.name);
}

// --- Emit -------------------------------------------------------------------

const banner = `// GENERATED FILE — do not edit.
// Mirrors the C# enums; regenerate with:
//   node WebHMI/devserver/generate_enums.mjs
`;

const body = `
const SIGNAL_NAMES = ${JSON.stringify(signalNames, null, 2)};
const SIG = Object.fromEntries(SIGNAL_NAMES.map((n, i) => [n, i]));

// Coils 1-20, written by the PC, sent as PcSignals.
const PC_SIGNAL_NAMES = ${JSON.stringify(pcSignalNames, null, 2)};
const PSIG = Object.fromEntries(PC_SIGNAL_NAMES.map((n, i) => [n, i]));

const CAR_POSITION = ${JSON.stringify(
  Object.fromEntries(carPositions.map((c) => [c.name, c.value])), null, 2)};

// Machine class -> state names, in ordinal order.
const STATES_BY_CLASS = ${JSON.stringify(statesByClass, null, 2)};

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
`;

const exports = `
export {
  SIGNAL_NAMES, SIG, PC_SIGNAL_NAMES, PSIG, CAR_POSITION,
  STATES_BY_CLASS, statesForMachine, PE, EA,
};
`;

fs.writeFileSync(path.join(here, '..', 'wwwroot', 'enums.js'), `${banner}${body}`);
fs.writeFileSync(path.join(here, 'enums.mjs'), `${banner}${body}${exports}`);

console.log(`Signals: ${signalNames.length} PLC coils (${PLC_BASE}+), `
  + `${pcSignalNames.length} PC coils (${PC_BASE}-${PLC_BASE - 1})`);
console.log(`Car.Position: ${carPositions.length}`);
console.log(`Machines: ${Object.keys(statesByClass).length}`);
console.log('Wrote wwwroot/enums.js and devserver/enums.mjs');
