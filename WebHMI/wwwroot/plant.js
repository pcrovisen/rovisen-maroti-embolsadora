// Vista de planta — SCADA-style synoptic of the bagging line.
//
// Plan view, north up. Pallets enter on a vertical conveyor (south → north);
// the QR reader sits at the north end of the conveyor, and from there the
// pallet moves sideways: right into Bocedi 1, or left onto the transfer car,
// which runs a straight rail to Bocedi 2's entrance. The two Bocedis are
// mirrored enclosures containing entry queue → processing (bagging) →
// measurement → labeler (circle), discharging north.
//
// Pallet positions inside a Bocedi are educated guesses from the FIFO:
// Queue[0] is shown at the processing station when another pallet is queued
// behind it (its arrival proves the first advanced), otherwise in the entry
// queue (1 slot in B1, 2 in B2; overflow waits on the approach outside).
// LabelPallet sits at measurement, moving onto the labeler circle only while
// the machine is in its labeling states. ExitPallet is at the output.

'use strict';

const SVGNS = 'http://www.w3.org/2000/svg';
const $ = (id) => document.getElementById(id);
const svg = $('plantSvg');

function svgEl(tag, attrs = {}, parent) {
  const el = document.createElementNS(SVGNS, tag);
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  if (parent) parent.appendChild(el);
  return el;
}

// ---------------------------------------------------------------------------
// Geometry (viewBox units). North is up. Main east-west axis at y = 320.
// ---------------------------------------------------------------------------

const Y = 320;

const GEO = {
  world: { x: 0, y: 40, w: 2000, h: 940 },
  entryX: 1050,
  entrySouth: 860,
  qrY: 370,                       // reading position: north end of the conveyor
  b1: {                           // flow west → east
    box: { x: 1200, y: 240, w: 580, h: 160 },
    approach: [{ x: 1160 }, { x: 1104 }, { x: 1048 }],  // backlog outside, toward the junction
    entryQueue: [{ x: 1262 }],
    processing: { x: 1392 },
    measurement: { x: 1524 },
    labeler: { x: 1662, r: 36 },
    output: { x: 1662, y: 170 },
    entrance: 1226,
  },
  b2: {                           // mirrored: flow east → west
    box: { x: 220, y: 240, w: 580, h: 160 },
    approach: [{ x: 830, y: 420 }, { x: 830, y: 486 }], // rare overflow, beside the dock
    entryQueue: [{ x: 738 }, { x: 668 }],
    processing: { x: 548 },
    measurement: { x: 428 },
    labeler: { x: 308, r: 36 },
    output: { x: 308, y: 170 },
  },
  car: { inB1: 990, inB2: 830, unknown: 910 },
};

const PALLET_HALF = 22;
const AT_LABELER_STATES = [3, 4, 5]; // PalletLabel: Labeling, WaitUpdate2, WaitAck

// ---------------------------------------------------------------------------
// Static scene
// ---------------------------------------------------------------------------

const layers = {};

function lane(x, y, w, h, parent) {
  svgEl('rect', { x, y, width: w, height: h, class: 'lane', rx: 6 }, parent);
}

function chevrons(parent, x0, y0, x1, y1, count) {
  const dx = x1 - x0;
  const dy = y1 - y0;
  const len = Math.hypot(dx, dy);
  const ux = dx / len;
  const uy = dy / len;
  for (let i = 1; i <= count; i++) {
    const cx = x0 + (dx * i) / (count + 1);
    const cy = y0 + (dy * i) / (count + 1);
    const p1 = `${cx - ux * 7 - uy * 6},${cy - uy * 7 + ux * 6}`;
    const p2 = `${cx + ux * 7},${cy + uy * 7}`;
    const p3 = `${cx - ux * 7 + uy * 6},${cy - uy * 7 - ux * 6}`;
    svgEl('polyline', { points: `${p1} ${p2} ${p3}`, class: 'chevron' }, parent);
  }
}

