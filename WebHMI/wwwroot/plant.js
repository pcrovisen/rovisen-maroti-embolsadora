// Vista de planta — SCADA-style synoptic of the bagging line.
//
// Plan view, north up. Pallets enter on a vertical conveyor (south → north),
// get their QR read, and at the junction go right to Bocedi 1 or left onto
// the transfer car, which runs a straight rail to Bocedi 2's entrance.
// Bocedi 2 mirrors Bocedi 1 (both flow outward from the center); each has a
// labeling station drawn as a circle, and discharges north. Pallets carry
// their data above them and glide between stations; wheel/pinch zooms,
// dragging pans.

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
// Geometry (viewBox units). North is up.
// ---------------------------------------------------------------------------

const GEO = {
  world: { x: 0, y: 40, w: 1600, h: 900 },
  laneY: 320,              // main east-west axis (B2 lane, car rail, B1 lane)
  entryX: 800,             // entry conveyor (vertical)
  entrySouth: 860,
  qrY: 470,                // reading position on the conveyor
  approachY: 385,          // waiting just before the junction
  b1: {
    entrance: 872,
    slots: [1192, 1128, 1064, 1000, 936],   // queue[0] nearest the machine
    circle: { x: 1298, y: 320, r: 34 },
    rect: { x: 1244, y: 268, w: 108, h: 104 },
    exitY: 182,
  },
  b2: {
    slots: [214, 278, 342, 406, 470],       // mirrored: queue[0] nearest the machine
    circle: { x: 108, y: 320, r: 34 },
    rect: { x: 54, y: 268, w: 108, h: 104 },
    exitY: 182,
  },
  car: { inB1: 736, inB2: 534, unknown: 635, y: 320 },
};

const PALLET_HALF = 22;
const LABELING_STATE = 3; // PalletLabel States.Labeling

// ---------------------------------------------------------------------------
// Static scene
// ---------------------------------------------------------------------------

const layers = {};

function lane(x, y, w, h, parent) {
  svgEl('rect', { x, y, width: w, height: h, class: 'lane', rx: 6 }, parent);
}

