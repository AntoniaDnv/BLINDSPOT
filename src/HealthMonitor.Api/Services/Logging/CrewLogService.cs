namespace HealthMonitor.Api.Services.Logging;

using System.Globalization;
using Data;
using Microsoft.EntityFrameworkCore;
using Models.Dtos;
using Models.Entities;
using Services.Nutrition;
using Services.Vitals;

/// <summary>Either a value or a validation error message (→ 400).</summary>
public sealed record LogResult<T>(T? Value, string? Field, string? Error)
{
    public static LogResult<T> Ok(T value) => new(value, null, null);

    public static LogResult<T> Fail(string field, string error) => new(default, field, error);
}

/// <summary>
/// Log &amp; Data Entry: nutrition, medications, symptoms and the "recent entries" feed.
/// Every crewId passed in has already been authorised by ICrewAccessService.
/// </summary>
public interface ICrewLogService
{
    Task<NutritionCrewDto> GetNutritionAsync(string crewId, CancellationToken cancellationToken = default);

    Task<LogResult<NutritionDayDto>> LogNutritionAsync(string crewId, NutritionLogCreateDto dto, CancellationToken cancellationToken = default);

    Task<LogResult<MedicationLogDto>> LogMedicationAsync(string crewId, MedicationLogCreateDto dto, CancellationToken cancellationToken = default);

    Task<LogResult<IReadOnlyList<MedicationLogDto>>> GetMedicationsAsync(string crewId, DateOnly? date, CancellationToken cancellationToken = default);

