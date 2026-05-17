using Content.Client._RMC14.Xenonids.UI;
using Content.Shared._CMU14.Underground.BurrowerTunnel;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Underground.BurrowerTunnel;

[UsedImplicitly]
public sealed class BurrowerTunnelBui : BoundUserInterface
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private BurrowerTunnelWindow? _window;
    private BurrowerTunnelBuiState? _lastState;

    public BurrowerTunnelBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BurrowerTunnelWindow>();
        Rebuild();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BurrowerTunnelBuiState s)
        {
            _lastState = s;
            Rebuild();
        }
    }

    private void Rebuild()
    {
        if (_window == null)
            return;

        _window.TunnelContainer.RemoveAllChildren();

        // Hive Tunnel button
        var hiveTunnelControl = new XenoChoiceControl();
        hiveTunnelControl.Button.ToggleMode = true;

        var hiveName = Loc.GetString("cmu-underground-tunnel-hive-tunnel");
        if (_lastState?.HiveTunnelCooldownRemaining > TimeSpan.Zero)
        {
            var seconds = (int)_lastState.HiveTunnelCooldownRemaining.TotalSeconds;
            hiveName += $" [{Loc.GetString("cmu-underground-tunnel-cooldown", ("seconds", seconds))}]";
            hiveTunnelControl.Button.Disabled = true;
        }

        hiveTunnelControl.Set(hiveName, null);
        hiveTunnelControl.Button.Pressed = _lastState?.Selected == BurrowerTunnelType.HiveTunnel;
        hiveTunnelControl.Button.OnPressed += _ =>
        {
            SendPredictedMessage(new BurrowerTunnelChosenBuiMsg(BurrowerTunnelType.HiveTunnel));
            Close();
        };
        _window.TunnelContainer.AddChild(hiveTunnelControl);

        // Underground Entrance button
        var entranceControl = new XenoChoiceControl();
        entranceControl.Button.ToggleMode = true;

        var entranceName = Loc.GetString("cmu-underground-tunnel-underground-entrance");
        entranceControl.Set(entranceName, null);
        entranceControl.Button.Pressed = _lastState?.Selected == BurrowerTunnelType.UndergroundEntrance;
        entranceControl.Button.OnPressed += _ =>
        {
            SendPredictedMessage(new BurrowerTunnelChosenBuiMsg(BurrowerTunnelType.UndergroundEntrance));
            Close();
        };
        _window.TunnelContainer.AddChild(entranceControl);
    }
}
