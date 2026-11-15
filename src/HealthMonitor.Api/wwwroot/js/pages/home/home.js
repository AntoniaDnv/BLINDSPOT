// Daily Readiness & Vitals: latest readings vs limits, personal baseline, sleep chart, trend.
// Data: /api/readings/latest, /api/readings (history), /api/vitals/indicators (limits).
import { api } from "../../api.js";
import { getActiveCrewId } from "../../session.js";
import { themeColors, withAlpha } from "../../charts.js";
import { escapeHtml, formatDateTime, renderErrorState } from "../../utils.js";

const PANELS = [
  { id: "cardio", title: "Cardiovascular", sub: "Heart rate & blood pressure", icon: "favorite", keys: ["heartRate", "systolicBp", "diastolicBp"] },
  { id: "oxy", title: "Oxygen & Temperature", sub: "Blood oxygen & body temperature", icon: "air", keys: ["spo2", "bodyTemp"] },
  { id: "sleep", title: "Sleep & Rest", sub: "Nightly duration & 7-day pattern", icon: "bedtime", keys: ["sleepHours"] },
  { id: "mood", title: "Subjective Well-being", sub: "Crew self-reported mood", icon: "sentiment_satisfied", keys: ["mood"] },
];
const OTHER = { id: "other", title: "Other", sub: "Additional indicators", icon: "monitor_heart", keys: [] };
const STATUS = {
  ok: { label: "Nominal", chip: "chip--ok", tone: "ok", rank: 0 },
  warning: { label: "Warning", chip: "chip--warning", tone: "warning", rank: 1 },
  critical: { label: "Critical", chip: "chip--critical", tone: "critical", rank: 2 },
};
const MOOD_WORDS = ["", "Very low", "Low", "Neutral", "Good", "Very good"];

let token = 0;
let charts = [];
let state = null;

const statusOf = (s) => STATUS[s] ?? STATUS.ok;
const shortDate = (iso) => new Date(iso).toLocaleDateString(undefined, { month: "short", day: "numeric" });
const fmt = (n) => (Number.isInteger(n) ? String(n) : String(Math.round(n * 10) / 10));

export async function init(root) {
  const my = ++token;
  const body = root.querySelector("#hv-body");
  const crewId = getActiveCrewId();
  renderSkeleton(body);
  try {
    const q = `?crewId=${encodeURIComponent(crewId ?? "")}`;
    const [latest, history, defs] = await Promise.all([
      api.get(`/api/readings/latest${q}`),
      api.get(`/api/readings${q}`),
      api.get("/api/vitals/indicators"),
    ]);
    if (my !== token) return;
    state = { root, body, crewId, latest, history, defs };
    render();
  } catch (error) {
    if (my !== token) return;
    root.querySelector("#hv-meta")?.classList.add("hidden");
    renderErrorState(body, { title: "Could not load vitals", error });
  }
}

export function destroy() {
  token++;
  charts.forEach((c) => c.destroy());
  charts = [];
  state = null;
}

function renderSkeleton(body) {
  body.innerHTML = `
    <div class="grid-2" role="status" aria-busy="true">
      <span class="visually-hidden">Loading vitals</span>
      ${[0, 1, 2, 3].map(() => `<div class="panel"><span class="skeleton skeleton--title"></span><div class="grid-2"><span class="skeleton skeleton--block"></span><span class="skeleton skeleton--block"></span></div></div>`).join("")}
    </div>`;
}

function worst(statuses) {
  return statuses.reduce((w, s) => (statusOf(s).rank > statusOf(w).rank ? s : w), "ok");
}

