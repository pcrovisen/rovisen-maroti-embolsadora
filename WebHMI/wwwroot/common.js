// Shared constants for the Web HMI pages (loaded before app.js / diag.js).
// Enum mirrors: same order as ModbusServer / FatekPLC.Signals coils 21+.

'use strict';

const SIGNAL_NAMES = [
  'ReadQR', 'Label1', 'Label2', 'SendingFIFOs', 'Ready',
  'Del1Valid', 'Del1Error', 'Del2Valid', 'Del2Error',
  'SendUpdate', 'SendUpdate2', 'CarWithPallet', 'CarInB1', 'CarInB2',
  'bcd1Avaliable', 'bcd2Avaliable', 'Leave1', 'Leave2',
  'WaitBocedi', 'WaitCar', 'SlaveConnected', 'WaitingPallet',
  'PLCStarting', 'PLCLabeling1', 'PLCLabeling2',
  'WaitCorrection1', 'WaitCorrection2', 'BCD2EntryError',
  'CarEntryError', 'BCD1OK', 'BCD2OK', 'Pause', 'ElevatorRequest',
  'LabelNull1', 'LabelNull2', 'WaitLabel1', 'WaitLabel2',
];
const SIG = Object.fromEntries(SIGNAL_NAMES.map((n, i) => [n, i]));

// Coils 1-20, written by the PC, sent as PcSignals.
const PC_SIGNAL_NAMES = [
  'ReadingPallet', 'SendingQR', 'Labeling1', 'Labeling2',
  'ToEmb1', 'ToEmb2', 'ReceivingFIFOs', 'Alive',
  'DelEmb1', 'DelEmb2', 'ConfirmUpdate', 'ConfirmUpdate2',
  'WeightOk1', 'WeightOk2', 'PalletLeave1', 'PalletLeave2',
  'ErrorQr', 'Waiting', 'ElevatorAuth', 'ElevatorFailedQr',
];
const PSIG = Object.fromEntries(PC_SIGNAL_NAMES.map((n, i) => [n, i]));

// Value of a coil by name, whichever side writes it; null if the name is
// unknown or the server predates PcSignals.
function signalValue(st, name) {
  if (name in SIG) return !!st.Signals[SIG[name]];
  if (name in PSIG) return st.PcSignals ? !!st.PcSignals[PSIG[name]] : null;
  return null;
}

const PE = {
  Waiting: 0, ReadingQR: 1, WaitingSetQr: 2, WaitingSetEntryPallet: 3,
  WaitingAvailability: 4, DefaultBehavior: 5, AskingDB: 6, SendingID: 7,
  WaitForBocedi1: 8, WaitEnterBocedi: 9, WaitUpdateFIFO1: 10, UpdateFIFO1: 11,
  WaitForCar: 12, WaitEnterCar: 13, WaitUpdateCar: 14, UpdateCar: 15,
  ReadingQrInError: 16, Paused: 17,
};

// ElevatorAccess.States
const EA = {
  WaitingRequest: 0, ReadingQr: 1, WaitingAuth: 2, WaitingLeave: 3, Delay: 4,
};

const CAR_POS_TEXT = {
  0: 'Posición desconocida',
  1: 'Hacia Bocedi 1',
  2: 'En Bocedi 1',
  3: 'Hacia Bocedi 2',
  4: 'En Bocedi 2',
};

const CONN_LABELS = {
  MasterPLC: 'PLC Maestro',
  SlavePLC: 'PLC Esclavo',
  WencoDB: 'BD Wenco',
  QrReader: 'Lector QR',
  Packager1: 'Embolsadora 1',
  Packager2: 'Embolsadora 2',
  Labeler1: 'Etiquetadora 1',
  Labeler2: 'Etiquetadora 2',
};

// ---------- Marca Wenco ----------

