// Log & Data Entry: vitals (batch), nutrition & hydration, symptoms, medications.
// Every save goes to the API for the active crew member (getActiveCrewId()); the server's
// crew-access check decides what is allowed. User text is always rendered as text.
import { api } from "../../api.js";
import { escapeHtml, formatDateTime, renderErrorState } from "../../utils.js";
import { getActiveCrewId, getCrewList, getSession, isFlightSurgeon, setActiveCrewId } from "../../session.js";

const STATUS = {
  ok: { label: "Nominal", tone: "chip--ok" },
  warning: { label: "Warning", tone: "chip--warning" },
  critical: { label: "Critical", tone: "chip--critical" },
  taken: { label: "Taken", tone: "chip--ok" },
  pending: { label: "Pending", tone: "chip--warning" },
};
const TYPE = {
  vitals: { icon: "monitor_heart", label: "Vitals" },
  nutrition: { icon: "restaurant", label: "Nutrition" },
  symptoms: { icon: "psychology", label: "Symptoms" },
  meds: { icon: "medication", label: "Medication" },
};
const SECTIONS = ["vitals", "nutrition", "symptoms", "meds"];
// Team demo parameter: hydration target when the crew member has no personal target.
const DEFAULT_WATER_TARGET_ML = 2000;
const SEVERITY = ["None", "None", "Mild", "Mild", "Mild", "Moderate", "Moderate", "Severe", "Severe", "Severe", "Worst"];

let token = 0;
let el = {};
let crewId = null;
let today = null; // { day, date } from the server
let toastTimer = null;

export async function init(root) {
  const my = ++token;
  crewId = getActiveCrewId();
  el = collect(root);

  renderWho();
  wireStatic();

  if (!crewId) {
    el.layout.classList.add("hidden");
    el.load.classList.remove("hidden");
    el.load.innerHTML = `<div class="state state--empty"><span class="state__icon" aria-hidden="true"><span class="icon material-symbols-outlined">group_off</span></span><p class="state__title">No crew member to log for</p><p class="state__text">No astronaut accounts exist yet.</p></div>`;
    return;
  }

  try {
    const [indicators, nutrition] = await Promise.all([
      api.get("/api/vitals/indicators"),
      api.get(`/api/nutrition?crewId=${q(crewId)}`),
    ]);
    if (my !== token) return;
    today = nutrition.today;
    el.date.textContent = `Mission day ${today.day} · ${today.date}`;
    buildVitals(indicators);
    renderNutrition(nutrition);
    await Promise.all([refreshMeds(my), refreshRecent(my)]);
  } catch (error) {
    if (my !== token) return;
    el.layout.classList.add("hidden");
    renderErrorState(el.load, { title: "Could not load the log page", error });
  }
}

export function destroy() {
  token++;
  clearTimeout(toastTimer);
}

// ---- setup ---------------------------------------------------------------------

function collect(root) {
  const $ = (s) => root.querySelector(s);
  return {
    root,
    who: $("#log-who"),
    date: $("#log-date"),
    load: $("#log-load"),
    layout: $("#log-layout"),
    vitalsForm: $("#vitals-form"),
    vitalsFields: $("#vitals-fields"),
    vitalsError: $("#vitals-error"),
    vitalsSave: $("#vitals-save"),
    vitalsResults: $("#vitals-results"),
    waterRing: $("#water-ring"),
    waterPct: $("#water-pct"),
    waterTotal: $("#water-total"),
    waterTarget: $("#water-target"),
    waterNote: $("#water-note"),
    waterError: $("#water-error"),
    waterButtons: [...root.querySelectorAll("[data-water]")],
    foodForm: $("#food-form"),
    foodToday: $("#food-today"),
    foodError: $("#food-error"),
    foodSave: $("#food-save"),
    symForm: $("#symptoms-form"),
    fatigue: $("#sym-fatigue"),
    fatigueOut: $("#sym-fatigue-out"),
    headache: $("#sym-headache"),
    headacheOut: $("#sym-headache-out"),
    congestion: $("#sym-congestion"),
    vision: $("#sym-vision"),
    notes: $("#sym-notes"),
    notesCount: $("#sym-notes-count"),
    symError: $("#symptoms-error"),
    symSave: $("#symptoms-save"),
    medsForm: $("#meds-form"),
    medName: $("#med-name"),
    medDose: $("#med-dose"),
    medTime: $("#med-time"),
    medTaken: $("#med-taken"),
    medSleep: $("#med-sleep"),
    medsError: $("#meds-error"),
    medsSave: $("#meds-save"),
    medsList: $("#meds-list"),
    recent: $("#recent-list"),
    progress: $("#log-progress"),
    clear: $("#log-clear"),
    toast: $("#log-toast"),
  };
}

