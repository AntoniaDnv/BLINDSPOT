// Nutrition, Hydration & Meds page.
// Reads simulated sample data (plan section 6.5 shape). Alerts are precomputed in the
// data file, as the rules engine will provide them later; this page only displays them.
// TODO (JS teammate): switch DATA_URL to the crew API and the pickers to the app shell's
// crew selector and day slider once they exist.

import { api } from "../../api.js";
import { getActiveCrewId } from "../../session.js";
import { showMessage, hideMessage } from "../../utils.js";

const DEFAULT_DAY = 46;
const LOW_WATER_L = 2.0;
const LOW_KCAL_SHARE = 0.8;
const WEEK = 7;
const DASH = "—";

const LEVELS = {
  advisory: { rank: 1, label: "Advisory", tone: "info" },
  caution: { rank: 2, label: "Caution", tone: "caution" },
  warning: { rank: 3, label: "Warning", tone: "warning" },
};

let data = null;
let el = {};

export async function init(root) {
  data = null;
  el = {
    root,
    crew: root.querySelector("#nhm-crew"),
    day: root.querySelector("#nhm-day"),
    message: root.querySelector("#nhm-message"),
    energyCard: root.querySelector(".nhm-energy"),
    waterCard: root.querySelector(".nhm-water"),
    medsCard: root.querySelector(".nhm-meds"),
    ring: root.querySelector(".nhm-ring"),
    progress: root.querySelector(".nhm-progress-fill"),
    segments: [...root.querySelectorAll(".nhm-seg")],
    waterNote: root.querySelector("#nhm-water-note"),
    waterNotes: root.querySelector("#nhm-water-notes"),
    nights: root.querySelector("#nhm-hypnotic-week"),
    alertsBar: root.querySelector("#nhm-alerts-details"),
    alerts: root.querySelector("#nhm-alerts"),
    alertTpl: root.querySelector("#nhm-alert-tpl"),
    weekKcal: root.querySelector("#nhm-week-kcal"),
    weekWater: root.querySelector("#nhm-week-water"),
    weekDays: root.querySelector("#nhm-week-days"),
  };

  try {
    const crewId = getActiveCrewId();
    const res = await api.get(`/api/nutrition?crewId=${encodeURIComponent(crewId)}`);
    // Adapt the per-crew response to the shape the render code expects.
    data = {
      note: res.note,
      days: res.days ?? [],
      alerts: res.alerts ?? [],
      crew: res.baseline ? [{ id: res.crewId, baseline: res.baseline }] : [],
      crewId: res.crewId,
    };
  } catch (error) {
    showMessage(el.message, `Could not load nutrition data: ${error.message}`, "error");
    return;
  }

  if (data.crew.length === 0) {
    setText("#nhm-crew-role", data.crewId);
    el.crew.closest(".nhm-picker").hidden = true;
    el.day.closest(".nhm-picker").hidden = true;
    showMessage(el.message, "No nutrition data for this crew member yet.", "info");
    return;
  }
  fillPickers();
  el.crew.closest(".nhm-picker").hidden = true; // the crew member comes from the session
  el.crew.addEventListener("change", render);
  el.day.addEventListener("change", render);
  render();
}

export function destroy() {
  data = null;
}