function render() {
  const { root, body, latest, history } = state;
  const metaEl = root.querySelector("#hv-meta");
  const readiness = root.querySelector("#hv-readiness");
  const simChip = root.querySelector("#hv-sim");

  if (latest.length === 0) {
    readiness.className = "chip chip--muted";
    readiness.textContent = "No readings yet";
    metaEl.replaceChildren();
    metaEl.classList.add("hidden");
    simChip.classList.add("hidden");
    body.innerHTML = `
      <div class="state state--empty">
        <span class="state__icon" aria-hidden="true"><span class="icon material-symbols-outlined">monitor_heart</span></span>
        <div class="state__body">
          <p class="state__title">No vitals logged yet</p>
          <p class="state__text">There are no readings for ${escapeHtml(state.crewId ?? "this crew member")} yet. Log a first set of vital signs and this page will show ranges, personal baselines and trends.</p>
        </div>
        <div class="state__actions"><a class="btn btn--primary" href="#/log">
          <span class="icon material-symbols-outlined" aria-hidden="true">edit_note</span>Go to Log &amp; Data Entry</a></div>
      </div>`;
    return;
  }

  // Header: readiness, simulated flag, meta.
  const bad = latest.filter((l) => l.status !== "ok").length;
  const crit = latest.filter((l) => l.status === "critical").length;
  if (crit) {
    readiness.className = "chip chip--critical";
    readiness.textContent = `${crit} critical`;
  } else if (bad) {
    readiness.className = "chip chip--warning";
    readiness.textContent = `${bad} need${bad === 1 ? "s" : ""} attention`;
  } else {
    readiness.className = "chip chip--ok";
    readiness.textContent = "All vitals nominal";
  }
  metaEl.classList.remove("hidden");
  simChip.classList.toggle("hidden", !latest.some((l) => l.isSimulated));
  const newest = latest.reduce((a, b) => (new Date(b.recordedAt) > new Date(a.recordedAt) ? b : a));
  metaEl.innerHTML = `
    <div class="page-header__meta-item"><span class="label">Last reading</span><span class="page-header__meta-value">${escapeHtml(formatDateTime(newest.recordedAt))}</span></div>
    <div class="page-header__meta-item"><span class="label">Readings this week</span><span class="page-header__meta-value">${history.length}</span></div>`;

  // Panels (unknown indicators go to an extra "Other" panel).
  const known = new Set(PANELS.flatMap((p) => p.keys));
  const panels = PANELS.map((p) => ({ ...p, items: p.keys.map((k) => latest.find((l) => l.indicator === k)).filter(Boolean) }));
  const extra = latest.filter((l) => !known.has(l.indicator));
  if (extra.length) panels.push({ ...OTHER, items: extra });

  body.innerHTML = `
    <div class="hv-stack">
      <div class="grid-2 hv-grid">
        ${panels.filter((p) => p.items.length).map(panelHtml).join("")}
      </div>
      <section class="panel" aria-labelledby="hv-trend-title">
        <div class="panel__header">
          <span class="panel__icon"><span class="icon material-symbols-outlined" aria-hidden="true">show_chart</span></span>
          <div class="panel__heading">
            <h2 class="panel__title" id="hv-trend-title">Trend</h2>
            <p class="panel__subtitle">Past week, shaded band = normal range</p>
          </div>
        </div>
        <div class="hv-picker cluster" id="hv-picker" role="group" aria-label="Choose indicator">
          ${latest.map((l, i) => `<button type="button" class="tab${i === 0 ? " is-active" : ""}" data-key="${escapeHtml(l.indicator)}" aria-pressed="${i === 0}">${escapeHtml(l.name)}</button>`).join("")}
        </div>
        <div class="hv-chart hv-chart--trend"><canvas id="hv-trend" role="img" aria-label="Trend chart of the selected indicator over the past week"></canvas></div>
      </section>
      <p class="muted hv-foot">Limits: American Heart Association, MedlinePlus and studies listed in docs/research.md. Readings are simulated.</p>
    </div>`;

  drawSleep();
  const picker = body.querySelector("#hv-picker");
  picker.addEventListener("click", (e) => {
    const btn = e.target.closest("button[data-key]");
    if (!btn) return;
    picker.querySelectorAll("button").forEach((b) => {
      const on = b === btn;
      b.classList.toggle("is-active", on);
      b.setAttribute("aria-pressed", String(on));
    });
    drawTrend(btn.dataset.key);
  });
  drawTrend(latest[0].indicator);
}

function panelHtml(p) {
  const st = statusOf(worst(p.items.map((i) => i.status)));
  return `
    <section class="panel" aria-label="${escapeHtml(p.title)}">
      <div class="panel__header">
        <span class="panel__icon"><span class="icon material-symbols-outlined" aria-hidden="true">${p.icon}</span></span>
        <div class="panel__heading"><h2 class="panel__title">${escapeHtml(p.title)}</h2><p class="panel__subtitle">${escapeHtml(p.sub)}</p></div>
        <div class="panel__aside"><span class="chip ${st.chip}">${st.label}</span></div>
      </div>
      <div class="panel__body">
        <div class="hv-tiles">${p.items.map(tileHtml).join("")}</div>
        ${p.id === "sleep" ? sleepCardHtml() : ""}
      </div>
    </section>`;
}

function baselineOf(item) {
  const earlier = state.history.filter(
    (h) => h.indicator === item.indicator && h.id !== item.id && new Date(h.recordedAt) <= new Date(item.recordedAt),
  );
  if (!earlier.length) return null;
  return earlier.reduce((s, h) => s + h.value, 0) / earlier.length;
}

