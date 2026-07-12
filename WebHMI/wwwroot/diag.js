// Diagnóstico — live state-machine graphs.
//
// Sidebar lists every machine with its live state; selecting one renders a
// large flow graph (layers by distance from the initial state, from
// transitions.json — generated from the C# sources). Edges are labeled with
// the FatekPLC signal that gates each transition (⏱ = time-based); labels
// light up green while their condition is currently satisfied. Declared
// edges are faint, observed ones brighten, the latest animates, and an
// observed-but-undeclared transition shows orange.

'use strict';

const SVGNS = 'http://www.w3.org/2000/svg';
const $ = (id) => document.getElementById(id);

const DIMS = {
  W: 780,
  rowGap: 80,
  topPad: 46,
  bottomPad: 40,
  nodeR: 10,
  circleH: 620,
  circleR: 240,
};

const metas = new Map(); // machine name -> persistent info
let selected = null;
let view = null; // DOM refs for the selected machine's graph
let lastStatus = null;

function svgEl(tag, attrs) {
  const el = document.createElementNS(SVGNS, tag);
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  return el;
}

let DECLARED = {};

function findDeclared(name) {
  if (DECLARED[name]) return DECLARED[name];
  // Machine names are class name + optional identifier (e.g. PrinterMachineWolrdjet1)
  const key = Object.keys(DECLARED).find((k) => name.startsWith(k));
  return key ? DECLARED[key] : null;
}

// ---------------------------------------------------------------------------
// Layouts
// ---------------------------------------------------------------------------

function circleLayout(n) {
  const H = DIMS.circleH;
  const pos = [];
  for (let i = 0; i < n; i++) {
    const a = -Math.PI / 2 + (i * 2 * Math.PI) / n;
    pos.push({
      x: DIMS.W / 2 + DIMS.circleR * Math.cos(a),
      y: H / 2 + DIMS.circleR * Math.sin(a),
      angle: a,
    });
  }
  return { H, pos, circle: true, edges: [] };
}