function chevrons(parent, x0, y0, x1, y1, count) {
  // Direction arrows along a lane from (x0,y0) to (x1,y1).
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

function station(rect, name, parent) {
  svgEl('rect', {
    x: rect.x, y: rect.y, width: rect.w, height: rect.h, class: 'station', rx: 8,
  }, parent);
  svgEl('text', {
    x: rect.x + rect.w / 2, y: rect.y - 12, class: 'station-name', 'text-anchor': 'middle',
  }, parent).textContent = name;
}

let carEl;
let qrBoxEl;
let labelCircle1;
let labelCircle2;

function buildScene() {
  svg.setAttribute('viewBox', `${GEO.world.x} ${GEO.world.y} ${GEO.world.w} ${GEO.world.h}`);
  layers.floor = svgEl('g', {}, svg);
  layers.lanes = svgEl('g', {}, svg);
  layers.stations = svgEl('g', {}, svg);
  layers.car = svgEl('g', {}, svg);
  layers.pallets = svgEl('g', {}, svg);

  const Y = GEO.laneY;

  // Compass
  const comp = svgEl('g', { class: 'compass', transform: 'translate(52 120)' }, layers.floor);
  svgEl('line', { x1: 0, y1: 18, x2: 0, y2: -14, class: 'compass-line' }, comp);
  svgEl('polygon', { points: '-6,-8 0,-22 6,-8', class: 'compass-n' }, comp);
  svgEl('text', { x: 0, y: 38, 'text-anchor': 'middle', class: 'station-name' }, comp).textContent = 'N';

  // Lanes
  lane(GEO.entryX - 24, Y - 22, 48, GEO.entrySouth - Y + 80, layers.lanes);      // entry conveyor
  lane(GEO.b1.entrance - 32, Y - 22, GEO.b1.rect.x - GEO.b1.entrance + 32, 44, layers.lanes); // B1 lane
  lane(GEO.b2.rect.x + GEO.b2.rect.w, Y - 22, GEO.b2.slots[4] + 60 - (GEO.b2.rect.x + GEO.b2.rect.w), 44, layers.lanes); // B2 lane
  lane(GEO.b1.circle.x - 22, 120, 44, Y - 34 - 120, layers.lanes);               // B1 exit north
  lane(GEO.b2.circle.x - 22, 120, 44, Y - 34 - 120, layers.lanes);               // B2 exit north

  chevrons(layers.lanes, GEO.entryX, GEO.entrySouth + 30, GEO.entryX, Y + 10, 5);
  chevrons(layers.lanes, GEO.b1.entrance, Y, GEO.b1.rect.x - 10, Y, 4);
  chevrons(layers.lanes, GEO.b2.slots[4] + 40, Y, GEO.b2.rect.x + GEO.b2.rect.w + 10, Y, 4);
  chevrons(layers.lanes, GEO.b1.circle.x, Y - 40, GEO.b1.circle.x, 130, 3);
  chevrons(layers.lanes, GEO.b2.circle.x, Y - 40, GEO.b2.circle.x, 130, 3);

  // Car rail
  svgEl('line', {
    x1: GEO.car.inB2 - 40, y1: Y, x2: GEO.car.inB1 + 40, y2: Y, class: 'rail',
  }, layers.lanes);
  for (let x = GEO.car.inB2 - 40; x <= GEO.car.inB1 + 40; x += 26) {
    svgEl('line', { x1: x, y1: Y - 9, x2: x, y2: Y + 9, class: 'rail-tie' }, layers.lanes);
  }

  // Stations
  station(GEO.b1.rect, 'Bocedi 1', layers.stations);
  station(GEO.b2.rect, 'Bocedi 2', layers.stations);
  labelCircle1 = svgEl('circle', {
    cx: GEO.b1.circle.x, cy: GEO.b1.circle.y, r: GEO.b1.circle.r, class: 'labeling-circle',
  }, layers.stations);
  labelCircle2 = svgEl('circle', {
    cx: GEO.b2.circle.x, cy: GEO.b2.circle.y, r: GEO.b2.circle.r, class: 'labeling-circle',
  }, layers.stations);
  svgEl('text', {
    x: GEO.b1.circle.x, y: GEO.b1.rect.y + GEO.b1.rect.h + 20, class: 'station-sub', 'text-anchor': 'middle',
  }, layers.stations).textContent = 'Etiquetado';
  svgEl('text', {
    x: GEO.b2.circle.x, y: GEO.b2.rect.y + GEO.b2.rect.h + 20, class: 'station-sub', 'text-anchor': 'middle',
  }, layers.stations).textContent = 'Etiquetado';

  // QR reader beside the entry conveyor
  qrBoxEl = svgEl('g', { class: 'qr-station', transform: `translate(${GEO.entryX + 44} ${GEO.qrY - 24})` }, layers.stations);
  svgEl('rect', { x: 0, y: 0, width: 52, height: 48, rx: 8, class: 'qr-box' }, qrBoxEl);
  svgEl('text', { x: 26, y: 30, 'text-anchor': 'middle', class: 'qr-text' }, qrBoxEl).textContent = 'QR';
  svgEl('line', { x1: 0, y1: 24, x2: -18, y2: 24, class: 'qr-beam' }, qrBoxEl);

  // Text labels
  const tag = (x, y, text) => {
    svgEl('text', { x, y, class: 'station-name', 'text-anchor': 'middle' }, layers.floor).textContent = text;
  };
  tag(GEO.entryX, GEO.entrySouth + 66, 'Entrada (S)');
  tag(GEO.b1.circle.x, 104, 'Salida (N)');
  tag(GEO.b2.circle.x, 104, 'Salida (N)');
  tag((GEO.car.inB1 + GEO.car.inB2) / 2, Y + 46, 'Carro de transferencia');

  // The car
  carEl = svgEl('g', { class: 'car-body' }, layers.car);
  svgEl('rect', { x: -38, y: -22, width: 76, height: 44, rx: 6, class: 'car-rect' }, carEl);
  svgEl('circle', { cx: -22, cy: 24, r: 6, class: 'car-wheel' }, carEl);
  svgEl('circle', { cx: 22, cy: 24, r: 6, class: 'car-wheel' }, carEl);
  moveCar(GEO.car.inB1, 0);
}

function moveCar(x, seconds) {
  carEl.style.transitionDuration = `${seconds}s`;
  carEl.style.transform = `translate(${x}px, ${GEO.laneY}px)`;
}

// ---------------------------------------------------------------------------
// Pallets: create/move/remove by QR identity
// ---------------------------------------------------------------------------

const palletEls = new Map(); // qr -> { g, riding }

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
  const X = GEO.entryX;
  if (s >= PE.ReadingQR && s <= PE.SendingID) return { x: X, y: GEO.qrY };
  if (s === PE.ReadingQrInError) return { x: X, y: GEO.qrY };
  if (s === PE.WaitForBocedi1 || s === PE.WaitForCar) return { x: X, y: GEO.approachY };
  if (s === PE.WaitEnterBocedi) return { x: GEO.b1.entrance, y: GEO.laneY };
  if (s === PE.WaitEnterCar) return { x: GEO.car.inB1, y: GEO.laneY };
  return null; // Waiting / update states / Paused: shown elsewhere or gone
}

function collectTargets(st) {
  const targets = new Map();
  const add = (pallet, x, y, opts = {}) => {
    if (!pallet || !pallet.Qr || targets.has(pallet.Qr)) return;
    targets.set(pallet.Qr, { pallet, x, y, ...opts });
  };

  for (const [pk, geo] of [[st.Packager1, GEO.b1], [st.Packager2, GEO.b2]]) {
    if (!pk) continue;
    (pk.Queue || []).forEach((p, i) => {
      if (i < geo.slots.length) add(p, geo.slots[i], GEO.laneY, { labelHigh: i % 2 === 1 });
    });
    add(pk.LabelPallet, geo.circle.x, geo.circle.y);
    add(pk.ExitPallet, geo.circle.x, geo.exitY);
  }

  if (st.Car && st.Car.HasPallet && st.Car.Pallet) {
    const t = carTarget(st.Car.CarPosition);
    add(st.Car.Pallet, t.x, GEO.laneY, { seconds: t.seconds });
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

  // Car
  const ct = carTarget(st.Car ? st.Car.CarPosition : 0);
  moveCar(ct.x, ct.seconds);
  carEl.classList.toggle('unknown', !st.Car || st.Car.CarPosition === 0);

  // QR reader status (mirrors the dashboard camera logic)
  const s = st.MachineState.PalletEntry;
  qrBoxEl.setAttribute('class', 'qr-station');
  if (s === PE.ReadingQrInError) qrBoxEl.classList.add('bad', 'blink');
  else if (s === PE.ReadingQR || s === PE.Paused) qrBoxEl.classList.add('ok', 'blink');
  else if (s !== undefined && s !== PE.Waiting) qrBoxEl.classList.add('ok');

  // Labeling circles pulse while labeling
  labelCircle1.classList.toggle('active', st.MachineState.PalletLabel1 === LABELING_STATE);
  labelCircle2.classList.toggle('active', st.MachineState.PalletLabel2 === LABELING_STATE);

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
  const w = Math.min(Math.max(vb.w * factor, 220), GEO.world.w * 2.2);
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
