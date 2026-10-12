using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoulCore.Inference.Presence;

namespace SoulCore.Host.Loop;

/// <summary>
/// While an assignment is active, keep taking the next step after the chat turn has returned.
/// </summary>
public sealed class AssignedWorkHostedService : BackgroundService
{
    private readonly IAssignedWorkStore _store;
    private readonly AssignedWorkRunner _runner;
    private readonly WorkTurnGate _gate;
    private readonly ILogger<AssignedWorkHostedService> _logger;

    public AssignedWorkHostedService(
        IAssignedWorkStore store,
        AssignedWorkRunner runner,
        WorkTurnGate gate,
        ILogger<AssignedWorkHostedService> logger)
    {
        _store = store;
        _runner = runner;
        _gate = gate;
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

        if (_store.Current is { Status: "active" } open)
            _logger.LogInformation("Open assignment will keep running: {Text}", Trim(open.Text));

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_store.Current is not { Status: "active" })
            {
                await DelayAsync(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                continue;
            }

            var delay = TimeSpan.FromSeconds(8);
            try
            {
                await _gate.WaitAsync(stoppingToken).ConfigureAwait(false);
                try
                {
                    if (_store.Current is { Status: "active" })
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
                _logger.LogWarning(ex, "Assigned work pass failed; the assignment stays open");
                delay = TimeSpan.FromSeconds(20);
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
            // Host is stopping.
        }
    }

    private static string Trim(string text)
        => text.Length <= 120 ? text : text[..117] + "...";
}
