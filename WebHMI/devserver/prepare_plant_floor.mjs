// Prepares wwwroot/plant_floor.svg from Pablo's plant drawing
// (wenco_plant_5.svg at the repo root).
//
// The drawing contains sample pallets, the car drawn at both docks and a
// sample data label — all of which the plant view draws live — so this strips
// them and keeps only the static infrastructure (conveyors, machines,
// enclosures, labeling circles, QR tags, elevator).
//
//   node WebHMI/devserver/prepare_plant_floor.mjs

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const SRC = path.join(here, '..', '..', 'wenco_plant_5.svg');
const OUT = path.join(here, '..', 'wwwroot', 'plant_floor.svg');

let svg = fs.readFileSync(SRC, 'utf8');

const inRegion = (el, x0, x1, y0, y1) => {
  const m = el.match(/[dx]="M?([\d.]+)[ ,]([\d.]+)/) || el.match(/x="([\d.]+)" y="([\d.]+)"/)
    || el.match(/transform="matrix\([^)]* ([\d.]+) ([\d.]+)\)"/);
  if (!m) return false;
  const x = +m[1];
  const y = +m[2];
  return x >= x0 && x <= x1 && y >= y0 && y <= y1;
};

// Both car docks and the elevator label area
const CAR_REGIONS = [[6400, 7000, 1800, 2350], [3550, 4100, 1800, 2350]];
const LABEL_REGION = [6450, 7050, 3250, 4050];

let removed = { pallets: 0, car: 0, label: 0, bg: 0 };

svg = svg.replace(/<(rect|path)[^>]*\/>/g, (el) => {
  if (el.includes('width="10859" height="4923"')) { removed.bg++; return ''; }
  if (el.includes('fill="#B27916"')) { removed.pallets++; return ''; }
  if ((el.includes('fill="#940303"') || el.includes('fill="black"') || el.includes('fill="#D9D9D9"'))
      && CAR_REGIONS.some((r) => inRegion(el, ...r))) { removed.car++; return ''; }
  if (el.startsWith('<path') && el.includes('fill="white"')
      && inRegion(el, ...LABEL_REGION)) { removed.label++; return ''; }
  return el;
});

// Recolor the drawing for the dark theme (blacks/grays -> light tones;
// the orange chevrons and red machine/QR accents stay as drawn).
const RECOLOR = {
  'fill="#D9D9D9"': 'fill="#39445C"',
  'fill="#7A7A7A"': 'fill="#93A1B8"',
  'fill="black"': 'fill="#DFE6F0"',
  'fill="#181616"': 'fill="#8FA0B8"',
  'fill="#0C0B0B"': 'fill="#2E3A52"',
  'fill="#B9B9B9"': 'fill="#55617A"',
  'fill="#655D5D"': 'fill="#6B7891"',
  'fill="#022B56"': 'fill="#5B8BC9"',
  'stroke="black"': 'stroke="#9AA5B5"',
};
for (const [from, to] of Object.entries(RECOLOR)) svg = svg.split(from).join(to);

fs.writeFileSync(OUT, svg);
console.log(`removed: ${removed.pallets} sample pallets, ${removed.car} car shapes, ${removed.label} label paths`);
console.log(`wrote ${OUT} (${(svg.length / 1024).toFixed(0)} kB)`);