    Task<SymptomResponseDto> LogSymptomAsync(string crewId, SymptomCreateDto dto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SymptomResponseDto>> GetSymptomsAsync(string crewId, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecentEntryDto>> GetRecentAsync(string crewId, int limit, CancellationToken cancellationToken = default);
}

public class CrewLogService(
    AppDbContext data,
    INutritionService nutrition,
    IVitalThresholdService vitals) : ICrewLogService
{
    // Read-modify-write of a NutritionDay must not interleave (two quick water taps).
    private static readonly SemaphoreSlim DayLock = new(1, 1);

    public async Task<NutritionCrewDto> GetNutritionAsync(string crewId, CancellationToken cancellationToken = default)
    {
        var (role, baseline) = nutrition.GetProfile(crewId);

        var days = await data.NutritionDays.AsNoTracking()
            .Where(d => d.CrewId == crewId)
            .OrderBy(d => d.Day)
            .ToListAsync(cancellationToken);

        var entries = await data.NutritionLogEntries.AsNoTracking()
            .Where(e => e.CrewId == crewId)
            .OrderBy(e => e.LoggedAt)
            .ToListAsync(cancellationToken);

        var meds = await data.MedicationLogs.AsNoTracking()
            .Where(m => m.CrewId == crewId)
            .OrderBy(m => m.LoggedAt)
            .ToListAsync(cancellationToken);

        return new NutritionCrewDto
        {
            CrewId = crewId,
            Note = nutrition.Note,
            Role = role,
            Baseline = baseline,
            Today = nutrition.Today,
            Days = days.Select(d => this.ToDayDto(d, entries, meds)).ToList(),
            Alerts = nutrition.GetAlerts(crewId),
        };
    }

    public async Task<LogResult<NutritionDayDto>> LogNutritionAsync(
        string crewId,
        NutritionLogCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var meal = string.IsNullOrWhiteSpace(dto.Meal) ? null : dto.Meal.Trim();

        if (dto.WaterMl is null && dto.Kcal is null && dto.ProteinG is null
            && dto.CarbsG is null && dto.FatG is null && meal is null)
        {
            return LogResult<NutritionDayDto>.Fail("waterMl", "Enter water, calories, a macro or a meal name.");
        }

        var day = this.ResolveDay(dto.Date, out var dayError);
        if (dayError is not null)
            return LogResult<NutritionDayDto>.Fail("date", dayError);

        await DayLock.WaitAsync(cancellationToken);
        try
        {
            var record = await this.GetOrCreateDayAsync(crewId, day, cancellationToken);

            record.WaterL = Math.Round(record.WaterL + ((dto.WaterMl ?? 0) / 1000.0), 3);
            record.Kcal += dto.Kcal ?? 0;
            if (dto.ProteinG is not null)
                record.ProteinG = (record.ProteinG ?? 0) + dto.ProteinG;
            if (dto.CarbsG is not null)
                record.CarbsG = (record.CarbsG ?? 0) + dto.CarbsG;
            if (dto.FatG is not null)
                record.FatG = (record.FatG ?? 0) + dto.FatG;

            data.NutritionLogEntries.Add(new NutritionLogEntry
            {
                CrewId = crewId,
                Day = day,
                LoggedAt = DateTime.UtcNow,
                WaterMl = dto.WaterMl,
                Kcal = dto.Kcal,
                ProteinG = dto.ProteinG,
                CarbsG = dto.CarbsG,
                FatG = dto.FatG,
                Meal = meal,
            });

            await data.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            DayLock.Release();
        }

        return LogResult<NutritionDayDto>.Ok(await this.GetDayDtoAsync(crewId, day, cancellationToken));
    }

    public async Task<LogResult<MedicationLogDto>> LogMedicationAsync(
        string crewId,
        MedicationLogCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var medication = dto.Medication.Trim();
        var dose = dto.Dose.Trim();
        if (medication.Length == 0)
            return LogResult<MedicationLogDto>.Fail("medication", "Medication is required.");
        if (dose.Length == 0)
            return LogResult<MedicationLogDto>.Fail("dose", "Dose is required.");

        var day = this.ResolveDay(dto.Date, out var dayError);
        if (dayError is not null)
            return LogResult<MedicationLogDto>.Fail("date", dayError);

        var log = new MedicationLog
        {
            CrewId = crewId,
            Day = day,
            Medication = medication,
            Dose = dose,
            Taken = dto.Taken ?? false,
            Time = string.IsNullOrWhiteSpace(dto.Time) ? null : dto.Time,
            IsSleepMedication = dto.IsSleepMedication,
            LoggedAt = DateTime.UtcNow,
        };

        await DayLock.WaitAsync(cancellationToken);
        try
        {
            // Merge into the day structure the Nutrition & Meds page reads.
            var record = await this.GetOrCreateDayAsync(crewId, day, cancellationToken);
            if (log.IsSleepMedication && log.Taken)
                record.HypnoticTaken = true;

            data.MedicationLogs.Add(log);
            await data.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            DayLock.Release();
        }

        return LogResult<MedicationLogDto>.Ok(this.ToMedDto(log));
    }

    public async Task<LogResult<IReadOnlyList<MedicationLogDto>>> GetMedicationsAsync(
        string crewId,
        DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var query = data.MedicationLogs.AsNoTracking().Where(m => m.CrewId == crewId);

        if (date is not null)
        {
            var day = nutrition.DayFor(date.Value);
            query = query.Where(m => m.Day == day);
        }

        var rows = await query
            .OrderByDescending(m => m.LoggedAt)
            .ThenByDescending(m => m.Id)
            .ToListAsync(cancellationToken);

        return LogResult<IReadOnlyList<MedicationLogDto>>.Ok(rows.Select(this.ToMedDto).ToList());
    }

    public async Task<SymptomResponseDto> LogSymptomAsync(
        string crewId,
        SymptomCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var log = new SymptomLog
        {
            CrewId = crewId,
            RecordedAt = dto.RecordedAt?.ToUniversalTime() ?? DateTime.UtcNow,
            Fatigue = dto.Fatigue ?? 0,
            Headache = dto.Headache ?? 0,
            Congestion = dto.Congestion,
            VisionChange = dto.VisionChange,
            Notes = dto.Notes?.Trim() ?? "",
        };

        data.SymptomLogs.Add(log);
        await data.SaveChangesAsync(cancellationToken);

        return ToSymptomDto(log);
    }

    public async Task<IReadOnlyList<SymptomResponseDto>> GetSymptomsAsync(
        string crewId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var rows = await data.SymptomLogs.AsNoTracking()
            .Where(s => s.CrewId == crewId)
            .OrderByDescending(s => s.RecordedAt)
            .ThenByDescending(s => s.Id)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);

        return rows.Select(ToSymptomDto).ToList();
    }

    public async Task<IReadOnlyList<RecentEntryDto>> GetRecentAsync(
        string crewId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 50);
        var result = new List<RecentEntryDto>();

        // Vitals: one entry per timestamp (a batch save shares one), worst status wins.
        var readings = await data.Readings.AsNoTracking()
            .Where(r => r.CrewId == crewId)
            .OrderByDescending(r => r.RecordedAt)
            .Take(limit * 8)
            .ToListAsync(cancellationToken);

        foreach (var group in readings.GroupBy(r => r.RecordedAt))
        {
            var parts = new List<string>();
            var worst = "ok";
            foreach (var r in group.OrderBy(r => r.Indicator, StringComparer.Ordinal))
            {
                var threshold = vitals.Find(r.Indicator);
                if (threshold is null)
                    continue;

                var status = vitals.GetStatus(threshold, r.Value);
                worst = Worse(worst, status);
                parts.Add($"{threshold.Name} {r.Value.ToString("0.##", CultureInfo.InvariantCulture)} {threshold.Unit}");
            }

            if (parts.Count > 0)
            {
                result.Add(new RecentEntryDto
                {
                    Type = "vitals",
                    RecordedAt = group.Key,
                    Summary = string.Join(" · ", parts),
                    Status = worst,
                });
            }
        }

        var symptoms = await data.SymptomLogs.AsNoTracking()
            .Where(s => s.CrewId == crewId)
            .OrderByDescending(s => s.RecordedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        result.AddRange(symptoms.Select(s => new RecentEntryDto
        {
            Type = "symptoms",
            RecordedAt = s.RecordedAt,
            Summary = SymptomSummary(s),
        }));

        var entries = await data.NutritionLogEntries.AsNoTracking()
            .Where(e => e.CrewId == crewId)
            .OrderByDescending(e => e.LoggedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        result.AddRange(entries.Select(e => new RecentEntryDto
        {
            Type = "nutrition",
            RecordedAt = e.LoggedAt,
            Summary = NutritionSummary(e),
        }));

        var meds = await data.MedicationLogs.AsNoTracking()
            .Where(m => m.CrewId == crewId)
            .OrderByDescending(m => m.LoggedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        result.AddRange(meds.Select(m => new RecentEntryDto
        {
            Type = "meds",
            RecordedAt = m.LoggedAt,
            Summary = $"{m.Medication} {m.Dose}" + (m.Time is null ? "" : $" at {m.Time}"),
            Status = m.Taken ? "taken" : "pending",
        }));

        return result
            .OrderByDescending(r => r.RecordedAt)
            .Take(limit)
            .ToList();
    }

    private int ResolveDay(DateOnly? date, out string? error)
    {
        error = null;
        var today = nutrition.Today.Date;
        var value = date ?? today;

        // Clients may be up to a day ahead of UTC.
        if (value > today.AddDays(1))
        {
            error = "Date cannot be in the future.";
            return 0;
        }

        var day = nutrition.DayFor(value);
        if (day < 1)
        {
            error = $"Date must be on or after mission day 1 ({nutrition.DateFor(1):yyyy-MM-dd}).";
            return 0;
        }

        return day;
    }

    private async Task<NutritionDay> GetOrCreateDayAsync(string crewId, int day, CancellationToken cancellationToken)
    {
        var record = await data.NutritionDays
            .FirstOrDefaultAsync(d => d.CrewId == crewId && d.Day == day, cancellationToken);

        if (record is null)
        {
            record = new NutritionDay { CrewId = crewId, Day = day };
            data.NutritionDays.Add(record);
        }

        return record;
    }

    private async Task<NutritionDayDto> GetDayDtoAsync(string crewId, int day, CancellationToken cancellationToken)
    {
        var record = await data.NutritionDays.AsNoTracking()
            .FirstAsync(d => d.CrewId == crewId && d.Day == day, cancellationToken);
        var entries = await data.NutritionLogEntries.AsNoTracking()
            .Where(e => e.CrewId == crewId && e.Day == day)
            .OrderBy(e => e.LoggedAt)
            .ToListAsync(cancellationToken);
        var meds = await data.MedicationLogs.AsNoTracking()
            .Where(m => m.CrewId == crewId && m.Day == day)
            .OrderBy(m => m.LoggedAt)
            .ToListAsync(cancellationToken);

        return this.ToDayDto(record, entries, meds);
    }

    private NutritionDayDto ToDayDto(NutritionDay d, List<NutritionLogEntry> entries, List<MedicationLog> meds)
    {
        var dayEntries = entries.Where(e => e.Day == d.Day).ToList();

        return new NutritionDayDto
        {
            CrewId = d.CrewId,
            Day = d.Day,
            Date = nutrition.DateFor(d.Day),
            Kcal = d.Kcal,
            WaterL = d.WaterL,
            ProteinG = d.ProteinG,
            CarbsG = d.CarbsG,
            FatG = d.FatG,
            HypnoticTaken = d.HypnoticTaken,
            Meals = dayEntries.Where(e => e.Meal is not null).Select(e => e.Meal!).ToList(),
            Entries = dayEntries.Select(e => new NutritionLogEntryDto
            {
                Id = e.Id,
                LoggedAt = e.LoggedAt,
                WaterMl = e.WaterMl,
                Kcal = e.Kcal,
                ProteinG = e.ProteinG,
                CarbsG = e.CarbsG,
                FatG = e.FatG,
                Meal = e.Meal,
            }).ToList(),
            Meds = meds.Where(m => m.Day == d.Day).Select(this.ToMedDto).ToList(),
        };
    }

    private MedicationLogDto ToMedDto(MedicationLog m) => new()
    {
        Id = m.Id,
        CrewId = m.CrewId,
        Day = m.Day,
        Date = nutrition.DateFor(m.Day),
        Medication = m.Medication,
        Dose = m.Dose,
        Taken = m.Taken,
        Time = m.Time,
        IsSleepMedication = m.IsSleepMedication,
        LoggedAt = m.LoggedAt,
    };

    private static SymptomResponseDto ToSymptomDto(SymptomLog s) => new()
    {
        Id = s.Id,
        CrewId = s.CrewId,
        RecordedAt = s.RecordedAt,
        Fatigue = s.Fatigue,
        Headache = s.Headache,
        Congestion = s.Congestion,
        VisionChange = s.VisionChange,
        Notes = s.Notes,
    };

    private static string SymptomSummary(SymptomLog s)
    {
        var parts = new List<string> { $"Fatigue {s.Fatigue}/10", $"Headache {s.Headache}/10" };
        if (s.Congestion)
            parts.Add("congestion");
        if (s.VisionChange)
            parts.Add("vision change");
        if (s.Notes.Length > 0)
            parts.Add($"\"{(s.Notes.Length > 60 ? s.Notes[..60] + "…" : s.Notes)}\"");
        return string.Join(" · ", parts);
    }

    private static string NutritionSummary(NutritionLogEntry e)
    {
        var parts = new List<string>();
        if (e.Meal is not null)
            parts.Add(e.Meal);
        if (e.WaterMl is not null)
            parts.Add($"+{e.WaterMl.Value.ToString("0", CultureInfo.InvariantCulture)} ml water");
        if (e.Kcal is not null)
            parts.Add($"{e.Kcal.Value.ToString("0", CultureInfo.InvariantCulture)} kcal");
        var macros = new List<string>();
        if (e.ProteinG is not null)
            macros.Add($"P {e.ProteinG.Value.ToString("0", CultureInfo.InvariantCulture)} g");
        if (e.CarbsG is not null)
            macros.Add($"C {e.CarbsG.Value.ToString("0", CultureInfo.InvariantCulture)} g");
        if (e.FatG is not null)
            macros.Add($"F {e.FatG.Value.ToString("0", CultureInfo.InvariantCulture)} g");
        if (macros.Count > 0)
            parts.Add(string.Join(" / ", macros));
        return string.Join(" · ", parts);
    }

    private static string Worse(string a, string b)
    {
        static int Rank(string s) => s switch { "critical" => 2, "warning" => 1, _ => 0 };
        return Rank(b) > Rank(a) ? b : a;
    }
}
