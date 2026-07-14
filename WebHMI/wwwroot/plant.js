// Vista de planta — the operator's own plant drawing (plant_floor.svg,
// generated from wenco_plant_5.svg) with live pallets, car and station
// status overlaid. Coordinates are in the drawing's space (10859 × 4923,
// north up): pallets rise from the elevator at the south along the vertical
// conveyor, get their QR read at the junction, and go east into Bocedi 1 or
// west onto the transfer car toward Bocedi 2.
//
// Queue model (minimum semantics): every new pallet entering a Bocedi pushes
// the ones ahead to the next station. Stations per Bocedi, in flow order:
// entry queue (1 slot in B1, 2 in B2) → processing (bagging) → two-slot
// queue that packs toward the labeler → labeling circle (LabelPallet) →
// output (ExitPallet). With 2 queued pallets that reads X X _ _; with 3,
// X X _ X (the front one slides to the far slot of the two-space queue).

'use strict';

const SVGNS = 'http://www.w3.org/2000/svg';
const $ = (id) => document.getElementById(id);
const svg = $('plantSvg');
const floorSvg = $('plantFloorSvg');

function svgEl(tag, attrs = {}, parent) {
  const el = document.createElementNS(SVGNS, tag);
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  if (parent) parent.appendChild(el);
  return el;
}

// ---------------------------------------------------------------------------
// Geometry: anchors taken from the sample pallets in the original drawing
// ---------------------------------------------------------------------------

const Y = 2062;

const GEO = {
  world: { x: 0, y: 0, w: 10859, h: 4923 },
  entryX: 7255,
  spawn: { x: 7255, y: 3450 },          // appears at the south conveyor, glides north
  elevator: { x: 6743, y: 3806 },
  qrTag: { x: 7224, y: 1621, w: 62, h: 99 },
  elevQrTag: { x: 6696, y: 3427, w: 62, h: 99 },
  // Dashed enclosures in the drawing (painted by the availability signals)
  b1Box: { x: 7483.5, y: 1650.5, w: 2222, h: 657 },
  b2Box: { x: 864.5, y: 1650.5, w: 2703, h: 653 },
  elevBox: { x: 6442.5, y: 3260.5, w: 569, h: 783 },
  b1: {
    stations: [{ x: 7745 }, { x: 8395 }, { x: 9031 }, { x: 9486 }], // entry, processing, post1, post2
    entrySlots: 1,
    labeler: { x: 9983, y: 2062, r: 275 },
    output: { x: 9982, y: 1362 },
    overflow: [{ x: 7255, y: 2560 }, { x: 7255, y: 2990 }], // backs up down the entry conveyor
  },
  b2: {
    stations: [{ x: 3320 }, { x: 2830 }, { x: 2161 }, { x: 1544 }, { x: 1114 }],
    entrySlots: 2,
    labeler: { x: 592, y: 2062, r: 275 },
    output: { x: 592, y: 1347 },
    overflow: [{ x: 3822, y: 2560 }],
  },
  car: { inB1: 6789, inB2: 3822, unknown: 5300 },
};

const PAL_W = 400;
const PAL_H = 333;
const AT_LABELER_STATES = [3, 4, 5]; // PalletLabel: Labeling, WaitUpdate2, WaitAck
const ELEVATOR_WAITING_LEAVE = 4;    // ElevatorAccess.States.WaitingLeave

// ---------------------------------------------------------------------------
// Scene: the drawing as floor + live overlays
// ---------------------------------------------------------------------------

const layers = {};
let carEl;
let qrLiveEl;
let elevQrLiveEl;
let labelRing1;
let labelRing2;
let availBox1;
let availBox2;
let lastCarX = GEO.car.inB1;

function injectFloor(markup) {
  const doc = new DOMParser().parseFromString(markup, 'image/svg+xml');
  const root = doc.documentElement;
  for (const child of [...root.childNodes]) {
    layers.floor.appendChild(document.importNode(child, true));
  }
  buildOverlays();
}

