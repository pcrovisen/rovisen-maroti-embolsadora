// Diagnóstico — live state-machine graphs.
//
// Machines with declared transitions (transitions.json, generated from the
// C# sources by devserver/generate_transitions.mjs) are laid out as a flow:
// layers by distance from the initial state, all declared edges drawn faintly,
// observed edges highlighted, the latest transition animated, and observed
// transitions that are NOT declared shown in orange (either the dummy sim
// taking a shortcut, or a real discrepancy with the code). Machines without
// declared data fall back to a ring layout with observed edges only.

'use strict';

const SVGNS = 'http://www.w3.org/2000/svg';
const $ = (id) => document.getElementById(id);

const W = 360;
const machines = new Map();
let DECLARED = {};

function svgEl(tag, attrs) {
  const el = document.createElementNS(SVGNS, tag);
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  return el;
}

function findDeclared(name) {
  if (DECLARED[name]) return DECLARED[name];
  // Machine names are class name + optional identifier (e.g. PrinterMachineWolrdjet1)
  const key = Object.keys(DECLARED).find((k) => name.startsWith(k));
  return key ? DECLARED[key] : null;
}

// ---------------------------------------------------------------------------
// Layouts: node positions (+ layers for the flow layout)
// ---------------------------------------------------------------------------

function circleLayout(n) {
  const H = 300;
  const R = 96;
  const pos = [];
  for (let i = 0; i < n; i++) {
    const a = -Math.PI / 2 + (i * 2 * Math.PI) / n;
    pos.push({ x: W / 2 + R * Math.cos(a), y: H / 2 + R * Math.sin(a), angle: a });
  }
  return { H, pos, circle: true, edges: [] };
}

function layeredLayout(stateNames, decl) {
  const n = stateNames.length;
  const idx = (s) => stateNames.indexOf(s);
  const edges = [];
  const out = Array.from({ length: n }, () => []);
  for (const [a, b] of decl.edges) {
    const ia = idx(a);
    const ib = idx(b);
    if (ia < 0 || ib < 0 || ia === ib) continue;
    edges.push([ia, ib]);
    out[ia].push(ib);
  }

  // Layer = BFS distance from the initial state along declared edges.
  const layer = Array(n).fill(-1);
  const start = Math.max(0, idx(decl.init));
  layer[start] = 0;
  const queue = [start];
  while (queue.length) {
    const v = queue.shift();
    for (const w of out[v]) {
      if (layer[w] === -1) {
        layer[w] = layer[v] + 1;
        queue.push(w);
      }
    }
  }
  // States never reached via declared edges go to a bottom row.
  const maxLayer = Math.max(...layer);
  for (let i = 0; i < n; i++) if (layer[i] === -1) layer[i] = maxLayer + 1;

  const rows = new Map();
  for (let i = 0; i < n; i++) {
    if (!rows.has(layer[i])) rows.set(layer[i], []);
    rows.get(layer[i]).push(i);
  }

  const rowGap = 56;
  const topPad = 26;
  const numRows = Math.max(...layer) + 1;
  const H = topPad + (numRows - 1) * rowGap + 30;

  const pos = Array(n);
  for (const [l, members] of rows) {
    members.sort((a, b) => a - b);
    members.forEach((m, j) => {
      pos[m] = {
        x: (W * (j + 1)) / (members.length + 1),
        y: topPad + l * rowGap,
        slot: j,
        rowSize: members.length,
      };
    });
  }
  return { H, pos, layers: layer, edges, circle: false };
}

// ---------------------------------------------------------------------------
// Edge geometry
// ---------------------------------------------------------------------------

function edgeD(m, a, b) {
  const A = m.pos[a];
  const B = m.pos[b];
  let c;
  if (m.circle) {
    // Pull the control point toward the ring center.
    c = {
      x: ((A.x + B.x) / 2) * 0.62 + (W / 2) * 0.38,
      y: ((A.y + B.y) / 2) * 0.62 + (m.H / 2) * 0.38,
    };
  } else if (m.layers[b] > m.layers[a]) {
    // Forward edge: gentle curve perpendicular to the direction. Opposite
    // directions bend to opposite sides, so A→B and B→A never overlap.
    const dx = B.x - A.x;
    const dy = B.y - A.y;
    const len = Math.hypot(dx, dy) || 1;
    const bend = m.layers[b] - m.layers[a] > 1 ? 24 : 9;
    c = { x: (A.x + B.x) / 2 + (dy / len) * bend, y: (A.y + B.y) / 2 - (dx / len) * bend };
  } else {
    // Back edge (or same layer): arc out to the nearest side.
    const side = (A.x + B.x) / 2 <= W / 2 ? -1 : 1;
    const mag = 30 + 13 * Math.abs(m.layers[a] - m.layers[b]);
    c = { x: (A.x + B.x) / 2 + side * mag, y: (A.y + B.y) / 2 };
  }
  const trim = (p, q, d) => {
    const len = Math.hypot(q.x - p.x, q.y - p.y) || 1;
    return { x: p.x + ((q.x - p.x) / len) * d, y: p.y + ((q.y - p.y) / len) * d };
  };
  const s = trim(A, c, 9);
  const e = trim(B, c, 11);
  return `M ${s.x.toFixed(1)} ${s.y.toFixed(1)} Q ${c.x.toFixed(1)} ${c.y.toFixed(1)} ${e.x.toFixed(1)} ${e.y.toFixed(1)}`;
}

