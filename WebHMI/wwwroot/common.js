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
  let w0 = { ...world };
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
    setWorld(newWorld) {
      w0 = { ...newWorld };
      Object.assign(vb, w0);
      apply();
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
