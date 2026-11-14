# Nutrition, Hydration & Meds Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `/nutrition` page showing one astronaut's energy intake, water and sleep-medication status for a chosen mission day, styled after the Stitch mockup, using only plan-backed data.

**Architecture:** A static sample file `wwwroot/data/nutrition-days.json` (plan §6.5 shape, alerts precomputed) is loaded by a thin page module. HTML holds the full markup; CSS draws the ring, bars and segments from a `--pct` custom property; JS only fills text, sets `--pct` and toggles classes.

**Tech Stack:** Plain HTML, CSS, one ES module (no framework, no chart library). Existing `api.js` and router. Node 18 only for one-off checks (not committed).

**Spec:** `docs/superpowers/specs/2026-11-14-nutrition-hydration-meds-design.md`

## Global Constraints

- English only. No React, Angular, framework, build step or new library.
- Touch only: `wwwroot/data/nutrition-days.json`, `wwwroot/js/pages/nutrition/*`, a block appended to the end of `wwwroot/css/style.css` under `/* ===== Nutrition page ===== */`, one route line in `wwwroot/js/router.js`, one nav link in `wwwroot/index.html`.
- All page CSS scoped under `.nhm`. Status colours reuse `--color-ok / --color-warning / --color-critical / --color-info`.
- Mockup tokens (from Stitch `DESIGN.md`): surface `#0e131f`, card `#1b1f2c`, card-high `#252a37`, border `#3e484f`, text `#dee2f4`, muted `#bdc8d1`, accent `#44e2cd`, accent-2 `#8ed5ff`; font Inter (Google Fonts `@import`), fallback `var(--font)`.
- Text ≥ 16 px for body, contrast ≥ 4.5:1, status = icon/word + colour, never colour alone.
- Every number shown has a plan field: `kcal`, `baseline.kcalTarget`, `waterL`, `baseline.waterTargetL`, `hypnoticTaken`. Nothing else.
- API/user text goes in via `textContent` only.
- Visible disclaimer: "Simulated data. BLINDSPOT is a student hackathon prototype, not a medical device."

## Review Focus

1. **Day with no record** for the chosen crew → cards show "No data for day N", no `NaN`, alerts empty.
2. **JSON fails to load** (file missing / server down) → error message in `#nhm-message`, page does not crash.
3. **Intake above target** (e.g. 105%) → text shows real %, ring is capped at full (no overflow artefact).
4. **Switching crew keeps the chosen day** and re-renders everything (no stale alerts from the previous crew).
5. **Day 1–6 in the 7-day strip** → only existing days drawn, no empty/broken bars for days < 1.

Each is checked in Task 4, Step 4.

---

### Task 1: Sample data file

**Files:**
- Create: `src/HealthMonitor.Api/wwwroot/data/nutrition-days.json`

**Interfaces:**
- Produces: JSON object
  ```
  { note: string,
    crew:   [{ id, name, role, baseline: { kcalTarget: number, waterTargetL: number } }],
    days:   [{ crewId, day: 1..60, kcal: number, waterL: number, hypnoticTaken: boolean }],
    alerts: [{ crewId, day, level: "advisory"|"caution"|"warning", system: "food"|"water",
               what, why, action, source }] }
  ```

- [ ] **Step 1: Generate the file** with a one-off seeded script kept in the scratchpad (not committed). Values:
  - `note`: "Simulated crew data for the BLINDSPOT prototype. Not real astronaut data."
  - Crew (fictional, scripted one first): `crew-1` "Sam Okafor", Flight Engineer, 2600 kcal, 2.3 L · `crew-2` "Alex Rowan", Commander, 2800 kcal, 2.5 L · `crew-3` "Riley Chen", Mission Specialist, 3000 kcal, 2.7 L.
  - Normal days: kcal 82–96% of target, water target ±0.3 L (never < 2.0), hypnoticTaken true on ~1 in 8 nights, rounded (kcal to 10, water to 0.1).
  - Scripted (`crew-1`, plan days 44–46 fatigue story): kcal ~70% of target days 44–47; water 1.8, 1.9, 1.7 on days 45–47; hypnoticTaken true days 44–46.
  - Alerts computed from the data, one per crew/day/system while the condition holds:
    - food, `caution`: kcal < 80% of target on this day and the 2 days before. what "Eating less than 80% of target for 3 days", why "ISS crews ate about 80% of their energy needs, and under-eating leads to weight loss", action "Plan an extra snack and tell the CMO if it continues", source "Smith SM et al., J Nutr 2005; NASA Risk of Inadequate Food and Nutrition".
    - water, `caution`: waterL < 2.0 on this day and the day before. what "Less than 2.0 L of water for 2 days", why "In-flight dehydration is a known spaceflight problem", action "Drink a pouch now and log water every few hours", source "Demo parameter; NASA Risk of Inadequate Food and Nutrition".

