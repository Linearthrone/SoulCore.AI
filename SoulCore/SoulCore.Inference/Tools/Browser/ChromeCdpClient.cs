using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Minimal Chrome DevTools Protocol helper for the native browser backend.
/// Talks only to a configured HTTP base (must resolve to loopback).
/// </summary>
internal static class ChromeCdpClient
{
    public const int MaxDomChars = 32_000;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

    public static bool TryValidateLoopbackBase(string? cdpUrl, out Uri baseUri, out string error)
    {
        baseUri = null!;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(cdpUrl))
        {
            error = "BrowserCdpUrl is empty";
            return false;
        }

        if (!Uri.TryCreate(cdpUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "BrowserCdpUrl must be an absolute http(s) URL";
            return false;
        }

        if (!IsLoopbackHost(uri.Host))
        {
            error = $"BrowserCdpUrl host '{uri.Host}' is not loopback (SEC-004)";
            return false;
        }

        baseUri = uri;
        return true;
    }

    public static async Task<(bool Ok, string Message, IReadOnlyList<CdpTarget> Targets)> ListTargetsAsync(
        Uri baseUri,
        CancellationToken ct)
    {
        try
        {
            using var http = CreateHttp(baseUri);
            using var resp = await http.GetAsync(Combine(baseUri, "/json/list"), ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return (false, $"CDP /json/list HTTP {(int)resp.StatusCode}", Array.Empty<CdpTarget>());
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return (false, "CDP /json/list did not return an array", Array.Empty<CdpTarget>());

            var list = new List<CdpTarget>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var type = el.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
                if (!string.Equals(type, "page", StringComparison.OrdinalIgnoreCase))
                    continue;

                var title = el.TryGetProperty("title", out var ti) ? ti.GetString() ?? "" : "";
                var url = el.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                var ws = el.TryGetProperty("webSocketDebuggerUrl", out var w) ? w.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(ws))
                    continue;
                list.Add(new CdpTarget(title, url, ws));
            }

            return (true, $"found {list.Count} page target(s)", list);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SocketException or IOException)
        {
            return (false, $"CDP unreachable: {ex.Message}", Array.Empty<CdpTarget>());
        }
    }

    public static async Task<(bool Ok, string Message, string? Path, string? Dom, string? Title, string? Url)> CaptureAsync(
        Uri baseUri,
        int tab,
        string captureDirectory,
        CancellationToken ct)
    {
        var (ok, msg, targets) = await ListTargetsAsync(baseUri, ct).ConfigureAwait(false);
        if (!ok)
            return (false, msg, null, null, null, null);
        if (targets.Count == 0)
            return (false, "no browser page targets (open a tab with Chrome --remote-debugging-port)", null, null, null, null);
        if (tab < 0 || tab >= targets.Count)
            return (false, $"tab index {tab} out of range (0..{targets.Count - 1})", null, null, null, null);

        var target = targets[tab];
        try
        {
            await using var session = await CdpSession.ConnectAsync(target.WebSocketDebuggerUrl, ct).ConfigureAwait(false);
            await session.SendAsync("Page.enable", null, ct).ConfigureAwait(false);

            using var shot = await session.SendAsync("Page.captureScreenshot", new { format = "png" }, ct)
                .ConfigureAwait(false);
            if (!shot.RootElement.TryGetProperty("data", out var dataProp)
                || dataProp.ValueKind != JsonValueKind.String)
            {
                return (false, "Page.captureScreenshot returned no data", null, null, target.Title, target.Url);
            }

            var bytes = Convert.FromBase64String(dataProp.GetString() ?? "");
            Directory.CreateDirectory(captureDirectory);
            var path = Path.Combine(
                captureDirectory,
                $"browser_tab{tab}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.png");
            await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);

            string? dom = null;
            try
            {
                using var eval = await session.SendAsync(
                    "Runtime.evaluate",
                    new
                    {
                        expression = "document.documentElement ? document.documentElement.outerHTML : ''",
                        returnByValue = true,
                    },
                    ct).ConfigureAwait(false);
                if (eval.RootElement.TryGetProperty("result", out var result)
                    && result.TryGetProperty("value", out var value)
                    && value.ValueKind == JsonValueKind.String)
                {
                    dom = value.GetString();
                    if (dom is { Length: > MaxDomChars })
                        dom = dom[..MaxDomChars] + "\n…(truncated)";
                }
            }
            catch
            {
                // DOM is optional — screenshot alone is enough.
            }

            return (true, $"captured tab {tab} → {path}", path, dom, target.Title, target.Url);
        }
        catch (Exception ex)
        {
            return (false, $"CDP capture failed: {ex.Message}", null, null, target.Title, target.Url);
        }
    }

    public static async Task<(bool Ok, string Message)> ClickAsync(Uri baseUri, int tab, int x, int y, CancellationToken ct)
    {
        return await WithPageAsync(baseUri, tab, async session =>
        {
            await session.SendAsync("Input.dispatchMouseEvent", new
            {
                type = "mousePressed",
                x,
                y,
                button = "left",
                clickCount = 1,
            }, ct).ConfigureAwait(false);
            await session.SendAsync("Input.dispatchMouseEvent", new
            {
                type = "mouseReleased",
                x,
                y,
                button = "left",
                clickCount = 1,
            }, ct).ConfigureAwait(false);
            return $"clicked at ({x},{y}) via CDP";
        }, ct).ConfigureAwait(false);
    }

    public static async Task<(bool Ok, string Message)> TypeAsync(Uri baseUri, int tab, string text, CancellationToken ct)
    {
        return await WithPageAsync(baseUri, tab, async session =>
        {
            await session.SendAsync("Input.insertText", new { text }, ct).ConfigureAwait(false);
            return $"typed {text.Length} chars via CDP";
        }, ct).ConfigureAwait(false);
    }

    public static async Task<(bool Ok, string Message)> KeyAsync(Uri baseUri, int tab, string key, CancellationToken ct)
    {
        return await WithPageAsync(baseUri, tab, async session =>
        {
            var keyName = key.Trim();
            await session.SendAsync("Input.dispatchKeyEvent", new
            {
                type = "keyDown",
                key = keyName,
            }, ct).ConfigureAwait(false);
            await session.SendAsync("Input.dispatchKeyEvent", new
            {
                type = "keyUp",
                key = keyName,
            }, ct).ConfigureAwait(false);
            return $"pressed key '{keyName}' via CDP";
        }, ct).ConfigureAwait(false);
    }

    public static async Task<(bool Ok, string Message)> ScrollAsync(Uri baseUri, int tab, int dx, int dy, CancellationToken ct)
    {
        return await WithPageAsync(baseUri, tab, async session =>
        {
            await session.SendAsync("Input.dispatchMouseEvent", new
            {
                type = "mouseWheel",
                x = 0,
                y = 0,
                deltaX = dx,
                deltaY = dy,
            }, ct).ConfigureAwait(false);
            return $"scrolled dx={dx} dy={dy} via CDP";
        }, ct).ConfigureAwait(false);
    }

    private static async Task<(bool Ok, string Message)> WithPageAsync(
        Uri baseUri,
        int tab,
        Func<CdpSession, Task<string>> action,
        CancellationToken ct)
    {
        var (ok, msg, targets) = await ListTargetsAsync(baseUri, ct).ConfigureAwait(false);
        if (!ok)
            return (false, msg);
        if (targets.Count == 0)
            return (false, "no browser page targets (open a tab with Chrome --remote-debugging-port)");
        if (tab < 0 || tab >= targets.Count)
            return (false, $"tab index {tab} out of range (0..{targets.Count - 1})");

        try
        {
            await using var session = await CdpSession.ConnectAsync(targets[tab].WebSocketDebuggerUrl, ct)
                .ConfigureAwait(false);
            var resultMsg = await action(session).ConfigureAwait(false);
            return (true, resultMsg);
        }
        catch (Exception ex)
        {
            return (false, $"CDP action failed: {ex.Message}");
        }
    }

    private static HttpClient CreateHttp(Uri baseUri)
    {
        var http = new HttpClient { Timeout = DefaultTimeout };
        http.BaseAddress = new Uri($"{baseUri.Scheme}://{baseUri.Authority}");
        return http;
    }

    private static Uri Combine(Uri baseUri, string path)
        => new(baseUri, path);

    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "[::1]", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(host.Trim('[', ']'), out var ip))
            return IPAddress.IsLoopback(ip);

        return false;
    }

    internal sealed record CdpTarget(string Title, string Url, string WebSocketDebuggerUrl);

    private sealed class CdpSession : IAsyncDisposable
    {
        private readonly ClientWebSocket _ws;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private int _nextId = 1;

        private CdpSession(ClientWebSocket ws) => _ws = ws;

        public static async Task<CdpSession> ConnectAsync(string webSocketUrl, CancellationToken ct)
        {
            if (!Uri.TryCreate(webSocketUrl, UriKind.Absolute, out var wsUri)
                || (wsUri.Scheme != "ws" && wsUri.Scheme != "wss"))
            {
                throw new InvalidOperationException("invalid CDP webSocketDebuggerUrl");
            }

            if (!IsLoopbackHost(wsUri.Host))
                throw new InvalidOperationException($"CDP WebSocket host '{wsUri.Host}' is not loopback");

            var ws = new ClientWebSocket();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(DefaultTimeout);
            await ws.ConnectAsync(wsUri, linked.Token).ConfigureAwait(false);
            return new CdpSession(ws);
        }

        public async Task<JsonDocument> SendAsync(string method, object? parameters, CancellationToken ct)
        {
            await _sendLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var id = Interlocked.Increment(ref _nextId);
                using var payload = new MemoryStream();
                await using (var writer = new Utf8JsonWriter(payload))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("id", id);
                    writer.WriteString("method", method);
                    if (parameters is not null)
                    {
                        writer.WritePropertyName("params");
                        JsonSerializer.Serialize(writer, parameters);
                    }

                    writer.WriteEndObject();
                }

                var bytes = payload.ToArray();
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linked.CancelAfter(DefaultTimeout);
                await _ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, linked.Token)
                    .ConfigureAwait(false);

                while (true)
                {
                    using var response = await ReceiveMessageAsync(linked.Token).ConfigureAwait(false);
                    if (!response.RootElement.TryGetProperty("id", out var idProp)
                        || idProp.ValueKind != JsonValueKind.Number
                        || idProp.GetInt32() != id)
                    {
                        continue; // event — keep reading
                    }

                    if (response.RootElement.TryGetProperty("error", out var err))
                    {
                        var message = err.TryGetProperty("message", out var m) ? m.GetString() : err.ToString();
                        throw new InvalidOperationException($"CDP {method}: {message}");
                    }

                    if (response.RootElement.TryGetProperty("result", out var result))
                        return JsonDocument.Parse(result.GetRawText());

                    return JsonDocument.Parse("{}");
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task<JsonDocument> ReceiveMessageAsync(CancellationToken ct)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            try
            {
                using var ms = new MemoryStream();
                while (true)
                {
                    var result = await _ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                        throw new InvalidOperationException("CDP WebSocket closed");
                    ms.Write(buffer, 0, result.Count);
                    if (result.EndOfMessage)
                        break;
                }

                return JsonDocument.Parse(ms.ToArray());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_ws.State == WebSocketState.Open)
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None)
                        .ConfigureAwait(false);
            }
            catch
            {
                // ignore close races
            }

            _ws.Dispose();
            _sendLock.Dispose();
        }
    }
}
