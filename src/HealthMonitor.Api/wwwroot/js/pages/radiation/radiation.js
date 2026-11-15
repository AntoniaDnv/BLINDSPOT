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
  root.querySelector("#rad-live").addEventListener("click", () => load("live"));
  root.querySelector("#rad-30").addEventListener("click", () => load("days30"));
  root.querySelector("#rad-replay").addEventListener("click", () => load("replay"));
  return load("live");
}

export function destroy() {
  el = null;
}

async function load(mode) {
  const local = el;
  try {
    const data = await api.get(`/api/radiation/status${QUERIES[mode]}`);
    if (el !== local) return; // page was left while loading
    render(data, mode);
  } catch (error) {
    if (el !== local) return;
    setBadge("error");
    local.guidance.textContent = "Radiation status unavailable.";
    showMessage(local.message, `Could not load radiation data: ${error.message}`, "error");
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