// Corporate wordmark, traced from `wenco-logo-azul.svg` (Illustrator export)
// and inlined instead of loaded with <img src>: the single-file demos ship
// without assets, and inlining lets the paths take `currentColor` — white on
// the dark topbar, azul Wenco on anything light.
const WENCO_BLUE = '#0033a1';
const WENCO_LOGO_VIEWBOX = '0 0 164 50';
const WENCO_LOGO_PATHS = [
  // ® mark
  'M160.85,45.3c0.11,0,0.21,0,0.3-0.01c0.1-0.01,0.18-0.03,0.25-0.06c0.07-0.03,0.13-0.08,0.18-0.15c0.04-0.07,0.07-0.16,0.07-0.28c0-0.1-0.02-0.18-0.06-0.24c-0.04-0.06-0.09-0.1-0.15-0.14c-0.06-0.03-0.13-0.05-0.21-0.07c-0.08-0.01-0.16-0.02-0.24-0.02h-0.62v0.95H160.85z M161.06,43.96c0.36,0,0.63,0.07,0.8,0.21c0.17,0.14,0.26,0.36,0.26,0.65c0,0.27-0.08,0.47-0.23,0.59c-0.15,0.13-0.34,0.2-0.57,0.22l0.87,1.33h-0.5l-0.83-1.29h-0.5v1.29h-0.47v-3.01H161.06z M158.92,46.33c0.11,0.27,0.26,0.5,0.45,0.7c0.19,0.2,0.42,0.35,0.68,0.46c0.26,0.11,0.54,0.17,0.85,0.17c0.3,0,0.58-0.06,0.84-0.17c0.26-0.11,0.49-0.27,0.68-0.46c0.19-0.2,0.34-0.43,0.45-0.7c0.11-0.27,0.16-0.56,0.16-0.88c0-0.31-0.06-0.59-0.16-0.86c-0.11-0.27-0.26-0.5-0.45-0.69c-0.19-0.19-0.42-0.35-0.68-0.46c-0.26-0.11-0.54-0.17-0.84-0.17c-0.31,0-0.59,0.06-0.85,0.17c-0.26,0.11-0.49,0.27-0.68,0.46c-0.19,0.2-0.34,0.43-0.45,0.69c-0.11,0.27-0.16,0.55-0.16,0.86C158.76,45.77,158.82,46.07,158.92,46.33 M158.5,44.44c0.14-0.31,0.33-0.58,0.56-0.81c0.24-0.23,0.52-0.41,0.83-0.54c0.32-0.13,0.65-0.2,1.01-0.2c0.35,0,0.69,0.07,1,0.2c0.31,0.13,0.59,0.31,0.82,0.54c0.24,0.23,0.42,0.5,0.56,0.81c0.14,0.31,0.21,0.65,0.21,1.01c0,0.37-0.07,0.71-0.21,1.03c-0.14,0.31-0.33,0.59-0.56,0.82c-0.23,0.23-0.51,0.41-0.82,0.54c-0.31,0.13-0.65,0.19-1,0.19c-0.36,0-0.69-0.06-1.01-0.19c-0.32-0.13-0.6-0.31-0.83-0.54c-0.24-0.23-0.43-0.51-0.56-0.82c-0.14-0.31-0.21-0.66-0.21-1.03C158.29,45.09,158.36,44.75,158.5,44.44',
  // W
  'M38.77,1.66c0,0-4.12,22.62-5.54,30.43c-1.02,5.63-1.93,8.32-4.64,8.32c-3.09,0-3.15-3.43-2.52-6.78c1.36-7.22,3.75-19.96,3.75-19.96l-9.26,0c0,0-2.67,14.2-3.59,19.07c-0.98,5.21-1.8,7.67-4.52,7.67c-3.55,0-2.84-5.13-2.44-7.26c1.66-8.84,5.88-31.49,5.88-31.49l-9.26,0c0,0-4.21,22.26-5.75,30.39c-0.25,1.34-1.41,7.64,2.19,12.16c1.5,1.88,3.92,4.13,9.07,4.13c2.31,0,5.08-0.58,7.97-3.13c1.65,1.6,3.9,3.13,8.16,3.13c4.4,0,11.54-2.11,14.1-16.04c1.47-8,5.65-30.63,5.65-30.63L38.77,1.66z',
  // enco
  'M53.96,28.84c0,0.01,0.3-1.77,0.52-3.09c0.16-0.96,1.74-4.51,6.16-4.5c1.4,0,2.37,0.99,2.37,2.4C63.01,26.03,60.17,28.6,53.96,28.84 M151.46,25.61l-0.19,1.05c-0.38,2.09-1.09,5.97-1.75,9.55c-0.27,1.45-2,4.5-5.66,4.5c-4.11,0-5.06-2.3-4.69-4.33c0.52-2.85,1.28-7.04,1.95-10.62c0.01-0.04,1-4.27,5.66-4.49c1.82,0,3.19,0.49,3.99,1.4C151.4,23.42,151.64,24.43,151.46,25.61 M145.69,13.92c-6.77,0.49-12.03,4.62-13.08,10.29c-0.41,2.22-0.84,4.56-1.19,6.48c-1.26,5.48-6.95,9.77-13.73,9.77c-3.73,0-5.13-1.68-4.69-4.08c0.51-2.79,1.28-7.04,1.95-10.62c0.01-0.04,1.06-4.51,6.05-4.51c0.37,0,0.74,0.03,1.08,0.1l-0.99,5.47l8.35,0l1.68-8.79c-0.64-0.7-4.12-4.17-10.24-4.17c-7.37,0-13.3,4.25-14.43,10.34c-0.63,3.39-2.53,14.05-2.53,14.05c0,0.02-0.36,2.46-2.15,2.18c-0.71-0.11-1.42-0.8-1.09-2.74c0,0,1.54-8.54,1.77-9.67c0.63-3.12,0.11-7.66-2.46-10.78c-1.82-2.21-4.45-3.37-7.6-3.37c-4.17,0-6.99,1.73-8.34,2.81l-1.19-2.81l-6.46,0l-2.88,15.49c-0.9,4.99-8.59,11.08-16.28,11.08l-0.24,0c-1.77-0.04-3.09-0.51-3.82-1.35c-0.59-0.69-0.8-1.61-0.63-2.73l0.08-0.46c9.64-0.12,15.53-2.83,17.99-8.28c1.09-2.42,1.63-6.38-0.46-9.58c-1.25-1.92-3.91-4.2-9.52-4.2c-7.38,0-13.54,4.35-14.65,10.34c-0.67,3.6-1.9,10.38-1.96,10.68c-0.68,3.74,1.06,13.23,13.2,13.23c5.86,0,10.51-2.32,13.75-4.71l-0.62,3.4l9.21,0c0,0,2.91-15.65,3.58-19.27c1.05-5.69,6.1-5.74,7.33-5.4c3.37,0.92,3,4.13,2.94,4.5c-0.25,1.43-1.81,9.91-1.81,9.91c-0.45,2.41-0.68,4.98,0.68,7.47c1.47,2.7,4.24,4.12,8.47,4.12c2.77,0,6.06-1.44,7.53-2.99c0.92,0.87,3.71,2.99,9.41,2.99c6.92,0,11.72-3.4,14.33-5.92c0.33,0.69,3.21,5.92,11.67,5.92c7.74,0,13.28-4.67,14.33-10.34c0.67-3.6,1.38-7.49,1.76-9.58c0,0,0.15-0.82,0.2-1.1c1.1-5.7-3.02-13.22-13.08-13.22C146.63,13.87,146.09,13.89,145.69,13.92z',
];

