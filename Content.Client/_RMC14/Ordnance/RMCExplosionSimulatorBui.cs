using Content.Shared._RMC14.Ordnance;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Ordnance;

[UsedImplicitly]
public sealed class RMCExplosionSimulatorBui : BoundUserInterface
{
    private RMCExplosionSimulatorWindow? _window;
    private RMCExplosionSimulatorBuiState? _lastState;
    private float _processingSecondsLeft;

    public RMCExplosionSimulatorBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<RMCExplosionSimulatorWindow>();

        _window.MarinesButton.OnPressed += _ =>
            SendPredictedMessage(new RMCExplosionSimulatorTargetMsg(RMCExplosionSimulatorTarget.Marines));
        _window.SpecialForcesButton.OnPressed += _ =>
            SendPredictedMessage(new RMCExplosionSimulatorTargetMsg(RMCExplosionSimulatorTarget.SpecialForces));
        _window.XenomorphsButton.OnPressed += _ =>
            SendPredictedMessage(new RMCExplosionSimulatorTargetMsg(RMCExplosionSimulatorTarget.Xenomorphs));

        _window.SimulateButton.OnPressed += _ =>
            SendPredictedMessage(new RMCExplosionSimulatorSimulateMsg());
        _window.ReplayButton.OnPressed += _ =>
            SendPredictedMessage(new RMCExplosionSimulatorReplayMsg());

        _window.OnFrameUpdate += OnFrameUpdate;

        if (_lastState != null)
            _window.UpdateState(_lastState, _processingSecondsLeft);
    }

    private void OnFrameUpdate(FrameEventArgs args)
    {
        if (_window == null || _lastState == null || !_lastState.IsProcessing)
            return;

        _processingSecondsLeft = MathF.Max(0f, _processingSecondsLeft - args.DeltaSeconds);
        _window.UpdateState(_lastState, _processingSecondsLeft);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not RMCExplosionSimulatorBuiState simulatorState)
            return;

        _lastState = simulatorState;
        _processingSecondsLeft = simulatorState.ProcessingSecsLeft;

        if (_window == null)
            return;

        _window.UpdateState(simulatorState, _processingSecondsLeft);
    }
}
