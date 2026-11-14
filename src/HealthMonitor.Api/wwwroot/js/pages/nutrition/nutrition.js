// Nutrition, Hydration & Meds page.
// Reads simulated sample data (plan section 6.5 shape). Alerts are precomputed in the
// data file, as the rules engine will provide them later; this page only displays them.
// TODO (JS teammate): switch DATA_URL to the crew API and the pickers to the app shell's
// crew selector and day slider once they exist.

import { api } from "../../api.js";
import { showMessage, hideMessage } from "../../utils.js";

const DATA_URL = "/data/nutrition-days.json";
const DEFAULT_DAY = 46;
const LOW_WATER_L = 2.0;
const LOW_KCAL_SHARE = 0.8;
const DASH = "—";

const LEVELS = {
  advisory: { rank: 1, label: "Advisory", status: "info", badge: "status-info" },
  caution: { rank: 2, label: "Caution", status: "caution", badge: "status-warning" },
  warning: { rank: 3, label: "Warning", status: "warning", badge: "status-critical" },
};

let data = null;
let el = {};

export async function init(root) {
  data = null;
  el = {
    root,
    crew: root.querySelector("#nhm-crew"),
    day: root.querySelector("#nhm-day"),
    role: root.querySelector("#nhm-crew-role"),
    message: root.querySelector("#nhm-message"),
    energyCard: root.querySelector(".nhm-energy"),
    waterCard: root.querySelector(".nhm-water"),
    medsCard: root.querySelector(".nhm-meds"),
    ring: root.querySelector(".nhm-ring"),
    segments: [...root.querySelectorAll(".nhm-seg")],
    alerts: root.querySelector("#nhm-alerts"),
    alertTpl: root.querySelector("#nhm-alert-tpl"),
    weekKcal: root.querySelector("#nhm-week-kcal"),
    weekWater: root.querySelector("#nhm-week-water"),
    weekDays: root.querySelector("#nhm-week-days"),
  };

  try {
    data = await api.get(DATA_URL);
  } catch (error) {
    showMessage(el.message, `Could not load nutrition data: ${error.message}`, "error");
    return;
  }

  fillPickers();
  el.crew.addEventListener("change", render);
  el.day.addEventListener("change", render);
  render();
}

export function destroy() {
  data = null;
}

function fillPickers() {
  el.crew.replaceChildren(...data.crew.map((c) => new Option(c.name, c.id)));
  const lastDay = Math.max(...data.days.map((d) => d.day));
  const options = [];
  for (let day = 1; day <= lastDay; day++) options.push(new Option(`Day ${day}`, String(day)));
  el.day.replaceChildren(...options);
  el.day.value = String(Math.min(DEFAULT_DAY, lastDay));
}

function render() {
  const crew = data.crew.find((c) => c.id === el.crew.value);
  const day = Number(el.day.value);
  const record = findDay(crew.id, day);
  const alerts = data.alerts.filter((a) => a.crewId === crew.id && a.day === day);

  setText("#nhm-crew-role", `${crew.role} · Mission day ${day}`);
  renderAlerts(alerts);
  renderWeek(crew, day);

  if (!record) {
    showMessage(el.message, `No data for day ${day}.`, "info");
    clearCards();
    return;
  }
  hideMessage(el.message);

  const { kcalTarget, waterTargetL } = crew.baseline;
  const kcalPct = Math.round((record.kcal / kcalTarget) * 100);
  setText("#nhm-kcal", record.kcal.toLocaleString("en-US"));
  setText("#nhm-kcal-target", kcalTarget.toLocaleString("en-US"));
  setText("#nhm-kcal-pct", `${kcalPct}%`);
  el.ring.style.setProperty("--pct", kcalPct);

  const filled = Math.round(Math.min(record.waterL / waterTargetL, 1) * 10);
  el.segments.forEach((seg, i) => seg.classList.toggle("is-on", i < filled));
  const left = Math.max(waterTargetL - record.waterL, 0);
  setText("#nhm-water", record.waterL.toFixed(1));
  setText("#nhm-water-target", waterTargetL.toFixed(1));
  setText("#nhm-water-left", left > 0 ? `${left.toFixed(1)} L below target` : "Target reached");

  setStatus(el.energyCard, "#nhm-energy-status", worstLevel(alerts, "food"));
  setStatus(el.waterCard, "#nhm-water-status", worstLevel(alerts, "water"));

  const streak = medicationStreak(crew.id, day);
  setText("#nhm-hypnotic", record.hypnoticTaken ? "Yes" : "No");
  setText("#nhm-hypnotic-streak", streak === 1 ? "1 night" : `${streak} nights in a row`);
  setBadge(el.medsCard, "#nhm-meds-status", "Logged", "info", "status-info");
}

