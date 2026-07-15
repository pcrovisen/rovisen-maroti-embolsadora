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
// two-finger touch drag pans+zooms, mouse drag pans, single touch is ignored.
function setupPanZoom(svgs, world) {
  const top = svgs[svgs.length - 1];
  let w0 = { ...world };
  const vb = { ...world };

  const apply = () => {
    const box = `${vb.x} ${vb.y} ${vb.w} ${vb.h}`;
    for (const s of svgs) s.setAttribute('viewBox', box);
  };

  const zoomAt = (cx, cy, factor) => {
    const r = top.getBoundingClientRect();
    const px = vb.x + ((cx - r.left) / r.width) * vb.w;
    const py = vb.y + ((cy - r.top) / r.height) * vb.h;
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
      const r = top.getBoundingClientRect();
      vb.x += (e.deltaX / r.width) * vb.w;
      vb.y += (e.deltaY / r.height) * vb.h;
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

  top.addEventListener('pointerdown', (e) => {
    top.setPointerCapture(e.pointerId);
    pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    lastGesture = pointers.size === 2 ? gesture() : null;
  });

  top.addEventListener('pointermove', (e) => {
    const prev = pointers.get(e.pointerId);
    if (!prev) return;
    pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    if (pointers.size === 2) {
      const g = gesture();
      if (lastGesture) {
        const r = top.getBoundingClientRect();
        vb.x -= ((g.cx - lastGesture.cx) / r.width) * vb.w;
        vb.y -= ((g.cy - lastGesture.cy) / r.height) * vb.h;
        apply();
        zoomAt(g.cx, g.cy, lastGesture.dist / g.dist);
      }
      lastGesture = g;
    } else if (pointers.size === 1 && e.pointerType === 'mouse') {
      const r = top.getBoundingClientRect();
      vb.x -= ((e.clientX - prev.x) / r.width) * vb.w;
      vb.y -= ((e.clientY - prev.y) / r.height) * vb.h;
      apply();
    }
  });

  for (const ev of ['pointerup', 'pointercancel', 'pointerleave']) {
    top.addEventListener(ev, (e) => {
      pointers.delete(e.pointerId);
      lastGesture = pointers.size === 2 ? gesture() : null;
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
