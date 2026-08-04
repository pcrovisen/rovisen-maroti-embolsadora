// Regenerates everything the Web HMI derives from the C# sources:
//
//   wwwroot/enums.js + devserver/enums.mjs   the enum mirrors
//   wwwroot/transitions.json                 the state-machine graphs
//
// Run after touching FatekPLC.Signals, Car.Position, any machine's States, or
// any Step()/StateTimeouts:
//
//   node WebHMI/devserver/generate.mjs
//
// CI runs this and fails if anything it writes differs from what is committed.

import './generate_enums.mjs';
import './generate_transitions.mjs';
