using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoulCore.Inference.Presence;
using SoulCore.Memory;

namespace SoulCore.Host.Loop;

/// <summary>
/// PROP-17.3: when no runnable released work, self-start R&amp;R on LeisureModel.
/// Takes the exclusive inference lease so the work runner does not tick.
/// </summary>
public sealed class LeisureHostedService : BackgroundService
{
    private readonly IAssignedWorkStore _store;
    private readonly IVictoriaTaskStore _tasks;
    private readonly ISurfaceAvailability _surfaces;
    private readonly IInferenceLease _lease;
    private readonly ILeisureSession _session;
    private readonly LeisureRunner _runner;
    private readonly WorkTurnGate _gate;
    private readonly ILogger<LeisureHostedService> _logger;

    public LeisureHostedService(
        IAssignedWorkStore store,
        IVictoriaTaskStore tasks,
        ISurfaceAvailability surfaces,
        IInferenceLease lease,
        ILeisureSession session,
        LeisureRunner runner,
        WorkTurnGate gate,
        ILogger<LeisureHostedService> logger)
    {
        _store = store;
        _tasks = tasks;
        _surfaces = surfaces;
        _lease = lease;
        _session = session;
        _runner = runner;
        _gate = gate;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(12), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(15);
            try
            {
                if (_lease.IsHeld
                    && !string.Equals(_lease.Holder, InferenceLease.LeisureHolder, StringComparison.Ordinal))
                {
                    await DelayAsync(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                var ledger = await _tasks.ListAsync(null, stoppingToken).ConfigureAwait(false);
                VictoriaTask? running = null;
                if (_store.RunningId is { } rid)
                    running = await _tasks.GetAsync(rid, stoppingToken).ConfigureAwait(false);

                var canRr = LeisureEligibility.CanEnterRestAndResearch(
                    _store.RunningId,
                    running,
                    ledger,
                    _surfaces.IsUp);

                if (!canRr)
                {
                    if (_session.IsActive
                        && string.Equals(_lease.Holder, InferenceLease.LeisureHolder, StringComparison.Ordinal))
                    {
                        _lease.Release(InferenceLease.LeisureHolder);
                        _session.Clear();
                    }

                    await DelayAsync(TimeSpan.FromSeconds(8), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                if (!_lease.TryAcquire(InferenceLease.LeisureHolder, TimeSpan.FromSeconds(90), out var abort))
                {
                    await DelayAsync(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    await _gate.WaitAsync(stoppingToken).ConfigureAwait(false);
                    try
                    {
                        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, abort);
                        if (!_session.IsActive)
                            _session.BeginOrContinue(LeisurePrompt.DefaultInterest);
                        delay = await _runner.RunOnePassAsync(linked.Token).ConfigureAwait(false);
                    }
                    finally
                    {
                        _gate.Release();
                    }
                }
                finally
                {
                    _lease.Release(InferenceLease.LeisureHolder);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Leisure R&R pass failed; staying quiet");
                delay = TimeSpan.FromSeconds(30);
                try { _lease.Release(InferenceLease.LeisureHolder); } catch { /* ignore */ }
            }

            await DelayAsync(delay, stoppingToken).ConfigureAwait(false);
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
            // Host stopping.
        }
    }
}