function renderWho() {
  if (isFlightSurgeon()) {
    const crew = getCrewList();
    const select = document.createElement("select");
    select.id = "log-crew";
    select.setAttribute("aria-label", "Logging for crew member");
    for (const c of crew) select.append(new Option(`${c.crewId} · ${c.displayName}`, c.crewId));
    select.value = crewId ?? "";
    select.addEventListener("change", () => setActiveCrewId(select.value)); // router re-renders
    el.who.replaceChildren(select);
  } else {
    const s = getSession();
    el.who.textContent = s ? `${s.displayName} · ${s.crewId}` : "—";
  }
}

function wireStatic() {
  el.root.querySelectorAll("[data-jump]").forEach((tab) =>
    tab.addEventListener("click", () => {
      el.root.querySelector(`#${tab.dataset.jump}`)?.scrollIntoView({ behavior: "smooth", block: "start" });
    }));

  el.vitalsForm.addEventListener("submit", (e) => { e.preventDefault(); saveVitals(); });
  el.foodForm.addEventListener("submit", (e) => { e.preventDefault(); saveFood(); });
  el.symForm.addEventListener("submit", (e) => { e.preventDefault(); saveSymptoms(); });
  el.medsForm.addEventListener("submit", (e) => { e.preventDefault(); saveMed(); });
  el.waterButtons.forEach((b) => b.addEventListener("click", () => addWater(Number(b.dataset.water))));

  const slider = (input, out) => {
    const update = () => { out.textContent = `${input.value} · ${SEVERITY[Number(input.value)]}`; };
    input.addEventListener("input", update);
    update();
  };
  slider(el.fatigue, el.fatigueOut);
  slider(el.headache, el.headacheOut);
  el.notes.addEventListener("input", () => { el.notesCount.textContent = `${el.notes.value.length} / 1000`; });
  el.clear.addEventListener("click", clearForms);
}

// ---- vitals --------------------------------------------------------------------

function buildVitals(indicators) {
  const byId = Object.fromEntries(indicators.map((t) => [t.indicator, t]));
  const tiles = [];
  for (const t of indicators) {
    if (t.indicator === "diastolicBp" && byId.systolicBp) continue;
    if (t.indicator === "systolicBp" && byId.diastolicBp) tiles.push(bpTile(t, byId.diastolicBp));
    else if (t.indicator === "sleepHours") tiles.push(stepperTile(t));
    else if (t.indicator === "mood") tiles.push(sliderTile(t));
    else tiles.push(numberTile(t));
  }
  el.vitalsFields.innerHTML = tiles.join("");

  el.vitalsFields.querySelectorAll("[data-step]").forEach((b) =>
    b.addEventListener("click", () => {
      const input = el.vitalsFields.querySelector('[data-indicator="sleepHours"]');
      const current = input.value === "" ? 7 : Number(input.value) + Number(b.dataset.step);
      input.value = String(Math.min(24, Math.max(0, current)));
    }));
  const mood = el.vitalsFields.querySelector('[data-indicator="mood"]');
  if (mood) {
    const out = el.vitalsFields.querySelector("#v-mood-out");
    mood.addEventListener("input", () => {
      mood.dataset.touched = "true";
      out.textContent = `${mood.value} / ${mood.max}`;
    });
  }
}

