// Generates wwwroot/transitions.json from the C# state machines.
//
// For every `class X : Machine` in ModbusServer/StateMachine/*.cs it extracts
// the States enum, the initial state (base(States.X)) and the declared
// transitions: every `NextState(States.Y)` inside a `case States.X:` block of
// Step(). Best effort — NextState calls outside case blocks (helper methods)
// have no attributable source state and are skipped.
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

  const edges = new Set();
  groups.forEach((g, i) => {
    const blockEnd = i + 1 < groups.length ? groups[i + 1].start : src.length;
    const block = src.slice(g.end, blockEnd);
    for (const m of block.matchAll(/NextState\s*\(\s*States\.(\w+)/g)) {
      for (const from of g.states) {
        if (from !== m[1]) edges.add(`${from}>${m[1]}`);
      }
    }
  });

  result[cls[1]] = {
    init,
    states,
    edges: [...edges].map((e) => e.split('>')),
  };
}

fs.writeFileSync(OUT, JSON.stringify(result, null, 1));

for (const [name, m] of Object.entries(result)) {
  console.log(`${name}: ${m.states.length} states, ${m.edges.length} transitions (init ${m.init})`);
}
console.log(`\nWrote ${OUT}`);
