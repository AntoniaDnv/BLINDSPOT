// Vitals dashboard: latest readings vs thresholds + 7-day trend.
import { api } from "../../api.js";
import { createLineChart } from "../../charts.js";
import {
  escapeHtml,
  formatShortDateTime,
  showMessage,
  hideMessage,
} from "../../utils.js";

let chart = null;
let el = {};
let readings = [];
let indicators = [];

export async function init(root) {
  el = {
    message: root.querySelector("#vitals-message"),
    grid: root.querySelector("#vitals-grid"),
    select: root.querySelector("#vitals-indicator"),
    canvas: root.querySelector("#vitals-chart"),
  };
  const rootEl = el;

  el.select.addEventListener("change", drawChart);

  try {
    const [latest, all, defs] = await Promise.all([
      api.get("/api/readings/latest"),
      api.get("/api/readings"),
      api.get("/api/vitals/indicators"),
    ]);
    if (el !== rootEl) return; // page was left while loading
    readings = all;
    indicators = defs;
    hideMessage(el.message);
    renderCards(latest);
    el.select.innerHTML = latest
      .map((l) => `<option value="${escapeHtml(l.indicator)}">${escapeHtml(l.name)}</option>`)
      .join("");
    drawChart();
  } catch (error) {
    if (el !== rootEl) return; // page was left while loading
    showMessage(el.message, `Could not load vitals: ${error.message}`, "error");
  }
}

export function destroy() {
  chart?.destroy();
  chart = null;
  readings = [];
  indicators = [];
  el = {};
}

function renderCards(latest) {
  if (latest.length === 0) {
    el.grid.innerHTML = `<p class="muted">No readings available.</p>`;
    return;
  }
  el.grid.innerHTML = latest
    .map((r) => {
      const status = ["ok", "warning", "critical"].includes(r.status) ? r.status : "info";
      const label = status.charAt(0).toUpperCase() + status.slice(1);
      return `
        <section class="card">
          <h2>${escapeHtml(r.name)}</h2>
          <p><strong>${escapeHtml(r.value)}</strong> ${escapeHtml(r.unit)}
            <span class="badge status-${status}">${label}</span></p>
          <p>${escapeHtml(r.guidance?.[r.status] ?? "")}</p>
          <p class="muted">${escapeHtml(r.nasaRisk)}</p>
        </section>`;
    })
    .join("");
}

function drawChart() {
  chart?.destroy();
  chart = null;
  const key = el.select.value;
  const points = readings
    .filter((r) => r.indicator === key)
    .sort((a, b) => new Date(a.recordedAt) - new Date(b.recordedAt));
  const def = indicators.find((i) => i.indicator === key);
  const labels = points.map((p) => formatShortDateTime(p.recordedAt));
  const datasets = [
    { label: def ? `${def.name} (${def.unit})` : key, data: points.map((p) => p.value) },
  ];
  if (def) {
    const dashed = { borderDash: [6, 4], pointRadius: 0, borderWidth: 1 };
    datasets.push({ label: "OK min", data: points.map(() => def.okMin), borderColor: "#2fbf71", ...dashed });
    datasets.push({ label: "OK max", data: points.map(() => def.okMax), borderColor: "#ef4f4f", ...dashed });
  }
  try {
    chart = createLineChart(el.canvas, labels, datasets);
  } catch (error) {
    showMessage(el.message, error.message, "error");
  }
}