function findDay(crewId, day) {
  return data.days.find((d) => d.crewId === crewId && d.day === day);
}

function worstLevel(alerts, system) {
  return alerts
    .filter((a) => a.system === system && LEVELS[a.level])
    .reduce((worst, a) => (!worst || LEVELS[a.level].rank > LEVELS[worst].rank ? a.level : worst), null);
}

function medicationStreak(crewId, day) {
  let count = 0;
  for (let d = day; d >= 1 && findDay(crewId, d)?.hypnoticTaken; d--) count++;
  return count;
}

function setStatus(card, badgeSelector, level) {
  if (level) {
    const l = LEVELS[level];
    setBadge(card, badgeSelector, l.label, l.status, l.badge);
  } else {
    setBadge(card, badgeSelector, "On track", "ok", "status-ok");
  }
}

function setBadge(card, badgeSelector, text, status, badgeClass) {
  const badge = el.root.querySelector(badgeSelector);
  badge.textContent = text;
  badge.className = `badge nhm-badge ${badgeClass}`;
  card.dataset.status = status;
}

function clearCards() {
  for (const id of ["#nhm-kcal", "#nhm-kcal-target", "#nhm-kcal-pct", "#nhm-water", "#nhm-water-target",
    "#nhm-water-left", "#nhm-hypnotic", "#nhm-hypnotic-streak"]) {
    setText(id, DASH);
  }
  el.ring.style.setProperty("--pct", 0);
  el.segments.forEach((seg) => seg.classList.remove("is-on"));
  for (const [card, badge] of [[el.energyCard, "#nhm-energy-status"], [el.waterCard, "#nhm-water-status"],
    [el.medsCard, "#nhm-meds-status"]]) {
    setBadge(card, badge, DASH, "none", "");
  }
}

function renderAlerts(alerts) {
  if (alerts.length === 0) {
    const li = document.createElement("li");
    li.className = "nhm-muted";
    li.textContent = "No alerts for this day.";
    el.alerts.replaceChildren(li);
    return;
  }
  const sorted = [...alerts].sort((a, b) => (LEVELS[b.level]?.rank ?? 0) - (LEVELS[a.level]?.rank ?? 0));
  el.alerts.replaceChildren(...sorted.map((a) => {
    const item = el.alertTpl.content.firstElementChild.cloneNode(true);
    const level = LEVELS[a.level];
    const levelEl = item.querySelector(".nhm-alert-level");
    levelEl.textContent = level ? level.label : a.level;
    if (level) levelEl.classList.add(level.badge);
    item.querySelector(".nhm-alert-what").textContent = a.what;
    item.querySelector(".nhm-alert-why").textContent = a.why;
    item.querySelector(".nhm-alert-action").textContent = a.action;
    item.querySelector(".nhm-alert-source").textContent = a.source;
    return item;
  }));
}

function renderWeek(crew, day) {
  const kcalBars = [];
  const waterBars = [];
  const labels = [];
  for (let d = Math.max(1, day - 6); d <= day; d++) {
    const r = findDay(crew.id, d);
    const kcalShare = r ? r.kcal / crew.baseline.kcalTarget : 0;
    const waterShare = r ? r.waterL / crew.baseline.waterTargetL : 0;
    kcalBars.push(bar(kcalShare, r && kcalShare < LOW_KCAL_SHARE, r ? `Day ${d}: ${Math.round(kcalShare * 100)}%` : `Day ${d}: no data`));
    waterBars.push(bar(waterShare, r && r.waterL < LOW_WATER_L, r ? `Day ${d}: ${r.waterL.toFixed(1)} L` : `Day ${d}: no data`));
    const label = document.createElement("span");
    label.textContent = String(d);
    labels.push(label);
  }
  el.weekKcal.replaceChildren(...kcalBars);
  el.weekWater.replaceChildren(...waterBars);
  el.weekDays.replaceChildren(...labels);
}

function bar(share, isLow, title) {
  const b = document.createElement("span");
  b.className = isLow ? "nhm-bar is-low" : "nhm-bar";
  b.style.setProperty("--pct", Math.round(share * 100));
  b.title = title;
  return b;
}

function setText(selector, text) {
  el.root.querySelector(selector).textContent = text;
}