function baselineHtml(item) {
  const base = baselineOf(item);
  if (base === null) return `<p class="hv-base"><span class="hv-base__note">No earlier readings to compare yet.</span></p>`;
  const diff = item.value - base;
  const pct = base !== 0 ? (diff / base) * 100 : 0;
  const flat = Math.abs(diff) < 0.05;
  const icon = flat ? "trending_flat" : diff > 0 ? "trending_up" : "trending_down";
  const sign = flat ? "" : diff > 0 ? "+" : "-";
  const unit = item.unit === "score" ? "" : ` ${item.unit}`;
  return `
    <p class="hv-base">
      <span class="label label--sm">vs personal baseline</span>
      <span class="hv-delta tabular"><span class="icon icon--sm material-symbols-outlined" aria-hidden="true">${icon}</span>${sign}${fmt(Math.abs(diff))}${escapeHtml(unit)} (${sign}${fmt(Math.abs(pct))}%)</span>
      <span class="hv-base__note">baseline = average of previous readings this week (${fmt(base)})</span>
    </p>`;
}

function rangeHtml(item, def) {
  if (!def) return "";
  const lo = def.warnMin;
  const hi = def.warnMax;
  const pad = (hi - lo) * 0.08 || 1;
  const min = lo - pad;
  const max = hi + pad;
  const pos = (v) => `${Math.max(0, Math.min(100, ((v - min) / (max - min)) * 100)).toFixed(1)}%`;
  const tone = statusOf(item.status).tone;
  return `
    <div class="range-bar range-bar--${tone}" role="img" aria-label="${escapeHtml(item.name)} ${fmt(item.value)} ${escapeHtml(item.unit)}; normal range ${fmt(def.okMin)} to ${fmt(def.okMax)}">
      <div class="range-bar__track">
        <span class="range-bar__band" style="--from:${pos(def.okMin)};--to:${pos(def.okMax)}"></span>
        <span class="range-bar__marker" style="--value:${pos(item.value)}"></span>
      </div>
      <div class="range-bar__labels"><span>${fmt(lo)}</span><span class="range-bar__value">Normal ${fmt(def.okMin)}-${fmt(def.okMax)}</span><span>${fmt(hi)}</span></div>
    </div>`;
}

function tileHtml(item) {
  const def = state.defs.find((d) => d.indicator === item.indicator);
  const st = statusOf(item.status);
  const guidance = item.guidance?.[item.status] ?? "";
  const unit = item.unit === "score" ? "/ 5" : item.unit;
  const isMood = item.indicator === "mood";
  const mood = isMood
    ? `<p class="tile__note hv-mood">${escapeHtml(MOOD_WORDS[Math.round(item.value)] ?? "")}</p>${moodScale(item.value)}`
    : "";
  return `
    <article class="tile hv-tile">
      <div class="hv-tile__top"><h3 class="tile__label">${escapeHtml(item.name)}</h3><span class="chip ${st.chip}">${st.label}</span></div>
      <p class="metric metric--${st.tone}"><span class="metric__value">${escapeHtml(fmt(item.value))}</span> <span class="metric__unit">${escapeHtml(unit)}</span></p>
      ${mood}
      ${isMood ? "" : rangeHtml(item, def)}
      ${baselineHtml(item)}
      <p class="tile__note">${escapeHtml(guidance)}</p>
      <p class="label label--sm hv-risk">${escapeHtml(item.nasaRisk)}</p>
    </article>`;
}

function moodScale(value) {
  const v = Math.round(value);
  return `<div class="hv-mood-scale" aria-hidden="true">${[1, 2, 3, 4, 5].map((n) => `<span class="${n <= v ? "is-on" : ""}${n === v ? " is-current" : ""}"></span>`).join("")}</div>`;
}

function sleepCardHtml() {
  const def = state.defs.find((d) => d.indicator === "sleepHours");
  return `
    <div class="tile hv-sleepcard">
      <div class="hv-tile__top">
        <h3 class="tile__label">7-day sleep duration</h3>
        ${def ? `<span class="hv-target">Target &ge; ${fmt(def.okMin)} h</span>` : ""}
      </div>
      <div class="hv-chart hv-chart--sleep"><canvas id="hv-sleep" role="img" aria-label="Bar chart of sleep hours for the last 7 days"></canvas></div>
    </div>`;
}