function buildOverlays() {
  // Labeling rings (pulse while labeling)
  labelRing1 = svgEl('circle', {
    cx: GEO.b1.labeler.x, cy: GEO.b1.labeler.y, r: GEO.b1.labeler.r + 26, class: 'labeling-ring',
  }, layers.overlays);
  labelRing2 = svgEl('circle', {
    cx: GEO.b2.labeler.x, cy: GEO.b2.labeler.y, r: GEO.b2.labeler.r + 26, class: 'labeling-ring',
  }, layers.overlays);

  // Live QR status: frame around the QR tags of the drawing
  const tagFrame = (t) => svgEl('rect', {
    x: t.x - 22, y: t.y - 22, width: t.w + 44, height: t.h + 44, rx: 18, class: 'qr-live',
  }, layers.overlays);
  qrLiveEl = tagFrame(GEO.qrTag);
  svgEl('line', {
    x1: GEO.qrTag.x + GEO.qrTag.w / 2, y1: GEO.qrTag.y + GEO.qrTag.h + 24,
    x2: GEO.qrTag.x + GEO.qrTag.w / 2, y2: Y - PAL_H / 2 - 20, class: 'qr-beam',
  }, layers.overlays);
  elevQrLiveEl = tagFrame(GEO.elevQrTag);
  // The elevator's QR tag lacks its lettering in the drawing
  svgEl('text', {
    x: GEO.elevQrTag.x + GEO.elevQrTag.w / 2, y: GEO.elevQrTag.y + 66,
    class: 'qr-tag-text', 'text-anchor': 'middle',
  }, layers.overlays).textContent = 'QR';

  // Station title for the elevator, inside its dashed box
  svgEl('text', {
    x: GEO.elevBox.x + GEO.elevBox.w / 2, y: GEO.elevBox.y + 110,
    class: 'station-title', 'text-anchor': 'middle',
  }, layers.overlays).textContent = 'Elevador';

  // Availability of each Bocedi painted on its dashed enclosure
  const availFrame = (b) => svgEl('rect', {
    x: b.x, y: b.y, width: b.w, height: b.h, class: 'avail-box',
  }, layers.overlays);
  availBox1 = availFrame(GEO.b1Box);
  availBox2 = availFrame(GEO.b2Box);

  // The transfer car: roller platform (the striped deck from the drawing)
  // under the red frame, so the rollers travel with the car.
  carEl = svgEl('g', { class: 'car-body' }, layers.car);
  for (let rx = -228; rx <= 208; rx += 36) {
    svgEl('rect', { x: rx, y: -205, width: 20, height: 410, class: 'car-roller' }, carEl);
  }
  svgEl('rect', { x: -240, y: -210, width: 480, height: 420, rx: 24, class: 'car-rect' }, carEl);
  for (const [px, py] of [[-150, -232], [90, -232], [-150, 208], [90, 208]]) {
    svgEl('rect', { x: px, y: py, width: 120, height: 26, class: 'car-pad' }, carEl);
  }
  moveCar(GEO.car.inB1, 0);
}

function moveCar(x, seconds) {
  lastCarX = x;
  carEl.style.transitionDuration = `${seconds}s`;
  carEl.style.transform = `translate(${x}px, ${Y}px)`;
}

// ---------------------------------------------------------------------------
// Pallets
// ---------------------------------------------------------------------------

const palletEls = new Map(); // key -> { g, label, mode: 'world'|'car' }

function createPallet(t) {
  const g = svgEl('g', { class: 'pallet' + (t.ghost ? ' ghost' : '') }, layers.pallets);
  const x0 = t.spawnAt ? t.spawnAt.x : t.x;
  const y0 = t.spawnAt ? t.spawnAt.y : t.y;
  g.style.transform = `translate(${x0}px, ${y0}px)`;
  svgEl('rect', {
    x: -PAL_W / 2, y: -PAL_H / 2, width: PAL_W, height: PAL_H, rx: 16, class: 'pallet-box',
  }, g);
  // Data written on the pallet, like the sample in the drawing.
  const label = svgEl('text', { class: 'pallet-data', 'text-anchor': 'middle' }, g);
  if (t.ghost) {
    svgEl('tspan', { x: 0, y: 20 }, label).textContent = '· · ·'; // identity unknown until read
    return { g, mode: 'world', ghost: true };
  }
  const tspans = [
    svgEl('tspan', { x: 0, y: -50, class: 'pallet-qr' }, label),
    svgEl('tspan', { x: 0, y: 25 }, label),
    svgEl('tspan', { x: 0, y: 95 }, label),
  ];
  return { g, mode: 'world', tspans };
}

