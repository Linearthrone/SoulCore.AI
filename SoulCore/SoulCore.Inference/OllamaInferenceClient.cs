using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference;

/// <summary>
/// Ollama HTTP client (quarry default <c>http://127.0.0.1:11434</c>).
/// </summary>
/// <remarks>
/// <para>
/// Two inference paths:
/// <list type="bullet">
/// <item><see cref="CompleteAsync"/> — single-shot <c>/api/generate</c>, text-only fallback for non-tool chat.</item>
/// <item><see cref="CompleteWithToolsAsync"/> — agent loop over <c>/api/chat</c> with <c>tools[]</c>, <c>tool_calls</c> parsing, dispatch via <see cref="IToolRegistry"/>, and re-prompting (capped at <see cref="InferenceOptions.MaxToolIterations"/>).</item>
/// </list>
/// </para>
/// <para>
/// <see cref="IToolRegistry"/> is optional at construction — passing <c>null</c> keeps the single-shot path working for callers that never use the tool-loop (and for existing tests). The loop itself requires a non-null registry; passing <c>null</c> to <see cref="CompleteWithToolsAsync"/> throws at call time.
/// </para>
/// <para>
/// ISSUE-20260726-001 fallback: <see cref="CompleteWithToolsAsync"/> also
/// attempts to recover tool calls that the <c>qwen2.5</c> family sometimes
/// leaks as bare JSON in <c>message.content</c> (with <c>tool_calls: null</c>
/// — ollama #13968, #12174). When <c>tool_calls</c> is null/empty and tools
/// are advertised, the loop parses <c>message.content</c> as a JSON object
/// matching <c>{ "name":"...", "arguments":{...} }</c> and dispatches it as a
/// tool call when <c>name</c> matches a registered tool. Mirrors the Hermes
/// fallback in <c>HermesHttpClient.TryRecoverToolCallsFromContent</c> (BED-127).
/// </para>
/// </remarks>
public sealed class OllamaInferenceClient : IInferenceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Marker returned when the agent loop hits <see cref="InferenceOptions.MaxToolIterations"/>
    /// and the model's final turn emitted only <c>tool_calls</c> (no assistant text).
    /// Lets callers distinguish "finished with text" from "capped mid-tool-call".
    /// </summary>
    public const string IterationCapMarker = "[agent loop hit MaxToolIterations cap without a final text turn]";

    private readonly HttpClient _http;
    private readonly InferenceOptions _options;
    private readonly ILogger<OllamaInferenceClient> _logger;
    private readonly IToolRegistry? _toolRegistry;

    /// <summary>
    /// DI-friendly constructor (the one <c>AddHttpClient&lt;OllamaInferenceClient&gt;</c>
    /// should bind). Annotated with <see cref="ActivatorUtilitiesConstructorAttribute"/>
    /// to disambiguate from the 4-arg overload below — without this, once
    /// <c>IToolRegistry</c> is registered in the container, <c>ActivatorUtilities</c>
    /// sees both constructors as applicable and throws
    /// <c>InvalidOperationException: Multiple constructors accepting all given
    /// argument types</c> on the first chat request.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public OllamaInferenceClient(
        HttpClient http,
        IOptions<InferenceOptions> options,
        ILogger<OllamaInferenceClient> logger)
        : this(http, options, logger, toolRegistry: null)
    {
    }

    /// <param name="toolRegistry">
    /// Optional tool registry for <see cref="CompleteWithToolsAsync"/>. May be
    /// <c>null</c> when the host only uses the single-shot path. The loop
    /// method accepts its own registry argument (callers may pass a scoped
    /// registry per turn); the ctor-injected one is a fallback when the call
    /// does not supply one.
    /// </param>
    public OllamaInferenceClient(
        HttpClient http,
        IOptions<InferenceOptions> options,
        ILogger<OllamaInferenceClient> logger,
        IToolRegistry? toolRegistry)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _toolRegistry = toolRegistry;
    }

    public async Task<string> CompleteAsync(
        string prompt,
        string? systemPreamble = null,
        CancellationToken cancellationToken = default,
        int? maxTokens = null)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt must be non-empty.", nameof(prompt));

        var numPredict = maxTokens is > 0 ? maxTokens.Value : _options.MaxTokens;

        var options = new OllamaGenerateOptions { NumPredict = numPredict };
        if (_options.NumCtx > 0)
            options.NumCtx = _options.NumCtx;

        var payload = new OllamaGenerateRequest
        {
            Model = _options.Model,
            Prompt = prompt,
            System = string.IsNullOrWhiteSpace(systemPreamble) ? null : systemPreamble.Trim(),
            Stream = false,
            // Thinking models otherwise spend num_predict on CoT and leave response empty.
            Think = _options.ThinkEnabled,
            Options = options
        };

        using var response = await _http.PostAsJsonAsync(
            "api/generate",
            payload,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Ollama generate failed: {Status} {Body}",
                (int)response.StatusCode,
                TextUtil.Truncate(body, 400));
            response.EnsureSuccessStatusCode();
        }

        var parsed = JsonSerializer.Deserialize<OllamaGenerateResponse>(body, JsonOptions);
        return parsed?.Response ?? string.Empty;
    }

    /// <summary>
    /// Agent loop over <c>POST /api/chat</c>. Sends <paramref name="messages"/>
    /// + <paramref name="tools"/>, parses <c>tool_calls</c>, dispatches via
    /// <paramref name="registry"/> (falls back to the ctor-injected
    /// <see cref="IToolRegistry"/> when <paramref name="registry"/> is null),
    /// appends <c>{role:"tool",name,content}</c> results, re-prompts, and
    /// returns the final assistant text. Capped at
    /// <see cref="InferenceOptions.MaxToolIterations"/>.
    /// <para>
    /// ISSUE-20260726-001 fallback: when <c>tool_calls</c> is null/empty and
    /// tools are advertised, the loop attempts to parse
    /// <c>message.content</c> as a JSON object matching
    /// <c>{ "name":"...", "arguments":{...} }</c> and dispatches it as a tool
    /// call when <c>name</c> matches a registered tool. This recovers the
    /// known qwen2.5 leak (bare JSON in content with <c>tool_calls: null</c>).
    /// </para>
    /// </summary>
    public async Task<string> CompleteWithToolsAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        IToolRegistry? registry = null,
        CancellationToken cancellationToken = default)
    {
        if (messages is null)
            throw new ArgumentNullException(nameof(messages));
        if (messages.Count == 0)
            throw new ArgumentException("messages must contain at least one turn.", nameof(messages));

        var toolRegistry = registry ?? _toolRegistry
            ?? throw new ArgumentNullException(
                nameof(registry),
                "CompleteWithToolsAsync requires an IToolRegistry (pass one, or inject one via the 4-arg ctor).");

        var cap = Math.Max(1, _options.MaxToolIterations);
        var ollamaMessages = BuildInitialMessages(messages);
        var ollamaTools = BuildTools(tools);
        var toolNames = BuildToolNameSet(tools);

        // Track the last assistant text so the cap-return path has something
        // to surface when the model's final turn emitted only tool_calls.
        var lastAssistantText = string.Empty;

        _logger.LogDebug(
            "Ollama agent loop start: model={Model} messages={Count} tools={ToolCount} maxIter={Cap}",
            _options.Model,
            ollamaMessages.Count,
            ollamaTools.Count,
            cap);

        for (var iteration = 0; iteration < cap; iteration++)
        {
            var payload = new OllamaChatRequest
            {
                Model = _options.Model,
                Messages = ollamaMessages,
                Tools = ollamaTools.Count == 0 ? null : ollamaTools,
                Stream = false,
                Think = _options.ThinkEnabled
            };

            using var response = await _http.PostAsJsonAsync(
                "api/chat",
                payload,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Ollama /api/chat failed at iteration {Iter}: {Status} {Body}",
                    iteration,
                    (int)response.StatusCode,
                    TextUtil.Truncate(body, 400));
                response.EnsureSuccessStatusCode();
            }

            var parsed = JsonSerializer.Deserialize<OllamaChatResponse>(body, JsonOptions);
            var msg = parsed?.Message;
            if (msg is null)
            {
                _logger.LogWarning("Ollama /api/chat returned no message at iteration {Iter}: {Body}", iteration, TextUtil.Truncate(body, 400));
                return lastAssistantText;
            }

            var assistantText = msg.Content ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(assistantText))
                lastAssistantText = assistantText;

            // 1. Structured tool_calls (Ollama standard path).
            var toolCalls = msg.ToolCalls;
            var recovered = false;

            // 2. ISSUE-20260726-001 fallback: qwen2.5 leak — tool call embedded
            //    as bare JSON in message.content with tool_calls: null. Try to
            //    recover so Victoria still acts on the flaky runs.
            if ((toolCalls is null || toolCalls.Count == 0) && toolNames.Count > 0)
            {
                var recoveredCalls = TryRecoverToolCallsFromContent(assistantText, toolNames);
                if (recoveredCalls is { Count: > 0 })
                {
                    toolCalls = recoveredCalls;
                    recovered = true;
                    _logger.LogInformation(
                        "Ollama tool call recovered from content-embedded JSON at iteration {Iter} (count={Count})",
                        iteration, recoveredCalls.Count);
                    // The leaked-JSON content is the tool call, not assistant
                    // text — drop it from the surfaced "last assistant text"
                    // so the cap path does not return raw JSON to the user.
                    if (!string.IsNullOrWhiteSpace(msg.Content)
                        && ContainsRecoverableToolCall(msg.Content, toolNames))
                    {
                        lastAssistantText = string.Empty;
                    }
                }
            }

            if (toolCalls is null || toolCalls.Count == 0)
            {
                _logger.LogDebug(
                    "Ollama agent loop end at iteration {Iter}: text reply (no tool_calls, recovered={Recovered}).",
                    iteration, recovered);
                return assistantText;
            }

            // Append the assistant turn (with tool_calls) so the conversation
            // shape matches what the model produced. Ollama expects the
            // assistant tool_calls echoed back on the next round.
            ollamaMessages.Add(new OllamaChatMessage
            {
                Role = "assistant",
                Content = assistantText,
                ToolCalls = toolCalls
            });

            // Dispatch each tool call and append role:"tool" results.
            for (var i = 0; i < toolCalls.Count; i++)
            {
                var tc = toolCalls[i];
                var name = tc.Function?.Name ?? string.Empty;
                var args = ParseArguments(tc.Function?.Arguments);

                _logger.LogInformation(
                    "Ollama tool dispatch: iter={Iter} tool#{Index} name={Name} recovered={Recovered}",
                    iteration, i, name, recovered);

                var result = await toolRegistry.ExecuteAsync(name, args, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "Ollama tool result: iter={Iter} tool#{Index} name={Name} success={Success} contentLen={Len}",
                    iteration, i, name, result.Success, result.Content?.Length ?? 0);

                // BED-125 contract: forward ToolResult.Content as the role:"tool"
                // message string. ToolResult.Data is host-side only — never
                // serialized to the model.
                ollamaMessages.Add(new OllamaChatMessage
                {
                    Role = "tool",
                    Name = name,
                    Content = result.Content ?? string.Empty
                });
            }
        }

        _logger.LogWarning(
            "Ollama agent loop hit MaxToolIterations cap ({Cap}) — returning last assistant text or cap marker.",
            cap);
        return string.IsNullOrEmpty(lastAssistantText)
            ? IterationCapMarker
            : lastAssistantText;
    }

    private static List<OllamaChatMessage> BuildInitialMessages(IReadOnlyList<ChatMessage> messages)
    {
        var list = new List<OllamaChatMessage>(messages.Count);
        foreach (var m in messages)
        {
            if (m is null) continue;
            list.Add(new OllamaChatMessage
            {
                Role = string.IsNullOrWhiteSpace(m.Role) ? "user" : m.Role,
                Content = m.Content,
                Name = m.Name
                // ToolCalls on initial messages are rare (only set on assistant
                // turns the loop itself produces); we forward them to keep the
                // shape faithful if a caller pre-seeds an assistant tool-call turn.
                ,ToolCalls = ConvertToolCalls(m.ToolCalls)
            });
        }
        return list;
    }

    private static List<OllamaToolCallDto>? ConvertToolCalls(IReadOnlyList<ChatToolCall>? calls)
    {
        if (calls is null || calls.Count == 0) return null;
        var list = new List<OllamaToolCallDto>(calls.Count);
        foreach (var c in calls)
        {
            if (c?.Function is null) continue;
            list.Add(new OllamaToolCallDto
            {
                Function = new OllamaFunctionCallDto
                {
                    Name = c.Function.Name,
                    // Re-serialize parsed arguments to a JsonElement for the wire.
                    Arguments = c.Function.Arguments
                }
            });
        }
        return list;
    }

    private static List<OllamaToolDto> BuildTools(IReadOnlyList<ToolDefinition> tools)
    {
        if (tools is null || tools.Count == 0) return new List<OllamaToolDto>(0);
        var list = new List<OllamaToolDto>(tools.Count);
        foreach (var t in tools)
        {
            if (t is null) continue;
            list.Add(new OllamaToolDto
            {
                Type = "function",
                Function = new OllamaFunctionDto
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = t.Parameters
                }
            });
        }
        return list;
    }

    private static HashSet<string> BuildToolNameSet(IReadOnlyList<ToolDefinition> tools)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (tools is null) return set;
        foreach (var t in tools)
        {
            if (t is null) continue;
            if (!string.IsNullOrWhiteSpace(t.Name))
                set.Add(t.Name);
        }
        return set;
    }

    /// <summary>
    /// ISSUE-20260726-001: when the model leaks a tool call as bare JSON in
    /// <c>message.content</c> (qwen2.5 family flakiness — ollama #13968,
    /// #12174), attempt to recover it. Accept either:
    /// <list>
    /// <item>A pure JSON object <c>{"name":"...","arguments":{...}}</c> (whole content).</item>
    /// <item>A JSON object embedded in text (extracted via brace matching).</item>
    /// </list>
    /// Returns recovered tool calls only when <c>name</c> matches a
    /// registered tool. Returns null when the content is not a recoverable
    /// tool call (treated as a normal text reply). Mirrors the Hermes
    /// <c>TryRecoverToolCallsFromContent</c> in <c>HermesHttpClient</c> (BED-127).
    /// </summary>
    private List<OllamaToolCallDto>? TryRecoverToolCallsFromContent(string content, HashSet<string> toolNames)
    {
        if (string.IsNullOrWhiteSpace(content) || toolNames.Count == 0)
            return null;

        // Fast path: the whole content is the JSON object.
        if (TryParseRecoveryObject(content, toolNames, out var direct))
            return direct;

        // Slow path: extract a JSON object embedded in text via brace matching.
        var json = ExtractFirstJsonObject(content);
        if (json is not null && TryParseRecoveryObject(json, toolNames, out var embedded))
            return embedded;

        return null;
    }

    private bool TryParseRecoveryObject(string json, HashSet<string> toolNames, out List<OllamaToolCallDto>? calls)
    {
        calls = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            if (!root.TryGetProperty("name", out var nameEl))
                return false;
            var name = nameEl.ValueKind == JsonValueKind.String ? nameEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(name) || !toolNames.Contains(name))
                return false;

            // arguments is optional; default to an empty object element.
            JsonElement? argsEl = null;
            if (root.TryGetProperty("arguments", out var rawArgs))
            {
                // Clone the element — root is backed by a `using var doc`
                // that disposes at the end of this method, so the element
                // must be standalone to survive past the return.
                argsEl = rawArgs.ValueKind switch
                {
                    JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Number
                        or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null
                        => rawArgs.Clone(),
                    // arguments as a JSON string — parse it defensively.
                    JsonValueKind.String => ParseStringArguments(rawArgs.GetString()),
                    _ => null
                };
            }

            calls = new List<OllamaToolCallDto>(1)
            {
                new OllamaToolCallDto
                {
                    Function = new OllamaFunctionCallDto
                    {
                        Name = name,
                        Arguments = argsEl
                    }
                }
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Parse a JSON-string-form <c>arguments</c> value (qwen2.5 sometimes
    /// leaks <c>"arguments": "{\"query\":\"...\"}"</c>) into a JsonElement
    /// object form. Returns null on empty/unparseable input.
    /// </summary>
    private static JsonElement? ParseStringArguments(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Cheap brace-matching extraction of the first <c>{...}</c> object in
    /// <paramref name="text"/>. Returns null when no balanced object is found.
    /// </summary>
    private static string? ExtractFirstJsonObject(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0)
            return null;
        var depth = 0;
        var inString = false;
        var escape = false;
        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString)
            {
                if (escape) { escape = false; continue; }
                if (ch == '\\') { escape = true; continue; }
                if (ch == '"') { inString = false; }
                continue;
            }
            if (ch == '"') { inString = true; continue; }
            if (ch == '{') depth++;
            else if (ch == '}')
            {
                depth--;
                if (depth == 0)
                    return text.Substring(start, i - start + 1);
            }
        }
        return null;
    }

    private bool ContainsRecoverableToolCall(string? content, HashSet<string> toolNames)
        => TryRecoverToolCallsFromContent(content ?? "", toolNames) is { Count: > 0 };

    /// <summary>
    /// Ollama has shipped <c>arguments</c> as both a JSON object and a JSON
    /// string across versions. Parse defensively: if it's a string, parse it
    /// back to a JsonElement; if it's already an object, pass it through; if
    /// missing/null, return <c>default(JsonElement)</c> (the registry handles
    /// that as "no args").
    /// </summary>
    private static JsonElement ParseArguments(JsonElement? raw)
    {
        if (!raw.HasValue)
            return default;

        var el = raw.Value;
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
            case JsonValueKind.Array:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return el;
            case JsonValueKind.String:
                var s = el.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    return default;
                try
                {
                    using var doc = JsonDocument.Parse(s);
                    return doc.RootElement.Clone();
                }
                catch (JsonException)
                {
                    // Not parseable JSON — surface as a string-valued element so
                    // the tool sees the raw value rather than losing it.
                    return el;
                }
            default:
                return el;
        }
    }

    private sealed class OllamaGenerateRequest
    {
        public string Model { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string? System { get; set; }
        public bool Stream { get; set; }
        public bool Think { get; set; }
        public OllamaGenerateOptions? Options { get; set; }
    }

    private sealed class OllamaGenerateOptions
    {
        public int NumPredict { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? NumCtx { get; set; }
    }

    private sealed class OllamaGenerateResponse
    {
        public string? Response { get; set; }
    }

    private sealed class OllamaChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<OllamaChatMessage> Messages { get; set; } = new();
        public List<OllamaToolDto>? Tools { get; set; }
        public bool Stream { get; set; }
        public bool Think { get; set; }
    }

    private sealed class OllamaChatMessage
    {
        public string Role { get; set; } = "user";
        public string? Content { get; set; }
        public string? Name { get; set; }
        public List<OllamaToolCallDto>? ToolCalls { get; set; }
    }

    private sealed class OllamaToolCallDto
    {
        public OllamaFunctionCallDto Function { get; set; } = new();
    }

    private sealed class OllamaFunctionCallDto
    {
        public string Name { get; set; } = string.Empty;
        public JsonElement? Arguments { get; set; }
    }

    private sealed class OllamaToolDto
    {
        public string Type { get; set; } = "function";
        public OllamaFunctionDto Function { get; set; } = new();
    }

    private sealed class OllamaFunctionDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public JsonElement Parameters { get; set; }
    }

    private sealed class OllamaChatResponse
    {
        public OllamaChatResponseMessage? Message { get; set; }
    }

    private sealed class OllamaChatResponseMessage
    {
        public string? Content { get; set; }
        public List<OllamaToolCallDto>? ToolCalls { get; set; }
    }
}
