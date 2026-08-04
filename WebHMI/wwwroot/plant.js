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
const INTENT_Y = 1795; // routing arrows: above the pallets, clear of the QR tag
const AT_LABELER_STATES = [3, 4, 5]; // PalletLabel: Labeling, WaitUpdate2, WaitAck

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
let routeIntent1;
let routeIntent2;
let carChevE;
let carChevW;
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

  // Routing intent: chevrons above the junction lighting up in sequence
  // toward where the entry pallet wants to go (the pallet itself only moves
  // once it's in the FIFO/car).
  routeIntent1 = chevronRow(layers.overlays, GEO.entryX + 170, INTENT_Y, 1, 40); // east, into Bocedi 1
  routeIntent2 = chevronRow(layers.overlays, GEO.entryX - 170, INTENT_Y, -1, 40); // west, onto the car

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
  // Travel-direction chevrons riding with the car (shown while GoingToB1/B2)
  carChevE = chevronRow(carEl, 330, 0, 1, 42, 'car-chev');
  carChevW = chevronRow(carEl, -330, 0, -1, 42, 'car-chev');
  moveCar(GEO.car.inB1, 0);
}

// A row of three chevrons pointing toward dir (+1 east, -1 west), lighting
// up in sequence away from the start like a conveyor direction indicator.
function chevronRow(parent, x, y, dir, size, cls = 'route-intent') {
  const g = svgEl('g', { class: cls }, parent);
  for (let i = 0; i < 3; i++) {
    svgEl('path', {
      d: `M ${x + dir * i * size * 1.65} ${y - size} l ${dir * size} ${size} l ${-dir * size} ${size}`,
      class: 'route-chevron', style: `animation-delay:${i * 0.15}s`,
    }, g);
  }
  return g;
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
  // Once routed, the physical pallet holds at the junction until it actually
  // shows up in the FIFO or on the car — those targets win the QR dedupe in
  // collectTargets, and the glide there is the arrival animation. The
  // route-intent arrows say where it wants to go meanwhile. One exception:
  // with the car docked in B1 the PLC is rolling the pallet onto it, so
  // glide to the car spot to make the mount (Car.HasPallet) seamless.
  if (s >= PE.WaitEnterCar && s <= PE.UpdateCar
      && st.Car && st.Car.CarPosition === 2 /* InB1 */) {
    return { x: GEO.car.inB1, y: Y, seconds: 1.4 };
  }
  if (s >= PE.WaitForBocedi1 && s <= PE.UpdateCar) return { x: GEO.entryX, y: Y };
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

  if (st.MachineState.ElevatorAccess === EA.WaitingLeave) {
    targets.set('__elevator__', { ghost: true, x: GEO.elevator.x, y: GEO.elevator.y });
  }

  return targets;
}

const CAR_MID = (GEO.car.inB1 + GEO.car.inB2) / 2;

function carTarget(pos) {
  switch (pos) {
    case 2: return { x: GEO.car.inB1, seconds: 0.7 };
    case 4: return { x: GEO.car.inB2, seconds: 0.7 };
    // Traveling: the PLC only reports the direction, and the real travel
    // time varies (the safety sensor pauses the car near people), so hold
    // mid-rail with the traveling look until arrival is confirmed.
    case 1: case 3: return { x: CAR_MID, seconds: 2 };
    default: return { x: GEO.car.unknown, seconds: 1 };
  }
}

// ---------------------------------------------------------------------------
// Render
// ---------------------------------------------------------------------------

