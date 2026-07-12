// Web HMI — Línea Embolsadora
// Receives full SystemStatus snapshots over SSE and renders them.
// Wire contract: see WebHMI/README.md (mirror of HMIConnection.CreateMessage).

'use strict';

// Enum mirrors and helpers live in common.js (loaded first).

const QUEUE_SLOTS = 5;

// ---------------------------------------------------------------------------
// DOM handles
// ---------------------------------------------------------------------------

const $ = (id) => document.getElementById(id);

const els = {
  serverDot: $('serverDot'),
  connChips: $('connChips'),
  offline: $('offlineOverlay'),
  qrCamera: $('qrCamera'),
  entryPallet: $('entryPallet'),
  routeB1: $('routeB1'),
  routeCar: $('routeCar'),
  entryError: $('entryError'),
  queues: [$('queue1'), $('queue2')],
  labels: [$('label1'), $('label2')],
  exits: [$('exit1'), $('exit2')],
  laneStates: [$('lane1State'), $('lane2State')],
  bcdErrors: [$('bcd1Error'), $('bcd2Error')],
  carIcon: $('carIcon'),
  carPalletMini: $('carPalletMini'),
  carPallet: $('carPallet'),
  carStateText: $('carStateText'),
  carError: $('carError'),
  machinesTable: $('machinesTable').querySelector('tbody'),
  signalsGrid: $('signalsGrid'),
  toasts: $('toasts'),
  loginBtn: $('loginBtn'),
  logoutBtn: $('logoutBtn'),
  sessionInfo: $('sessionInfo'),
  palletDialog: $('palletDialog'),
  loginDialog: $('loginDialog'),
};

let currentStatus = null;

// ---------------------------------------------------------------------------
// Static DOM setup (chips, queue slots, signal LEDs)
// ---------------------------------------------------------------------------

const connChips = {};
for (const [key, label] of Object.entries(CONN_LABELS)) {
  const chip = document.createElement('span');
  chip.className = 'chip';
  chip.textContent = label;
  els.connChips.appendChild(chip);
  connChips[key] = chip;
}

const queueSlots = [[], []];
for (let pk = 0; pk < 2; pk++) {
  for (let i = 0; i < QUEUE_SLOTS; i++) {
    const slot = document.createElement('div');
    slot.className = 'slot';
    slot.addEventListener('click', () => onQueueSlotClick(pk + 1, i));
    els.queues[pk].appendChild(slot);
    queueSlots[pk].push(slot);
  }
}

const signalEls = SIGNAL_NAMES.map((name) => {
  const el = document.createElement('div');
  el.className = 'signal';
  el.textContent = name;
  els.signalsGrid.appendChild(el);
  return el;
});

// ---------------------------------------------------------------------------
// Rendering
// ---------------------------------------------------------------------------

function palletText(p) {
  if (!p) return '';
  const info = [];
  if (p.Id && p.Id !== '0') info.push(`Id: ${escapeHtml(p.Id)}`);
  if (p.Injector) info.push(`Maq: ${escapeHtml(p.Injector)}`);
  // The QR div is block-level: no <br> after it, or an empty line sneaks in.
  return `<div class="qr">${escapeHtml(p.Qr) || '(sin QR)'}</div>${info.join('<br>')}`;
}

function setBanner(el, message) {
  el.classList.toggle('empty', !message);
  el.textContent = message || '';
}

function renderSlot(el, pallet) {
  el.className = 'slot' + (pallet && pallet.Qr ? ' filled' : '');
  el.innerHTML = pallet && pallet.Qr ? palletText(pallet) : '';
}

function render(st) {
  currentStatus = st;

  // Connections
  for (const [key, chip] of Object.entries(connChips)) {
    chip.classList.toggle('on', !!st.Connections[key]);
  }

  renderEntry(st);
  renderLane(st, 0);
  renderLane(st, 1);
  renderCar(st);
  renderDiagnostics(st);

  setBanner(els.entryError, st.ErrorMessages.EntryError);
  setBanner(els.bcdErrors[0], st.ErrorMessages.BDC1Error);
  setBanner(els.bcdErrors[1], st.ErrorMessages.BDC2Error);
  setBanner(els.carError, st.ErrorMessages.CarError);
}

