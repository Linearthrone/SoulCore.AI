using System.Net.Http;
using System.Text.Json;

namespace House.ChatDesktop.Services;

/// <summary>PROP-22.8 — one weekly reminder from <c>/operator/agenda</c> (0 = Monday … 6 = Sunday).</summary>
public sealed class AgendaItem
{
    public long Id { get; init; }
    public string Title { get; init; } = "";
    public int Weekday { get; init; }
    public string TimeLocal { get; init; } = "09:00";
    public string? Notes { get; init; }

    public string Line =>
        string.IsNullOrWhiteSpace(Notes)
            ? $"{TimeLocal}  {Title}"
            : $"{TimeLocal}  {Title} — {Notes.Trim()}";
}

public sealed record AgendaListFetch(IReadOnlyList<AgendaItem> Items, string? Problem);

/// <summary>Host <c>/operator/agenda</c> (PROP-22 §3.2): week reminders, add / remove.</summary>
public sealed class SoulCoreAgendaClient : IDisposable
{
    public static readonly string[] WeekdayNames =
        { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    /// <summary>0 = Monday … 6 = Sunday for a local date.</summary>
    public static int WeekdayIndex(DateTime date) =>
        ((int)date.DayOfWeek + 6) % 7;

    /// <summary>Monday of the week containing <paramref name="date"/>.</summary>
    public static DateTime WeekStart(DateTime date) =>
        date.Date.AddDays(-WeekdayIndex(date));

    public static bool IsValidTimeLocal(string? text) =>
        TimeSpan.TryParseExact(text?.Trim(), @"hh\:mm", null, out var t)
        && t >= TimeSpan.Zero && t < TimeSpan.FromHours(24);

    public async Task<AgendaListFetch> ListAsync(CancellationToken ct = default)
    {
        if (!OperatorHttp.IsLoopback)
            return new AgendaListFetch(Array.Empty<AgendaItem>(), "Agenda stays on this machine.");

        try
        {
            using var request = OperatorHttp.Request(HttpMethod.Get, "/operator/agenda");
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new AgendaListFetch(
                    Array.Empty<AgendaItem>(),
                    await OperatorHttp.ErrorTextAsync(response, ct).ConfigureAwait(false));
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseList(json);
        }
        catch (Exception ex) when (OperatorHttp.IsExpectedFailure(ex))
        {
            return new AgendaListFetch(Array.Empty<AgendaItem>(), $"Agenda unavailable: {ex.GetType().Name}");
        }
    }

    public async Task<OperatorWriteResult> CreateAsync(
        string title,
        int weekday,
        string timeLocal,
        string? notes,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return new OperatorWriteResult(false, "Title is required.");
        if (weekday is < 0 or > 6)
            return new OperatorWriteResult(false, "Pick a weekday.");
        if (!IsValidTimeLocal(timeLocal))
            return new OperatorWriteResult(false, "Time must be HH:mm (e.g. 09:30).");
        if (!OperatorHttp.IsLoopback)
            return new OperatorWriteResult(false, "Agenda stays on this machine.");

        try
        {
            var body = new Dictionary<string, object?>
            {
                ["title"] = title.Trim(),
                ["weekday"] = weekday,
                ["time_local"] = timeLocal.Trim()
            };
            if (!string.IsNullOrWhiteSpace(notes))
                body["notes"] = notes.Trim();

            using var request = OperatorHttp.Request(HttpMethod.Post, "/operator/agenda", body);
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new OperatorWriteResult(false, await OperatorHttp.ErrorTextAsync(response, ct).ConfigureAwait(false));

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return new OperatorWriteResult(true, null, OperatorHttp.Long(doc.RootElement, "id"));
        }
        catch (Exception ex) when (OperatorHttp.IsExpectedFailure(ex))
        {
            return new OperatorWriteResult(false, $"Add failed: {ex.GetType().Name}");
        }
    }

    public async Task<OperatorWriteResult> DeleteAsync(long id, CancellationToken ct = default)
    {
        if (id <= 0)
            return new OperatorWriteResult(false, "Invalid item.");
        if (!OperatorHttp.IsLoopback)
            return new OperatorWriteResult(false, "Agenda stays on this machine.");

        try
        {
            using var request = OperatorHttp.Request(HttpMethod.Delete, $"/operator/agenda/{id}");
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? new OperatorWriteResult(true, null, id)
                : new OperatorWriteResult(false, await OperatorHttp.ErrorTextAsync(response, ct).ConfigureAwait(false));
        }
        catch (Exception ex) when (OperatorHttp.IsExpectedFailure(ex))
        {
            return new OperatorWriteResult(false, $"Remove failed: {ex.GetType().Name}");
        }
    }

    internal static AgendaListFetch ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new AgendaListFetch(Array.Empty<AgendaItem>(), "Agenda response was empty.");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Array)
            {
                return new AgendaListFetch(Array.Empty<AgendaItem>(), "Agenda response missing items[].");
            }

            var rows = new List<AgendaItem>();
            foreach (var el in items.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                    continue;
                rows.Add(new AgendaItem
                {
                    Id = OperatorHttp.Long(el, "id"),
                    Title = OperatorHttp.Str(el, "title"),
                    Weekday = Math.Clamp(OperatorHttp.Int(el, "weekday"), 0, 6),
                    TimeLocal = OperatorHttp.Str(el, "time_local") is { Length: > 0 } t ? t : "09:00",
                    Notes = OperatorHttp.StrOrNull(el, "notes")
                });
            }

            return new AgendaListFetch(
                rows.OrderBy(r => r.Weekday).ThenBy(r => r.TimeLocal, StringComparer.Ordinal).ToList(),
                null);
        }
        catch (JsonException ex)
        {
            return new AgendaListFetch(Array.Empty<AgendaItem>(), $"Agenda JSON error: {ex.Message}");
        }
    }

    public void Dispose() => _http.Dispose();
}