function subStation(x, label, parent, labelBelowY = 392) {
  svgEl('rect', {
    x: x - 30, y: Y - 30, width: 60, height: 60, rx: 6, class: 'sub-station',
  }, parent);
  if (label) {
    svgEl('text', { x, y: labelBelowY, class: 'station-sub', 'text-anchor': 'middle' }, parent)
      .textContent = label;
  }
}

let carEl;
let qrBoxEl;
let labelCircle1;
let labelCircle2;

function buildBocedi(geo, name, parent) {
  const b = geo.box;
  svgEl('rect', { x: b.x, y: b.y, width: b.w, height: b.h, rx: 12, class: 'bocedi-box' }, parent);
  svgEl('text', {
    x: b.x + b.w / 2, y: b.y - 14, class: 'station-name', 'text-anchor': 'middle',
  }, parent).textContent = name;

  geo.entryQueue.forEach((s, i) => subStation(s.x, i === 0 ? 'Cola' : '', parent));
  subStation(geo.processing.x, 'Embolsado', parent);
  subStation(geo.measurement.x, 'Medición', parent);

  const circle = svgEl('circle', {
    cx: geo.labeler.x, cy: Y, r: geo.labeler.r, class: 'labeling-circle',
  }, parent);
  svgEl('text', {
    x: geo.labeler.x, y: 392, class: 'station-sub', 'text-anchor': 'middle',
  }, parent).textContent = 'Etiquetado';

  // Output lane heading north
  lane(geo.labeler.x - 22, 120, 44, b.y - 120, layers.lanes);
  chevrons(layers.lanes, geo.labeler.x, b.y - 8, geo.labeler.x, 130, 2);
  svgEl('text', {
    x: geo.output.x, y: 104, class: 'station-name', 'text-anchor': 'middle',
  }, parent).textContent = 'Salida (N)';

  return circle;
}

function buildScene() {
  layers.floor = svgEl('g', {}, svg);
  layers.lanes = svgEl('g', {}, svg);
  layers.stations = svgEl('g', {}, svg);
  layers.car = svgEl('g', {}, svg);
  layers.pallets = svgEl('g', {}, svg);

  // Compass
  const comp = svgEl('g', { class: 'compass', transform: 'translate(60 130)' }, layers.floor);
  svgEl('line', { x1: 0, y1: 18, x2: 0, y2: -14, class: 'compass-line' }, comp);
  svgEl('polygon', { points: '-6,-8 0,-22 6,-8', class: 'compass-n' }, comp);
  svgEl('text', { x: 0, y: 38, 'text-anchor': 'middle', class: 'station-name' }, comp).textContent = 'N';

  // Entry conveyor (south → north) and the approach to Bocedi 1
  lane(GEO.entryX - 24, Y - 22, 48, GEO.entrySouth - Y + 100, layers.lanes);
  lane(GEO.entryX + 24, Y - 22, GEO.b1.box.x - GEO.entryX - 24, 44, layers.lanes);
  chevrons(layers.lanes, GEO.entryX, GEO.entrySouth + 40, GEO.entryX, Y + 16, 5);
  chevrons(layers.lanes, GEO.entryX + 40, Y, GEO.b1.box.x - 10, Y, 2);

  // Car rail (junction ← west → Bocedi 2 entrance)
  svgEl('line', {
    x1: GEO.b2.box.x + GEO.b2.box.w, y1: Y, x2: GEO.car.inB1 + 46, y2: Y, class: 'rail',
  }, layers.lanes);
  for (let x = GEO.b2.box.x + GEO.b2.box.w + 6; x <= GEO.car.inB1 + 44; x += 26) {
    svgEl('line', { x1: x, y1: Y - 9, x2: x, y2: Y + 9, class: 'rail-tie' }, layers.lanes);
  }

  // Bocedis
  labelCircle1 = buildBocedi(GEO.b1, 'Bocedi 1', layers.stations);
  labelCircle2 = buildBocedi(GEO.b2, 'Bocedi 2', layers.stations);

  // QR reader at the north end of the entry conveyor
  qrBoxEl = svgEl('g', {
    class: 'qr-station', transform: `translate(${GEO.entryX + 44} ${GEO.qrY - 24})`,
  }, layers.stations);
  svgEl('rect', { x: 0, y: 0, width: 52, height: 48, rx: 8, class: 'qr-box' }, qrBoxEl);
  svgEl('text', { x: 26, y: 30, 'text-anchor': 'middle', class: 'qr-text' }, qrBoxEl).textContent = 'QR';
  svgEl('line', { x1: 0, y1: 24, x2: -18, y2: 24, class: 'qr-beam' }, qrBoxEl);

  // Tags
  const tag = (x, y, text) => {
    svgEl('text', { x, y, class: 'station-name', 'text-anchor': 'middle' }, layers.floor).textContent = text;
  };
  tag(GEO.entryX, GEO.entrySouth + 86, 'Entrada (S)');
  tag((GEO.car.inB1 + GEO.car.inB2) / 2, Y + 52, 'Carro');

  // The car
  carEl = svgEl('g', { class: 'car-body' }, layers.car);
  svgEl('rect', { x: -38, y: -22, width: 76, height: 44, rx: 6, class: 'car-rect' }, carEl);
  svgEl('circle', { cx: -22, cy: 24, r: 6, class: 'car-wheel' }, carEl);
  svgEl('circle', { cx: 22, cy: 24, r: 6, class: 'car-wheel' }, carEl);
  moveCar(GEO.car.inB1, 0);
}

