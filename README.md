# BLINDSPOT

**Health monitoring for astronauts who are too far from Earth to ask a doctor.**

NASA Space Apps Challenge 2026 · *Create Health Monitoring Software for Astronauts on Space Missions*

BLINDSPOT helps astronauts on long missions check their own health. It shows vital signs against limits from medical guidelines. It tracks blood test results using **real NASA astronaut data**, comparing each person with their own pre-flight baseline. It gives **live radiation alerts** from NASA's space weather database, with clear advice on what to do. Astronauts log their daily data; a flight surgeon sees the whole crew.

> BLINDSPOT is a hackathon prototype, not a medical device. It does not diagnose or treat.

- **Code:** https://github.com/AntoniaDnv/BLINDSPOT
- **Live app:** `[ add Render URL ]`

---

## The problem

On missions to the Moon and Mars, astronauts are far from Earth and help from the ground can take a long time, so they must assess their own health. NASA groups the dangers of spaceflight into five hazards (RIDGE): space **R**adiation, **I**solation and confinement, **D**istance from Earth, **G**ravity fields and **H**ostile/closed **E**nvironments ([NASA](https://www.nasa.gov/humans-in-space/the-human-body-in-space/)).

A blood value can be inside the normal lab range and still be a big change *for that person*. BLINDSPOT is built to catch those blind spots.

## What we found in the real data

We compared each Inspiration4 crew member's average before launch with their value the day after landing (NASA OSDR [OSD-569](https://osdr.nasa.gov/bio/repo/data/studies/OSD-569)):

- **3 of the 4 crew had 20-34% fewer lymphocytes** (a type of immune cell) the day after landing: −27%, −34% and −20%. The fourth was unchanged (+2%).
- By 45 days after landing, values were back near or above each person's own pre-flight average.
- **Every value stayed inside the lab's normal range.** A normal-range check shows nothing; a personal-baseline check shows a clear change. This matches NASA's [Risk of Altered Immune System Responses](https://www.nasa.gov/reference/risk-of-altered-immune-system-responses/).

## Features

| Page | What it shows | Data |
|---|---|---|
| **Daily Readiness & Vitals** | Heart rate, blood pressure, blood oxygen, temperature, sleep and mood as ok / warning / critical, with the normal range, change versus the astronaut's own recent baseline, guidance and the related NASA risk; weekly trends | Simulated readings, limits from AHA / MedlinePlus (sources in `docs/research.md`) |
| **Blood Biomarkers & Lab** | Blood counts and metabolic panel per crew member and timepoint (3 before launch, 4 after landing), lab range, change versus own pre-flight average, trend charts, plain-language explanations | **Real**: Inspiration4, NASA OSDR OSD-569 / OSD-575 |
| **Radiation & Environment** | Live solar particle event alert (OK / Warning / Critical) with crew actions; a "Replay May 2024 storm" button shows a real historic storm | **Real**: NASA DONKI SEP; rules from NOAA SWPC, actions from NASA SRAG |
| **Nutrition, Hydration & Meds** | Calories, macros, water and sleep medication per day | Simulated crew data |
| **Log & Data Entry** | Astronauts log vitals, food and water, symptoms and medications; each entry is checked immediately and shows up on the matching page | Saved on the server |
| **Crew Overview** (flight surgeon) | One card per crew member with their status at a glance | All crew |

### Accounts and roles

- **Astronaut:** signs up or logs in and sees **only their own** health data.
- **Flight surgeon:** sees **every crew member**, can switch between them on every page and log on their behalf. Flight surgeon accounts are created in advance; sign-up always creates an astronaut, so nobody can make themselves a doctor.
- Access is enforced **on the server** (an astronaut asking for another crew member's data gets 403) and covered by automated tests.

### Demo accounts

Test accounts for trying the app (the `.test` domain is reserved, so these can never belong to a real person):

| Email | Password | Role |
|---|---|---|
| `c001@blindspot.test` | `Dragon-Orbit-7341` | Astronaut, crew C001 |
| `c002@blindspot.test` | `Dragon-Orbit-5826` | Astronaut, crew C002 |
| `c003@blindspot.test` | `Dragon-Orbit-2958` | Astronaut, crew C003 |
| `c004@blindspot.test` | `Dragon-Orbit-4617` | Astronaut, crew C004 |
| `surgeon@blindspot.test` | `Flight-Surgeon-9052` | Flight surgeon (all crew) |

C001-C004 are the four Inspiration4 crew members, kept anonymous.

## How it works

- **Backend:** ASP.NET Core (.NET 10) API, EF Core in-memory database seeded from JSON/CSV files at startup, cookie login with hashed passwords (ASP.NET Core `PasswordHasher`), role-based access, rate-limited login. API docs at `/scalar`.
- **Frontend:** plain HTML, CSS and JavaScript (no framework, no build step), Chart.js charts, Inter and Material Symbols fonts. Design system adapted from our Google Stitch mockups ("Aerospace Clinical Telemetry").
- **NASA DONKI** is called through the backend, so the API key stays on the server; responses are cached, and the May 2024 storm is stored in the app so the replay works even when NASA rate-limits the key.
- **Hosting:** one Docker container on Render.

## Run it locally

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
cd src/HealthMonitor.Api
dotnet run
```

Open http://localhost:5080 and log in with a demo account. Run the tests:

```bash
dotnet test
```

## Deploy on Render

1. Render → **New → Web Service** → connect this repo → runtime **Docker** (uses the `Dockerfile` in the repo root).
2. Environment variables:
   - `ExternalApi__ApiKey` = your free NASA API key from https://api.nasa.gov (the shared `DEMO_KEY` is rate-limited). Never commit it.
   - `PORT` = `8080`
3. Deploy, then open the URL and log in with a demo account.

## Limits

- Only 4 people and a 3-day flight. Long missions (months to years) may look very different.
- No blood samples were taken during the flight, only before and after.
- Some values are affected by sample handling (low glucose and high potassium at one post-flight visit).
- Normal ranges come from an Earth lab and can differ between labs.
- Vital signs and nutrition in the demo are simulated.
- The database lives in memory: accounts and entries created during a demo disappear when the server restarts (demo accounts and NASA data always come back).
- Sign-up says when an email is already registered (no email verification in this prototype).

## Sources, data and AI use

- Every claim, threshold and source: [`docs/research.md`](docs/research.md)
- NASA data: [OSD-569](https://osdr.nasa.gov/bio/repo/data/studies/OSD-569), [OSD-575](https://osdr.nasa.gov/bio/repo/data/studies/OSD-575), [DONKI](https://api.nasa.gov/), [NASA HRP risks](https://www.nasa.gov/hrp/risks/), [NASA SRAG](https://srag.jsc.nasa.gov/SpaceRadiation/How/How.cfm)
- Other: [NOAA SWPC space weather scales](https://www.swpc.noaa.gov/noaa-scales-explanation); Jones CW et al. (2024), *Nature*, https://doi.org/10.1038/s41586-024-07648-x
- AI use: Claude (Anthropic) via Claude Code for sources, documentation and code; Google Stitch for UI mockups. Details in `docs/research.md`.

## Team

`[ names and roles ]` · Project skeleton by Aleksander Milev (teammate and mentor); his original setup guide is in [`docs/SKELETON_GUIDE.md`](docs/SKELETON_GUIDE.md).
