// Diagnóstico — live state-machine graphs.
//
// Each machine in the status JSON becomes a card with its states laid out
// on a ring. The current state is highlighted; transitions are learned by
// observation (an arrow appears the first time a from→to change is seen)
// and the most recent transition is animated.

'use strict';

const SVGNS = 'http://www.w3.org/2000/svg';
const $ = (id) => document.getElementById(id);

const W = 360;
const H = 300;
const CX = W / 2;
const CY = H / 2;
const R = 96;

const machines = new Map();

// ---------------------------------------------------------------------------
// Machine cards
// ---------------------------------------------------------------------------

function polar(i, n) {
  const a = -Math.PI / 2 + (i * 2 * Math.PI) / n;
  return { x: CX + R * Math.cos(a), y: CY + R * Math.sin(a), a };
}

function svgEl(tag, attrs) {
  const el = document.createElementNS(SVGNS, tag);
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  return el;
}

function ensureMachine(name, statesDict) {
  let m = machines.get(name);
  if (m) return m;

  const stateNames = Object.keys(statesDict)
    .sort((a, b) => Number(a) - Number(b))
    .map((k) => statesDict[k]);
  const n = stateNames.length;

  const card = document.createElement('div');
  card.className = 'machine-card';
  card.innerHTML = `
    <div class="machine-head">
      <h2>${escapeHtml(name)}</h2>
      <span class="machine-state"></span>
    </div>`;

  const svg = svgEl('svg', { viewBox: `0 0 ${W} ${H}` });
  const edgeLayer = svgEl('g', {});
  const nodeLayer = svgEl('g', {});
  svg.append(edgeLayer, nodeLayer);

  const nodes = stateNames.map((sn, i) => {
    const { x, y, a } = polar(i, n);
    const circle = svgEl('circle', { cx: x, cy: y, r: 7, class: 'st-node' });
    const cos = Math.cos(a);
    // Crowded rings: alternate the label distance so neighbors don't overlap.
    const dist = n > 9 ? 13 + (i % 2) * 15 : 13;
    const label = svgEl('text', {
      x: x + cos * dist,
      y: y + Math.sin(a) * dist,
      class: 'st-label',
      'dominant-baseline': 'middle',
      'text-anchor': Math.abs(cos) < 0.35 ? 'middle' : (cos > 0 ? 'start' : 'end'),
    });
    label.textContent = sn.length > 16 ? `${sn.slice(0, 15)}…` : sn;
    const title = svgEl('title', {});
    title.textContent = sn;
    label.appendChild(title);
    nodeLayer.append(circle, label);
    return { x, y, circle, label };
  });

  card.appendChild(svg);
  $('machinesGrid').appendChild(card);

  m = {
    nodes,
    edgeLayer,
    stateEl: card.querySelector('.machine-state'),
    stateNames,
    edges: new Map(),
    activeEdge: null,
    current: null,
    since: Date.now(),
  };
  machines.set(name, m);
  return m;
}

function edgePath(m, a, b) {
  const A = m.nodes[a];
  const B = m.nodes[b];
  // Curve the edge by pulling its control point toward the ring center,
  // then trim both ends so they stop at the node border.
  const c = {
    x: (A.x + B.x) / 2 * 0.62 + CX * 0.38,
    y: (A.y + B.y) / 2 * 0.62 + CY * 0.38,
  };
  const trim = (p, q, d) => {
    const len = Math.hypot(q.x - p.x, q.y - p.y) || 1;
    return { x: p.x + ((q.x - p.x) / len) * d, y: p.y + ((q.y - p.y) / len) * d };
  };
  const start = trim(A, c, 9);
  const end = trim(B, c, 11);
  return `M ${start.x.toFixed(1)} ${start.y.toFixed(1)} Q ${c.x.toFixed(1)} ${c.y.toFixed(1)} ${end.x.toFixed(1)} ${end.y.toFixed(1)}`;
}

function recordTransition(m, from, to) {
  if (!m.nodes[from] || !m.nodes[to] || from === to) return;
  const key = `${from}>${to}`;
  let edge = m.edges.get(key);
  if (!edge) {
    edge = svgEl('path', { d: edgePath(m, from, to), class: 'st-edge' });
    m.edgeLayer.appendChild(edge);
    m.edges.set(key, edge);
  }
  if (m.activeEdge && m.activeEdge !== edge) m.activeEdge.classList.remove('active');
  edge.classList.add('active');
  m.activeEdge = edge;
  clearTimeout(edge._timer);
  edge._timer = setTimeout(() => edge.classList.remove('active'), 4000);
}

function setMachineState(m, idx) {
  if (m.current === idx) return;
  if (m.current !== null) {
    m.nodes[m.current]?.circle.classList.remove('current');
    m.nodes[m.current]?.label.classList.remove('current');
    recordTransition(m, m.current, idx);
  }
  m.nodes[idx]?.circle.classList.add('current');
  m.nodes[idx]?.label.classList.add('current');
  m.current = idx;
  m.since = Date.now();
}

function refreshStateTexts() {
  for (const m of machines.values()) {
    if (m.current === null) continue;
    const secs = Math.floor((Date.now() - m.since) / 1000);
    m.stateEl.textContent = `${m.stateNames[m.current] ?? m.current} · ${secs} s`;
  }
}
setInterval(refreshStateTexts, 1000);

// ---------------------------------------------------------------------------
// Signals
// ---------------------------------------------------------------------------

const signalEls = SIGNAL_NAMES.map((name) => {
  const el = document.createElement('div');
  el.className = 'signal';
  el.textContent = name;
  $('signalsGrid').appendChild(el);
  return el;
});

// ---------------------------------------------------------------------------
// SSE
// ---------------------------------------------------------------------------

const source = new EventSource('/api/events');

source.addEventListener('status', (ev) => {
  $('serverDot').classList.add('on');
  $('offlineOverlay').classList.add('hidden');
  const st = JSON.parse(ev.data);

  for (const [name, idx] of Object.entries(st.MachineState)) {
    const statesDict = st.States && st.States[name];
    if (!statesDict) continue;
    setMachineState(ensureMachine(name, statesDict), idx);
  }
  refreshStateTexts();

  signalEls.forEach((el, i) => el.classList.toggle('on', !!st.Signals[i]));
});

source.onerror = () => {
  $('serverDot').classList.remove('on');
  $('offlineOverlay').classList.remove('hidden');
};