function wencoLogoSvg(cls = 'wenco-logo') {
  return `<svg class="${cls}" viewBox="${WENCO_LOGO_VIEWBOX}" fill="currentColor"`
    + ' role="img" aria-label="Wenco">'
    + WENCO_LOGO_PATHS.map((d) => `<path d="${d}"/>`).join('')
    + '</svg>';
}

// Tab icon: just the W, white on a rounded azul-Wenco tile (the full wordmark
// is illegible at 16 px).
function wencoFaviconHref() {
  const svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">'
    + `<rect width="64" height="64" rx="13" fill="${WENCO_BLUE}"/>`
    + `<path transform="translate(8 7)" fill="#fff" d="${WENCO_LOGO_PATHS[1]}"/>`
    + '</svg>';
  return `data:image/svg+xml,${encodeURIComponent(svg)}`;
}

// Stamps the wordmark on every page (topbar `.brand`, plus any element marked
// `data-wenco-logo`) and sets the favicon. Runs on load — common.js is the
// last-but-one script on every page and is embedded first in the demos.
function applyWencoBranding() {
  for (const brand of document.querySelectorAll('.brand')) {
    if (brand.querySelector('.wenco-logo')) continue;
    brand.insertAdjacentHTML('afterbegin', `${wencoLogoSvg()}<span class="brand-sep"></span>`);
  }
  for (const holder of document.querySelectorAll('[data-wenco-logo]')) {
    holder.innerHTML = wencoLogoSvg();
  }
  let icon = document.querySelector('link[rel="icon"]');
  if (!icon) {
    icon = document.createElement('link');
    icon.rel = 'icon';
    document.head.appendChild(icon);
  }
  icon.type = 'image/svg+xml';
  icon.href = wencoFaviconHref();
}

