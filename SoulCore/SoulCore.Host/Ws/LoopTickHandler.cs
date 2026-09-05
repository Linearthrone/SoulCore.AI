using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using SoulCore.Core.Abstractions;
using SoulCore.Protocol;

namespace SoulCore.Host.Ws;

/// <summary>Handles <c>loop.tick</c> frames — SoulLoop tick + ack on this socket.</summary>
public sealed class LoopTickHandler
{
    private readonly ISoulLoop _soulLoop;
    private readonly ILogger<LoopTickHandler> _logger;

    public LoopTickHandler(ISoulLoop soulLoop, ILogger<LoopTickHandler> logger)
    {
        _soulLoop = soulLoop ?? throw new ArgumentNullException(nameof(soulLoop));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(WebSocket socket, SoulCoreFrame frame, CancellationToken cancellationToken)
    {
        if (!_soulLoop.IsEnabled)
        {
            await WsFrameSender.SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.Error,
                    new
                    {
                        code = "soulloop.disabled",
                        message = "SoulLoop:Enabled=false (kill switch). No tick work."
                    },
                    id: frame.Id),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await _soulLoop.TickAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("loop.tick completed for frame {FrameId}", frame.Id);

        await WsFrameSender.SendFrameAsync(
            socket,
            SoulCoreFrame.Create(
                SoulCoreFrameTypes.LoopTickOk,
                new { ok = true },
                id: frame.Id),
            cancellationToken).ConfigureAwait(false);
    }
}
