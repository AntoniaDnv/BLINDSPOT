// Radiation page: live DONKI solar particle status + replay of the May 2024 storm.
import { api } from "../../api.js";
import { escapeHtml, formatDateTime, showMessage, hideMessage } from "../../utils.js";

const QUERIES = {
  live: "",
  days30: "?days=30",
  replay: "?start=2024-05-01&end=2024-05-31",
};
const RANGE_LABELS = {
  live: "Showing the last 3 days (live).",
  days30: "Showing the last 30 days.",
  replay: "Replay: 1-31 May 2024 (real storm).",
};

let el = null;
let buttons = [];
let requestId = 0;

export function init(root) {
  const local = {
    message: root.querySelector("#rad-message"),
    badge: root.querySelector("#rad-status"),
    guidance: root.querySelector("#rad-guidance"),
    count: root.querySelector("#rad-count"),
    flares: root.querySelector("#rad-flares"),
    cmes: root.querySelector("#rad-cmes"),
    last: root.querySelector("#rad-last"),
    range: root.querySelector("#rad-range"),
  };
  el = local;
  buttons = [...root.querySelectorAll(".rad-buttons button")];
  root.querySelector("#rad-live").addEventListener("click", () => load("live"));
  root.querySelector("#rad-30").addEventListener("click", () => load("days30"));
  root.querySelector("#rad-replay").addEventListener("click", () => load("replay"));
  load("live"); // not awaited: router must not wait on a slow DONKI call
}

export function destroy() {
  el = null;
  buttons = [];
  requestId++;
}

async function load(mode) {
  const local = el;
  const myId = ++requestId;
  buttons.forEach((b) => (b.disabled = true));
  local.range.textContent = "Loading...";
  try {
    const data = await api.get(`/api/radiation/status${QUERIES[mode]}`);
    if (el !== local || myId !== requestId) return; // left page or superseded
    render(data, mode);
  } catch (error) {
    if (el !== local || myId !== requestId) return;
    setBadge("error");
    local.guidance.textContent = "Radiation status unavailable.";
    local.range.textContent = "";
    local.count.textContent = "-";
    local.flares.textContent = "-";
    local.cmes.textContent = "-";
    local.last.textContent = "-";
    showMessage(local.message, `Could not load radiation data: ${error.message}`, "error");
  } finally {
    if (el === local && myId === requestId) buttons.forEach((b) => (b.disabled = false));
  }
}

function setBadge(status) {
  const known = ["ok", "warning", "critical"].includes(status);
  el.badge.className = `badge status-${known ? status : "info"}`;
  el.badge.textContent = status === "ok" ? "OK" : known ? status.charAt(0).toUpperCase() + status.slice(1) : "Error";
}

function render(data, mode) {
  setBadge(data.status);
  el.guidance.innerHTML = `<strong>${escapeHtml(data.guidance)}</strong>`;
  el.count.textContent = data.eventCount ?? 0;
  el.flares.textContent = data.flareCount ?? 0;
  el.cmes.textContent = data.cmeCount ?? 0;
  el.last.textContent = data.lastEventTime ? formatDateTime(data.lastEventTime) : "none";
  el.range.textContent = RANGE_LABELS[mode];
  if (data.status === "error") {
    showMessage(el.message, `${data.guidance ?? ""} ${data.error ?? ""}`.trim(), "error");
  } else {
    hideMessage(el.message);
  }
}
