using Content.Shared.CCVar;
using Content.Shared.CMU14.Diagnostics;
using Robust.Client.GameStates;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Diagnostics;

/// <summary>Reports state-application callbacks, independently of receipt ACKs and predicted simulation ticks.</summary>
public sealed partial class CMUClientStateHealthSystem : EntitySystem
{
    [Dependency] private IClientGameStateManager _states = default!;
    [Dependency] private IClientNetManager _net = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IGameTiming _timing = default!;

    private GameTick _appliedTick;
    private TimeSpan _appliedAt;
    private TimeSpan _nextReport;
    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();
        _states.GameStateApplied += OnStateApplied;
        _net.ClientConnectStateChanged += OnConnectionChanged;
        Subs.CVar(_config, CCVars.CMUClientStateHealthEnabled, value => _enabled = value, true);
    }

    public override void Shutdown()
    {
        _states.GameStateApplied -= OnStateApplied;
        _net.ClientConnectStateChanged -= OnConnectionChanged;
        base.Shutdown();
    }

    private void OnConnectionChanged(ClientConnectionState state)
    {
        _appliedTick = GameTick.Zero;
        _nextReport = _timing.RealTime + TimeSpan.FromSeconds(5);
    }

    private void OnStateApplied(GameStateAppliedArgs args)
    {
        // Reapplying the same state is not forward progress.
        if (args.AppliedState.ToSequence <= _appliedTick)
            return;
        _appliedTick = args.AppliedState.ToSequence;
        _appliedAt = _timing.RealTime;
    }

    public override void FrameUpdate(float frameTime)
    {
        if (!_enabled || !_net.IsConnected || _timing.RealTime < _nextReport)
            return;
        ReportHealth();
    }

    internal void ReportHealth()
    {
        _nextReport = _timing.RealTime + TimeSpan.FromSeconds(5);
        var fps = _timing.FramesPerSecondAvg;
        RaiseNetworkEvent(new CMUClientStateHealthEvent
        {
            AppliedTick = _appliedTick,
            AppliedAgeSeconds = _appliedTick == GameTick.Zero ? -1 : (_timing.RealTime - _appliedAt).TotalSeconds,
            BufferedStates = _states.StateCount,
            TargetBuffer = _states.TargetBufferSize,
            // No completed frame samples can produce infinity. Keep application evidence usable.
            AverageFps = double.IsFinite(fps) && fps is >= 0 and <= 10000 ? fps : -1,
        });
    }
}