function moveCar(x, seconds) {
  carEl.style.transitionDuration = `${seconds}s`;
  carEl.style.transform = `translate(${x}px, ${Y}px)`;
}

// ---------------------------------------------------------------------------
// Pallets: create/move/remove by QR identity
// ---------------------------------------------------------------------------

const palletEls = new Map(); // qr -> { g, label }

function palletLabelText(p) {
  return [p.Qr, [p.Id && p.Id !== '0' ? `Id ${p.Id}` : '', p.Injector].filter(Boolean).join(' · ')];
}

function createPallet(p, x, y) {
  const g = svgEl('g', { class: 'pallet' }, layers.pallets);
  g.style.transform = `translate(${x}px, ${y}px)`;
  svgEl('rect', {
    x: -PALLET_HALF, y: -PALLET_HALF, width: PALLET_HALF * 2, height: PALLET_HALF * 2, rx: 4, class: 'pallet-box',
  }, g);
  for (const off of [-8, 0, 8]) {
    svgEl('line', { x1: -PALLET_HALF + 4, y1: off, x2: PALLET_HALF - 4, y2: off, class: 'pallet-plank' }, g);
  }
  const label = svgEl('text', { class: 'pallet-data', 'text-anchor': 'middle' }, g);
  const [l1, l2] = palletLabelText(p);
  svgEl('tspan', { x: 0 }, label).textContent = l1;
  svgEl('tspan', { x: 0, dy: 13 }, label).textContent = l2;
  return { g, label };
}

function setPalletTargets(targets) {
  for (const [qr, t] of targets) {
    let entry = palletEls.get(qr);
    if (!entry) {
      entry = createPallet(t.pallet, t.x, t.y);
      palletEls.set(qr, entry);
      // Force a frame at the spawn position so the first move animates.
      entry.g.getBoundingClientRect();
    }
    entry.g.style.transitionDuration = `${t.seconds ?? 0.7}s`;
    entry.g.style.transform = `translate(${t.x}px, ${t.y}px)`;
    entry.label.setAttribute('transform', `translate(0 ${t.labelHigh ? -58 : -32})`);
  }
  for (const [qr, entry] of palletEls) {
    if (targets.has(qr)) continue;
    palletEls.delete(qr);
    entry.g.classList.add('leaving');
    setTimeout(() => entry.g.remove(), 700);
  }
}

// ---------------------------------------------------------------------------
// Status → positions
// ---------------------------------------------------------------------------

function entryPalletTarget(st) {
  const p = st.EntryPallet;
  if (!p || !p.Qr) return null;
  const s = st.MachineState.PalletEntry;
  if ((s >= PE.ReadingQR && s <= PE.SendingID) || s === PE.ReadingQrInError) {
    return { x: GEO.entryX, y: GEO.qrY };
  }
  if (s === PE.WaitForBocedi1 || s === PE.WaitForCar) return { x: GEO.entryX, y: Y };
  if (s === PE.WaitEnterBocedi) return { x: GEO.b1.entrance, y: Y };
  if (s === PE.WaitEnterCar) return { x: GEO.car.inB1, y: Y };
  return null; // Waiting / update states / Paused: shown elsewhere or gone
}