// ---------------------------------------------------------------------------
// Machine cards
// ---------------------------------------------------------------------------

function ensureMachine(name, statesDict) {
  let m = machines.get(name);
  if (m) return m;

  const stateNames = Object.keys(statesDict)
    .sort((a, b) => Number(a) - Number(b))
    .map((k) => statesDict[k]);
  const decl = findDeclared(name);
  const layout = decl && decl.edges.length
    ? layeredLayout(stateNames, decl)
    : circleLayout(stateNames.length);

  const card = document.createElement('div');
  card.className = 'machine-card';
  card.innerHTML = `
    <div class="machine-head">
      <h2>${escapeHtml(name)}</h2>
      <span class="machine-state"></span>
    </div>`;

  const svg = svgEl('svg', { viewBox: `0 0 ${W} ${layout.H}` });
  const edgeLayer = svgEl('g', {});
  const nodeLayer = svgEl('g', {});
  svg.append(edgeLayer, nodeLayer);
  card.appendChild(svg);
  $('machinesGrid').appendChild(card);

  m = {
    ...layout,
    edgeLayer,
    stateEl: card.querySelector('.machine-state'),
    stateNames,
    edgeEls: new Map(),
    activeEdge: null,
    current: null,
    since: Date.now(),
  };

  m.nodes = stateNames.map((sn, i) => {
    const p = layout.pos[i];
    const circle = svgEl('circle', { cx: p.x, cy: p.y, r: 7, class: 'st-node' });
    let lx;
    let ly;
    let anchor;
    if (layout.circle) {
      const cos = Math.cos(p.angle);
      const dist = stateNames.length > 9 ? 13 + (i % 2) * 15 : 13;
      lx = p.x + cos * dist;
      ly = p.y + Math.sin(p.angle) * dist;
      anchor = Math.abs(cos) < 0.35 ? 'middle' : cos > 0 ? 'start' : 'end';
    } else if (p.rowSize > 3) {
      // Crowded row: center the label and alternate above/below the node.
      anchor = 'middle';
      lx = p.x;
      ly = p.y + (p.slot % 2 ? 16 : -13);
    } else {
      anchor = p.x > W - 80 ? 'end' : 'start';
      lx = p.x + (anchor === 'start' ? 11 : -11);
      ly = p.y - 9;
    }
    const label = svgEl('text', {
      x: lx, y: ly, class: 'st-label',
      'dominant-baseline': 'middle', 'text-anchor': anchor,
    });
    label.textContent = sn.length > 17 ? `${sn.slice(0, 16)}…` : sn;
    const title = svgEl('title', {});
    title.textContent = sn;
    label.appendChild(title);
    nodeLayer.append(circle, label);
    return { circle, label };
  });

  // Draw every declared transition, faintly, from the start.
  for (const [a, b] of layout.edges) {
    const path = svgEl('path', { d: edgeD(m, a, b), class: 'st-edge declared' });
    m.edgeLayer.appendChild(path);
    m.edgeEls.set(`${a}>${b}`, path);
  }

  machines.set(name, m);
  return m;
}

function recordTransition(m, from, to) {
  if (!m.nodes[from] || !m.nodes[to] || from === to) return;
  const key = `${from}>${to}`;
  let edge = m.edgeEls.get(key);
  if (!edge) {
    // Observed but not declared: either the sim took a shortcut or the
    // machine did something the C# code doesn't declare. Shown in orange.
    edge = svgEl('path', {
      d: edgeD(m, from, to),
      class: m.circle ? 'st-edge' : 'st-edge unexpected',
    });
    m.edgeLayer.appendChild(edge);
    m.edgeEls.set(key, edge);
  }
  edge.classList.add('observed');
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
// Startup: load the declared transitions, then connect
// ---------------------------------------------------------------------------

fetch('transitions.json')
  .then((r) => (r.ok ? r.json() : {}))
  .catch(() => ({}))
  .then((data) => {
    DECLARED = data;
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
  });
