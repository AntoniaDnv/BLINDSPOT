# LabClean

Checks a lab-results CSV for problems, cleans it, and writes a report.
Built for the NASA OSDR Inspiration4 blood tests in `data/raw/`, but works on any similar file.

## Run it

From the repo root:

```bash
dotnet run --project tools/LabClean/src/LabClean.Cli -- data/raw/*.csv
```

For each CSV it writes a folder in `data/clean/` with:

| File | What it is |
|---|---|
| `ASSESSMENT.md` | Readable report: problems found and every value it changed, with the reason |
| `records.json` | Cleaned data: one record per sample × test |
| `baselines.json` | Each person's average and spread per test, from their pre-flight visits |
| `assessment.json` | The report in machine-readable form |

Run it with `--help` to see the options.

## What it checks

- Missing values written in different ways (`NA`, `N/A`, blank …)
- Wrong unit labels (e.g. hemoglobin marked as `%` instead of `g/dL`)
- Impossible values (e.g. glucose 9000)
- Values that break standard formulas (e.g. MCHC = hemoglobin ÷ hematocrit), then fills or fixes them
- Values sitting in the wrong column, then moves them back
- Unusual values, compared with everyone **and** with the same person's other visits
- Many people abnormal at the same visit, which can mean a sample-handling problem

It never silently overwrites anything: each changed value keeps its `originalValue` and a note saying why.
**Check the "Changes made" table in `ASSESSMENT.md` before trusting a repaired value.**

## Test it

```bash
dotnet test tools/LabClean/tests/LabClean.Tests
```

## Project layout

| Folder | What it is |
|---|---|
| `src/LabClean.Core` | The logic. Lab facts (units, limits, formulas) are in `Knowledge.cs`; add new tests there |
| `src/LabClean.Cli` | The command-line tool |
| `tests/LabClean.Tests` | xUnit tests, including a deliberately messy CSV in `Fixtures/` |

The API doesn't use this yet, and the Dockerfile only publishes the API, so it doesn't affect the Render deploy.
