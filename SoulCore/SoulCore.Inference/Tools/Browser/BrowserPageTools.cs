using System.Text.Json;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Browser;

public sealed class BrowserNavigateTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"url":{"type":"string","description":"http(s) URL to open in guest Firefox."}},"required":["url"]}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly IToolsAccessSettings _access;

    public BrowserNavigateTool(IBrowserBridge bridge, IToolsAccessSettings access)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _access = access ?? throw new ArgumentNullException(nameof(access));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_navigate",
        Description:
            "Open a URL in Ubuntu Firefox inside the VM (not Kurt's Windows Chrome). " +
            "Prefer this for websites, then browser_snapshot / browser_click_text.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsControlAllowed(_access))
            return new ToolResult(false, BrowserToolGate.ControlDenied, null);
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("url", out var urlProp)
            || urlProp.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(urlProp.GetString()))
        {
            return new ToolResult(false, "error: browser_navigate requires 'url'.", null);
        }

        var result = await _bridge.NavigateAsync(urlProp.GetString()!.Trim(), ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Content, result.Data);
    }
}

public sealed class BrowserSnapshotTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"query":{"type":"string","description":"Optional filter (Login, Email, link text)."}}}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly IToolsAccessSettings _access;

    public BrowserSnapshotTool(IBrowserBridge bridge, IToolsAccessSettings access)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _access = access ?? throw new ArgumentNullException(nameof(access));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_snapshot",
        Description:
            "List labeled controls in guest Firefox (buttons, links, inputs) with guest coordinates. " +
            "Use this to find Login / Sign in — do not guess window-center clicks.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsCaptureAllowed(_access))
            return new ToolResult(false, BrowserToolGate.CaptureDenied, null);
        string? query = null;
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("query", out var q)
            && q.ValueKind == JsonValueKind.String)
        {
            query = q.GetString();
        }

        var result = await _bridge.SnapshotAsync(query, ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Content, result.Data);
    }
}

public sealed class BrowserClickTextTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"text":{"type":"string","description":"Visible label to click (Login, Sign in, Next)."},"nth":{"type":"integer","description":"1-based match if several share the same label.","default":1}},"required":["text"]}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly IToolsAccessSettings _access;
    private readonly IDesktopViewHub? _view;

    public BrowserClickTextTool(IBrowserBridge bridge, IToolsAccessSettings access, IDesktopViewHub? view = null)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _view = view;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_click_text",
        Description:
            "Click a control in guest Firefox by visible text (Login, Sign in, a link). " +
            "Prefer this over desktop_click pixel guesses.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsControlAllowed(_access))
            return new ToolResult(false, BrowserToolGate.ControlDenied, null);
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("text", out var textProp)
            || textProp.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(textProp.GetString()))
        {
            return new ToolResult(false, "error: browser_click_text requires 'text'.", null);
        }

        var nth = 1;
        if (args.TryGetProperty("nth", out var n) && n.ValueKind == JsonValueKind.Number && n.TryGetInt32(out var ni))
            nth = ni;

        var result = await _bridge.ClickTextAsync(textProp.GetString()!.Trim(), nth, ct).ConfigureAwait(false);
        _view?.RecordAction(result.Success ? $"browser click_text '{textProp.GetString()}'" : "browser click_text failed");
        return new ToolResult(result.Success, result.Content, result.Data);
    }
}

public sealed class BrowserFillTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"field":{"type":"string","description":"Field label or placeholder (Email, Username, Search)."},"value":{"type":"string","description":"Text to type. Do not type secrets unless Kurt asked."}},"required":["field","value"]}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly IToolsAccessSettings _access;

    public BrowserFillTool(IBrowserBridge bridge, IToolsAccessSettings access)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _access = access ?? throw new ArgumentNullException(nameof(access));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_fill",
        Description:
            "Click a named input in guest Firefox and type into it (Email, Username, Search). " +
            "Do not type passwords or secrets unless Kurt explicitly asked.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsControlAllowed(_access))
            return new ToolResult(false, BrowserToolGate.ControlDenied, null);
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("field", out var fieldProp)
            || fieldProp.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(fieldProp.GetString())
            || !args.TryGetProperty("value", out var valueProp)
            || valueProp.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(false, "error: browser_fill requires 'field' and 'value'.", null);
        }

        var result = await _bridge.FillAsync(fieldProp.GetString()!.Trim(), valueProp.GetString() ?? "", ct)
            .ConfigureAwait(false);
        return new ToolResult(result.Success, result.Content, result.Data);
    }
}

public sealed class BrowserBackTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{}}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly IToolsAccessSettings _access;

    public BrowserBackTool(IBrowserBridge bridge, IToolsAccessSettings access)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _access = access ?? throw new ArgumentNullException(nameof(access));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_back",
        Description: "Go back one page in guest Firefox (Alt+Left inside the Ubuntu VM).",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsControlAllowed(_access))
            return new ToolResult(false, BrowserToolGate.ControlDenied, null);
        var result = await _bridge.BackAsync(ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Content, result.Data);
    }
}

public sealed class BrowserTabsTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{}}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly IToolsAccessSettings _access;

    public BrowserTabsTool(IBrowserBridge bridge, IToolsAccessSettings access)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _access = access ?? throw new ArgumentNullException(nameof(access));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_tabs",
        Description: "List Firefox tabs in the Ubuntu VM (guest accessibility tree).",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsCaptureAllowed(_access))
            return new ToolResult(false, BrowserToolGate.CaptureDenied, null);
        var result = await _bridge.TabsAsync(ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Content, result.Data);
    }
}