applyWencoBranding();

// ---------- Connection chips ----------

// The status strip under the header, on every page: one chip per device, big
// enough to read from a couple of metres away. Pages call setupConnChips()
// once and then renderConnChips() on each snapshot.
let connChipEls = {};

function setupConnChips(container = document.getElementById('connChips')) {
  connChipEls = {};
  if (!container) return connChipEls;
  container.innerHTML = '';
  for (const [key, label] of Object.entries(CONN_LABELS)) {
    const chip = document.createElement('span');
    chip.className = 'chip';
    chip.textContent = label;
    container.appendChild(chip);
    connChipEls[key] = chip;
  }
  return connChipEls;
}

function renderConnChips(st) {
  for (const [key, chip] of Object.entries(connChipEls)) {
    chip.classList.toggle('on', !!st.Connections[key]);
  }
}

function clearConnChips() {
  for (const chip of Object.values(connChipEls)) chip.classList.remove('on');
}

// Human-readable state of a machine in a snapshot ("Waiting"), falling back
// to the raw index when the server didn't send the name table.
function machineStateName(st, machine) {
  const idx = st.MachineState[machine];
  const names = st.States && st.States[machine];
  return (names && names[idx]) ?? (idx === undefined ? '' : String(idx));
}

function escapeHtml(s) {
  return String(s ?? '').replace(/[&<>"']/g, (c) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
  }[c]));
}