- [ ] **Step 2: Verify the file** with a one-off Node check (run from repo root):
  ```
  node -e "const d=require('./src/HealthMonitor.Api/wwwroot/data/nutrition-days.json'); /* assert below */"
  ```
  Assertions: 3 crew; 180 days, each crew has days 1–60 once; every alert matches its rule recomputed from `days` and every rule hit has an alert (both directions); `crew-1` has food alerts on days 46–47 and water alerts on days 46–47; no other crew has any alert. Expected: prints `OK`.

- [ ] **Step 3: Commit**
  ```bash
  git add src/HealthMonitor.Api/wwwroot/data/nutrition-days.json
  git commit -m "Add simulated nutrition, water and sleep-med sample data"
  ```

### Task 2: Page markup, route and nav link

**Files:**
- Create: `src/HealthMonitor.Api/wwwroot/js/pages/nutrition/nutrition.html`
- Create: `src/HealthMonitor.Api/wwwroot/js/pages/nutrition/nutrition.js` (stub: `export function init(root) {}`)
- Modify: `src/HealthMonitor.Api/wwwroot/js/router.js` (add `"/nutrition": "nutrition",` to `routes`)
- Modify: `src/HealthMonitor.Api/wwwroot/index.html` (add `<a href="#/nutrition" data-route="/nutrition">Nutrition &amp; Meds</a>` before the "Add a link" comment)

**Interfaces:**
- Produces element ids/classes used by Tasks 3–4, all inside a root `<section class="nhm">`:
  - header: `#nhm-crew` (select), `#nhm-day` (select), `#nhm-crew-role`, `#nhm-message`
  - energy card `.nhm-card.nhm-energy`: `.nhm-ring` (style `--pct`), `#nhm-kcal`, `#nhm-kcal-target`, `#nhm-kcal-pct`, `#nhm-energy-status` (badge)
  - water card `.nhm-card.nhm-water`: `#nhm-water`, `#nhm-water-target`, `#nhm-water-left`, `.nhm-segments` with 10 `<span class="nhm-seg">`, `#nhm-water-status`
  - meds card `.nhm-card.nhm-meds`: `#nhm-hypnotic` ("Yes"/"No"), `#nhm-hypnotic-streak`, `#nhm-meds-status`
  - `#nhm-alerts` (`<ul>`), `<template id="nhm-alert-tpl">` with `.nhm-alert-level`, `.nhm-alert-what`, `.nhm-alert-why`, `.nhm-alert-action`, `.nhm-alert-source`
  - 7-day strip: `#nhm-week-kcal`, `#nhm-week-water` (containers for bars), `#nhm-week-days` (labels)
  - footer `.nhm-disclaimer` with the Global Constraints disclaimer text
- Default text in every value slot: `—`.

- [ ] **Step 1: Write the markup** in the layout order from the spec: header (eyebrow "Nutrition · Hydration · Sleep meds", `<h1>` "Nutrition, Hydration & Meds", crew + day selects with `<label>`s) → two-card row (energy, water) → meds card → alerts → 7-day strip → disclaimer.
- [ ] **Step 2: Add route and nav link.**
- [ ] **Step 3: Verify** — `cd src/HealthMonitor.Api && dotnet run`, open `http://localhost:5080/#/nutrition`. Expected: page loads (no "Failed to load page"), nav link highlighted, all slots show `—`, F12 console has no errors.
- [ ] **Step 4: Commit**
  ```bash
  git add src/HealthMonitor.Api/wwwroot/js/pages/nutrition src/HealthMonitor.Api/wwwroot/js/router.js src/HealthMonitor.Api/wwwroot/index.html
  git commit -m "Add Nutrition & Meds page markup and route"
  ```

### Task 3: Page styles (mockup look)

**Files:**
- Modify: `src/HealthMonitor.Api/wwwroot/css/style.css` (append block at end only)

**Interfaces:**
- Consumes: Task 2 classes/ids.
- Produces CSS contract for Task 4: `.nhm-ring` reads `--pct` (0–100, unitless); `.nhm-bar` reads `--pct` for height; `.nhm-seg.is-on` = filled segment; `[data-status="ok|caution|warning|none"]` on `.nhm-card` sets the card's accent edge.

