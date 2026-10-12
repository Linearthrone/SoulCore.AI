using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoulCore.Inference.Presence;
using SoulCore.Memory;

namespace SoulCore.Host.Loop;

/// <summary>
/// PROP-17.1: while a runningId points at an in_progress (or due parked probe) row,
/// keep taking the next step after the chat turn has returned.
/// Never schedules suspended. Parked rows are only probed after park_until.
/// PROP-17.3: does not tick while the leisure inference lease is held.
/// </summary>
public sealed class AssignedWorkHostedService : BackgroundService
{
    private readonly IAssignedWorkStore _store;
    private readonly IVictoriaTaskStore _tasks;
    private readonly AssignedWorkRunner _runner;
    private readonly WorkTurnGate _gate;
    private readonly IInferenceLease _lease;
    private readonly ILogger<AssignedWorkHostedService> _logger;

    public AssignedWorkHostedService(
        IAssignedWorkStore store,
        IVictoriaTaskStore tasks,
        AssignedWorkRunner runner,
        WorkTurnGate gate,
        IInferenceLease lease,
        ILogger<AssignedWorkHostedService> logger)
    {
        _store = store;
        _tasks = tasks;
        _runner = runner;
        _gate = gate;
        _lease = lease;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            await _store.MigrateLegacyAsync(_tasks, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Assigned-work legacy migration failed");
        }

        if (_store.RunningId is { } openId)
            _logger.LogInformation("Open assignment will keep running: id={Id}", openId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureProbeCandidateAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Parked probe scan failed");
            }

            if (_store.RunningId is null)
            {
                await DelayAsync(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                continue;
            }

            // PROP-17.3: exclusive leisure lease — work runner does not tick.
            if (_lease.IsHeld
                && string.Equals(_lease.Holder, InferenceLease.LeisureHolder, StringComparison.Ordinal))
            {
                await DelayAsync(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);
                continue;
            }

            var delay = TimeSpan.FromSeconds(8);
            try
            {
                await _gate.WaitAsync(stoppingToken).ConfigureAwait(false);
                try
                {
                    if (_lease.IsHeld
                        && string.Equals(_lease.Holder, InferenceLease.LeisureHolder, StringComparison.Ordinal))
                    {
                        delay = TimeSpan.FromSeconds(3);
                    }
                    else if (_store.RunningId is not null)
                        delay = await _runner.RunOnePassAsync(stoppingToken).ConfigureAwait(false);
                }
                finally
                {
                    _gate.Release();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Assigned work pass failed; the pointer stays");
                delay = TimeSpan.FromSeconds(20);
            }

            await DelayAsync(delay, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// When idle, find one parked row whose park_until has elapsed and set it as running for a single probe.
    /// </summary>
    private async Task EnsureProbeCandidateAsync(CancellationToken cancellationToken)
    {
        if (_store.RunningId is not null)
            return;

        var parked = await _tasks.ListAsync("parked", cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        foreach (var row in parked)
        {
            if (string.IsNullOrWhiteSpace(row.ParkUntil))
                continue;
            if (!DateTimeOffset.TryParse(row.ParkUntil, out var until))
                continue;
            if (until > now)
                continue;
            _store.SetRunning(row.Id);
            _logger.LogInformation("Parked task due for probe: id={Id}", row.Id);
            return;
        }
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Host is stopping.
        }
    }
}
