// Builds self-contained, single-file demos of the Web HMI pages by embedding
// the real sources (style.css, common.js, app.js / diag.js, transitions.json)
// plus the dummy server's line simulation, so they run anywhere with no
// server — e.g. published as a private Claude artifact for viewing remotely.
//
//   node WebHMI/devserver/build_demos.mjs [outDir]   (default: OS temp dir)
//
// Outputs: diagnostico-demo.html and hmi-demo.html.

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const www = path.join(here, '..', 'wwwroot');
const outDir = process.argv[2] || fs.mkdtempSync(path.join(os.tmpdir(), 'webhmi-demo-'));
fs.mkdirSync(outDir, { recursive: true });

const read = (p) => fs.readFileSync(p, 'utf8');
const css = read(path.join(www, 'style.css'));
const common = read(path.join(www, 'common.js'));
const transitions = read(path.join(www, 'transitions.json')).trim();
const app = read(path.join(www, 'app.js'));

let diag = read(path.join(www, 'diag.js'));
const diagCut = diag.indexOf('// Startup: load the declared');
if (diagCut < 0) throw new Error('diag.js startup marker not found');
diag = diag.slice(0, diagCut);

const dummy = read(path.join(here, 'dummy_server.mjs'));
const simStart = dummy.indexOf('const MAX_QUEUE');
const simEnd = dummy.indexOf('// Auth + delete');
if (simStart < 0 || simEnd < 0) throw new Error('dummy_server sim markers not found');
const sim = dummy.slice(simStart, simEnd);

let plant = read(path.join(www, 'plant.js'));
const plantCut = plant.indexOf('// Startup: connect to the server');
if (plantCut < 0) throw new Error('plant.js startup marker not found');
plant = plant.slice(0, plantCut);

const extractBody = (page) => {
  let body = page.slice(page.indexOf('<body>') + 6, page.indexOf('</body>'));
  body = body.replace(/\s*<script src="[^"]*"><\/script>/g, '');
  body = body.replace(/<a class="diag-open"[\s\S]*?<\/a>/, '');
  body = body.replace(/<nav class="page-nav">[\s\S]*?<\/nav>/, '');
  body = body.replace(
    '<div class="session">',
    '<span class="demo-badge">DEMO · simulación en el navegador</span>\n    <div class="session">',
  );
  body = body.replace(
    '</header>',
    (body.includes('demo-badge') ? '' : '<span class="demo-badge">DEMO · simulación en el navegador</span>') + '</header>',
  );
  return body;
};

const body = extractBody(read(path.join(www, 'index.html')));
const plantBody = extractBody(read(path.join(www, 'planta.html')));

const fill = (template, parts) => {
  let out = template;
  for (const [marker, content] of Object.entries(parts)) {
    out = out.split(`/*__${marker}__*/`).join(content);
  }
  const leftover = out.match(/\/\*__[A-Z]+__\*\//);
  if (leftover) throw new Error(`marker left over: ${leftover[0]}`);
  if ((out.match(/<script>/g) || []).length !== (out.match(/<\/script>/g) || []).length) {
    throw new Error('unbalanced script tags');
  }
  return out;
};

const outputs = {
  'diagnostico-demo.html': fill(read(path.join(here, 'demo', 'diag.template.html')), {
    CSS: css, COMMON: common, TRANSITIONS: transitions, DIAG: diag, SIM: sim,
  }),
  'hmi-demo.html': fill(read(path.join(here, 'demo', 'main.template.html')), {
    CSS: css, BODY: body, COMMON: common, TRANSITIONS: transitions, SIM: sim, APP: app,
  }),
  'planta-demo.html': fill(read(path.join(here, 'demo', 'plant.template.html')), {
    CSS: css, BODY: plantBody, COMMON: common, TRANSITIONS: transitions, SIM: sim, PLANT: plant,
    FLOOR: read(path.join(www, 'plant_floor.svg')),
  }),
};

for (const [name, content] of Object.entries(outputs)) {
  const p = path.join(outDir, name);
  fs.writeFileSync(p, content);
  console.log(`wrote ${p} (${(content.length / 1024).toFixed(0)} kB)`);
}