// Figma-style pan & zoom over one or more stacked SVGs sharing a viewBox:
// wheel / two-finger scroll pans, pinch (Ctrl+wheel on trackpads) zooms,
// dragging with mouse or one finger pans, two-finger touch drag pans+zooms,
// double-tap zooms in on the tapped point.
function setupPanZoom(svgs, world) {
  const top = svgs[svgs.length - 1];

  // Everything is drawn inside these groups (see .roots) so the scene can be
  // rotated without disturbing the pan/zoom math: the viewBox stays aligned
  // with the screen and only the content turns inside it.
  const roots = svgs.map((s) => {
    const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    while (s.firstChild) g.appendChild(s.firstChild);
    s.appendChild(g);
    return g;
  });

  let base = { ...world }; // world box as drawn
  let rotation = 0;
  let w0 = { ...world };   // world box as displayed (rotation applied)
  const vb = { ...world };

  // iOS Safari: touch-action + pointer events alone don't stop the native
  // gestures — pinch still zooms the page and vertical drags rubber-band the
  // document (which is why panning felt horizontal-only). Cancel the touch
  // defaults and Safari's proprietary gesture events on the stage itself.
  for (const ev of ['touchstart', 'touchmove', 'touchend']) {
    top.addEventListener(ev, (e) => e.preventDefault(), { passive: false });
  }
  for (const ev of ['gesturestart', 'gesturechange', 'gestureend']) {
    top.addEventListener(ev, (e) => e.preventDefault());
  }

  const apply = () => {
    const box = `${vb.x} ${vb.y} ${vb.w} ${vb.h}`;
    for (const s of svgs) s.setAttribute('viewBox', box);
  };

  // Turns the scene about the centre of its world box; a quarter turn swaps
  // the box's width and height, so a wide drawing becomes a tall one that
  // fills a phone screen. Refits the view, like the zoom-to-fit button.
  const applyRotation = () => {
    const cx = base.x + base.w / 2;
    const cy = base.y + base.h / 2;
    for (const g of roots) {
      if (rotation) g.setAttribute('transform', `rotate(${rotation} ${cx} ${cy})`);
      else g.removeAttribute('transform');
    }
    const quarter = rotation % 180 !== 0;
    const w = quarter ? base.h : base.w;
    const h = quarter ? base.w : base.h;
    w0 = { x: cx - w / 2, y: cy - h / 2, w, h };
    Object.assign(vb, w0);
    apply();
  };

  // The SVG renders the viewBox with ONE uniform scale (preserveAspectRatio
  // xMidYMid meet), so px ↔ viewBox conversion must use that same factor on
  // both axes. Mapping each axis separately made vertical pans crawl
  // whenever the stage and viewBox aspect ratios differ (portrait phone vs
  // the wide plant drawing).
  const metrics = () => {
    const r = top.getBoundingClientRect();
    const s = Math.max(vb.w / r.width, vb.h / r.height); // viewBox units per px
    return {
      s,
      ox: r.left + (r.width - vb.w / s) / 2,
      oy: r.top + (r.height - vb.h / s) / 2,
    };
  };

  const zoomAt = (cx, cy, factor) => {
    const m = metrics();
    const px = vb.x + (cx - m.ox) * m.s;
    const py = vb.y + (cy - m.oy) * m.s;
    const w = Math.min(Math.max(vb.w * factor, w0.w / 12), w0.w * 2.5);
    const scale = w / vb.w;
    vb.x = px - (px - vb.x) * scale;
    vb.y = py - (py - vb.y) * scale;
    vb.w = w;
    vb.h *= scale;
    apply();
  };

  top.addEventListener('wheel', (e) => {
    e.preventDefault();
    if (e.ctrlKey || e.metaKey) {
      zoomAt(e.clientX, e.clientY, Math.exp(e.deltaY * 0.01));
    } else {
      const { s } = metrics();
      vb.x += e.deltaX * s;
      vb.y += e.deltaY * s;
      apply();
    }
  }, { passive: false });

  const pointers = new Map();
  let lastGesture = null;
  const gesture = () => {
    const [a, b] = [...pointers.values()];
    return {
      cx: (a.x + b.x) / 2,
      cy: (a.y + b.y) / 2,
      dist: Math.hypot(a.x - b.x, a.y - b.y) || 1,
    };
  };

  // Double-tap zoom (touch/pen): a tap only counts if the finger didn't
  // drag, so quick successive pan flicks never trigger a false zoom.
  let lastTap = { t: 0, x: 0, y: 0 };
  let tapCandidate = null;
  top.addEventListener('pointerdown', (e) => {
    top.setPointerCapture(e.pointerId);
    pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    lastGesture = pointers.size === 2 ? gesture() : null;
    if (e.pointerType !== 'mouse' && pointers.size === 1) {
      if (Date.now() - lastTap.t < 350
          && Math.hypot(e.clientX - lastTap.x, e.clientY - lastTap.y) < 40) {
        zoomAt(e.clientX, e.clientY, 1 / 1.8);
        lastTap.t = 0;
        tapCandidate = null;
      } else {
        tapCandidate = { id: e.pointerId, x: e.clientX, y: e.clientY };
      }
    } else {
      tapCandidate = null; // a second finger means gesture, not tap
    }
  });

  top.addEventListener('pointermove', (e) => {
    const prev = pointers.get(e.pointerId);
    if (!prev) return;
    pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    if (pointers.size === 2) {
      const g = gesture();
      if (lastGesture) {
        const { s } = metrics();
        vb.x -= (g.cx - lastGesture.cx) * s;
        vb.y -= (g.cy - lastGesture.cy) * s;
        apply();
        zoomAt(g.cx, g.cy, lastGesture.dist / g.dist);
      }
      lastGesture = g;
    } else if (pointers.size === 1) {
      // One pointer pans, mouse and touch alike (the stage never scrolls
      // the page: touch-action none).
      if (tapCandidate && e.pointerId === tapCandidate.id
          && Math.hypot(e.clientX - tapCandidate.x, e.clientY - tapCandidate.y) > 12) {
        tapCandidate = null; // it's a drag, not a tap
      }
      const { s } = metrics();
      vb.x -= (e.clientX - prev.x) * s;
      vb.y -= (e.clientY - prev.y) * s;
      apply();
    }
  });

  for (const ev of ['pointerup', 'pointercancel', 'pointerleave']) {
    top.addEventListener(ev, (e) => {
      pointers.delete(e.pointerId);
      lastGesture = pointers.size === 2 ? gesture() : null;
      if (tapCandidate && e.pointerId === tapCandidate.id) {
        lastTap = { t: ev === 'pointerup' ? Date.now() : 0, x: tapCandidate.x, y: tapCandidate.y };
        tapCandidate = null;
      }
    });
  }

  apply();
  return {
    // The groups every caller must draw into, or rotation would leave its
    // content behind.
    roots,
    setWorld(newWorld) {
      base = { ...newWorld };
      applyRotation();
    },
    rotate(deg = 90) {
      rotation = (rotation + deg + 360) % 360;
      applyRotation();
      return rotation;
    },
    reset() {
      Object.assign(vb, w0);
      apply();
    },
    zoomCenter(factor) {
      const r = top.getBoundingClientRect();
      zoomAt(r.left + r.width / 2, r.top + r.height / 2, factor);
    },
  };
}
