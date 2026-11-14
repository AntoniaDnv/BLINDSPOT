# Research

The single source of truth for BLINDSPOT. The README, the deck and the project page are built from this file.

**Rules:** English only. Reputable sources only (NASA, CSA, ESA, peer-reviewed papers, official medical bodies). No source = it doesn't go in.

---

## The problem: astronauts must check their own health

- On long missions, astronauts are far from Earth and must assess their own health.
- NASA groups the dangers of spaceflight into **5 hazards ("RIDGE")**: Space **R**adiation, **I**solation and confinement, **D**istance from Earth, **G**ravity fields, **H**ostile/closed **E**nvironments.
- Source: https://www.nasa.gov/humans-in-space/the-human-body-in-space/ (NASA, "The Human Body in Space")
- NASA's full list of human spaceflight risks: https://www.nasa.gov/hrp/risks/ (NASA Human Research Program, "Risks of Human Spaceflight")

## Our data: real astronaut blood tests (Inspiration4)

- Blood tests from the 4 crew of **Inspiration4**, the first all-civilian orbital mission (3 days, 2021).
- Tests before launch (L-92, L-44, L-3 = days before launch) and after return (R+1, R+45, R+82, R+194 = days after landing). **No samples were taken during the flight.**
- Complete blood count (CBC): NASA OSDR study OSD-569, https://osdr.nasa.gov/bio/repo/data/studies/OSD-569
- Comprehensive metabolic panel (CMP): NASA OSDR study OSD-575, https://osdr.nasa.gov/bio/repo/data/studies/OSD-575
- Main paper on the mission: Jones CW et al., "Molecular and physiological changes in the SpaceX Inspiration4 civilian crew", *Nature* 2024. https://doi.org/10.1038/s41586-024-07648-x

## Real astronaut data finding: lymphocytes after landing (deck slide 5)

- We compared each crew member's **average before launch** with their value **the day after landing (R+1)**, using `blood-panels.json` (OSD-569).
- **3 of 4 crew had 20–34% fewer lymphocytes at R+1**: C001 −27%, C002 −34%, C003 −20%. C004 was unchanged (+2%).
- By R+45 the values were close to, or above, each person's own pre-flight average.
- All values, even at R+1, stayed inside the lab's normal range: this is a change **compared with the person's own baseline**, which is exactly what BLINDSPOT looks for.
- Why it matters: spaceflight changes the immune system. NASA lists this as the "Risk of Altered Immune System Responses".
- Caveats: only 4 people, a 3-day flight, and C003 has only 2 pre-flight values (one row in the raw data was corrupted).
- Sources: OSD-569 (above); https://www.nasa.gov/reference/risk-of-altered-immune-system-responses/

## Live space weather: radiation alerts (deck slide 6)

- Astronauts are exposed to more radiation in space than on Earth, which can raise cancer risk. Missions to the Moon and Mars leave the protection of Earth's magnetic field.
- Source: https://www.nasa.gov/reference/risk-of-radiation-induced-cancers/ (NASA, "Risk of Radiation-Induced Cancers")
- One source of space radiation is **solar energetic particles** from the Sun.
- Source: https://www.nasa.gov/humans-in-space/the-human-body-in-space/
- Live data: NASA **DONKI** (Space Weather Database Of Notifications, Knowledge, Information) reports solar energetic particle events. API: https://api.nasa.gov/ (endpoint `DONKI/SEP`)

### Radiation alert rules (used by `RadiationService.cs`)

How the backend turns DONKI events into ok / warning / critical. Each line has a source or is marked **team demo parameter** (our own choice, no official rule), as in `vital-thresholds.json`.

**Thresholds**

- **Warning: an event seen on a `>10 MeV` channel.** NOAA measures solar radiation storms with protons of **≥10 MeV**. A storm starts when that flux reaches **10 pfu** (proton flux units), which is level **S1 (Minor)**. Source: https://www.swpc.noaa.gov/phenomena/solar-radiation-storm (NOAA SWPC, "Solar Radiation Storm") and https://www.swpc.noaa.gov/noaa-scales-explanation (NOAA Space Weather Scales, S-scale table)
  - Calling this a "warning" is a **team demo parameter**. NOAA lists no biological effect at S1 ("None"). The first level that mentions astronauts is **S3 (Strong, 1,000 pfu)**: "Radiation hazard avoidance recommended for astronauts on EVA". S4 and S5 say the hazard to astronauts on EVA is "unavoidable". Source: https://www.swpc.noaa.gov/noaa-scales-explanation
