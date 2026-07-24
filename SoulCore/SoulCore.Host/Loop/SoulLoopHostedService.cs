using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core.Abstractions;

namespace SoulCore.Host.Loop;

/// <summary>
/// Optional timer ticks for <see cref="ISoulLoop"/>. No-ops entirely when <c>SoulLoop:Enabled=false</c>.
/// </summary>
public sealed class SoulLoopHostedService : BackgroundService
{
    private readonly ISoulLoop _loop;
    private readonly SoulLoopOptions _options;
    private readonly ILogger<SoulLoopHostedService> _logger;

    public SoulLoopHostedService(
        ISoulLoop loop,
        IOptions<SoulLoopOptions> options,
        ILogger<SoulLoopHostedService> logger)
    {
        _loop = loop ?? throw new ArgumentNullException(nameof(loop));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "SoulLoop hosted timer idle (SoulLoop:Enabled=false — kill switch). Use WS loop.tick only after enabling.");
            return;
        }

        var seconds = Math.Clamp(_options.TickIntervalSeconds, 5, 3600);
        _logger.LogInformation(
            "SoulLoop hosted timer started (interval={Interval}s). Scaffold only — no high-agency acts.",
            seconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await _loop.TickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SoulLoop scheduled tick failed");
            }
        }
    }
}