const num = (v) => Number(v).toLocaleString("en-US", { maximumFractionDigits: 1 });
const range = (t) => `Normal ${num(t.okMin)}–${num(t.okMax)} ${escapeHtml(t.unit)}`;
const fieldError = (id) => `<p class="log-field-error hidden" data-err="${escapeHtml(id)}" role="alert"></p>`;

function numberTile(t) {
  const step = t.indicator === "bodyTemp" ? "0.1" : "1";
  return `<div class="tile log-vital">
    <label class="tile__label" for="v-${escapeHtml(t.indicator)}">${escapeHtml(t.name)}</label>
    <div class="log-input-unit">
      <input id="v-${escapeHtml(t.indicator)}" type="number" step="${step}" inputmode="decimal" data-indicator="${escapeHtml(t.indicator)}" />
      <span class="muted">${escapeHtml(t.unit)}</span>
    </div>
    <p class="tile__note">${range(t)}</p>
    ${fieldError(t.indicator)}
  </div>`;
}

function stepperTile(t) {
  return `<div class="tile log-vital">
    <label class="tile__label" for="v-sleepHours">${escapeHtml(t.name)}</label>
    <div class="log-stepper">
      <button type="button" class="btn btn--icon" data-step="-0.5" aria-label="Half an hour less"><span class="icon material-symbols-outlined" aria-hidden="true">remove</span></button>
      <input id="v-sleepHours" type="number" step="0.5" min="0" max="24" inputmode="decimal" data-indicator="sleepHours" placeholder="—" />
      <button type="button" class="btn btn--icon" data-step="0.5" aria-label="Half an hour more"><span class="icon material-symbols-outlined" aria-hidden="true">add</span></button>
      <span class="muted">${escapeHtml(t.unit)}</span>
    </div>
    <p class="tile__note">${range(t)}</p>
    ${fieldError(t.indicator)}
  </div>`;
}

function sliderTile(t) {
  return `<div class="tile log-vital">
    <div class="log-slider-head">
      <label class="tile__label" for="v-mood">${escapeHtml(t.name)}</label>
      <span class="chip chip--info chip--plain tabular" id="v-mood-out">Not set</span>
    </div>
    <input id="v-mood" type="range" min="1" max="5" step="1" value="3" data-indicator="mood" data-slider="true" />
    <div class="log-scale" aria-hidden="true"><span>1 · Low</span><span>3</span><span>5 · Good</span></div>
    <p class="tile__note">${range(t)} · move the slider to include it</p>
    ${fieldError(t.indicator)}
  </div>`;
}

function bpTile(sys, dia) {
  return `<div class="tile log-vital">
    <span class="tile__label">Blood pressure</span>
    <div class="log-bp">
      <input id="v-systolicBp" type="number" step="1" inputmode="numeric" data-indicator="systolicBp" aria-label="${escapeHtml(sys.name)}" placeholder="Sys" />
      <span class="log-bp__slash" aria-hidden="true">/</span>
      <input id="v-diastolicBp" type="number" step="1" inputmode="numeric" data-indicator="diastolicBp" aria-label="${escapeHtml(dia.name)}" placeholder="Dia" />
      <span class="muted">${escapeHtml(sys.unit)}</span>
    </div>
    <p class="tile__note">Systolic ${num(sys.okMin)}–${num(sys.okMax)} · diastolic ${num(dia.okMin)}–${num(dia.okMax)} ${escapeHtml(sys.unit)}</p>
    ${fieldError(sys.indicator)}${fieldError(dia.indicator)}
  </div>`;
}