- [ ] **Step 1: Write the block.** Page-local tokens on `.nhm` from Global Constraints; cards rounded 12 px with 1 px border; small uppercase labels (0.75 rem, letter-spacing 0.06em, muted); big numbers ~3 rem weight 500. Ring: `conic-gradient(var(--nhm-accent) calc(min(var(--pct),100) * 1%), var(--nhm-card-high) 0)` with a masked centre. Two-card row = CSS grid, stacks to one column under 760 px.
- [ ] **Step 2: Verify** — temporarily set `style="--pct:82"` on the ring and `is-on` on 7 segments in DevTools. Expected: ring ~82% filled, 7 teal segments, matches the mockup's feel; at 375 px width no horizontal scroll; other pages (Home, Blood Panels) look unchanged.
- [ ] **Step 3: Commit**
  ```bash
  git add src/HealthMonitor.Api/wwwroot/css/style.css
  git commit -m "Style Nutrition & Meds page after the Stitch mockup"
  ```

### Task 4: Fill the page from the data

**Files:**
- Modify: `src/HealthMonitor.Api/wwwroot/js/pages/nutrition/nutrition.js`

**Interfaces:**
- Consumes: Task 1 JSON via `api.get("/data/nutrition-days.json")`; Task 2 ids; Task 3 CSS contract; `showMessage(el, text, "error")` from `utils.js`.
- Produces: `export async function init(root)`; `export function destroy()` (no-op).

- [ ] **Step 1: Implement** (plain JS; alerts are read from the data, not recomputed):
  - Load JSON once; fill `#nhm-crew` from `crew`, `#nhm-day` with 1–60; defaults `crew-1`, day 46; `change` on either → `render()`.
  - `render()`: find the day record → set numbers, `--pct` on ring (`round(kcal / kcalTarget * 100)`), segments on = `round(min(waterL / waterTargetL, 1) * 10)`, "x.x L remaining" (or "Target reached"); meds "Yes"/"No" and streak = consecutive nights with `hypnoticTaken` ending today ("3 nights in a row").
  - Card status: the highest `level` among today's alerts for that system → badge text ("Caution") + `data-status`; no alert → "On track" / `ok`. Meds card: always `info` badge "Logged" (no plan rule can fire from yes/no alone).
  - Alerts list: clone template per today's alert, fill via `textContent`; none → one muted item "No alerts for this day."
  - 7-day strip: days `max(1, day-6)`…`day`, one `.nhm-bar` each with `--pct` (kcal % capped 100; water % of target capped 100) and a day label.
- [ ] **Step 2: Verify the scripted story** — open `#/nutrition`. Expected at default (Sam Okafor, day 46): energy badge "Caution", water badge "Caution", meds "Yes · 3 nights in a row", 2 alerts with sources, ring ~70%.
- [ ] **Step 3: Verify a normal case** — Alex Rowan, day 20. Expected: both badges "On track", "No alerts for this day.", ring 82–96%.
- [ ] **Step 4: Verify Review Focus** —
  1. In DevTools remove one day from the loaded data (or pick a day after deleting it from a local copy) → "No data for day N", no `NaN`.
  2. Rename the JSON temporarily → red error message in `#nhm-message`, no console crash. Rename back.
  3. Force a record to 105% in DevTools → text "105%", ring full, no artefact.
  4. Sam day 46 → switch crew to Riley → day stays 46, alerts list clears.
  5. Day 3 → strip shows 3 bars (days 1–3).
  Also: F12 console clean; Tab reaches both selects.
- [ ] **Step 5: Commit**
  ```bash
  git add src/HealthMonitor.Api/wwwroot/js/pages/nutrition/nutrition.js
  git commit -m "Fill Nutrition & Meds page from sample data"
  ```

### Task 5: Wrap-up

**Files:**
- Add: `docs/superpowers/specs/2026-11-14-nutrition-hydration-meds-design.md`, this plan.

- [ ] **Step 1:** `git pull origin main` into the branch; re-run Task 4 Step 2. Expected: same result, no conflicts (or only the router/nav one-liners: keep both lines).
- [ ] **Step 2: Commit docs**
  ```bash
  git add docs/superpowers
  git commit -m "Add Nutrition & Meds design and plan"
  ```
- [ ] **Step 3:** Push and open a PR only after the user says so.