- **Critical: an event seen on a `>100 MeV` channel.** NOAA tracks ≥100 MeV protons separately and sends an alert when they reach **1 pfu**. Source: https://www.swpc.noaa.gov/products/goes-proton-flux (NOAA SWPC, "GOES Proton Flux") and https://www.swpc.noaa.gov/phenomena/solar-radiation-storm
  - Calling this "critical" is a **team demo parameter**. NOAA does not link the ≥100 MeV alert to a specific astronaut action.
- Caveat: our code checks the **energy channel named** in each DONKI event (for example "GOES-P: SEISS >100 MeV"). It does not read the flux value. We did not find a DONKI page that says which flux level makes DONKI log an SEP event, so the link "DONKI event = NOAA threshold crossed" is a **team demo parameter**.

**Guidance text**

The actions come from NASA's Space Radiation Analysis Group (SRAG), which watches space weather for crews. Source for all four: https://srag.jsc.nasa.gov/SpaceRadiation/How/How.cfm (NASA SRAG, "How do we protect the astronauts from space radiation?")

- **Shelter in a better-shielded area:** SRAG can recommend that the crew "shelter in higher shielded areas of the spacecraft". For Artemis, Orion's crew will build a shelter from stowage bags: https://science.nasa.gov/science-research/heliophysics/how-nasa-will-protect-astronauts-from-space-radiation-at-the-moon (NASA, "How NASA Will Protect Astronauts From Space Radiation at the Moon")
  - NASA says "higher shielded", not "most shielded". "The most heavily shielded area" is our wording.
- **No EVAs / postpone EVAs:** SRAG can recommend to "delay an EVA, or return from an ongoing EVA". NASA's radiation standard also calls EVAs a main risk during solar particle events: https://www.nasa.gov/wp-content/uploads/2023/03/radiation-protection-technical-brief-ochmo.pdf (NASA-STD-3001 Technical Brief OCHMO-TB-020)
- **Flight surgeons:** SRAG gives "recommendations to the Flight Surgeon in order to mitigate radiation exposure". The advice goes from SRAG *to* the flight surgeon; the crew then follows the flight surgeon.
- **Monitor the dosimeter:** Crew Active Dosimeters "continuously measure and log each astronaut's radiation exposure". Artemis II astronauts also wear them: https://science.nasa.gov/missions/artemis/artemis-2/to-protect-artemis-ii-astronauts-nasa-experts-keep-eyes-on-sun/ (NASA, "To Protect Artemis II Astronauts, NASA Experts Keep Eyes on Sun")
- **Which action goes with which level** (shelter at critical, postpone EVAs at warning) is a **team demo parameter**. NASA decides case by case. For Artemis II, "a higher threshold triggers a recommendation for the crew to take shelter" (same Artemis II page), but no number is published.

**Final guidance text (for the backend)**

| Status | Rule | Guidance |
|---|---|---|
| ok | No DONKI SEP event, or none on a ≥10 MeV channel | "No solar particle events detected." |
| warning | Event on a ≥10 MeV channel | "Solar particle event detected. Postpone EVAs and keep checking your dosimeter." |
| critical | Event on a ≥100 MeV channel | "Strong solar particle event. Move to the most heavily shielded area, do not start EVAs and end any EVA in progress. Follow flight surgeon instructions." |

---

## Blood tests (analytes)

Each section: what the test measures, and how it links to spaceflight. The short versions are in `src/HealthMonitor.Api/wwwroot/data/analyte-info.json`.

### WBC (white blood cells)

- White blood cells are part of the immune system and fight infection.
- During long flights, astronauts had a higher white blood cell count and changes in immune cell types.
- Inspiration4: WBC was lower at R+1 than the pre-flight average in all 4 crew (−2% to −27%).
- Sources: https://medlineplus.gov/lab-tests/white-blood-count-wbc/ ; Crucian B et al., "Alterations in adaptive immunity persist during long-duration spaceflight", *npj Microgravity* 2015, https://doi.org/10.1038/npjmgrav.2015.13 ; https://www.nasa.gov/reference/risk-of-altered-immune-system-responses/

### Lymphocytes (absolute)

- White blood cells (B cells and T cells) that fight viruses, bacteria and toxins, and destroy infected or cancer cells.
- Spaceflight disrupts parts of the immune system, and dormant viruses can reactivate.
- Inspiration4: lower at R+1 in 3 of 4 crew (see the finding above).
- Sources: https://medlineplus.gov/lab-tests/blood-differential/ ; Crucian BE et al., "Immune System Dysregulation During Spaceflight", *Frontiers in Immunology* 2018, https://doi.org/10.3389/fimmu.2018.01437 ; https://www.nasa.gov/reference/risk-of-altered-immune-system-responses/

