// Blood Panels page: Inspiration4 CBC / CMP data (NASA OSDR OSD-569 / OSD-575).
import { api } from "../../api.js";
import { createLineChart } from "../../charts.js";
import { escapeHtml, showMessage, hideMessage } from "../../utils.js";

let chart = null;
let el = {};
let rows = [];
let selectedAnalyte = null;
let analyteInfo = {};
let loadToken = 0;

export async function init(root) {
  el = {
    message: root.querySelector("#bp-message"),
    crew: root.querySelector("#bp-crew-select"),
    panel: root.querySelector("#bp-panel-select"),
    time: root.querySelector("#bp-time-select"),
    kpiCrew: root.querySelector("#bp-kpi-crew"),
    kpiPanel: root.querySelector("#bp-kpi-panel"),
    kpiFlagged: root.querySelector("#bp-kpi-flagged"),
    kpiDate: root.querySelector("#bp-kpi-date"),
    tableBody: root.querySelector("#bp-table-body"),
    canvas: root.querySelector("#bp-chart"),
    chartTitle: root.querySelector("#bp-chart-title"),
  };

  el.crew.addEventListener("change", loadPanel);
  el.panel.addEventListener("change", loadPanel);
  el.time.addEventListener("change", onTimeChange);
  el.tableBody.addEventListener("click", onRowClick);

  try {
    const info = await api.get("/data/analyte-info.json");
    analyteInfo = Object.fromEntries(info.map((i) => [i.analyte, i]));
  } catch {
    analyteInfo = {}; // optional tooltips only
  }

  try {
    const crew = await api.get("/api/bloodpanels/crew");
    if (crew.length > 0) {
      el.crew.innerHTML = crew
        .map((c) => `<option value="${escapeHtml(c)}">${escapeHtml(c)}</option>`)
        .join("");
    }
  } catch (error) {
    showMessage(el.message, error.message, "error");
  }

  await loadPanel();
}

export function destroy() {
  loadToken++;
  chart?.destroy();
  chart = null;
}

async function loadPanel() {
  const token = ++loadToken;
  const crewId = el.crew.value;
  const panel = el.panel.value;
  hideMessage(el.message);

  try {
    const data = await api.get(
      `/api/bloodpanels?crewId=${encodeURIComponent(crewId)}&panel=${encodeURIComponent(panel)}`,
    );
    if (token !== loadToken) return; // a newer request superseded this one
    rows = data;
  } catch (error) {
    if (token !== loadToken) return;
    rows = [];
    showMessage(el.message, error.message, "error");
  }

  selectedAnalyte = null;
  fillTimepoints();
  render(crewId, panel);
}

function fillTimepoints() {
  const seen = new Map(rows.map((r) => [r.dayOffset, r.timepoint]));
  const days = [...seen.keys()].sort((a, b) => a - b);
  el.time.innerHTML = days
    .map((d) => `<option value="${d}">${escapeHtml(seen.get(d))}</option>`)
    .join("");
  el.time.value = String(days[days.length - 1] ?? "");
}

function selectedRows() {
  return rows.filter((r) => String(r.dayOffset) === el.time.value);
}

function latestRows() {
  if (rows.length === 0) return [];
  const maxDay = Math.max(...rows.map((r) => r.dayOffset));
  return rows.filter((r) => r.dayOffset === maxDay);
}

function render(crewId, panel) {
  const latest = latestRows();
  el.kpiCrew.textContent = crewId;
  el.kpiPanel.textContent = panel;
  el.kpiFlagged.textContent = latest.length
    ? latest.filter((r) => r.status === "warning").length
    : "—";
  el.kpiDate.textContent = latest.length ? latest[0].timepoint : "—";

  if (latest.length === 0) {
    el.tableBody.innerHTML =
      '<tr><td colspan="6" class="muted">No data for this selection.</td></tr>';
    chart?.destroy();
    chart = null;
    el.chartTitle.textContent = "Biomarker trend over time";
    return;
  }

  const shown = selectedRows();
  selectedAnalyte = shown[0].analyte;
  renderTable(shown);
  renderChart();
}

function renderTable(latest) {
  el.tableBody.innerHTML = latest
    .map((r) => {
      const info = analyteInfo[r.analyte];
      const tip = info ? ` title="${escapeHtml(info.explanation)}"` : "";
      const badge = r.status === "warning" ? "status-warning" : "status-ok";
      const label = r.status === "warning" ? "Warning" : "OK";
      return `
    <tr data-analyte="${escapeHtml(r.analyte)}"${tip}>
      <td>${escapeHtml(r.analyte)}</td>
      <td class="bp-num">${escapeHtml(r.value)}</td>
      <td>${escapeHtml(r.unit)}</td>
      <td class="bp-num">${escapeHtml(r.rangeMin)} - ${escapeHtml(r.rangeMax)}</td>
      <td><span class="badge ${badge}">${label}</span></td>
      <td>${escapeHtml(r.timepoint)}</td>
    </tr>`;
    })
    .join("");
  markActiveRow();
}

function markActiveRow() {
  el.tableBody.querySelectorAll("tr").forEach((tr) => {
    tr.classList.toggle("bp-row-active", tr.dataset.analyte === selectedAnalyte);
  });
}

function renderChart() {
  const series = rows
    .filter((r) => r.analyte === selectedAnalyte)
    .sort((a, b) => a.dayOffset - b.dayOffset);
  if (series.length === 0) return;

  const dashed = { borderDash: [6, 4], pointRadius: 0, tension: 0 };
  chart?.destroy();
  chart = null;
  try {
    chart = createLineChart(
      el.canvas,
      series.map((r) => r.timepoint),
      [
        { label: `${selectedAnalyte} (${series[0].unit})`, data: series.map((r) => r.value) },
        { label: "Range min", data: series.map((r) => r.rangeMin), borderColor: "#2fbf71", backgroundColor: "#2fbf71", ...dashed },
        { label: "Range max", data: series.map((r) => r.rangeMax), borderColor: "#ef4f4f", backgroundColor: "#ef4f4f", ...dashed },
      ],
    );
  } catch (error) {
    showMessage(el.message, error.message, "error");
  }
  el.chartTitle.textContent = `${selectedAnalyte} trend (${series[0].unit})`;
}

function onTimeChange() {
  const shown = selectedRows();
  if (shown.length === 0) return;
  if (!shown.some((r) => r.analyte === selectedAnalyte)) {
    selectedAnalyte = shown[0].analyte;
  }
  renderTable(shown);
  renderChart();
}

function onRowClick(event) {
  const tr = event.target.closest("tr[data-analyte]");
  if (!tr) return;
  selectedAnalyte = tr.dataset.analyte;
  markActiveRow();
  renderChart();
}
