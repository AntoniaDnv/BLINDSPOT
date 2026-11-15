# Nutrition, Hydration & Meds page — design

Date: 2026-11-14 · Branch: `feature/nutrition-hydration-meds` · Size: small change (one page)

## Intent

A crew-facing page that answers "am I eating, drinking and taking sleep meds safely?" for one astronaut on one mission day. It follows the Stitch mockup `nutrition_hydration_meds` for look and layout, but shows **only what the BLINDSPOT plan supports** (plan section 6.5 data model, 6.6 rules).

## Goals

- Show today's **energy intake** vs personal target (ring), **water** vs personal target (segmented bar), and **sleep medication taken last night** (yes/no).
- Show the plan's alerts for these three systems, each with What / Why / Action / Source.
- Show a simple **last 7 days** view for kcal % and water (the plan's "daily rings with a 3-day trend").
- Match the mockup look: dark navy surfaces, teal accent, Inter font, rounded cards, small uppercase labels.

## Non-goals (in the mockup, not in the plan → dropped)

Protein / carbs / lipids split, electrolyte reserve, dispenser sync, hourly water target, drug name and dose, dose timestamps, pharmacokinetic clearance %, half-life, "residual drowsiness", "Safe for EVA", flight surgeon notes, mission/uplink header stats, the left sidebar and top bar (those belong to the app shell, person B).

## Anti-goals

- Any number on screen without a field in the plan's data model.
- Any threshold without a source or a "demo parameter" label.
- Restyling other people's pages.
- React, Angular or any framework or build step.

## Constraints

- HTML for structure, CSS for all visuals, plain JavaScript for behaviour. No framework.
- Only own files plus the allowed one-line edits (README §3): `router.js` route, `index.html` nav link, CSS appended at the end of `style.css` under a page comment.
- English only. Disclaimer line: simulated data, not a medical device.
- Contrast ≥ 4.5:1, body text ≥ 16 px, status shown with icon + text, never colour alone (plan Step 6).

## Approaches considered

1. **Frontend only, own sample JSON (chosen).** Page reads `wwwroot/data/nutrition-days.json`. Nothing else in the repo changes. Later, swap one URL for D's `/api/crew/{id}/days`.
2. **C# endpoint with seed data.** Rejected: duplicates the crew API person D owns (Steps 2–4) and causes conflicts in shared C# files.
3. **Paste the Stitch `code.html`.** Rejected: brings Tailwind via CDN, clashes with the app's CSS, and most of it is dropped content.

## Decisions

- **Data file:** `src/HealthMonitor.Api/wwwroot/data/nutrition-days.json`, shaped like the plan:
  - `crew`: 3 fictional members `{ id, name, role, baseline: { kcalTarget, waterTargetL } }`
  - `days`: `{ crewId, day, kcal, waterL, hypnoticTaken }` for days 1–60
  - `alerts`: `{ crewId, day, level: advisory|caution|warning, system: food|water|sleepMeds, what, why, action, source }`
  - Alerts are **written into the JSON** (precomputed), as if D's rules engine produced them, so the page can later switch to D's output without changing shape. Rules used: food < 80% of target 3 days in a row (Smith 2005); water < 2.0 L 2 days in a row (demo parameter).
  - The plan's sleep-med rules (2 hypnotics in one night; hypnotic within 8 h of an EVA) **cannot fire** from this data: `hypnoticTaken` is yes/no and there is no task schedule. The meds card shows "taken last night" and how many nights in a row, with no alert.
  - Scripted so the demo shows something: e.g. one crew member's intake drops below 80% around days 44–46 and they take a sleep med, matching the plan's days 44–46 fatigue story.
- **Page files:** `wwwroot/js/pages/nutrition/nutrition.html` + `nutrition.js`, route `/nutrition`, nav label "Nutrition & Meds".
- **Layout (from the mockup):** header (eyebrow + title + crew/day pickers) → row of two cards (Energy ring | Hydration) → full-width Sleep Medication card → alerts list → 7-day strip → disclaimer.
- **Visuals in CSS, not JS:** ring = `conic-gradient` driven by a CSS custom property `--pct`; water bar = 10 segments toggled by a class; 7-day strip = bars whose height comes from `--pct`. JS only sets numbers, text and classes.
- **Crew and day pickers:** two plain `<select>` elements on the page until the shell's crew selector and day slider exist (person B). Defaults: first crew member, day 46.
- **Theme scoping:** all page styles live under `.nhm` in a block at the end of `style.css`, with page-local custom properties (`--nhm-surface`, `--nhm-accent`, …) taken from the mockup's `DESIGN.md`. Status colours reuse the app's `--color-ok / warning / critical / info`. Inter comes from Google Fonts via `@import` in that block, falling back to the app font.
- **JavaScript:** one page module handles the behaviour: fetch JSON, read the pickers, fill `textContent`, set `--pct` and status classes, count medication nights in a row. Plain JS (user confirmed JS is fine for functionality); no framework, no chart library. Uses `textContent` per README rules.

## Left to the builder

- Exact sample values (inside the plan's ranges: target 2,500–3,000 kcal, actual ~80%; water target set per person).
- Exact wording of the alert texts (must cite the sources above).
- Small spacing/size choices within the mockup's style.

## Hand-off notes for the JavaScript teammate

- When `/api/crew/{id}/days` and the alerts endpoint exist, replace the single `api.get("/data/nutrition-days.json")` call, and wire the pickers to the shell's crew selector and day slider.
