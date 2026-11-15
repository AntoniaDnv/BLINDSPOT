// Crew Overview (flight surgeon only): one card per crew member with a data summary.
import { api } from "../../api.js";
import { escapeHtml, renderErrorState, renderLoading } from "../../utils.js";
import { getActiveCrewId, getCrewList, setActiveCrewId } from "../../session.js";
import { navigate } from "../../router.js";

const RANK = { ok: 0, info: 0, warning: 1, critical: 2 };
const TONE = { ok: "chip--ok", warning: "chip--warning", critical: "chip--critical" };
const LABEL = { ok: "Nominal", warning: "Warning", critical: "Critical" };

let token = 0;

export async function init(root) {
  const my = ++token;
  const grid = root.querySelector("#crew-grid");
  const crew = getCrewList();

  if (crew.length === 0) {
    grid.innerHTML = emptyState("No crew members yet", "No astronaut accounts exist yet.");
    return;
  }
  renderLoading(grid, "Loading crew overview");

  try {
    const summaries = await Promise.all(crew.map(loadSummary));
    if (my !== token) return;
    grid.innerHTML = summaries.map(card).join("");
    grid.addEventListener("click", (event) => {
      const button = event.target.closest("[data-open]");
      if (!button) return;
      setActiveCrewId(button.dataset.open);
      navigate("/");
    });
  } catch (error) {
    if (my !== token) return;
    renderErrorState(grid, { title: "Could not load the crew overview", error });
  }
}

export function destroy() {
  token++;
}

async function loadSummary(member) {
  const id = encodeURIComponent(member.crewId);
  const [latest, blood] = await Promise.all([
    member.hasVitals ? api.get(`/api/readings/latest?crewId=${id}`) : [],
    member.hasBlood ? api.get(`/api/bloodpanels?crewId=${id}`) : [],
  ]);

  let worst = null;
  let attention = 0;
  for (const r of latest) {
    const s = r.status in RANK ? r.status : "ok";
    if (!worst || RANK[s] > RANK[worst]) worst = s;
    if (RANK[s] > 0) attention++;
  }

  let outOfRange = [];
  let timepoint = "";
  if (blood.length > 0) {
    const maxDay = Math.max(...blood.map((b) => b.dayOffset));
    const last = blood.filter((b) => b.dayOffset === maxDay);
    timepoint = last[0].timepoint;
    outOfRange = last.filter((b) => b.status && b.status !== "ok").map((b) => b.analyte);
  }
  return { member, latest, worst, attention, blood, outOfRange, timepoint };
}

function card({ member, latest, worst, attention, blood, outOfRange, timepoint }) {
  const active = member.crewId === getActiveCrewId();
  const vitals = latest.length
    ? `<span class="chip ${TONE[worst] ?? "chip--ok"}">${LABEL[worst] ?? "Nominal"}</span>
       <span class="muted">${attention === 0 ? "All" : attention + " of"} ${latest.length} indicators ${attention === 0 ? "in range" : "need attention"}</span>`
    : `<span class="chip chip--muted">No data</span><span class="muted">No vitals logged yet</span>`;
  const bloodLine = blood.length
    ? `<span class="chip ${outOfRange.length ? "chip--warning" : "chip--ok"}">${outOfRange.length ? outOfRange.length + " out of range" : "In range"}</span>
       <span class="muted">${escapeHtml(timepoint)}${outOfRange.length ? ": " + escapeHtml(outOfRange.slice(0, 4).join(", ")) + (outOfRange.length > 4 ? ", ..." : "") : ""}</span>`
    : `<span class="chip chip--muted">No data</span><span class="muted">No blood results yet</span>`;
  const avail = [
    ["Vitals", member.hasVitals],
    ["Blood", member.hasBlood],
    ["Nutrition", member.hasNutrition],
  ]
    .map(([n, on]) => `<span class="chip chip--plain ${on ? "chip--info" : "chip--muted"}">${n}: ${on ? "available" : "none"}</span>`)
    .join("");

  return `
    <article class="card crew-card${active ? " crew-card--active" : ""}">
      <header class="crew-card__head">
        <div>
          <h2 class="crew-card__name">${escapeHtml(member.displayName)}</h2>
          <p class="label label--sm tabular">${escapeHtml(member.crewId)}</p>
        </div>
        ${active ? '<span class="chip chip--info">Viewing</span>' : ""}
      </header>
      <dl class="crew-card__rows">
        <div><dt class="label label--sm">Vitals (simulated)</dt><dd class="cluster">${vitals}</dd></div>
        <div><dt class="label label--sm">Blood (latest timepoint)</dt><dd class="cluster">${bloodLine}</dd></div>
        <div><dt class="label label--sm">Data available</dt><dd class="cluster">${avail}</dd></div>
      </dl>
      <button type="button" class="btn btn--primary btn--sm" data-open="${escapeHtml(member.crewId)}">
        Open ${escapeHtml(member.crewId)}
        <span class="icon material-symbols-outlined icon--sm" aria-hidden="true">arrow_forward</span>
      </button>
    </article>`;
}

function emptyState(title, text) {
  return `<div class="state state--empty"><div class="state__body">
    <p class="state__title">${escapeHtml(title)}</p><p class="state__text">${escapeHtml(text)}</p></div></div>`;
}