### Neutrophils (absolute)

- The most common white blood cell: the body's main defense when bacteria, viruses or other germs get in.
- Part of the immune system that spaceflight alters.
- Sources: https://medlineplus.gov/lab-tests/blood-differential/ ; https://www.nasa.gov/reference/risk-of-altered-immune-system-responses/

### RBC (red blood cells)

- Red blood cells carry oxygen from the lungs to the body.
- In space, the body destroys red blood cells faster than on Earth (hemolysis), which is linked to "space anemia". The effect was still seen one year after landing.
- But during flight, RBC and hemoglobin were measured **higher**, possibly because astronauts lose plasma (the liquid part of blood).
- Sources: https://medlineplus.gov/lab-tests/red-blood-cell-rbc-count/ ; Trudel G et al., "Hemolysis contributes to anemia during long-duration space flight", *Nature Medicine* 2022, https://doi.org/10.1038/s41591-021-01637-7 ; Kunz H et al., "Alterations in hematologic indices during long-duration spaceflight", *BMC Hematology* 2017, https://doi.org/10.1186/s12878-017-0083-y
- Note: "space anemia" is not one of NASA's official risk titles. It is a research finding from a study of 14 astronauts on 6-month ISS missions.

### Hemoglobin

- The iron-rich protein in red blood cells that carries oxygen. Used to check for anemia.
- Same spaceflight link as RBC (space anemia; higher values during flight).
- Sources: https://medlineplus.gov/lab-tests/hemoglobin-test/ ; https://doi.org/10.1038/s41591-021-01637-7 ; https://doi.org/10.1186/s12878-017-0083-y

### Hematocrit

- The percentage of blood made of red blood cells. Too high or too low can mean a blood disorder or dehydration.
- Rose during flight, by about 3 times more than sample handling alone explains.
- Sources: https://medlineplus.gov/lab-tests/hematocrit-test/ ; https://doi.org/10.1186/s12878-017-0083-y

### MCV (red blood cell size)

- The average size of red blood cells. Too small or too large can be a sign of anemia or a lack of vitamins.
- Linked to space anemia through red blood cells.
- Sources: https://medlineplus.gov/lab-tests/mcv-mean-corpuscular-volume/ ; https://doi.org/10.1038/s41591-021-01637-7

### Platelets

- Platelets help blood clot to stop bleeding.
- NASA lists blood clots under the "Risk of Cardiovascular Adaptations".
- Sources: https://medlineplus.gov/lab-tests/hematocrit-test/ (defines platelets) ; https://medlineplus.gov/lab-tests/platelet-tests/ ; https://www.nasa.gov/reference/risk-of-cardiovascular-adaptations/

### ALP (alkaline phosphatase)

- An enzyme found mostly in the liver, bile ducts and bones. Abnormal levels can point to liver or bone problems.
- In microgravity, bones lose 1–1.5% of their density per month on 4–6 month missions.
- Sources: https://medlineplus.gov/lab-tests/alkaline-phosphatase/ ; https://www.nasa.gov/reference/risk-of-spaceflight-induced-bone-changes/

### Calcium

- About 99% of the body's calcium is in bones and teeth. Blood calcium is needed for nerves, muscles and the heart.
- In space, calcium leaves the bones, which together with dehydration raises the risk of kidney stones.
- Sources: https://medlineplus.gov/lab-tests/calcium-blood-test/ ; https://www.nasa.gov/humans-in-space/the-human-body-in-space/ ; https://www.nasa.gov/reference/risk-of-spaceflight-induced-bone-changes/

### Creatinine

- A waste product from muscles, filtered out by the kidneys. High levels can mean a kidney problem.
- Kidney stones are a spaceflight risk (dehydration + calcium from bones).
- Sources: https://medlineplus.gov/lab-tests/creatinine-test/ ; https://www.nasa.gov/humans-in-space/the-human-body-in-space/

### Glucose

- Blood sugar: the body's main energy source, which comes from food.
- NASA lists the "Risk of Inadequate Food and Nutrition" for long missions.
- Inspiration4: 3 of 4 crew had low glucose at R+45. This is most likely caused by sample handling (blood cells keep using glucose when processing is delayed), not a real health problem.
- Sources: https://medlineplus.gov/lab-tests/blood-glucose-test/ ; https://www.nasa.gov/reference/risk-of-inadequate-food-and-nutrition/

### Potassium