setupConnChips($('connChips'));

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
  renderConnChips(st);

  const carPos = st.Car ? st.Car.CarPosition : 0;
  const ct = carTarget(carPos);
  moveCar(ct.x, ct.seconds);
  carEl.classList.toggle('unknown', carPos === 0);
  carEl.classList.toggle('traveling', carPos === 1 || carPos === 3);
  carChevE.classList.toggle('on', carPos === 1); // B1 is the east dock
  carChevE.classList.toggle('go', carPos === 1);
  carChevW.classList.toggle('on', carPos === 3);
  carChevW.classList.toggle('go', carPos === 3);

  const s = st.MachineState.PalletEntry;
  qrLiveEl.setAttribute('class', 'qr-live');
  if (s === PE.ReadingQrInError) qrLiveEl.classList.add('bad', 'blink');
  else if (s === PE.ReadingQR || s === PE.Paused) qrLiveEl.classList.add('ok', 'blink');
  else if (s !== undefined && s !== PE.Waiting) qrLiveEl.classList.add('ok');

  const e = st.MachineState.ElevatorAccess;
  elevQrLiveEl.setAttribute('class', 'qr-live');
  // A failed elevator read is the ElevatorFailedQr coil, not a state:
  // ElevatorAccess handles the failure inside ReadingQr and stays there.
  if (signalValue(st, 'ElevatorFailedQr')) elevQrLiveEl.classList.add('bad', 'blink');
  else if (e === EA.ReadingQr) elevQrLiveEl.classList.add('ok', 'blink');
  else if (e === EA.WaitingLeave) elevQrLiveEl.classList.add('ok');

  labelRing1.classList.toggle('active', AT_LABELER_STATES.includes(st.MachineState.PalletLabel1));
  labelRing2.classList.toggle('active', AT_LABELER_STATES.includes(st.MachineState.PalletLabel2));

  const avail = (el, on) => {
    el.classList.toggle('ok', !!on);
    el.classList.toggle('bad', !on);
  };
  avail(availBox1, st.Signals[SIG.bcd1Avaliable]);
  avail(availBox2, st.Signals[SIG.bcd2Avaliable]);

  // Routing intent: primarily the ToEmb1/ToEmb2 coils (the actual command
  // sent to the PLC), with the entry states as fallback for older servers.
  const hasEntry = !!(st.EntryPallet && st.EntryPallet.Qr);
  const toB1 = signalValue(st, 'ToEmb1') || (s >= PE.WaitForBocedi1 && s <= PE.UpdateFIFO1);
  const toB2 = signalValue(st, 'ToEmb2') || (s >= PE.WaitForCar && s <= PE.UpdateCar);
  const moving = (s >= PE.WaitEnterBocedi && s <= PE.UpdateFIFO1)
    || (s >= PE.WaitEnterCar && s <= PE.UpdateCar);
  const setIntent = (el, on) => {
    el.classList.toggle('on', on);
    el.classList.toggle('go', on && moving);
    el.classList.toggle('wait', on && !moving);
  };
  setIntent(routeIntent1, hasEntry && !!toB1);
  setIntent(routeIntent2, hasEntry && !!toB2);

  setPalletTargets(collectTargets(st));

  setNotice('entry', st.ErrorMessages.EntryError);
  setNotice('bcd1', st.ErrorMessages.BDC1Error);
  setNotice('bcd2', st.ErrorMessages.BDC2Error);
  setNotice('car', st.ErrorMessages.CarError);
}

// ---------------------------------------------------------------------------
// Pan & zoom (shared Figma-style controls from common.js)
// ---------------------------------------------------------------------------

const panZoom = setupPanZoom([floorSvg, svg], GEO.world);
$('zoomInBtn').addEventListener('click', () => panZoom.zoomCenter(1 / 1.35));
$('zoomOutBtn').addEventListener('click', () => panZoom.zoomCenter(1.35));
$('zoomFitBtn').addEventListener('click', () => panZoom.reset());
$('rotateBtn').addEventListener('click', () => panZoom.rotate(90));

// Inside the pan/zoom roots, so the rotate button turns the whole scene.
// fill="none" mirrors the original drawing's root attribute: its dashed
// enclosure rects have no fill and must not default to black.
layers.floor = svgEl('g', { fill: 'none' }, panZoom.roots[0]);
layers.overlays = svgEl('g', {}, panZoom.roots[1]);
layers.car = svgEl('g', {}, panZoom.roots[1]);
layers.pallets = svgEl('g', {}, panZoom.roots[1]);

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
  clearConnChips();
};
