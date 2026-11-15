namespace HealthMonitor.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>Task 6: nutrition / meds / symptoms logging, vitals batch, crew access rules.</summary>
public class LogEntryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private Task<HttpClient> Astronaut() => factory.LoggedInClient(ApiFactory.C001Email, ApiFactory.C001Password);

    private Task<HttpClient> Surgeon() => factory.LoggedInClient(ApiFactory.SurgeonEmail, ApiFactory.SurgeonPassword);

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static JsonElement TodayOf(JsonElement nutrition)
    {
        var todayDay = nutrition.GetProperty("today").GetProperty("day").GetInt32();
        return nutrition.GetProperty("days").EnumerateArray()
            .FirstOrDefault(d => d.GetProperty("day").GetInt32() == todayDay);
    }

    // ---- Nutrition -------------------------------------------------------------

    [Fact]
    public async Task Nutrition_is_seeded_from_the_file_with_today_after_the_last_simulated_day()
    {
        var client = await Astronaut();
        var body = await Json(await client.GetAsync("/api/nutrition"));

        Assert.Equal("C001", body.GetProperty("crewId").GetString());
        var days = body.GetProperty("days").EnumerateArray().ToList();
        Assert.Equal(60, days.Count(d => d.GetProperty("day").GetInt32() <= 60));
        Assert.Equal(61, body.GetProperty("today").GetProperty("day").GetInt32());
        Assert.Contains(days, d => d.GetProperty("day").GetInt32() == 46);
    }

    [Fact]
    public async Task Astronaut_logs_water_and_food_and_nutrition_for_today_changes()
    {
        var client = await Astronaut();
        var before = TodayOf(await Json(await client.GetAsync("/api/nutrition")));
        var waterBefore = before.ValueKind == JsonValueKind.Undefined ? 0 : before.GetProperty("waterL").GetDouble();

        var response = await client.PostAsJsonAsync("/api/nutrition/log", new
        {
            waterMl = 500,
            kcal = 650,
            proteinG = 30,
            meal = "Breakfast pack A",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var day = await Json(response);
        Assert.Equal("C001", day.GetProperty("crewId").GetString());
        Assert.Contains("Breakfast pack A", day.GetProperty("meals").EnumerateArray().Select(m => m.GetString()));

        var after = TodayOf(await Json(await client.GetAsync("/api/nutrition?crewId=C001")));
        Assert.Equal(waterBefore + 0.5, after.GetProperty("waterL").GetDouble(), 3);
        Assert.True(after.GetProperty("kcal").GetDouble() >= 650);
    }

    [Fact]
    public async Task Astronaut_cannot_log_or_read_nutrition_for_another_crew_member()
    {
        var client = await Astronaut();

        var post = await client.PostAsJsonAsync("/api/nutrition/log", new { crewId = "C002", waterMl = 250 });
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);

        var get = await client.GetAsync("/api/nutrition?crewId=C002");
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
    }

    [Theory]
    [InlineData("{\"waterMl\": -5}")]
    [InlineData("{\"waterMl\": 999999}")]
    [InlineData("{}")]
    [InlineData("{\"waterMl\": 250, \"date\": \"2999-01-01\"}")]
    [InlineData("{\"waterMl\": 250, \"date\": \"2000-01-01\"}")]
    public async Task Invalid_nutrition_log_gets_400(string json)
    {
        var client = await Astronaut();
        var response = await client.PostAsync(
            "/api/nutrition/log",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Surgeon_logs_water_for_C002_and_can_read_it()
    {
        var client = await Surgeon();

        var missing = await client.PostAsJsonAsync("/api/nutrition/log", new { waterMl = 250 });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var post = await client.PostAsJsonAsync("/api/nutrition/log", new { crewId = "C002", waterMl = 750 });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        var today = TodayOf(await Json(await client.GetAsync("/api/nutrition?crewId=C002")));
        Assert.True(today.GetProperty("waterL").GetDouble() >= 0.75);
    }

    // ---- Medications -----------------------------------------------------------

    [Fact]
    public async Task Astronaut_logs_medication_and_it_shows_in_meds_and_nutrition()
    {
        var client = await Astronaut();

        var post = await client.PostAsJsonAsync("/api/meds/log", new
        {
            medication = "Melatonin",
            dose = "3 mg",
            taken = true,
            time = "22:30",
            isSleepMedication = true,
        });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await Json(post);
        var date = created.GetProperty("date").GetString();

        var meds = await Json(await client.GetAsync($"/api/meds?date={date}"));
        Assert.Contains(meds.EnumerateArray(), m => m.GetProperty("medication").GetString() == "Melatonin");

        var today = TodayOf(await Json(await client.GetAsync("/api/nutrition")));
        Assert.True(today.GetProperty("hypnoticTaken").GetBoolean());
        Assert.Contains(today.GetProperty("meds").EnumerateArray(), m => m.GetProperty("dose").GetString() == "3 mg");
    }

    [Theory]
    [InlineData("{\"medication\": \"\", \"dose\": \"1 tab\", \"taken\": true}")]
    [InlineData("{\"medication\": \"Ibuprofen\", \"taken\": true}")]
    [InlineData("{\"medication\": \"Ibuprofen\", \"dose\": \"200 mg\"}")]
    [InlineData("{\"medication\": \"Ibuprofen\", \"dose\": \"200 mg\", \"taken\": true, \"time\": \"25:00\"}")]
    public async Task Invalid_medication_log_gets_400(string json)
    {
        var client = await Astronaut();
        var response = await client.PostAsync(
            "/api/meds/log",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Astronaut_cannot_log_or_read_meds_for_another_crew_member()
    {
        var client = await Astronaut();

        var post = await client.PostAsJsonAsync("/api/meds/log", new
        {
            crewId = "C002",
            medication = "Ibuprofen",
            dose = "200 mg",
            taken = true,
        });
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/meds?crewId=C002")).StatusCode);
    }

    // ---- Symptoms --------------------------------------------------------------

    [Fact]
    public async Task Astronaut_logs_symptom_and_reads_it_newest_first()
    {
        var client = await Astronaut();

        await client.PostAsJsonAsync("/api/symptoms", new { fatigue = 2, headache = 1 });
        var post = await client.PostAsJsonAsync("/api/symptoms", new
        {
            fatigue = 6,
            headache = 4,
            congestion = true,
            visionChange = false,
            notes = "<script>alert(1)</script> stuffy after EVA prep",
        });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        var list = (await Json(await client.GetAsync("/api/symptoms"))).EnumerateArray().ToList();
        Assert.True(list.Count >= 2);
        Assert.Equal(6, list[0].GetProperty("fatigue").GetInt32());
        // Stored as plain text; the page renders it with textContent.
        Assert.StartsWith("<script>", list[0].GetProperty("notes").GetString());
        Assert.All(list, s => Assert.Equal("C001", s.GetProperty("crewId").GetString()));
    }

    [Theory]
    [InlineData("{\"fatigue\": 11, \"headache\": 0}")]
    [InlineData("{\"fatigue\": -1, \"headache\": 0}")]
    [InlineData("{\"headache\": 0}")]
    public async Task Invalid_symptom_gets_400(string json)
    {
        var client = await Astronaut();
        var response = await client.PostAsync(
            "/api/symptoms",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Symptom_notes_longer_than_1000_chars_get_400()
    {
        var client = await Astronaut();
        var response = await client.PostAsJsonAsync("/api/symptoms", new
        {
            fatigue = 1,
            headache = 1,
            notes = new string('x', 1001),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Astronaut_cannot_log_or_read_symptoms_for_another_crew_member()
    {
        var client = await Astronaut();

        var post = await client.PostAsJsonAsync("/api/symptoms", new { crewId = "C002", fatigue = 1, headache = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/symptoms?crewId=C002")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/log/recent?crewId=C002")).StatusCode);
    }

    [Fact]
    public async Task Surgeon_reads_symptoms_logged_by_an_astronaut_and_logs_for_C002()
    {
        var astronaut = await Astronaut();
        await astronaut.PostAsJsonAsync("/api/symptoms", new { fatigue = 3, headache = 0, notes = "surgeon-visible" });

        var surgeon = await Surgeon();
        var c001 = await Json(await surgeon.GetAsync("/api/symptoms?crewId=C001"));
        Assert.Contains(c001.EnumerateArray(), s => s.GetProperty("notes").GetString() == "surgeon-visible");

        var post = await surgeon.PostAsJsonAsync("/api/symptoms", new { crewId = "C002", fatigue = 5, headache = 5 });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        Assert.Equal("C002", (await Json(post)).GetProperty("crewId").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await surgeon.GetAsync("/api/symptoms")).StatusCode);
    }

    // ---- Vitals batch ----------------------------------------------------------

    [Fact]
    public async Task Batch_vitals_compute_status_and_appear_in_latest()
    {
        var client = await Astronaut();
        var response = await client.PostAsJsonAsync("/api/readings/batch", new
        {
            values = new object[]
            {
                new { indicator = "heartRate", value = 130 },
                new { indicator = "spo2", value = 98 },
                new { indicator = "systolicBp", value = 112 },
                new { indicator = "diastolicBp", value = 72 },
            },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = (await Json(response)).EnumerateArray().ToList();
        Assert.Equal(4, created.Count);
        var hr = created.Single(c => c.GetProperty("indicator").GetString() == "heartRate");
        Assert.Equal("critical", hr.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(hr.GetProperty("guidance").GetString()));
        Assert.Equal("ok", created.Single(c => c.GetProperty("indicator").GetString() == "spo2").GetProperty("status").GetString());
        Assert.Single(created.Select(c => c.GetProperty("recordedAt").GetDateTime()).Distinct());

        var latest = await Json(await client.GetAsync("/api/readings/latest"));
        var latestHr = latest.EnumerateArray().Single(r => r.GetProperty("indicator").GetString() == "heartRate");
        Assert.Equal(130, latestHr.GetProperty("value").GetDouble());

        var recent = (await Json(await client.GetAsync("/api/log/recent"))).EnumerateArray().ToList();
        Assert.Contains(recent, r => r.GetProperty("type").GetString() == "vitals"
            && r.GetProperty("status").GetString() == "critical");
    }

    [Fact]
    public async Task Batch_with_any_invalid_value_gets_400_and_saves_nothing()
    {
        var client = await Astronaut();
        var before = (await Json(await client.GetAsync("/api/readings?indicator=spo2"))).GetArrayLength();

        var response = await client.PostAsJsonAsync("/api/readings/batch", new
        {
            values = new object[]
            {
                new { indicator = "spo2", value = 97 },
                new { indicator = "heartRate", value = 1300 },
                new { indicator = "nope", value = 1 },
            },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await Json(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("heartRate", out _));
        Assert.True(errors.TryGetProperty("nope", out _));

        var after = (await Json(await client.GetAsync("/api/readings?indicator=spo2"))).GetArrayLength();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Empty_batch_gets_400()
    {
        var client = await Astronaut();
        var response = await client.PostAsJsonAsync("/api/readings/batch", new { values = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Astronaut_cannot_batch_log_for_another_crew_member()
    {
        var client = await Astronaut();
        var response = await client.PostAsJsonAsync("/api/readings/batch", new
        {
            crewId = "C002",
            values = new object[] { new { indicator = "heartRate", value = 70 } },
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Surgeon_batch_logs_for_C002_and_sees_it_in_latest()
    {
        var client = await Surgeon();
        var response = await client.PostAsJsonAsync("/api/readings/batch", new
        {
            crewId = "C002",
            values = new object[] { new { indicator = "sleepHours", value = 7.5 } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var latest = await Json(await client.GetAsync("/api/readings/latest?crewId=C002"));
        Assert.Contains(latest.EnumerateArray(), r => r.GetProperty("indicator").GetString() == "sleepHours");
    }

    [Fact]
    public async Task Single_reading_rejects_implausible_value()
    {
        var client = await Astronaut();
        var response = await client.PostAsJsonAsync("/api/readings", new { indicator = "bodyTemp", value = 370 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recent_entries_mark_seeded_readings_as_simulated()
    {
        var client = await Astronaut();
        await client.PostAsJsonAsync("/api/readings/batch", new
        {
            values = new object[] { new { indicator = "heartRate", value = 66 } },
        });

        var recent = (await Json(await client.GetAsync("/api/log/recent?limit=50"))).EnumerateArray().ToList();
        var vitals = recent.Where(r => r.GetProperty("type").GetString() == "vitals").ToList();
        Assert.Contains(vitals, r => r.GetProperty("simulated").GetBoolean());
        Assert.Contains(vitals, r => !r.GetProperty("simulated").GetBoolean());
        Assert.All(
            recent.Where(r => r.GetProperty("type").GetString() != "vitals"),
            r => Assert.False(r.GetProperty("simulated").GetBoolean()));
    }

    [Fact]
    public async Task Batch_with_null_item_gets_400()
    {
        var client = await Astronaut();
        var response = await client.PostAsync(
            "/api/readings/batch",
            new StringContent("{\"values\":[null]}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Future_recordedAt_gets_400_for_batch_and_symptoms()
    {
        var client = await Astronaut();
        var future = DateTime.UtcNow.AddHours(1);

        var batch = await client.PostAsJsonAsync("/api/readings/batch", new
        {
            recordedAt = future,
            values = new object[] { new { indicator = "heartRate", value = 70 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, batch.StatusCode);

        var symptom = await client.PostAsJsonAsync("/api/symptoms", new { recordedAt = future, fatigue = 1, headache = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, symptom.StatusCode);

        var nearNow = await client.PostAsJsonAsync("/api/symptoms", new { recordedAt = DateTime.UtcNow.AddMinutes(2), fatigue = 1, headache = 1 });
        Assert.Equal(HttpStatusCode.Created, nearNow.StatusCode);
    }

    [Theory]
    [InlineData("/api/symptoms")]
    [InlineData("/api/meds")]
    [InlineData("/api/log/recent")]
    public async Task Anonymous_gets_401_on_log_endpoints(string url)
    {
        var response = await factory.NewClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