function renderEntry(st) {
  const s = st.MachineState.PalletEntry;
  const cam = els.qrCamera;

  cam.className = 'camera';
  if (s === PE.ReadingQrInError) cam.classList.add('bad', 'blink');
  else if (s === PE.ReadingQR || s === PE.Paused) cam.classList.add('ok', 'blink');
  else if (s !== undefined && s !== PE.Waiting) cam.classList.add('ok');

  const showPallet = st.EntryPallet && s !== PE.Waiting && s !== PE.UpdateCar && s !== PE.Paused;
  renderSlot(els.entryPallet, showPallet ? st.EntryPallet : null);

  els.routeB1.className = 'route';
  els.routeCar.className = 'route';
  if (s === PE.WaitEnterBocedi) els.routeB1.classList.add('blink');
  if (s === PE.WaitForBocedi1) els.routeB1.classList.add('on');
  if (s === PE.WaitForCar) els.routeCar.classList.add('blink');
  if (s === PE.WaitEnterCar) els.routeCar.classList.add('on');
  if (s === PE.Paused) { els.routeB1.classList.add('blink'); els.routeCar.classList.add('blink'); }
}

function renderLane(st, pk) {
  const packager = pk === 0 ? st.Packager1 : st.Packager2;
  if (!packager) return;
  const queue = packager.Queue || [];
  const correction = st.Signals[pk === 0 ? SIG.WaitCorrection1 : SIG.WaitCorrection2];

  queueSlots[pk].forEach((slot, i) => {
    const pallet = queue[i];
    slot.className = 'slot';
    if (pallet) {
      slot.classList.add('filled', 'clickable');
      // The last pallet in the queue is the one awaiting weight correction
      if (correction && i === queue.length - 1) slot.classList.add('correction');
      slot.innerHTML = palletText(pallet);
    } else {
      slot.innerHTML = '';
    }
  });

  renderSlot(els.labels[pk], packager.LabelPallet);
  renderSlot(els.exits[pk], packager.ExitPallet);

  const machine = pk === 0 ? 'PalletLabel1' : 'PalletLabel2';
  els.laneStates[pk].textContent = machineStateName(st, machine);
}

function renderCar(st) {
  if (!st.Car) return;
  const pos = st.Car.CarPosition;
  const car = els.carIcon;

  car.className = 'car';
  if (pos === 0) car.classList.add('unknown');
  else if (pos === 1 || pos === 3) car.classList.add('moving');

  car.style.left = { 0: '', 1: '40%', 2: '0.3rem', 3: '40%', 4: 'calc(100% - 5.3rem)' }[pos] || '';

  els.carStateText.textContent = CAR_POS_TEXT[pos] || '';
  els.carPalletMini.classList.toggle('hidden', !st.Car.HasPallet);
  renderSlot(els.carPallet, st.Car.HasPallet ? st.Car.Pallet : null);
}

function machineStateName(st, machine) {
  const idx = st.MachineState[machine];
  const names = st.States && st.States[machine];
  return (names && names[idx]) ?? (idx === undefined ? '' : String(idx));
}

function renderDiagnostics(st) {
  const rows = Object.keys(st.MachineState).map((m) =>
    `<tr><td>${escapeHtml(m)}</td><td>${escapeHtml(machineStateName(st, m))}</td></tr>`);
  els.machinesTable.innerHTML = rows.join('');

  signalEls.forEach((el, i) => el.classList.toggle('on', !!st.Signals[i]));
}

// ---------------------------------------------------------------------------
// SSE connection
// ---------------------------------------------------------------------------

function connect() {
  const source = new EventSource('/api/events');

  source.addEventListener('status', (ev) => {
    els.serverDot.classList.add('on');
    els.offline.classList.add('hidden');
    render(JSON.parse(ev.data));
  });

  source.onerror = () => {
    // EventSource retries on its own; just show the state.
    els.serverDot.classList.remove('on');
    els.offline.classList.remove('hidden');
    for (const chip of Object.values(connChips)) chip.classList.remove('on');
  };
}

connect();

// ---------------------------------------------------------------------------
// Session (supervisor PIN)
// ---------------------------------------------------------------------------

function getToken() { return sessionStorage.getItem('hmiToken'); }

function setToken(token) {
  if (token) sessionStorage.setItem('hmiToken', token);
  else sessionStorage.removeItem('hmiToken');
  const logged = !!token;
  els.loginBtn.classList.toggle('hidden', logged);
  els.logoutBtn.classList.toggle('hidden', !logged);
  els.sessionInfo.classList.toggle('hidden', !logged);
}