- An electrolyte the heart, nerves and muscles need to work properly.
- NASA lists heart arrhythmias under the "Risk of Cardiovascular Adaptations".
- Inspiration4: one crew member had high potassium at R+45, the same visit as the low glucose, so it is likely also sample handling.
- Sources: https://medlineplus.gov/lab-tests/potassium-blood-test/ ; https://www.nasa.gov/reference/risk-of-cardiovascular-adaptations/

### Sodium

- An electrolyte that controls body fluids. Abnormal levels can mean dehydration or a kidney problem.
- NASA has reduced the sodium content of space food.
- Sources: https://medlineplus.gov/lab-tests/sodium-blood-test/ ; https://www.nasa.gov/reference/risk-of-inadequate-food-and-nutrition/

### Albumin

- A protein made by the liver that keeps fluid inside blood vessels. Low levels can mean liver or kidney disease; high levels can mean dehydration.
- No direct NASA spaceflight risk found; shown as a general health check.
- Source: https://medlineplus.gov/lab-tests/albumin-blood-test/

### ALT

- A liver enzyme. High levels can mean liver damage.
- No direct NASA spaceflight risk found; shown as a general health check.
- Source: https://medlineplus.gov/lab-tests/alt-blood-test/

---

## Limits (deck slide 7)

- Only 4 people, and a 3-day flight. Long missions (months to years) may look very different.
- No blood samples were taken during the flight, only before and after.
- Some values are affected by sample handling (low glucose and high potassium at R+45).
- Normal ranges come from an Earth lab and can differ between labs (one crew member's ranges changed between visits).
- BLINDSPOT is a hackathon prototype, not a medical device. It does not diagnose or treat.

---

## References

Every link, dataset, library and image we use. Add to this list as you go.

**NASA**
- NASA, Risks of Human Spaceflight: https://www.nasa.gov/hrp/risks/
- NASA, The Human Body in Space: https://www.nasa.gov/humans-in-space/the-human-body-in-space/
- NASA, Risk of Altered Immune System Responses: https://www.nasa.gov/reference/risk-of-altered-immune-system-responses/
- NASA, Risk of Spaceflight-Induced Bone Changes: https://www.nasa.gov/reference/risk-of-spaceflight-induced-bone-changes/
- NASA, Risk of Inadequate Food and Nutrition: https://www.nasa.gov/reference/risk-of-inadequate-food-and-nutrition/
- NASA, Risk of Cardiovascular Adaptations: https://www.nasa.gov/reference/risk-of-cardiovascular-adaptations/
- NASA, Risk of Radiation-Induced Cancers: https://www.nasa.gov/reference/risk-of-radiation-induced-cancers/
- NASA SRAG, How do we protect the astronauts from space radiation?: https://srag.jsc.nasa.gov/SpaceRadiation/How/How.cfm
- NASA, How NASA Will Protect Astronauts From Space Radiation at the Moon: https://science.nasa.gov/science-research/heliophysics/how-nasa-will-protect-astronauts-from-space-radiation-at-the-moon
- NASA, To Protect Artemis II Astronauts, NASA Experts Keep Eyes on Sun: https://science.nasa.gov/missions/artemis/artemis-2/to-protect-artemis-ii-astronauts-nasa-experts-keep-eyes-on-sun/
- NASA-STD-3001 Technical Brief OCHMO-TB-020, Design for Ionizing Radiation Protection: https://www.nasa.gov/wp-content/uploads/2023/03/radiation-protection-technical-brief-ochmo.pdf

**NOAA**
- NOAA SWPC, Space Weather Scales (S-scale): https://www.swpc.noaa.gov/noaa-scales-explanation
- NOAA SWPC, Solar Radiation Storm: https://www.swpc.noaa.gov/phenomena/solar-radiation-storm
- NOAA SWPC, GOES Proton Flux: https://www.swpc.noaa.gov/products/goes-proton-flux

**Data**
- NASA OSDR OSD-569, Inspiration4 whole blood (CBC): https://osdr.nasa.gov/bio/repo/data/studies/OSD-569
- NASA OSDR OSD-575, Inspiration4 serum metabolic panel (CMP): https://osdr.nasa.gov/bio/repo/data/studies/OSD-575
- NASA DONKI space weather API: https://api.nasa.gov/

**Peer-reviewed papers**
- Jones CW et al. (2024). Molecular and physiological changes in the SpaceX Inspiration4 civilian crew. *Nature*. https://doi.org/10.1038/s41586-024-07648-x
- Trudel G et al. (2022). Hemolysis contributes to anemia during long-duration space flight. *Nature Medicine*. https://doi.org/10.1038/s41591-021-01637-7
- Kunz H et al. (2017). Alterations in hematologic indices during long-duration spaceflight. *BMC Hematology*. https://doi.org/10.1186/s12878-017-0083-y
- Crucian B et al. (2015). Alterations in adaptive immunity persist during long-duration spaceflight. *npj Microgravity*. https://doi.org/10.1038/npjmgrav.2015.13
- Crucian BE et al. (2018). Immune System Dysregulation During Spaceflight: Potential Countermeasures for Deep Space Exploration Missions. *Frontiers in Immunology*. https://doi.org/10.3389/fimmu.2018.01437

**Medical explanations (U.S. National Library of Medicine, MedlinePlus)**
- WBC: https://medlineplus.gov/lab-tests/white-blood-count-wbc/
- Blood differential (lymphocytes, neutrophils): https://medlineplus.gov/lab-tests/blood-differential/
- RBC: https://medlineplus.gov/lab-tests/red-blood-cell-rbc-count/
- Hemoglobin: https://medlineplus.gov/lab-tests/hemoglobin-test/
- Hematocrit: https://medlineplus.gov/lab-tests/hematocrit-test/
- MCV: https://medlineplus.gov/lab-tests/mcv-mean-corpuscular-volume/
- Platelets: https://medlineplus.gov/lab-tests/platelet-tests/
- ALP: https://medlineplus.gov/lab-tests/alkaline-phosphatase/
- ALT: https://medlineplus.gov/lab-tests/alt-blood-test/
- Albumin: https://medlineplus.gov/lab-tests/albumin-blood-test/
- Calcium: https://medlineplus.gov/lab-tests/calcium-blood-test/
- Creatinine: https://medlineplus.gov/lab-tests/creatinine-test/
- Glucose: https://medlineplus.gov/lab-tests/blood-glucose-test/
- Potassium: https://medlineplus.gov/lab-tests/potassium-blood-test/
- Sodium: https://medlineplus.gov/lab-tests/sodium-blood-test/

**Libraries, tools and hosting**

Versions are the ones in `HealthMonitor.Api.csproj`, `index.html` and `Dockerfile`.

| What | Version | Used for | License | Link |
|---|---|---|---|---|
| .NET / ASP.NET Core | 10.0 (`net10.0`) | Backend web app and API | MIT | https://dotnet.microsoft.com/ |
| Microsoft.AspNetCore.OpenApi | 10.0.15 | API description (OpenAPI) | MIT | https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi |
| Microsoft.EntityFrameworkCore.InMemory (EF Core InMemory) | 10.0.15 | In-memory database | MIT | https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.InMemory |
| Scalar.AspNetCore | 2.20.0 | API docs page at `/scalar` | MIT | https://www.nuget.org/packages/Scalar.AspNetCore |
| CsvHelper | 33.1.0 | Reading the CSV data files | MS-PL / Apache 2.0 | https://joshclose.github.io/CsvHelper/ |
| Chart.js | 4.6.1 (from the jsDelivr CDN) | Charts | MIT | https://www.chartjs.org/ |
| Docker images `mcr.microsoft.com/dotnet/sdk:10.0` and `aspnet:10.0` | 10.0 | Building and running the app | MIT | https://mcr.microsoft.com/ |
| Render | n/a | Hosting the live app | n/a (hosting service) | https://render.com/ |
| Project skeleton | n/a | Starting structure: folders, `Samples` example feature, Dockerfile, README | n/a | Our README calls it the "Project skeleton for the NASA Space Apps Challenge 2026". **To do: add who provided it and a link.** |

**Fonts, icons and images**
- Inter font from Google Fonts (Nutrition & Meds page, PR #12). SIL Open Font License 1.1. https://fonts.google.com/specimen/Inter
- Icons: the Nutrition & Meds icons are simple inline SVGs drawn for this project. No icon library.
- Images: none. The app uses no photos or image files.
- Design mockups: the UI design was explored with Google Stitch (see AI usage). https://stitch.withgoogle.com/

---

## AI usage

Which AI tool, for what. Needed for the AI disclosure on the project page. Add to this list as you go.

| Tool | What it was used for | Checked by a person? |
|---|---|---|
| Claude (Anthropic), via Claude Code | Finding and checking sources (opened every link; confirmed every paper's title and abstract in Europe PMC), drafting this file and `analyte-info.json`, calculating the Inspiration4 lymphocyte changes | ☐ To do: a team member reads every claim against its source |
| Google Stitch | UI design mockups that the pages were styled after | ☐ To do |
| Claude (Anthropic), via Claude Code | Finding NOAA and NASA sources for the radiation alert rules and guidance text (opened every link, checked the SRAG quotes on the live page) | ☐ To do |