function refreshPalletLabel(entry, p) {
  // The data can complete after creation (the DB assigns the Id later).
  entry.tspans[0].textContent = p.Qr;
  entry.tspans[1].textContent = p.Id && p.Id !== '0' ? `ID: ${p.Id}` : '';
  entry.tspans[2].textContent = p.Injector ? `Maq: ${p.Injector}` : '';
}

function setPalletTargets(targets) {
  for (const [key, t] of targets) {
    let entry = palletEls.get(key);
    if (!entry) {
      entry = createPallet(t);
      palletEls.set(key, entry);
      entry.g.getBoundingClientRect(); // paint the spawn position first
    }
    if (!entry.ghost) refreshPalletLabel(entry, t.pallet);
    if (t.ride) {
      // Riding the car: parent the pallet into the car group so they move
      // as one rigid body.
      if (entry.mode !== 'car') {
        carEl.appendChild(entry.g);
        entry.g.style.transitionDuration = '0s';
        entry.g.style.transform = 'translate(0px, 0px)';
        entry.mode = 'car';
      }
    } else {
      if (entry.mode === 'car') {
        // Dismount where the car currently is, then glide to the target.
        layers.pallets.appendChild(entry.g);
        entry.g.style.transitionDuration = '0s';
        entry.g.style.transform = `translate(${lastCarX}px, ${Y}px)`;
        entry.g.getBoundingClientRect();
        entry.mode = 'world';
      }
      entry.g.style.transitionDuration = `${t.seconds ?? 0.8}s`;
      entry.g.style.transform = `translate(${t.x}px, ${t.y}px)`;
    }
  }
  for (const [key, entry] of palletEls) {
    if (targets.has(key)) continue;
    palletEls.delete(key);
    if (entry.ghost) {
      // The elevator released the pallet: slide it right onto the conveyor
      // while it fades (its identity appears once the QR is read).
      entry.g.style.transitionDuration = '1.1s';
      entry.g.style.transform = `translate(${GEO.entryX}px, ${GEO.elevator.y}px)`;
    }
    entry.g.classList.add('leaving');
    const node = entry.g;
    setTimeout(() => node.remove(), 1200);
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
    // Read at the junction; newly created pallets glide up from the south.
    return { x: GEO.entryX, y: Y, spawnAt: GEO.spawn, seconds: 1.6 };
  }
  if (s === PE.WaitForBocedi1 || s === PE.WaitForCar) return { x: GEO.entryX, y: Y };
  if (s === PE.WaitEnterBocedi) return { x: GEO.b1.stations[0].x - 340, y: Y };
  if (s === PE.WaitEnterCar) return { x: GEO.car.inB1, y: Y };
  return null;
}

// Push semantics: a pallet's station index (from the entry slot, in flow
// order) equals the number of pallets that entered after it. Pallets landing
// in the two-slot queue before the labeler pack toward the far slot.
function packQueue(q, geo, add) {
  const S = geo.stations;
  const postStart = S.length - 2;
  const inPost = []; // front-most first
  let next = S.length - 1; // deepest station still free
  let outside = 0;
  q.forEach((p, i) => {
    const idx = Math.min(q.length - 1 - i, next); // pushes, capped by pallets ahead
    if (idx < 0) {
      // The machine is full: the newest arrivals wait outside.
      const o = geo.overflow[Math.min(outside++, geo.overflow.length - 1)];
      add(p, o.x, o.y ?? Y, { labelHigh: i % 2 === 1 });
      return;
    }
    next = idx - 1;
    if (idx >= postStart) inPost.push(p);
    else add(p, S[idx].x, Y, { labelHigh: i % 2 === 1 });
  });
  inPost.forEach((p, k) => add(p, S[S.length - 1 - k].x, Y, { labelHigh: k % 2 === 1 }));
}

