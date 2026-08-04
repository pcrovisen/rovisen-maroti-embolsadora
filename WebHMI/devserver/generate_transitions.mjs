// Generates wwwroot/transitions.json from the C# state machines.
//
// For every `class X : Machine` in ModbusServer/StateMachine/*.cs it extracts
// the States enum, the initial state (base(States.X)) and the declared
// transitions: every `NextState(States.Y)` inside a `case States.X:` block of
// Step(). For each transition it also extracts the gating condition (the
// closest preceding `if (...)`): FatekPLC signals (with negation) and a "⏱"
// marker when the condition involves StateTime.ElapsedMilliseconds.
//
// Best effort — NextState calls outside case blocks (helper methods) have no
// attributable source state and are skipped, and only the innermost if
// condition is inspected.
//
// Run after changing any C# state machine:
//   node WebHMI/devserver/generate_transitions.mjs

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const SRC_DIR = path.join(here, '..', '..', 'ModbusServer', 'StateMachine');
const OUT = path.join(here, '..', 'wwwroot', 'transitions.json');

const result = {};

for (const file of fs.readdirSync(SRC_DIR).filter((f) => f.endsWith('.cs'))) {
  const src = fs.readFileSync(path.join(SRC_DIR, file), 'utf8');

  const cls = src.match(/class\s+(\w+)\s*:\s*Machine\b/);
  const en = src.match(/enum\s+States\s*\{([\s\S]*?)\}/);
  if (!cls || !en) continue;

  const states = en[1]
    .replace(/\/\/[^\n]*/g, '')
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean);

  const init = src.match(/base\s*\(\s*States\.(\w+)/)?.[1] ?? states[0];

  // Locate case labels; consecutive labels (only whitespace between them)
  // share the same block.
  const labels = [...src.matchAll(/case\s+States\.(\w+)\s*:/g)]
    .map((m) => ({ state: m[1], start: m.index, end: m.index + m[0].length }));

  const groups = [];
  for (const l of labels) {
    const g = groups[groups.length - 1];
    if (g && /^\s*$/.test(src.slice(g.end, l.start))) {
      g.states.push(l.state);
      g.end = l.end;
    } else {
      groups.push({ states: [l.state], start: l.start, end: l.end });
    }
  }

  // The closest `if (...)` condition that fully precedes position `pos`.
  const gateFor = (block, pos) => {
    let best = null;
    const re = /if\s*\(/g;
    let m;
    while ((m = re.exec(block)) && m.index < pos) {
      let depth = 1;
      let i = m.index + m[0].length;
      while (i < block.length && depth > 0) {
        if (block[i] === '(') depth++;
        else if (block[i] === ')') depth--;
        i++;
      }
      if (i - 1 < pos) best = block.slice(m.index + m[0].length, i - 1);
    }
    if (!best) return [];
    const tokens = [];
    for (const s of best.matchAll(/(!?)\s*FatekPLC\.ReadBit\(\s*FatekPLC\.Signals\.(\w+)\s*\)/g)) {
      tokens.push(`${s[1]}${s[2]}`);
    }
    if (/ElapsedMilliseconds/.test(best)) tokens.push('⏱');
    return [...new Set(tokens)];
  };

  // End of the `switch (State)` body. The last case block used to run to the
  // end of the file, so it absorbed every NextState in the methods that follow
  // — Reset(), helpers — and invented transitions the step logic never makes
  // (PrinterMachine showed a Completed -> Init edge that only exists in
  // Reset()). Braces are counted on a copy with strings and comments blanked
  // out, so format placeholders like "{0}" cannot unbalance the scan.
  const masked = src.replace(
    /@"(?:[^"]|"")*"|\$?"(?:\\.|[^"\\])*"|\/\/[^\n]*|\/\*[\s\S]*?\*\//g,
    (m) => ' '.repeat(m.length));
  const switchAt = masked.search(/switch\s*\(\s*State\s*\)/);
  let switchEnd = src.length;
  if (switchAt >= 0) {
    let depth = 0;
    for (let i = masked.indexOf('{', switchAt); i < masked.length; i++) {
      if (masked[i] === '{') depth++;
      else if (masked[i] === '}' && --depth === 0) { switchEnd = i; break; }
    }
  }

  const edges = new Map(); // "from>to" -> Set(gate tokens)
  groups.forEach((g, i) => {
    const blockEnd = i + 1 < groups.length ? groups[i + 1].start : switchEnd;
    const block = src.slice(g.end, blockEnd);
    for (const m of block.matchAll(/NextState\s*\(\s*States\.(\w+)/g)) {
      const gates = gateFor(block, m.index);
      for (const from of g.states) {
        if (from === m[1]) continue;
        const key = `${from}>${m[1]}`;
        if (!edges.has(key)) edges.set(key, new Set());
        gates.forEach((tk) => edges.get(key).add(tk));
      }
    }
  });

  result[cls[1]] = {
    init,
    states,
    edges: [...edges].map(([e, gates]) => [...e.split('>'), [...gates]]),
  };
}

fs.writeFileSync(OUT, JSON.stringify(result, null, 1));

for (const [name, m] of Object.entries(result)) {
  console.log(`${name}: ${m.states.length} states, ${m.edges.length} transitions (init ${m.init})`);
}
console.log(`\nWrote ${OUT}`);