setToken(getToken());

let afterLogin = null;

els.loginBtn.addEventListener('click', () => openLogin());
els.logoutBtn.addEventListener('click', () => { setToken(null); toast('Sesión cerrada'); });

function openLogin(next) {
  afterLogin = next || null;
  $('pinInput').value = '';
  setDialogStatus($('loginStatus'), '');
  els.loginDialog.showModal();
  $('pinInput').focus();
}

$('loginCancelBtn').addEventListener('click', () => els.loginDialog.close());

$('loginForm').addEventListener('submit', async (ev) => {
  ev.preventDefault();
  const status = $('loginStatus');
  setDialogStatus(status, 'Verificando…');
  try {
    const res = await fetch('/api/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ pin: $('pinInput').value }),
    });
    if (!res.ok) {
      setDialogStatus(status, 'PIN incorrecto', 'err');
      return;
    }
    const { token } = await res.json();
    setToken(token);
    els.loginDialog.close();
    toast('Sesión de supervisor iniciada', 'ok');
    if (afterLogin) { const f = afterLogin; afterLogin = null; f(); }
  } catch {
    setDialogStatus(status, 'Error de conexión', 'err');
  }
});

// ---------------------------------------------------------------------------
// Pallet detail + delete
// ---------------------------------------------------------------------------

let dialogPallet = null; // { Pallet, Packager, Position } — same DTO the server expects

function onQueueSlotClick(packager, position) {
  if (!currentStatus) return;
  const pk = packager === 1 ? currentStatus.Packager1 : currentStatus.Packager2;
  const pallet = pk && pk.Queue && pk.Queue[position];
  if (!pallet) return;

  dialogPallet = { Pallet: pallet, Packager: packager, Position: position };

  $('dQr').textContent = pallet.Qr;
  $('dId').textContent = pallet.Id;
  $('dInjector').textContent = pallet.Injector;
  $('dRecipe').textContent = pallet.Recipe;
  $('dLabeling').textContent = pallet.Labeling ? 'Si' : 'No';
  $('dPosition').textContent = `Bocedi ${packager}, puesto ${position + 1}`;
  setDialogStatus($('dStatus'), '');
  $('dDeleteBtn').disabled = false;
  $('dCloseBtn').disabled = false;
  els.palletDialog.showModal();
}

$('dCloseBtn').addEventListener('click', () => els.palletDialog.close());

$('dDeleteBtn').addEventListener('click', () => {
  if (!getToken()) {
    els.palletDialog.close();
    openLogin(() => { els.palletDialog.showModal(); doDelete(); });
    return;
  }
  doDelete();
});

async function doDelete() {
  const status = $('dStatus');
  $('dDeleteBtn').disabled = true;
  $('dCloseBtn').disabled = true;
  setDialogStatus(status, 'Eliminando palet… puede tardar unos segundos.');
  try {
    const res = await fetch('/api/delete', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${getToken()}`,
      },
      body: JSON.stringify(dialogPallet),
    });
    if (res.status === 401) {
      setToken(null);
      setDialogStatus(status, 'Sesión expirada. Inicie sesión nuevamente.', 'err');
      $('dDeleteBtn').disabled = false;
      $('dCloseBtn').disabled = false;
      return;
    }
    const body = await res.json();
    if (res.ok && body.result === 'OK') {
      toast('Palet eliminado de la cola', 'ok');
      els.palletDialog.close();
    } else {
      setDialogStatus(status, body.error || 'No se pudo eliminar el palet', 'err');
      $('dDeleteBtn').disabled = false;
      $('dCloseBtn').disabled = false;
    }
  } catch {
    setDialogStatus(status, 'Error de conexión con el servidor', 'err');
    $('dDeleteBtn').disabled = false;
    $('dCloseBtn').disabled = false;
  }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function setDialogStatus(el, message, kind) {
  el.className = 'dialog-status' + (kind ? ` ${kind}` : '') + (message ? '' : ' hidden');
  el.textContent = message || '';
}

function toast(message, kind) {
  const el = document.createElement('div');
  el.className = 'toast' + (kind ? ` ${kind}` : '');
  el.textContent = message;
  els.toasts.appendChild(el);
  setTimeout(() => el.remove(), 4000);
}