function fillPickers() {
  el.crew.replaceChildren(...data.crew.map((c) => new Option(c.id, c.id)));
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

  setText("#nhm-crew-role", `Simulated data · ${crew.id}`);
  setPill("#nhm-mission-pill", `Mission day ${day}`, "ok");
  renderAlerts(alerts);
  renderWeek(crew, day);
  renderNights(crew.id, day);
  renderEnergyStats(crew, day);

  if (!record) {
    showMessage(el.message, `No data for day ${day}.`, "info");
    clearCards();
    return;
  }
  hideMessage(el.message);

  const { kcalTarget, waterTargetL } = crew.baseline;
  const foodLevel = worstLevel(alerts, "food");
  const waterLevel = worstLevel(alerts, "water");

  // Energy
  const kcalPct = Math.round((record.kcal / kcalTarget) * 100);
  const kcalLeft = kcalTarget - record.kcal;
  setText("#nhm-kcal", formatNumber(record.kcal));
  setText("#nhm-kcal-target", formatNumber(kcalTarget));
  setText("#nhm-kcal-left", kcalLeft > 0 ? `+${formatNumber(kcalLeft)} kcal` : "Target reached");
  setText("#nhm-kcal-pct", `${kcalPct}%`);
  el.ring.style.setProperty("--pct", kcalPct);
  el.progress.style.setProperty("--pct", kcalPct);
  setStatus(el.energyCard, "#nhm-energy-status", foodLevel, `${kcalPct}% of target`);

  // Hydration
  const waterPct = Math.round((record.waterL / waterTargetL) * 100);
  const filled = Math.round(Math.min(record.waterL / waterTargetL, 1) * 10);
  const leftMl = Math.round(Math.max(waterTargetL - record.waterL, 0) * 1000);
  el.segments.forEach((seg, i) => seg.classList.toggle("is-on", i < filled));
  setText("#nhm-water", record.waterL.toFixed(1));
  setText("#nhm-water-target", waterTargetL.toFixed(1));
  setText("#nhm-water-target-foot", waterTargetL.toFixed(1));
  setText("#nhm-water-left", leftMl > 0 ? `${leftMl} mL below target` : "Target reached");
  setStatus(el.waterCard, "#nhm-water-status", waterLevel, `${waterPct}% of target`);
  renderWaterNotes(alerts.find((a) => a.system === "water"));

  // Medication
  const streak = medicationStreak(crew.id, day);
  setText("#nhm-hypnotic", record.hypnoticTaken ? "Yes" : "No");
  setText("#nhm-hypnotic-streak", streak === 1 ? "1 night" : `${streak} nights in a row`);
  setPill("#nhm-meds-status", "Logged", "info");
  el.medsCard.dataset.status = "info";
}

function findDay(crewId, day) {
  return data.days.find((d) => d.crewId === crewId && d.day === day);
}

function weekDays(day) {
  const days = [];
  for (let d = Math.max(1, day - (WEEK - 1)); d <= day; d++) days.push(d);
  return days;
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

// Pill text always carries the status word, so status is never shown by colour alone.
function setStatus(card, pillSelector, level, detail) {
  const l = LEVELS[level];
  const label = l ? l.label : "On track";
  const tone = l ? l.tone : "ok";
  setPill(pillSelector, `${label} · ${detail}`, tone);
  card.dataset.status = tone;
}

function setPill(selector, text, tone) {
  const pill = el.root.querySelector(selector);
  pill.querySelector(".nhm-pill-text").textContent = text;
  pill.dataset.tone = tone;
}

function clearCards() {
  for (const id of ["#nhm-kcal", "#nhm-kcal-target", "#nhm-kcal-left", "#nhm-kcal-pct", "#nhm-water",
    "#nhm-water-target", "#nhm-water-target-foot", "#nhm-water-left", "#nhm-hypnotic", "#nhm-hypnotic-streak"]) {
    setText(id, DASH);
  }
  el.ring.style.setProperty("--pct", 0);
  el.progress.style.setProperty("--pct", 0);
  el.segments.forEach((seg) => seg.classList.remove("is-on"));
  for (const id of ["#nhm-energy-status", "#nhm-water-status", "#nhm-meds-status"]) setPill(id, DASH, "none");
  for (const card of [el.energyCard, el.waterCard, el.medsCard]) card.dataset.status = "none";
  el.waterNote.textContent = DASH;
  el.waterNote.dataset.tone = "none";
  el.waterNotes.textContent = DASH;
}

function renderEnergyStats(crew, day) {
  const records = weekDays(day).map((d) => findDay(crew.id, d)).filter(Boolean);
  if (records.length === 0) {
    setText("#nhm-kcal-avg", DASH);
    setText("#nhm-kcal-lowdays", DASH);
    return;
  }
  const shares = records.map((r) => r.kcal / crew.baseline.kcalTarget);
  const avg = Math.round((shares.reduce((sum, s) => sum + s, 0) / shares.length) * 100);
  const low = shares.filter((s) => s < LOW_KCAL_SHARE).length;
  setText("#nhm-kcal-avg", `${avg}%`);
  setText("#nhm-kcal-lowdays", `${low} / ${records.length}`);
}

function renderWaterNotes(alert) {
  if (alert) {
    el.waterNote.textContent = `⚠ ${alert.what}`;
    el.waterNote.dataset.tone = "caution";
    el.waterNotes.replaceChildren(
      line("Why it matters", alert.why),
      line("What to do", alert.action),
      line("Source", alert.source),
    );
  } else {
    el.waterNote.textContent = "✓ Hydration on track";
    el.waterNote.dataset.tone = "ok";
    el.waterNotes.replaceChildren(
      line("Rule", "An alert appears after 2 days in a row below 2.0 L (demo parameter)."),
      line("Source", "NASA Risk of Inadequate Food and Nutrition (in-flight dehydration)"),
    );
  }
}

function line(label, text) {
  const p = document.createElement("p");
  const l = document.createElement("span");
  l.className = "nhm-label";
  l.textContent = `${label} `;
  p.append(l, document.createTextNode(text));
  return p;
}

function renderNights(crewId, day) {
  el.nights.replaceChildren(...weekDays(day).map((d) => {
    const r = findDay(crewId, d);
    const dot = document.createElement("span");
    dot.className = r?.hypnoticTaken ? "nhm-night is-on" : "nhm-night";
    const state = r ? (r.hypnoticTaken ? "medication taken" : "no medication") : "no data";
    dot.setAttribute("role", "img");
    dot.setAttribute("aria-label", `Night of day ${d}: ${state}`);
    dot.title = `Day ${d}: ${state}`;
    return dot;
  }));
}

function renderAlerts(alerts) {
  const count = alerts.length;
  setPill("#nhm-alerts-count", count === 0 ? "None active" : `${count} active`, count === 0 ? "ok" : worstTone(alerts));
  el.alertsBar.open = count > 0;

  if (count === 0) {
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
    const pill = item.querySelector(".nhm-alert-level");
    pill.querySelector(".nhm-pill-text").textContent = level ? level.label : a.level;
    pill.dataset.tone = level ? level.tone : "none";
    item.querySelector(".nhm-alert-what").textContent = a.what;
    item.querySelector(".nhm-alert-why").textContent = a.why;
    item.querySelector(".nhm-alert-action").textContent = a.action;
    item.querySelector(".nhm-alert-source").textContent = a.source;
    return item;
  }));
}

