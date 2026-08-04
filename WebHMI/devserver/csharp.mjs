// The bits of C# parsing both generators need, in one place.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
export const ROOT = path.join(here, '..', '..');
export const SERVER = path.join(ROOT, 'ModbusServer');
export const CONTRACTS = path.join(ROOT, 'Contracts');

// Every Contracts source concatenated: enums shared with the HMIs live there
// (PalletEntryState, Signals), so a machine's states may not be in its own file.
const contractsSrc = fs.readdirSync(CONTRACTS)
  .filter((f) => f.endsWith('.cs'))
  .map((f) => fs.readFileSync(path.join(CONTRACTS, f), 'utf8'))
  .join('\n');

/**
 * Members of a C# enum in declaration order, honouring explicit `= n` values and
 * the auto-increment between them, and noting the [FaultState] attribute.
 * Returns null when the enum is not in `src`.
 */
export function parseEnum(src, name) {
  const m = src.match(new RegExp(`enum\\s+${name}\\s*\\{([\\s\\S]*?)\\}`));
  if (!m) return null;
  const out = [];
  let next = 0;
  for (const raw of m[1].replace(/\/\/[^\n]*/g, '').split(',')) {
    const e = raw.trim().match(/^((?:\[\w+\]\s*)*)(\w+)\s*(?:=\s*(\d+))?$/);
    if (!e) continue;
    const value = e[3] !== undefined ? Number(e[3]) : next;
    out.push({ name: e[2], value, fault: /\[FaultState\]/.test(e[1]) });
    next = value + 1;
  }
  return out;
}

/**
 * The state enum of a `class X : Machine<...>`, wherever it lives: nested in the
 * machine's own file (`Machine<X.States>`) or shared in Contracts
 * (`Machine<PalletEntryState>`, because the HMIs key on its ordinals).
 *
 * Returns { className, enumName, members } or null if `src` is not a machine.
 */
export function machineEnum(src) {
  const cls = src.match(/class\s+(\w+)\s*:\s*Machine\s*<\s*([\w.]+)\s*>/);
  if (!cls) return null;
  const [, className, arg] = cls;
  const enumName = arg.includes('.') ? arg.split('.').pop() : arg;
  const members = parseEnum(src, enumName) ?? parseEnum(contractsSrc, enumName);
  return members ? { className, enumName, members } : null;
}

/** Every machine source file, as text. */
export function machineSources() {
  const dir = path.join(SERVER, 'StateMachine');
  return fs.readdirSync(dir)
    .filter((f) => f.endsWith('.cs'))
    .map((f) => fs.readFileSync(path.join(dir, f), 'utf8'));
}

export const contracts = contractsSrc;
