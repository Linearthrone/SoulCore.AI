using System.Text.Json;

namespace SoulCore.Inference.Tools.Trading;

/// <summary>
/// Maps SoulCore MT4 tool JSON args to LLMOD MCP HTTP <c>/command</c> parameters.
/// Hermes previously relied on LLM routing; direct LLMOD HTTP needs explicit names.
/// </summary>
public static class Mt4LlmodArgsMapper
{
    public static Dictionary<string, object?> ToLlmodParameters(string mcpToolName, JsonElement args)
    {
        var raw = JsonElementToDictionary(args);
        return mcpToolName.Trim().ToLowerInvariant() switch
        {
            "mt4_execute_trade" => MapExecuteTrade(raw),
            "mt4_run_backtest" => MapRunBacktest(raw),
            "mt4_export_history" => MapExportHistory(raw),
            "mt4_get_historical_bars" => MapHistoricalBars(raw),
            "mt4_get_market_data" => MapMarketData(raw),
            _ => raw
        };
    }

    private static Dictionary<string, object?> MapExecuteTrade(Dictionary<string, object?> raw)
    {
        var mapped = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in raw)
        {
            if (string.Equals(key, "direction", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(key, "sl", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(key, "tp", StringComparison.OrdinalIgnoreCase))
                continue;
            mapped[key] = value;
        }

        if (raw.TryGetValue("direction", out var directionObj)
            && directionObj is string direction
            && !string.IsNullOrWhiteSpace(direction))
        {
            var dir = direction.Trim().ToUpperInvariant();
            mapped["trade_type"] = dir == "SELL" ? 1 : 0;
        }

        if (raw.TryGetValue("sl", out var sl))
            mapped["stop_loss"] = sl;

        if (raw.TryGetValue("tp", out var tp))
            mapped["take_profit"] = tp;

        return mapped;
    }

    private static Dictionary<string, object?> MapRunBacktest(Dictionary<string, object?> raw)
    {
        var mapped = RenameKeys(raw,
            ("ea", "strategy_name"),
            ("from", "start_date"),
            ("to", "end_date"));
        return mapped;
    }

    private static Dictionary<string, object?> MapExportHistory(Dictionary<string, object?> raw) =>
        RenameKeys(raw, ("from", "start_date"), ("to", "end_date"));

    private static Dictionary<string, object?> MapHistoricalBars(Dictionary<string, object?> raw)
    {
        var mapped = RenameKeys(raw, ("timeframe", "time_frame"));
        if (mapped.TryGetValue("count", out var count))
        {
            mapped.Remove("count");
            mapped["max_bars"] = count;
        }

        return mapped;
    }

    private static Dictionary<string, object?> MapMarketData(Dictionary<string, object?> raw)
    {
        var mapped = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (raw.TryGetValue("symbol", out var symbol))
            mapped["symbol"] = symbol;
        return mapped;
    }

    private static Dictionary<string, object?> RenameKeys(
        Dictionary<string, object?> raw,
        params (string From, string To)[] renames)
    {
        var mapped = new Dictionary<string, object?>(StringComparer.Ordinal);
        var renameLookup = renames.ToDictionary(
            r => r.From,
            r => r.To,
            StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in raw)
        {
            var outKey = renameLookup.TryGetValue(key, out var to) ? to : key;
            mapped[outKey] = value;
        }

        return mapped;
    }

    private static Dictionary<string, object?> JsonElementToDictionary(JsonElement args)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (args.ValueKind != JsonValueKind.Object)
            return dict;

        foreach (var prop in args.EnumerateObject())
            dict[prop.Name] = JsonElementToObject(prop.Value);

        return dict;
    }

    private static object? JsonElementToObject(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
            JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToObject).ToArray(),
            JsonValueKind.Object => el.EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonElementToObject(p.Value), StringComparer.Ordinal),
            _ => el.GetRawText()
        };
}