function packagerTargets(pk, geo, labelState, add) {
  const q = pk.Queue || [];
  const firstAdvanced = q.length >= 2; // someone arrived behind: queue[0] is processing
  q.forEach((p, i) => {
    let pos;
    if (i === 0 && firstAdvanced) {
      pos = geo.processing;
    } else {
      const j = firstAdvanced ? i - 1 : i; // index within the entry queue
      pos = geo.entryQueue[j] || geo.approach[j - geo.entryQueue.length];
    }
    if (pos) add(p, pos.x, pos.y ?? Y, { labelHigh: i % 2 === 1 });
  });
  if (pk.LabelPallet) {
    const pos = AT_LABELER_STATES.includes(labelState) ? geo.labeler : geo.measurement;
    add(pk.LabelPallet, pos.x, Y);
  }
  if (pk.ExitPallet) add(pk.ExitPallet, geo.output.x, geo.output.y);
}

function collectTargets(st) {
  const targets = new Map();
  const add = (pallet, x, y, opts = {}) => {
    if (!pallet || !pallet.Qr || targets.has(pallet.Qr)) return;
    targets.set(pallet.Qr, { pallet, x, y, ...opts });
  };

  if (st.Packager1) packagerTargets(st.Packager1, GEO.b1, st.MachineState.PalletLabel1, add);
  if (st.Packager2) packagerTargets(st.Packager2, GEO.b2, st.MachineState.PalletLabel2, add);

  if (st.Car && st.Car.HasPallet && st.Car.Pallet) {
    const t = carTarget(st.Car.CarPosition);
    add(st.Car.Pallet, t.x, Y, { seconds: t.seconds });
  }

  const et = entryPalletTarget(st);
  if (et) add(st.EntryPallet, et.x, et.y);

  return targets;
}

function carTarget(pos) {
  switch (pos) {
    case 2: return { x: GEO.car.inB1, seconds: 0.7 };
    case 4: return { x: GEO.car.inB2, seconds: 0.7 };
    case 3: return { x: GEO.car.inB2, seconds: 4 };  // traveling to B2
    case 1: return { x: GEO.car.inB1, seconds: 4 };  // traveling to B1
    default: return { x: GEO.car.unknown, seconds: 1 };
  }
}

// ---------------------------------------------------------------------------
// Render
// ---------------------------------------------------------------------------

const connChips = {};
for (const [key, label] of Object.entries(CONN_LABELS)) {
  const chip = document.createElement('span');
  chip.className = 'chip';
  chip.textContent = label;
  $('connChips').appendChild(chip);
  connChips[key] = chip;
}

function setBanner(el, message) {
  el.classList.toggle('empty', !message);
  el.textContent = message || '';
}

function handleStatus(st) {
  for (const [key, chip] of Object.entries(connChips)) {
    chip.classList.toggle('on', !!st.Connections[key]);
  }

  const ct = carTarget(st.Car ? st.Car.CarPosition : 0);
  moveCar(ct.x, ct.seconds);
  carEl.classList.toggle('unknown', !st.Car || st.Car.CarPosition === 0);

  const s = st.MachineState.PalletEntry;
  qrBoxEl.setAttribute('class', 'qr-station');
  if (s === PE.ReadingQrInError) qrBoxEl.classList.add('bad', 'blink');
  else if (s === PE.ReadingQR || s === PE.Paused) qrBoxEl.classList.add('ok', 'blink');
  else if (s !== undefined && s !== PE.Waiting) qrBoxEl.classList.add('ok');

  labelCircle1.classList.toggle('active', AT_LABELER_STATES.includes(st.MachineState.PalletLabel1));
  labelCircle2.classList.toggle('active', AT_LABELER_STATES.includes(st.MachineState.PalletLabel2));

  setPalletTargets(collectTargets(st));

  setBanner($('entryError'), st.ErrorMessages.EntryError);
  setBanner($('bcd1Error'), st.ErrorMessages.BDC1Error);
  setBanner($('bcd2Error'), st.ErrorMessages.BDC2Error);
  setBanner($('carError'), st.ErrorMessages.CarError);
}