async function saveVitals() {
  clearErrors(el.vitalsForm, el.vitalsError);
  const values = [];
  let invalid = false;
  el.vitalsFields.querySelectorAll("[data-indicator]").forEach((input) => {
    if (input.dataset.slider && input.dataset.touched !== "true") return;
    if (input.value.trim() === "") return;
    const value = Number(input.value);
    if (!Number.isFinite(value)) {
      showFieldError(el.vitalsForm, input.dataset.indicator, "Enter a number.");
      invalid = true;
      return;
    }
    values.push({ indicator: input.dataset.indicator, value });
  });
  if (invalid) return;
  if (values.length === 0) {
    showError(el.vitalsError, "Fill in at least one value.");
    return;
  }

  await submit(el.vitalsSave, el.vitalsForm, el.vitalsError, async () => {
    const created = await api.post("/api/readings/batch", { crewId, values });
    renderVitalResults(created);
    toast(`Saved ${created.length} vital${created.length === 1 ? "" : "s"}.`);
  }, saveVitals);
}

function renderVitalResults(rows) {
  el.vitalsResults.classList.remove("hidden");
  el.vitalsResults.innerHTML = rows.map((r) => {
    const s = STATUS[r.status] ?? { label: r.status, tone: "chip--muted" };
    return `<li class="log-result">
      <div class="list__main">
        <strong>${escapeHtml(r.name)}</strong>
        <span class="tabular">${escapeHtml(num(r.value))} ${escapeHtml(r.unit)}</span>
        <p class="tile__note">${escapeHtml(r.guidance)}</p>
      </div>
      <span class="chip ${s.tone}">${escapeHtml(s.label)}</span>
    </li>`;
  }).join("");
}

// ---- nutrition -----------------------------------------------------------------

function renderNutrition(nutrition) {
  const record = nutrition.days.find((d) => d.day === nutrition.today.day);
  renderDay(record, nutrition.baseline);
}

function renderDay(record, baseline) {
  if (baseline !== undefined) el.waterRing.dataset.target = baseline?.waterTargetL ? String(baseline.waterTargetL * 1000) : "";
  const personal = Number(el.waterRing.dataset.target) || 0;
  const target = personal || DEFAULT_WATER_TARGET_ML;
  const total = Math.round((record?.waterL ?? 0) * 1000);
  const pct = Math.round((total / target) * 100);

  el.waterRing.style.setProperty("--pct", Math.min(pct, 100));
  el.waterRing.setAttribute("aria-label", `Water today: ${total} of ${target} ml (${pct}%)`);
  el.waterPct.textContent = `${pct}%`;
  el.waterTotal.textContent = total.toLocaleString("en-US");
  el.waterTarget.textContent = target.toLocaleString("en-US");
  const left = target - total;
  el.waterNote.textContent = (left > 0 ? `${left.toLocaleString("en-US")} ml to go. ` : "Target reached. ")
    + (personal ? "Personal target (simulated data)." : "Default target: team demo parameter.");

  if (!record) {
    el.foodToday.textContent = "Nothing logged today yet.";
    return;
  }
  const parts = [`Today: ${num(record.kcal)} kcal`];
  const macros = [["P", record.proteinG], ["C", record.carbsG], ["F", record.fatG]]
    .filter(([, v]) => v != null).map(([k, v]) => `${k} ${num(v)} g`);
  if (macros.length) parts.push(macros.join(" / "));
  if (record.meals?.length) parts.push(`Meals: ${record.meals.join(", ")}`);
  el.foodToday.textContent = parts.join(" · ");
}

async function addWater(ml) {
  hide(el.waterError);
  await submit(el.waterButtons, null, el.waterError, async () => {
    const day = await api.post("/api/nutrition/log", { crewId, waterMl: ml });
    renderDay(day);
    toast(`Added ${ml} ml of water.`);
  }, () => addWater(ml));
}