function drawSleep() {
  const canvas = state.body.querySelector("#hv-sleep");
  if (!canvas || !window.Chart) return;
  const def = state.defs.find((d) => d.indicator === "sleepHours");
  const c = themeColors();
  // One bar per day (last reading of that day), most recent 7 days.
  const byDay = new Map();
  state.history
    .filter((h) => h.indicator === "sleepHours")
    .sort((a, b) => new Date(a.recordedAt) - new Date(b.recordedAt))
    .forEach((h) => byDay.set(h.recordedAt.slice(0, 10), h));
  const days = [...byDay.values()].slice(-7);
  const last = days.length - 1;
  const target = def?.okMin;
  const valueLabels = {
    id: "hvValueLabels",
    afterDatasetsDraw(chart) {
      const { ctx } = chart;
      ctx.save();
      ctx.font = '600 11px "Inter", sans-serif';
      ctx.textAlign = "center";
      chart.getDatasetMeta(0).data.forEach((bar, i) => {
        ctx.fillStyle = i === last ? c.primaryLight : c.textMuted;
        ctx.fillText(`${fmt(days[i].value)}h`, bar.x, bar.y - 6);
      });
      ctx.restore();
    },
  };
  const datasets = [{
    type: "bar",
    label: "Sleep (h)",
    data: days.map((d) => d.value),
    backgroundColor: days.map((_, i) => (i === last ? c.primaryLight : withAlpha(c.textMuted, 0.22))),
    borderRadius: 3,
    maxBarThickness: 44,
    order: 2,
  }];
  if (target !== undefined) {
    datasets.push({
      type: "line",
      label: `Target >= ${fmt(target)} h`,
      data: days.map(() => target),
      borderColor: c.ok,
      borderDash: [6, 4],
      borderWidth: 1.5,
      pointRadius: 0,
      pointHoverRadius: 0,
      order: 1,
    });
  }
  charts.push(new window.Chart(canvas, {
    type: "bar",
    data: { labels: days.map((d, i) => (i === last ? "Latest" : shortDate(d.recordedAt))), datasets },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      layout: { padding: { top: 18 } },
      plugins: { legend: { display: false }, tooltip: { filter: (i) => i.datasetIndex === 0 } },
      scales: {
        y: { beginAtZero: true, suggestedMax: Math.max(10, ...days.map((d) => d.value + 1)), grid: { color: c.grid }, ticks: { callback: (v) => `${v}h` } },
        x: { grid: { display: false } },
      },
    },
    plugins: [valueLabels],
  }));
}

function drawTrend(key) {
  const canvas = state.body.querySelector("#hv-trend");
  if (!canvas || !window.Chart) return;
  charts = charts.filter((ch) => {
    if (ch.canvas === canvas) {
      ch.destroy();
      return false;
    }
    return true;
  });
  const c = themeColors();
  const def = state.defs.find((d) => d.indicator === key);
  const meta = state.latest.find((l) => l.indicator === key);
  const pts = state.history
    .filter((h) => h.indicator === key)
    .sort((a, b) => new Date(a.recordedAt) - new Date(b.recordedAt));
  const color = (s) => ({ ok: c.ok, warning: c.warning, critical: c.critical })[s] ?? c.primary;
  const datasets = [];
  if (def) {
    const dashed = { borderColor: withAlpha(c.ok, 0.5), borderDash: [4, 4], borderWidth: 1, pointRadius: 0, pointHoverRadius: 0 };
    datasets.push({ label: "Normal min", data: pts.map(() => def.okMin), fill: false, ...dashed });
    datasets.push({ label: "Normal max", data: pts.map(() => def.okMax), backgroundColor: withAlpha(c.ok, 0.1), fill: "-1", ...dashed });
  }
  datasets.push({
    label: `${meta?.name ?? key} (${meta?.unit ?? ""})`,
    data: pts.map((p) => p.value),
    borderColor: c.primary,
    backgroundColor: c.primary,
    borderWidth: 2,
    tension: 0.3,
    pointRadius: 5,
    pointHoverRadius: 7,
    pointBackgroundColor: pts.map((p) => color(p.status)),
    pointBorderColor: pts.map((p) => color(p.status)),
  });
  const dataIdx = datasets.length - 1;
  charts.push(new window.Chart(canvas, {
    type: "line",
    data: { labels: pts.map((p) => shortDate(p.recordedAt)), datasets },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: "index", intersect: false },
      plugins: {
        legend: { display: false },
        tooltip: {
          filter: (i) => i.datasetIndex === dataIdx,
          callbacks: { afterLabel: (i) => `Status: ${statusOf(pts[i.dataIndex]?.status).label}` },
        },
      },
      scales: {
        x: { grid: { display: false } },
        y: { grace: "10%", grid: { color: c.grid } },
      },
    },
  }));
}