function collectTargets(st) {
  const targets = new Map();
  const add = (pallet, x, y, opts = {}) => {
    if (!pallet || !pallet.Qr || targets.has(pallet.Qr)) return;
    targets.set(pallet.Qr, { pallet, x, y, ...opts });
  };

  for (const [pk, geo, labelState] of [
    [st.Packager1, GEO.b1, st.MachineState.PalletLabel1],
    [st.Packager2, GEO.b2, st.MachineState.PalletLabel2],
  ]) {
    if (!pk) continue;
    packQueue(pk.Queue || [], geo, add);
    if (pk.LabelPallet) add(pk.LabelPallet, geo.labeler.x, geo.labeler.y);
    if (pk.ExitPallet) add(pk.ExitPallet, geo.output.x, geo.output.y);
  }

  if (st.Car && st.Car.HasPallet && st.Car.Pallet) {
    add(st.Car.Pallet, 0, 0, { ride: true });
  }

  const et = entryPalletTarget(st);
  if (et) add(st.EntryPallet, et.x, et.y, et);

  if (st.MachineState.ElevatorAccess === ELEVATOR_WAITING_LEAVE) {
    targets.set('__elevator__', { ghost: true, x: GEO.elevator.x, y: GEO.elevator.y });
  }

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

// Errors as floating notifications over the stage (they don't move the layout)
const NOTICE_LABELS = { entry: 'Entrada', bcd1: 'Bocedi 1', bcd2: 'Bocedi 2', car: 'Carro' };
const noticeEls = new Map();

function setNotice(key, message) {
  let el = noticeEls.get(key);
  if (!message) {
    if (el) { el.remove(); noticeEls.delete(key); }
    return;
  }
  if (!el) {
    el = document.createElement('div');
    el.className = 'plant-notice';
    el.innerHTML = '<strong></strong><span></span>';
    $('plantNotices').appendChild(el);
    noticeEls.set(key, el);
  }
  el.querySelector('strong').textContent = NOTICE_LABELS[key];
  el.querySelector('span').textContent = message;
}

function handleStatus(st) {
  if (!carEl) return; // floor not injected yet
  for (const [key, chip] of Object.entries(connChips)) {
    chip.classList.toggle('on', !!st.Connections[key]);
  }

  const ct = carTarget(st.Car ? st.Car.CarPosition : 0);
  moveCar(ct.x, ct.seconds);
  carEl.classList.toggle('unknown', !st.Car || st.Car.CarPosition === 0);

  const s = st.MachineState.PalletEntry;
  qrLiveEl.setAttribute('class', 'qr-live');
  if (s === PE.ReadingQrInError) qrLiveEl.classList.add('bad', 'blink');
  else if (s === PE.ReadingQR || s === PE.Paused) qrLiveEl.classList.add('ok', 'blink');
  else if (s !== undefined && s !== PE.Waiting) qrLiveEl.classList.add('ok');

  const e = st.MachineState.ElevatorAccess;
  elevQrLiveEl.setAttribute('class', 'qr-live');
  if (e === 2) elevQrLiveEl.classList.add('bad', 'blink');      // FailedQr
  else if (e === 1) elevQrLiveEl.classList.add('ok', 'blink');  // ReadingQr
  else if (e === ELEVATOR_WAITING_LEAVE) elevQrLiveEl.classList.add('ok');

  labelRing1.classList.toggle('active', AT_LABELER_STATES.includes(st.MachineState.PalletLabel1));
  labelRing2.classList.toggle('active', AT_LABELER_STATES.includes(st.MachineState.PalletLabel2));

  const avail = (el, on) => {
    el.classList.toggle('ok', !!on);
    el.classList.toggle('bad', !on);
  };
  avail(availBox1, st.Signals[SIG.bcd1Avaliable]);
  avail(availBox2, st.Signals[SIG.bcd2Avaliable]);

  setPalletTargets(collectTargets(st));

  setNotice('entry', st.ErrorMessages.EntryError);
  setNotice('bcd1', st.ErrorMessages.BDC1Error);
  setNotice('bcd2', st.ErrorMessages.BDC2Error);
  setNotice('car', st.ErrorMessages.CarError);
}

// ---------------------------------------------------------------------------
// Pan & zoom (viewBox manipulation; mouse, wheel and touch/pinch)
// ---------------------------------------------------------------------------

const vb = { ...GEO.world };

function applyViewBox() {
  const box = `${vb.x} ${vb.y} ${vb.w} ${vb.h}`;
  svg.setAttribute('viewBox', box);
  floorSvg.setAttribute('viewBox', box);
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
  const w = Math.min(Math.max(vb.w * factor, 900), GEO.world.w * 2.2);
  const scale = w / vb.w;
  vb.x = p.x - (p.x - vb.x) * scale;
  vb.y = p.y - (p.y - vb.y) * scale;
  vb.w = w;
  vb.h *= scale;
  applyViewBox();
}

// Figma-style navigation: scroll / two-finger drag pans, pinch (Ctrl+wheel
// on trackpads) zooms. A single touch does nothing; mouse drag still pans.
svg.addEventListener('wheel', (e) => {
  e.preventDefault();
  if (e.ctrlKey || e.metaKey) {
    zoomAt(e.clientX, e.clientY, Math.exp(e.deltaY * 0.01));
  } else {
    const r = svg.getBoundingClientRect();
    vb.x += (e.deltaX / r.width) * vb.w;
    vb.y += (e.deltaY / r.height) * vb.h;
    applyViewBox();
  }
}, { passive: false });

const pointers = new Map();

const centroidAndDist = () => {
  const [a, b] = [...pointers.values()];
  return {
    cx: (a.x + b.x) / 2,
    cy: (a.y + b.y) / 2,
    dist: Math.hypot(a.x - b.x, a.y - b.y) || 1,
  };
};
let lastGesture = null;

svg.addEventListener('pointerdown', (e) => {
  svg.setPointerCapture(e.pointerId);
  pointers.set(e.pointerId, { x: e.clientX, y: e.clientY, type: e.pointerType });
  lastGesture = pointers.size === 2 ? centroidAndDist() : null;
});

svg.addEventListener('pointermove', (e) => {
  const prev = pointers.get(e.pointerId);
  if (!prev) return;
  pointers.set(e.pointerId, { x: e.clientX, y: e.clientY, type: e.pointerType });

  if (pointers.size === 2) {
    // Two fingers: pan with the centroid, zoom with the distance.
    const g = centroidAndDist();
    if (lastGesture) {
      const r = svg.getBoundingClientRect();
      vb.x -= ((g.cx - lastGesture.cx) / r.width) * vb.w;
      vb.y -= ((g.cy - lastGesture.cy) / r.height) * vb.h;
      applyViewBox();
      zoomAt(g.cx, g.cy, lastGesture.dist / g.dist);
    }
    lastGesture = g;
  } else if (pointers.size === 1 && e.pointerType === 'mouse') {
    const r = svg.getBoundingClientRect();
    vb.x -= ((e.clientX - prev.x) / r.width) * vb.w;
    vb.y -= ((e.clientY - prev.y) / r.height) * vb.h;
    applyViewBox();
  }
});

for (const ev of ['pointerup', 'pointercancel', 'pointerleave']) {
  svg.addEventListener(ev, (e) => {
    pointers.delete(e.pointerId);
    lastGesture = pointers.size === 2 ? centroidAndDist() : null;
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
// fill="none" mirrors the original drawing's root attribute: its dashed
// enclosure rects have no fill and must not default to black.
layers.floor = svgEl('g', { fill: 'none' }, floorSvg);
layers.overlays = svgEl('g', {}, svg);
layers.car = svgEl('g', {}, svg);
layers.pallets = svgEl('g', {}, svg);

// Floor: inlined by the demo build, fetched on the real page.
const inlineFloor = document.getElementById('plantFloorInline');
if (inlineFloor) {
  injectFloor(inlineFloor.innerHTML);
} else {
  fetch('plant_floor.svg')
    .then((r) => r.text())
    .then(injectFloor);
}

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