async function saveFood() {
  clearErrors(el.foodForm, el.foodError);
  const read = (name) => {
    const v = el.foodForm.elements[name].value.trim();
    return v === "" ? undefined : Number(v);
  };
  const body = {
    crewId,
    meal: el.foodForm.elements.meal.value.trim() || undefined,
    kcal: read("kcal"),
    proteinG: read("proteinG"),
    carbsG: read("carbsG"),
    fatG: read("fatG"),
  };
  if ([body.kcal, body.proteinG, body.carbsG, body.fatG].some((v) => v !== undefined && !Number.isFinite(v))) {
    showError(el.foodError, "Calories and macros must be numbers.");
    return;
  }
  if (!body.meal && [body.kcal, body.proteinG, body.carbsG, body.fatG].every((v) => v === undefined)) {
    showError(el.foodError, "Enter a meal name, calories or a macro.");
    return;
  }

  await submit(el.foodSave, el.foodForm, el.foodError, async () => {
    const day = await api.post("/api/nutrition/log", body);
    renderDay(day);
    el.foodForm.reset();
    toast("Food saved.");
  }, saveFood);
}

// ---- symptoms ------------------------------------------------------------------

async function saveSymptoms() {
  clearErrors(el.symForm, el.symError);
  if (el.notes.value.length > 1000) {
    showError(el.symError, "Notes must be at most 1000 characters.");
    return;
  }
  await submit(el.symSave, el.symForm, el.symError, async () => {
    await api.post("/api/symptoms", {
      crewId,
      fatigue: Number(el.fatigue.value),
      headache: Number(el.headache.value),
      congestion: el.congestion.checked,
      visionChange: el.vision.checked,
      notes: el.notes.value.trim(),
    });
    resetSymptoms();
    toast("Symptoms saved.");
  }, saveSymptoms);
}

function resetSymptoms() {
  el.symForm.reset();
  el.fatigue.dispatchEvent(new Event("input"));
  el.headache.dispatchEvent(new Event("input"));
  el.notesCount.textContent = "0 / 1000";
}

// ---- medications ---------------------------------------------------------------

async function saveMed() {
  clearErrors(el.medsForm, el.medsError);
  const medication = el.medName.value.trim();
  const dose = el.medDose.value.trim();
  if (!medication || !dose) {
    showError(el.medsError, !medication ? "Enter the medication name." : "Enter the dose.");
    (!medication ? el.medName : el.medDose).focus();
    return;
  }
  await submit(el.medsSave, el.medsForm, el.medsError, async () => {
    await api.post("/api/meds/log", {
      crewId,
      medication,
      dose,
      taken: el.medTaken.checked,
      time: el.medTime.value || undefined,
      isSleepMedication: el.medSleep.checked,
    });
    el.medsForm.reset();
    await refreshMeds(token);
    toast(`${medication} logged.`);
  }, saveMed);
}

async function refreshMeds(my) {
  const meds = await api.get(`/api/meds?crewId=${q(crewId)}&date=${q(today.date)}`);
  if (my !== token) return;
  if (meds.length === 0) {
    el.medsList.innerHTML = `<li class="muted">No medications logged today.</li>`;
    return;
  }
  el.medsList.innerHTML = meds.map((m) => {
    const s = STATUS[m.taken ? "taken" : "pending"];
    return `<li>
      <div class="list__main">
        <strong>${escapeHtml(m.medication)}</strong> <span class="tabular">${escapeHtml(m.dose)}</span>
        <p class="tile__note">${m.time ? `At ${escapeHtml(m.time)}` : "No time given"}${m.isSleepMedication ? " · sleep medication" : ""}</p>
      </div>
      <span class="chip ${s.tone}">${s.label}</span>
    </li>`;
  }).join("");
}

// ---- recent entries + progress -------------------------------------------------