// ---------------------------------------------------------------------------
// Pan & zoom (viewBox manipulation; mouse, wheel and touch/pinch)
// ---------------------------------------------------------------------------

const vb = { ...GEO.world };

function applyViewBox() {
  svg.setAttribute('viewBox', `${vb.x} ${vb.y} ${vb.w} ${vb.h}`);
}

function clientToWorld(cx, cy) {
  const r = svg.getBoundingClientRect();
  return {
    x: vb.x + ((cx - r.left) / r.width) * vb.w,
    y: vb.y + ((cy - r.top) / r.height) * vb.h,
  };
}

function zoomAt(cx, cy, factor) {
  const p = clientToWorld(cx, cy);
  const w = Math.min(Math.max(vb.w * factor, 260), GEO.world.w * 2.2);
  const scale = w / vb.w;
  vb.x = p.x - (p.x - vb.x) * scale;
  vb.y = p.y - (p.y - vb.y) * scale;
  vb.w = w;
  vb.h *= scale;
  applyViewBox();
}

svg.addEventListener('wheel', (e) => {
  e.preventDefault();
  zoomAt(e.clientX, e.clientY, e.deltaY > 0 ? 1.18 : 1 / 1.18);
}, { passive: false });

const pointers = new Map();
let pinchStart = null;

svg.addEventListener('pointerdown', (e) => {
  svg.setPointerCapture(e.pointerId);
  pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
  if (pointers.size === 2) {
    const [a, b] = [...pointers.values()];
    pinchStart = { dist: Math.hypot(a.x - b.x, a.y - b.y), w: vb.w };
  }
});

svg.addEventListener('pointermove', (e) => {
  const prev = pointers.get(e.pointerId);
  if (!prev) return;
  if (pointers.size === 1) {
    const r = svg.getBoundingClientRect();
    vb.x -= ((e.clientX - prev.x) / r.width) * vb.w;
    vb.y -= ((e.clientY - prev.y) / r.height) * vb.h;
    applyViewBox();
  }
  pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
  if (pointers.size === 2 && pinchStart) {
    const [a, b] = [...pointers.values()];
    const dist = Math.hypot(a.x - b.x, a.y - b.y) || 1;
    const cx = (a.x + b.x) / 2;
    const cy = (a.y + b.y) / 2;
    const targetW = pinchStart.w * (pinchStart.dist / dist);
    zoomAt(cx, cy, targetW / vb.w);
  }
});

for (const ev of ['pointerup', 'pointercancel', 'pointerleave']) {
  svg.addEventListener(ev, (e) => {
    pointers.delete(e.pointerId);
    if (pointers.size < 2) pinchStart = null;
  });
}

$('zoomInBtn').addEventListener('click', () => {
  const r = svg.getBoundingClientRect();
  zoomAt(r.left + r.width / 2, r.top + r.height / 2, 1 / 1.35);
});
$('zoomOutBtn').addEventListener('click', () => {
  const r = svg.getBoundingClientRect();
  zoomAt(r.left + r.width / 2, r.top + r.height / 2, 1.35);
});
$('zoomFitBtn').addEventListener('click', () => {
  Object.assign(vb, GEO.world);
  applyViewBox();
});

applyViewBox();
buildScene();

// ---------------------------------------------------------------------------
// Startup: connect to the server (replaced by the local sim in demos)
// ---------------------------------------------------------------------------

const source = new EventSource('/api/events');

source.addEventListener('status', (ev) => {
  $('serverDot').classList.add('on');
  $('offlineOverlay').classList.add('hidden');
  handleStatus(JSON.parse(ev.data));
});

source.onerror = () => {
  $('serverDot').classList.remove('on');
  $('offlineOverlay').classList.remove('hidden');
  for (const chip of Object.values(connChips)) chip.classList.remove('on');
};
