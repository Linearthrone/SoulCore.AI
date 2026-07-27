using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference;

namespace SoulCore.Hermes;

/// <summary>
/// Hermes OpenAI-compatible HTTP client (quarry default <c>http://127.0.0.1:8642</c>).
/// Auth via <c>SOULCORE_HERMES_API_KEY</c> / user-secrets — never committed.
/// </summary>
/// <remarks>
/// <para>
/// Two inference paths:
/// <list type="bullet">
/// <item><see cref="ChatAsync"/> — single-shot <c>/v1/chat/completions</c>, text-only fallback for non-tool chat.</item>
/// <item><see cref="CompleteWithToolsAsync"/> — agent loop over <c>/v1/chat/completions</c> with <c>tools[]</c>,
/// <c>tool_choice</c>, OpenAI-compatible <c>tool_calls</c> parsing, dispatch via <see cref="IToolRegistry"/>,
/// and re-prompting (capped at <see cref="InferenceOptions.MaxToolIterations"/>, shared with BED-126).</item>
/// </list>
/// </para>
/// <para>
/// The iteration cap is read from <see cref="InferenceOptions.MaxToolIterations"/> (shared with the Ollama
/// path) so the host has one knob for both backends. <see cref="HermesOptions.ToolChoice"/> configures the
/// OpenAI <c>tool_choice</c> field (default <c>auto</c>).
/// </para>
/// </remarks>
public sealed class HermesHttpClient : IHermesClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Marker returned when the agent loop hits <see cref="InferenceOptions.MaxToolIterations"/>
    /// and the model's final turn emitted only <c>tool_calls</c> (no assistant text).
    /// Same semantics as <see cref="OllamaInferenceClient.IterationCapMarker"/>; distinct constant
    /// so callers can tell which backend capped.
    /// </summary>
    public const string IterationCapMarker = "[hermes agent loop hit MaxToolIterations cap without a final text turn]";

    private readonly HttpClient _http;
    private readonly HermesOptions _options;
    private readonly InferenceOptions _inferenceOptions;
    private readonly ILogger<HermesHttpClient> _logger;

    public HermesHttpClient(
        HttpClient http,
        IOptions<HermesOptions> options,
        IOptions<InferenceOptions> inferenceOptions,
        ILogger<HermesHttpClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _inferenceOptions = inferenceOptions?.Value ?? throw new ArgumentNullException(nameof(inferenceOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ApplyApiKey(_http, ResolveApiKey(_options));
    }

    public async Task<string> ChatAsync(
        string message,
        string? systemPreamble = null,
        CancellationToken cancellationToken = default,
        int? maxTokens = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message must be non-empty.", nameof(message));

        if (string.IsNullOrWhiteSpace(ResolveApiKey(_options)))
        {
            throw new InvalidOperationException(
                $"Hermes chat requires API key via env {SecretNames.HermesApiKey} or user-secrets. " +
                "Health checks do not require a key.");
        }

        var payload = new ChatCompletionRequest
        {
            Model = _options.Model,
            Messages = string.IsNullOrWhiteSpace(systemPreamble)
                ? new List<ChatMessageDto>
                {
                    new() { Role = "user", Content = message }
                }
                : new List<ChatMessageDto>
                {
                    new() { Role = "system", Content = systemPreamble!.Trim() },
                    new() { Role = "user", Content = message }
                },
            MaxTokens = maxTokens is > 0 ? maxTokens.Value : _options.MaxTokens
        };

        using var response = await _http.PostAsJsonAsync(
            "v1/chat/completions",
            payload,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Hermes chat failed: {Status} {Body}",
                (int)response.StatusCode,
                TextUtil.Truncate(body, 400));
            response.EnsureSuccessStatusCode();
        }

        var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOptions);
        return parsed?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    /// <summary>
    /// Agent loop over <c>POST /v1/chat/completions</c>. Sends
    /// <paramref name="messages"/> + <paramref name="tools"/> +
    /// <see cref="HermesOptions.ToolChoice"/>, parses OpenAI-compatible
    /// <c>choices[0].message.tool_calls</c>, dispatches each via
    /// <paramref name="registry"/>, appends
    /// <c>{ role:"tool", tool_call_id, name, content }</c> results, re-prompts,
    /// and returns the final assistant text. Capped at
    /// <see cref="InferenceOptions.MaxToolIterations"/> (shared with BED-126).
    /// <para>
    /// ISSUE-20260726-001 fallback: when <c>tool_calls</c> is null/empty, the
    /// loop attempts to parse <c>choices[0].message.content</c> as a JSON
    /// object matching <c>{ "name":"...", "arguments":{...} }</c> and
    /// dispatches it as a tool call when <c>name</c> matches a registered tool.
    /// </para>
    /// </summary>
    public async Task<string> CompleteWithToolsAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        IToolRegistry registry,
        CancellationToken cancellationToken = default)
    {
        if (messages is null)
            throw new ArgumentNullException(nameof(messages));
        if (messages.Count == 0)
            throw new ArgumentException("messages must contain at least one turn.", nameof(messages));
        if (registry is null)
            throw new ArgumentNullException(nameof(registry));

        var cap = Math.Max(1, _inferenceOptions.MaxToolIterations);
        var wireMessages = BuildInitialMessages(messages);
        var wireTools = BuildTools(tools);
        var toolNames = BuildToolNameSet(tools);

        // Resolve tool_choice: when no tools are advertised, OpenAI rejects
        // tool_choice entirely — omit it. Otherwise use the configured value.
        var toolChoice = wireTools.Count == 0 ? null : ResolveToolChoice(_options.ToolChoice);

        var lastAssistantText = string.Empty;

        _logger.LogDebug(
            "Hermes agent loop start: model={Model} messages={Count} tools={ToolCount} tool_choice={Choice} maxIter={Cap}",
            _options.Model,
            wireMessages.Count,
            wireTools.Count,
            toolChoice ?? "(omitted)",
            cap);

        for (var iteration = 0; iteration < cap; iteration++)
        {
            var payload = new ChatCompletionRequest
            {
                Model = _options.Model,
                Messages = wireMessages,
                Tools = wireTools.Count == 0 ? null : wireTools,
                ToolChoice = toolChoice,
                MaxTokens = _inferenceOptions.MaxTokens,
                Stream = false
            };

            using var response = await _http.PostAsJsonAsync(
                "v1/chat/completions",
                payload,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Hermes /v1/chat/completions failed at iteration {Iter}: {Status} {Body}",
                    iteration,
                    (int)response.StatusCode,
                    TextUtil.Truncate(body, 400));
                response.EnsureSuccessStatusCode();
            }

            var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOptions);
            var choice = parsed?.Choices?.FirstOrDefault();
            var msg = choice?.Message;
            if (msg is null)
            {
                _logger.LogWarning(
                    "Hermes /v1/chat/completions returned no choice at iteration {Iter}: {Body}",
                    iteration, TextUtil.Truncate(body, 400));
                return lastAssistantText;
            }

            var assistantText = msg.Content ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(assistantText))
                lastAssistantText = assistantText;

            // 1. Structured tool_calls (OpenAI standard path).
            var toolCalls = msg.ToolCalls;
            var recovered = false;

            // 2. ISSUE-001 fallback: qwen2.5 leak — tool call embedded as bare
            //    JSON in message.content with tool_calls: null. Try to recover.
            if ((toolCalls is null || toolCalls.Count == 0) && toolNames.Count > 0)
            {
                var recoveredCalls = TryRecoverToolCallsFromContent(assistantText, toolNames);
                if (recoveredCalls is { Count: > 0 })
                {
                    toolCalls = recoveredCalls;
                    recovered = true;
                    _logger.LogInformation(
                        "Hermes tool call recovered from content-embedded JSON at iteration {Iter} (count={Count})",
                        iteration, recoveredCalls.Count);
                    // The leaked-JSON content is the tool call, not assistant text —
                    // drop it from the surfaced "last assistant text" so the cap path
                    // does not return raw JSON to the user.
                    if (string.IsNullOrWhiteSpace(msg.Content) == false
                        && ContainsRecoverableToolCall(msg.Content, toolNames))
                    {
                        lastAssistantText = string.Empty;
                    }
                }
            }

            if (toolCalls is null || toolCalls.Count == 0)
            {
                _logger.LogDebug(
                    "Hermes agent loop end at iteration {Iter}: text reply (no tool_calls, recovered={Recovered}).",
                    iteration, recovered);
                return assistantText;
            }

            // Append the assistant turn (with tool_calls + the original content)
            // so the conversation shape matches what the model produced. OpenAI
            // expects the assistant tool_calls echoed back on the next round.
            wireMessages.Add(new ChatMessageDto
            {
                Role = "assistant",
                Content = msg.Content,
                ToolCalls = toolCalls
            });

            // Dispatch each tool call and append role:"tool" results. OpenAI
            // requires tool_call_id on the tool-result message so the model can
            // correlate the result with the call that produced it.
            for (var i = 0; i < toolCalls.Count; i++)
            {
                var tc = toolCalls[i];
                var name = tc.Function?.Name ?? string.Empty;
                var args = ParseArguments(tc.Function?.Arguments);

                _logger.LogInformation(
                    "Hermes tool dispatch: iter={Iter} tool#{Index} name={Name} recovered={Recovered} id={Id}",
                    iteration, i, name, recovered, tc.Id ?? "(none)");

                var result = await registry.ExecuteAsync(name, args, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "Hermes tool result: iter={Iter} tool#{Index} name={Name} success={Success} contentLen={Len}",
                    iteration, i, name, result.Success, result.Content?.Length ?? 0);

                wireMessages.Add(new ChatMessageDto
                {
                    Role = "tool",
                    ToolCallId = tc.Id,
                    Name = name,
                    Content = result.Content ?? string.Empty
                });
            }
        }

        _logger.LogWarning(
            "Hermes agent loop hit MaxToolIterations cap ({Cap}) — returning last assistant text or cap marker.",
            cap);
        return string.IsNullOrEmpty(lastAssistantText)
            ? IterationCapMarker
            : lastAssistantText;
    }

    /// <summary>GET /health — no API key required on quarry Hermes.</summary>
    public async Task<string> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("health", cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return body;
    }

    private static string? ResolveApiKey(HermesOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            return options.ApiKey.Trim();

        var fromEnv = Environment.GetEnvironmentVariable(SecretNames.HermesApiKey);
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv.Trim();
    }

    private static void ApplyApiKey(HttpClient http, string? apiKey)
    {
        http.DefaultRequestHeaders.Remove("Authorization");
        if (!string.IsNullOrWhiteSpace(apiKey))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    /// <summary>
    /// Resolve the configured <c>tool_choice</c> to a wire value. Accepts
    /// <c>"auto"</c>, <c>"none"</c>, or a JSON object string
    /// (<c>{"type":"function","function":{"name":"..."}}</c>) to force a
    /// specific tool. <c>null</c>/empty/<c>"auto"</c> all map to the string
    /// <c>"auto"</c>.
    /// </summary>
    private static string? ResolveToolChoice(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return "auto";
        var trimmed = configured.Trim();
        if (trimmed.StartsWith('{'))
            return trimmed; // specific-tool object form — pass through verbatim
        return trimmed; // "auto" / "none"
    }

    private static List<ChatMessageDto> BuildInitialMessages(IReadOnlyList<ChatMessage> messages)
    {
        var list = new List<ChatMessageDto>(messages.Count);
        foreach (var m in messages)
        {
            if (m is null) continue;
            list.Add(new ChatMessageDto
            {
                Role = string.IsNullOrWhiteSpace(m.Role) ? "user" : m.Role,
                Content = m.Content,
                Name = m.Name,
                // Forward pre-seeded assistant tool_calls to keep the shape faithful.
                ToolCalls = ConvertToolCalls(m.ToolCalls)
            });
        }
        return list;
    }

    private static List<ToolCallDto>? ConvertToolCalls(IReadOnlyList<ChatToolCall>? calls)
    {
        if (calls is null || calls.Count == 0) return null;
        var list = new List<ToolCallDto>(calls.Count);
        foreach (var c in calls)
        {
            if (c?.Function is null) continue;
            list.Add(new ToolCallDto
            {
                // Pre-seeded calls have no id; the loop generates one on dispatch
                // if missing. OpenAI requires an id on the tool-result correlation.
                Id = $"call_{list.Count}",
                Type = "function",
                Function = new FunctionCallDto
                {
                    Name = c.Function.Name,
                    Arguments = SerializeArguments(c.Function.Arguments)
                }
            });
        }
        return list;
    }

    private static List<ToolDto> BuildTools(IReadOnlyList<ToolDefinition> tools)
    {
        if (tools is null || tools.Count == 0) return new List<ToolDto>(0);
        var list = new List<ToolDto>(tools.Count);
        foreach (var t in tools)
        {
            if (t is null) continue;
            list.Add(new ToolDto
            {
                Type = "function",
                Function = new FunctionDto
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
    /// OpenAI ships <c>arguments</c> as a JSON **string** (e.g.
    /// <c>"{\"query\":\"...\"}"</c>), unlike Ollama which ships it as an
    /// object. The registry contract takes a <see cref="JsonElement"/>, so we
    /// normalize: string → parse to object; object → pass through; missing →
    /// <c>default(JsonElement)</c>.
    /// </summary>
    private static JsonElement ParseArguments(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return default;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Not parseable JSON — surface as a string-valued element so the
            // tool sees the raw value rather than losing it.
            return JsonSerializer.SerializeToElement(raw);
        }
    }

    /// <summary>
    /// Re-serialize parsed arguments (object form) back to the JSON string
    /// OpenAI expects on the wire. Null/missing → empty object string.
    /// </summary>
    private static string SerializeArguments(JsonElement? args)
    {
        if (!args.HasValue)
            return "{}";
        return args.Value.ValueKind == JsonValueKind.Undefined
            ? "{}"
            : args.Value.GetRawText();
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
    /// tool call (treated as a normal text reply).
    /// </summary>
    private List<ToolCallDto>? TryRecoverToolCallsFromContent(string content, HashSet<string> toolNames)
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

    private bool TryParseRecoveryObject(string json, HashSet<string> toolNames, out List<ToolCallDto>? calls)
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

            // arguments is optional; default to empty object.
            string argsJson = "{}";
            if (root.TryGetProperty("arguments", out var argsEl))
            {
                argsJson = argsEl.ValueKind switch
                {
                    JsonValueKind.Object or JsonValueKind.Array => argsEl.GetRawText(),
                    JsonValueKind.String => argsEl.GetString() ?? "{}",
                    _ => "{}"
                };
                // If arguments came in as a JSON string, validate it parses; otherwise
                // pass the raw text through (ParseArguments handles it on dispatch).
                if (argsEl.ValueKind == JsonValueKind.String)
                {
                    try { using var check = JsonDocument.Parse(argsJson); }
                    catch (JsonException) { argsJson = "{}"; }
                }
            }

            calls = new List<ToolCallDto>(1)
            {
                new ToolCallDto
                {
                    Id = $"recovered_0",
                    Type = "function",
                    Function = new FunctionCallDto
                    {
                        Name = name,
                        Arguments = argsJson
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

    private sealed class ChatCompletionRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<ChatMessageDto> Messages { get; set; } = new();
        public List<ToolDto>? Tools { get; set; }
        /// <summary>
        /// OpenAI <c>tool_choice</c>: <c>"auto"</c>, <c>"none"</c>, or
        /// <c>{"type":"function","function":{"name":"..."}}</c>. Null when
        /// <see cref="Tools"/> is null (OpenAI rejects tool_choice without tools).
        /// </summary>
        public string? ToolChoice { get; set; }
        public int? MaxTokens { get; set; }
        public bool Stream { get; set; }
    }

    private sealed class ChatMessageDto
    {
        public string Role { get; set; } = "user";
        public string? Content { get; set; }
        public string? Name { get; set; }
        /// <summary>OpenAI tool-result correlation id. Required on role:"tool" messages.</summary>
        public string? ToolCallId { get; set; }
        public List<ToolCallDto>? ToolCalls { get; set; }
    }

    private sealed class ToolCallDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "function";
        public FunctionCallDto Function { get; set; } = new();
    }

    private sealed class FunctionCallDto
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>OpenAI ships <c>arguments</c> as a JSON **string**, not an object.</summary>
        public string Arguments { get; set; } = "{}";
    }

    private sealed class ToolDto
    {
        public string Type { get; set; } = "function";
        public FunctionDto Function { get; set; } = new();
    }

    private sealed class FunctionDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public JsonElement Parameters { get; set; }
    }

    private sealed class ChatCompletionResponse
    {
        public ChatChoice[]? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        public ChatMessageDto? Message { get; set; }
        public string? FinishReason { get; set; }
    }
}