function worstTone(alerts) {
  const worst = alerts.reduce((w, a) => ((LEVELS[a.level]?.rank ?? 0) > (LEVELS[w]?.rank ?? 0) ? a.level : w), null);
  return LEVELS[worst]?.tone ?? "none";
}

function renderWeek(crew, day) {
  const kcalBars = [];
  const waterBars = [];
  const labels = [];
  for (const d of weekDays(day)) {
    const r = findDay(crew.id, d);
    const kcalShare = r ? r.kcal / crew.baseline.kcalTarget : 0;
    const waterShare = r ? r.waterL / crew.baseline.waterTargetL : 0;
    const kcalLow = Boolean(r) && kcalShare < LOW_KCAL_SHARE;
    const waterLow = Boolean(r) && r.waterL < LOW_WATER_L;
    kcalBars.push(bar(kcalShare, kcalLow, r
      ? `Day ${d}: ${Math.round(kcalShare * 100)}% of target${kcalLow ? ", below 80%" : ""}`
      : `Day ${d}: no data`));
    waterBars.push(bar(waterShare, waterLow, r
      ? `Day ${d}: ${r.waterL.toFixed(1)} L${waterLow ? ", below 2.0 L" : ""}`
      : `Day ${d}: no data`));
    const label = document.createElement("span");
    label.textContent = String(d);
    labels.push(label);
  }
  el.weekKcal.replaceChildren(...kcalBars);
  el.weekWater.replaceChildren(...waterBars);
  el.weekDays.replaceChildren(...labels);
}

function bar(share, isLow, label) {
  const b = document.createElement("span");
  b.className = isLow ? "nhm-bar is-low" : "nhm-bar";
  b.style.setProperty("--pct", Math.round(share * 100));
  b.setAttribute("role", "img");
  b.setAttribute("aria-label", label);
  b.title = label;
  return b;
}

function formatNumber(n) {
  return n.toLocaleString("en-US");
}

function setText(selector, text) {
  el.root.querySelector(selector).textContent = text;
}