function layeredLayout(stateNames, decl) {
  const n = stateNames.length;
  const idx = (s) => stateNames.indexOf(s);
  const edges = [];
  const out = Array.from({ length: n }, () => []);
  for (const [a, b, gates] of decl.edges) {
    const ia = idx(a);
    const ib = idx(b);
    if (ia < 0 || ib < 0 || ia === ib) continue;
    edges.push([ia, ib, gates || []]);
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

  const numRows = Math.max(...layer) + 1;
  const H = DIMS.topPad + (numRows - 1) * DIMS.rowGap + DIMS.bottomPad;

  const pos = Array(n);
  for (const [l, members] of rows) {
    members.sort((a, b) => a - b);
    members.forEach((m, j) => {
      pos[m] = {
        x: (DIMS.W * (j + 1)) / (members.length + 1),
        y: DIMS.topPad + l * DIMS.rowGap,
        slot: j,
        rowSize: members.length,
      };
    });
  }
  return { H, pos, layers: layer, edges, circle: false };
}

// ---------------------------------------------------------------------------
// Edge geometry: path + label anchor (quadratic midpoint)
// ---------------------------------------------------------------------------

function edgeGeom(layout, a, b) {
  const A = layout.pos[a];
  const B = layout.pos[b];
  let c;
  if (layout.circle) {
    c = {
      x: ((A.x + B.x) / 2) * 0.62 + (DIMS.W / 2) * 0.38,
      y: ((A.y + B.y) / 2) * 0.62 + (layout.H / 2) * 0.38,
    };
  } else if (layout.layers[b] > layout.layers[a]) {
    // Forward edge: gentle curve perpendicular to the direction. Opposite
    // directions bend to opposite sides, so A→B and B→A never overlap.
    const dx = B.x - A.x;
    const dy = B.y - A.y;
    const len = Math.hypot(dx, dy) || 1;
    const bend = layout.layers[b] - layout.layers[a] > 1 ? 46 : 18;
    c = { x: (A.x + B.x) / 2 + (dy / len) * bend, y: (A.y + B.y) / 2 - (dx / len) * bend };
  } else {
    // Back edge (or same layer): arc out to the nearest side.
    const side = (A.x + B.x) / 2 <= DIMS.W / 2 ? -1 : 1;
    const mag = Math.min(60 + 26 * Math.abs(layout.layers[a] - layout.layers[b]), 190);
    c = { x: (A.x + B.x) / 2 + side * mag, y: (A.y + B.y) / 2 };
  }
  const trim = (p, q, d) => {
    const len = Math.hypot(q.x - p.x, q.y - p.y) || 1;
    return { x: p.x + ((q.x - p.x) / len) * d, y: p.y + ((q.y - p.y) / len) * d };
  };
  const s = trim(A, c, DIMS.nodeR + 2);
  const e = trim(B, c, DIMS.nodeR + 4);
  return {
    d: `M ${s.x.toFixed(1)} ${s.y.toFixed(1)} Q ${c.x.toFixed(1)} ${c.y.toFixed(1)} ${e.x.toFixed(1)} ${e.y.toFixed(1)}`,
    mid: { x: 0.25 * A.x + 0.5 * c.x + 0.25 * B.x, y: 0.25 * A.y + 0.5 * c.y + 0.25 * B.y },
  };
}

// ---------------------------------------------------------------------------
// Sidebar
// ---------------------------------------------------------------------------

function ensureMeta(name, statesDict) {
  let m = metas.get(name);
  if (m) return m;

  const stateNames = Object.keys(statesDict)
    .sort((a, b) => Number(a) - Number(b))
    .map((k) => statesDict[k]);

  const item = document.createElement('div');
  item.className = 'machine-item';
  item.innerHTML = `<span class="mi-name">${escapeHtml(name)}</span><span class="mi-state"></span>`;
  item.addEventListener('click', () => selectMachine(name));
  $('machineList').appendChild(item);

  m = {
    stateNames,
    decl: findDeclared(name),
    current: null,
    since: Date.now(),
    observed: new Set(),
    item,
    itemState: item.querySelector('.mi-state'),
  };
  metas.set(name, m);
  if (!selected) selectMachine(name);
  return m;
}

// ---------------------------------------------------------------------------
// Big graph view
// ---------------------------------------------------------------------------

function gateTspans(label, gates, geomMid) {
  gates.forEach((token, i) => {
    const tspan = svgEl('tspan', {});
    if (token === '⏱') {
      tspan.setAttribute('class', 'sig timer');
    } else {
      const neg = token.startsWith('!');
      const sigName = neg ? token.slice(1) : token;
      tspan.setAttribute('class', 'sig');
      tspan.setAttribute('data-sig', sigName);
      if (neg) tspan.setAttribute('data-neg', '1');
    }
    tspan.textContent = (i ? ' · ' : '') + token;
    label.appendChild(tspan);
  });
  label.setAttribute('x', geomMid.x);
  label.setAttribute('y', geomMid.y - 4);
}

function selectMachine(name) {
  selected = name;
  for (const [n, m] of metas) m.item.classList.toggle('selected', n === name);

  const meta = metas.get(name);
  const layout = meta.decl && meta.decl.edges.length
    ? layeredLayout(meta.stateNames, meta.decl)
    : circleLayout(meta.stateNames.length);

  $('mvName').textContent = name;

  const svg = svgEl('svg', { viewBox: `0 0 ${DIMS.W} ${layout.H}` });
  const edgeLayer = svgEl('g', {});
  const labelLayer = svgEl('g', {});
  const nodeLayer = svgEl('g', {});
  svg.append(edgeLayer, labelLayer, nodeLayer);

  const nodes = meta.stateNames.map((sn, i) => {
    const p = layout.pos[i];
    const circle = svgEl('circle', { cx: p.x, cy: p.y, r: DIMS.nodeR, class: 'st-node' });
    let lx;
    let ly;
    let anchor;
    if (layout.circle) {
      const cos = Math.cos(p.angle);
      lx = p.x + cos * (DIMS.nodeR + 9);
      ly = p.y + Math.sin(p.angle) * (DIMS.nodeR + 9);
      anchor = Math.abs(cos) < 0.35 ? 'middle' : cos > 0 ? 'start' : 'end';
    } else if (p.rowSize > 3) {
      anchor = 'middle';
      lx = p.x;
      ly = p.y + (p.slot % 2 ? DIMS.nodeR + 14 : -(DIMS.nodeR + 8));
    } else {
      anchor = p.x > DIMS.W - 130 ? 'end' : 'start';
      lx = p.x + (anchor === 'start' ? DIMS.nodeR + 6 : -(DIMS.nodeR + 6));
      ly = p.y - DIMS.nodeR - 6;
    }
    const label = svgEl('text', {
      x: lx, y: ly, class: 'st-label',
      'dominant-baseline': 'middle', 'text-anchor': anchor,
    });
    label.textContent = sn;
    nodeLayer.append(circle, label);
    return { circle, label };
  });

  const edgeEls = new Map();
  for (const [a, b, gates] of layout.edges) {
    const geom = edgeGeom(layout, a, b);
    const path = svgEl('path', { d: geom.d, class: 'st-edge declared' });
    if (meta.observed.has(`${a}>${b}`)) path.classList.add('observed');
    edgeLayer.appendChild(path);
    edgeEls.set(`${a}>${b}`, path);
    if (gates && gates.length) {
      const label = svgEl('text', { class: 'edge-label', 'text-anchor': 'middle' });
      gateTspans(label, gates, geom.mid);
      labelLayer.appendChild(label);
    }
  }
  // Observed-but-undeclared edges from before this view was (re)built.
  for (const key of meta.observed) {
    if (edgeEls.has(key)) continue;
    const [a, b] = key.split('>').map(Number);
    const path = svgEl('path', {
      d: edgeGeom(layout, a, b).d,
      class: layout.circle ? 'st-edge observed' : 'st-edge unexpected observed',
    });
    edgeLayer.appendChild(path);
    edgeEls.set(key, path);
  }

  if (meta.current !== null) {
    nodes[meta.current]?.circle.classList.add('current');
    nodes[meta.current]?.label.classList.add('current');
  }

  // Signals panel: every signal referenced by this machine's transitions.
  const sigNames = [...new Set(
    layout.edges.flatMap(([, , g]) => (g || [])
      .filter((tk) => tk !== '⏱')
      .map((tk) => (tk.startsWith('!') ? tk.slice(1) : tk))),
  )];
  $('mvSignals').innerHTML = '';
  const sigChips = sigNames.map((sn) => {
    const el = document.createElement('div');
    el.className = 'signal';
    el.textContent = sn;
    if (!(sn in SIG)) {
      el.classList.add('unknown');
      el.title = 'Señal escrita por el PC (coils 1–20): sin estado visible';
    }
    $('mvSignals').appendChild(el);
    return { el, name: sn };
  });
  if (!sigNames.length) {
    $('mvSignals').innerHTML = '<span class="mv-nosignals">Esta máquina no depende de señales del PLC.</span>';
  }

  $('mvGraph').innerHTML = '';
  $('mvGraph').appendChild(svg);
  view = { name, layout, nodes, edgeEls, edgeLayer, sigChips, activeEdge: null };
  if (lastStatus) refreshView(lastStatus);
}

function refreshView(st) {
  if (!view) return;
  // Live coloring: edge-gate labels and the signal chips.
  for (const tspan of $('mvGraph').querySelectorAll('.edge-label .sig[data-sig]')) {
    const sigName = tspan.getAttribute('data-sig');
    if (!(sigName in SIG)) continue;
    const value = !!st.Signals[SIG[sigName]];
    const satisfied = tspan.hasAttribute('data-neg') ? !value : value;
    tspan.classList.toggle('on', satisfied);
  }
  for (const { el, name } of view.sigChips) {
    if (name in SIG) el.classList.toggle('on', !!st.Signals[SIG[name]]);
  }
}

// ---------------------------------------------------------------------------
// Transitions
// ---------------------------------------------------------------------------

function recordTransition(name, meta, from, to) {
  const key = `${from}>${to}`;
  meta.observed.add(key);
  if (selected !== name || !view) return;
  let edge = view.edgeEls.get(key);
  if (!edge) {
    edge = svgEl('path', {
      d: edgeGeom(view.layout, from, to).d,
      class: view.layout.circle ? 'st-edge' : 'st-edge unexpected',
    });
    view.edgeLayer.appendChild(edge);
    view.edgeEls.set(key, edge);
  }
  edge.classList.add('observed');
  if (view.activeEdge && view.activeEdge !== edge) view.activeEdge.classList.remove('active');
  edge.classList.add('active');
  view.activeEdge = edge;
  clearTimeout(edge._timer);
  edge._timer = setTimeout(() => edge.classList.remove('active'), 4000);
}

function setMachineState(name, meta, idx) {
  if (meta.current === idx) return;
  if (meta.current !== null) {
    recordTransition(name, meta, meta.current, idx);
    if (selected === name && view) {
      view.nodes[meta.current]?.circle.classList.remove('current');
      view.nodes[meta.current]?.label.classList.remove('current');
    }
  }
  if (selected === name && view) {
    view.nodes[idx]?.circle.classList.add('current');
    view.nodes[idx]?.label.classList.add('current');
  }
  meta.current = idx;
  meta.since = Date.now();
}

function refreshStateTexts() {
  for (const [name, m] of metas) {
    if (m.current === null) continue;
    const text = `${m.stateNames[m.current] ?? m.current} · ${Math.floor((Date.now() - m.since) / 1000)} s`;
    m.itemState.textContent = text;
    if (selected === name) $('mvState').textContent = text;
  }
}
setInterval(refreshStateTexts, 1000);

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
      lastStatus = st;

      for (const [name, idx] of Object.entries(st.MachineState)) {
        const statesDict = st.States && st.States[name];
        if (!statesDict) continue;
        setMachineState(name, ensureMeta(name, statesDict), idx);
      }
      refreshStateTexts();
      refreshView(st);
    });

    source.onerror = () => {
      $('serverDot').classList.remove('on');
      $('offlineOverlay').classList.remove('hidden');
    };
  });