async function refreshRecent(my) {
  const rows = await api.get(`/api/log/recent?crewId=${q(crewId)}&limit=50`);
  if (my !== token) return;

  const todayLocal = new Date().toDateString();
  const savedToday = new Set(rows.filter((r) => new Date(r.recordedAt).toDateString() === todayLocal).map((r) => r.type));
  for (const section of SECTIONS) {
    const chip = el.root.querySelector(`[data-saved="${section}"]`);
    const saved = savedToday.has(section);
    chip.className = `chip ${saved ? "chip--ok" : "chip--muted"}`;
    chip.textContent = saved ? "Saved today" : "Not saved today";
  }
  el.progress.textContent = `${savedToday.size} of 4 sections saved today`;

  const newest = rows.slice(0, 10);
  if (newest.length === 0) {
    el.recent.innerHTML = `<li class="muted">No entries yet. Saved entries appear here.</li>`;
    return;
  }
  el.recent.innerHTML = newest.map((r) => {
    const type = TYPE[r.type] ?? { icon: "description", label: r.type };
    const s = r.status ? STATUS[r.status] : null;
    return `<li class="log-recent__row">
      <span class="log-recent__icon" aria-hidden="true"><span class="icon material-symbols-outlined">${type.icon}</span></span>
      <div class="list__main">
        <p class="log-recent__meta"><span class="label label--sm">${escapeHtml(type.label)}</span> <span class="muted tabular">${escapeHtml(formatDateTime(r.recordedAt))}</span></p>
        <p class="log-recent__summary">${escapeHtml(r.summary)}</p>
      </div>
      ${s ? `<span class="chip ${s.tone}">${escapeHtml(s.label)}</span>` : ""}
    </li>`;
  }).join("");
}

// ---- shared helpers ------------------------------------------------------------

/**
 * Runs one save: disables the button(s), shows API validation errors next to the fields
 * (or in the section), uses the shared error state for network errors, then refreshes
 * the recent entries.
 */
async function submit(buttons, form, errorEl, work, retry) {
  const my = token;
  const list = Array.isArray(buttons) ? buttons : [buttons];
  list.forEach((b) => { b.disabled = true; });
  try {
    await work();
    if (my !== token) return;
    await refreshRecent(my);
  } catch (error) {
    if (my !== token) return;
    if (error.status === 0 || error instanceof TypeError) {
      renderErrorState(errorEl, { title: "Not saved", error, compact: true, onRetry: () => { hide(errorEl); retry(); } });
      return;
    }
    const fieldErrors = error.details?.errors;
    let placed = 0;
    if (form && fieldErrors && typeof fieldErrors === "object") {
      for (const [key, messages] of Object.entries(fieldErrors)) {
        if (showFieldError(form, key, [].concat(messages).join(" "))) placed++;
      }
    }
    if (!placed || placed < Object.keys(fieldErrors ?? {}).length) showError(errorEl, error.message || "Could not save.");
  } finally {
    list.forEach((b) => { b.disabled = false; });
  }
}

function showFieldError(form, key, message) {
  const want = String(key).replace(/^\$\./, "").toLowerCase();
  const target = [...form.querySelectorAll("[data-err]")].find((p) => p.dataset.err.toLowerCase() === want);
  if (!target) return false;
  target.textContent = message;
  target.classList.remove("hidden");
  return true;
}

function clearErrors(form, errorEl) {
  form.querySelectorAll("[data-err]").forEach((p) => { p.textContent = ""; p.classList.add("hidden"); });
  hide(errorEl);
}

function showError(errorEl, message) {
  errorEl.className = "log-error";
  errorEl.setAttribute("role", "alert");
  errorEl.textContent = message;
}

function hide(errorEl) {
  errorEl.classList.add("hidden");
  errorEl.textContent = "";
}

function clearForms() {
  el.vitalsForm.reset();
  const mood = el.vitalsFields.querySelector('[data-indicator="mood"]');
  if (mood) {
    delete mood.dataset.touched;
    el.vitalsFields.querySelector("#v-mood-out").textContent = "Not set";
  }
  el.vitalsResults.classList.add("hidden");
  el.foodForm.reset();
  resetSymptoms();
  el.medsForm.reset();
  for (const [form, err] of [[el.vitalsForm, el.vitalsError], [el.foodForm, el.foodError], [el.symForm, el.symError], [el.medsForm, el.medsError]]) {
    clearErrors(form, err);
  }
  hide(el.waterError);
  toast("Form cleared. Saved entries are kept.");
}

function toast(text) {
  el.toast.textContent = text;
  el.toast.classList.remove("hidden");
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => el.toast.classList.add("hidden"), 3000);
}

function q(value) {
  return encodeURIComponent(value ?? "");
}
