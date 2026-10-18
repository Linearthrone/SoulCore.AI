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
    private readonly IVictoriaResearchStore? _research;
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
        ILogger<LeisureHostedService> logger,
        IVictoriaResearchStore? research = null)
    {
        _store = store;
        _tasks = tasks;
        _research = research;
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
                        {
                            var interest = await PickInterestAsync(linked.Token).ConfigureAwait(false);
                            _session.BeginOrContinue(interest);
                        }
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
                _lease.Release(InferenceLease.LeisureHolder);
            }

            await DelayAsync(delay, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Prefer an existing research subject (least recently updated) so the band
    /// does not forever claim the PROP-17 example "neural interfaces".
    /// </summary>
    private async Task<string> PickInterestAsync(CancellationToken cancellationToken)
    {
        if (_research is null)
            return LeisurePrompt.DefaultInterest;
        try
        {
            var subjects = await _research.ListSubjectsAsync(cancellationToken).ConfigureAwait(false);
            if (subjects.Count == 0)
                return LeisurePrompt.DefaultInterest;
            // ListSubjects is updated_at DESC — take the oldest for rotation.
            return subjects[^1].Title;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "R&R interest pick from research subjects failed");
            return LeisurePrompt.DefaultInterest;
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
