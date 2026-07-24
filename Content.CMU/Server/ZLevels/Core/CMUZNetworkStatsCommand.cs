using System.Linq;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._CMU14.ZLevels.Core;

/// <summary>
/// Opt-in diagnostics for controlled real-network Phase 4 captures.
/// </summary>
public sealed partial class CMUZNetworkStatsCommand : IConsoleCommand
{
    [Dependency] private INetManager _network = default!;
    [Dependency] private ISharedPlayerManager _players = default!;

    private IConsoleShell? _armedShell;

    public string Command => "cmu_znet_stats";
    public string Description => "Reports real network totals and per-message bandwidth, resets counters, or arms an InGame snapshot.";
    public string Help => "Usage: cmu_znet_stats [reset|arm]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1 ||
            args.Length == 1 &&
            !args[0].Equals("reset", StringComparison.OrdinalIgnoreCase) &&
            !args[0].Equals("arm", StringComparison.OrdinalIgnoreCase))
        {
            shell.WriteError(Help);
            return;
        }

        if (args.Length == 1 && args[0].Equals("arm", StringComparison.OrdinalIgnoreCase))
        {
            _players.PlayerStatusChanged -= OnPlayerStatusChanged;
            _armedShell = shell;
            _players.PlayerStatusChanged += OnPlayerStatusChanged;
            shell.WriteLine("CMU Z net stats: armed for the next InGame session.");
            return;
        }

        if (args.Length == 1)
        {
            _network.ResetBandwidthMetrics();
            shell.WriteLine("CMU Z net stats: per-message counters reset.");
        }

        WriteStats(shell, "CMU Z net stats");
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.InGame ||
            _armedShell is not { } shell)
        {
            return;
        }

        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
        _armedShell = null;
        WriteStats(shell, "CMU Z net InGame");
    }

    private void WriteStats(IConsoleShell shell, string prefix)
    {
        var stats = _network.Statistics;
        shell.WriteLine(
            $"{prefix}: channels={_network.ChannelCount}, " +
            $"sentBytes={stats.SentBytes}, receivedBytes={stats.ReceivedBytes}, " +
            $"sentPackets={stats.SentPackets}, receivedPackets={stats.ReceivedPackets}");

        foreach (var (type, bytes) in _network.MessageBandwidthUsage.OrderBy(entry => entry.Key.FullName))
            shell.WriteLine($"CMU Z net message: type={type.FullName}, bytes={bytes}");
    }
}
