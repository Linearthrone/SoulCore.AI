using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Host.Inference;

/// <summary>
/// PROP-22.2: when the operator flips cloud/local via <see cref="InferenceRuntimeSelection"/>,
/// re-point outgoing chat requests at ollama.com or local :11434 without rebuilding the typed client.
/// No-op unless a cloud override is set and differs from the configured endpoint. The API key is
/// resolved from env/config at call time and is never logged.
/// </summary>
internal sealed class InferenceRuntimeRouteHandler : DelegatingHandler
{
    private const string LocalBase = "http://127.0.0.1:11434";

    private readonly InferenceRuntimeSelection _selection;
    private readonly IOptions<InferenceOptions> _options;

    public InferenceRuntimeRouteHandler(InferenceRuntimeSelection selection, IOptions<InferenceOptions> options)
    {
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var wantCloud = _selection.CloudOverride;
        var opts = _options.Value;
        if (wantCloud is { } cloud && cloud != opts.IsCloudEndpoint && request.RequestUri is { } uri)
        {
            var target = new Uri((cloud ? InferenceOptions.CloudBaseUrl : LocalBase).TrimEnd('/') + "/");
            request.RequestUri = new UriBuilder(uri)
            {
                Scheme = target.Scheme,
                Host = target.Host,
                Port = target.IsDefaultPort ? -1 : target.Port
            }.Uri;

            request.Headers.Authorization = null;
            if (cloud && opts.ResolveApiKey() is { Length: > 0 } key)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
