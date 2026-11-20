using System.Text.Json;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

/// <summary>PROP-22.7–22.9 — request shapes and parsers for the operator surface clients.</summary>
public sealed class OperatorSurfaceClientsTests
{
    [Fact]
    public void BuildCreateBody_task_omits_project_only_fields()
    {
        var body = SoulCoreOperatorWorkClient.BuildCreateBody(new OperatorWorkItemDraft
        {
            IsProject = false,
            Title = "  Pay invoice  ",
            Priority = 9,
            PhaseCheckpoints = new[] { "ignored" },
            Deliverables = new[] { "ignored" }
        });

        Assert.Equal("task", body["kind"]);
        Assert.Equal("Pay invoice", body["title"]);
        Assert.Equal(5, body["priority"]);
        Assert.False(body.ContainsKey("phase_checkpoints_json"));
        Assert.False(body.ContainsKey("deliverables_json"));
    }

    [Fact]
    public void BuildCreateBody_project_serializes_phases_and_deadline_in_utc()
    {
        var body = SoulCoreOperatorWorkClient.BuildCreateBody(new OperatorWorkItemDraft
        {
            IsProject = true,
            Title = "Ship it",
            Priority = 2,
            NoCheckin = true,
            Deadline = new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.FromHours(-5)),
            PhaseCheckpoints = new[] { "design", "build" },
            Deliverables = new[] { "demo" }
        });

        Assert.Equal("project", body["kind"]);
        Assert.Equal(1, body["no_checkin"]);
        Assert.Equal("2026-12-01T22:00:00.000Z", body["deadline_utc"]);
        Assert.Equal("[\"design\",\"build\"]", body["phase_checkpoints_json"]);
        Assert.Equal("[\"demo\"]", body["deliverables_json"]);
    }

    [Fact]
    public void ParseList_reads_items_and_labels_kind()
    {
        const string json = """
            {"items":[
              {"id":4,"kind":"project","title":"Garden","priority":2,"status":"open"},
              {"id":5,"title":"Call bank","priority":4,"no_checkin":1}
            ]}
            """;
        var fetch = SoulCoreOperatorWorkClient.ParseList(json);

        Assert.Null(fetch.Problem);
        Assert.Equal(2, fetch.Items.Count);
        Assert.Equal("Project", fetch.Items[0].KindLabel);
        Assert.Equal("Task", fetch.Items[1].KindLabel);
        Assert.True(fetch.Items[1].NoCheckin);
    }

    [Fact]
    public void ParseList_reports_a_problem_for_garbage()
    {
        Assert.NotNull(SoulCoreOperatorWorkClient.ParseList("not json").Problem);
        Assert.NotNull(SoulCoreOperatorWorkClient.ParseList("{}").Problem);
        Assert.NotNull(SoulCoreOperatorWorkClient.ParseList("").Problem);
    }

    [Theory]
    [InlineData("09:00", true)]
    [InlineData("23:59", true)]
    [InlineData("24:00", false)]
    [InlineData("9am", false)]
    [InlineData("", false)]
    public void Agenda_time_validation(string text, bool valid)
    {
        Assert.Equal(valid, SoulCoreAgendaClient.IsValidTimeLocal(text));
    }

    [Fact]
    public void Agenda_week_starts_on_monday()
    {
        // 2026-11-20 is a Friday.
        var friday = new DateTime(2026, 11, 20);
        Assert.Equal(4, SoulCoreAgendaClient.WeekdayIndex(friday));
        Assert.Equal(new DateTime(2026, 11, 16), SoulCoreAgendaClient.WeekStart(friday));
        Assert.StartsWith("Mon", SoulCoreAgendaClient.WeekdayNames[0], StringComparison.Ordinal);
        Assert.Equal(7, SoulCoreAgendaClient.WeekdayNames.Length);
    }

    [Fact]
    public void Agenda_ParseList_reads_weekday_and_time()
    {
        const string json = """{"items":[{"id":1,"title":"Stand-up","weekday":0,"time_local":"09:30","notes":"zoom"}]}""";
        var fetch = SoulCoreAgendaClient.ParseList(json);
        Assert.Null(fetch.Problem);
        var item = Assert.Single(fetch.Items);
        Assert.Equal(0, item.Weekday);
        Assert.Equal("09:30", item.TimeLocal);
        Assert.Equal("zoom", item.Notes);
    }

    [Fact]
    public void InferenceProfiles_Parse_finds_active_and_profiles()
    {
        const string json = """
            {"active":{"model":"m1","cloud":true},
             "profiles":[{"id":"a","label":"A","model":"m1","cloud":true},{"id":"b","label":"B","model":"m2","cloud":false}]}
            """;
        var fetch = SoulCoreInferenceProfilesClient.Parse(json);
        Assert.Null(fetch.Problem);
        Assert.Equal("m1", fetch.Active?.Model);
        Assert.Equal(2, fetch.Profiles.Count);
        Assert.False(string.IsNullOrWhiteSpace(fetch.Profiles[0].Display));
    }

    [Fact]
    public void SystemLog_Parse_returns_lines()
    {
        const string json = """{"available":true,"path":"x.log","lines":["a","b"]}""";
        var fetch = SoulCoreSystemLogClient.Parse(json);
        Assert.True(fetch.Available);
        Assert.Equal(new[] { "a", "b" }, fetch.Lines.ToArray());
        Assert.Equal("x.log", fetch.Path);
    }

    [Fact]
    public void PerformanceLog_keeps_most_recent_entries_only()
    {
        var log = new PerformanceLog();
        for (var i = 0; i < PerformanceLog.MaxEntries + 25; i++)
            log.Add("line " + i);

        Assert.Equal(PerformanceLog.MaxEntries, log.Count);
        Assert.Contains("line " + (PerformanceLog.MaxEntries + 24), log.Render(), StringComparison.Ordinal);
        Assert.DoesNotContain("line 0\n", log.Render(), StringComparison.Ordinal);

        log.Clear();
        Assert.Equal(0, log.Count);
    }

    [Fact]
    public void JsonDocument_sanity_for_snake_case_body()
    {
        var body = SoulCoreOperatorWorkClient.BuildCreateBody(new OperatorWorkItemDraft { Title = "x" });
        var json = JsonSerializer.Serialize(body);
        Assert.Contains("\"needs_extra_permissions\":0", json, StringComparison.Ordinal);
    }
}
