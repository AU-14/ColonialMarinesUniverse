using Content.Client.Gameplay;
using Content.Shared._CMU14.Input;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Input.Binding;
using MainViewport = Content.Client.UserInterface.Controls.MainViewport;
using ClientBodyZoneTargetingSystem = Content.Client._CMU14.Medical.BodyPart.BodyZoneTargetingSystem;

namespace Content.Client._CMU14.Medical.HUD;

public sealed class BodyZoneTargetWidgetController :
    UIController,
    IOnStateEntered<GameplayState>,
    IOnStateExited<GameplayState>
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IEntityNetworkManager _net = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [UISystemDependency] private readonly ClientBodyZoneTargetingSystem _bodyZone = default!;

    private BodyZoneTargetWidget? _widget;

    private static readonly TargetBodyZone[] CycleOrder =
    {
        TargetBodyZone.Head,
        TargetBodyZone.RightArm,
        TargetBodyZone.Chest,
        TargetBodyZone.LeftArm,
        TargetBodyZone.RightLeg,
        TargetBodyZone.LeftLeg,
    };

    public override void Initialize()
    {
        base.Initialize();

        _input.SetInputCommand(CMUKeyFunctions.CMUCycleBodyZoneTarget,
            InputCmdHandler.FromDelegate(_ => CycleSelectedZone(1), handle: true));
        _input.SetInputCommand(CMUKeyFunctions.CMUCycleBodyZoneTargetReverse,
            InputCmdHandler.FromDelegate(_ => CycleSelectedZone(-1), handle: true));
    }

    public void OnStateEntered(GameplayState state)
    {
        if (_widget != null)
            return;

        _widget = new BodyZoneTargetWidget();
        _widget.ZoneClicked += OnZoneClicked;
        _widget.GetSelectedZone = GetLocalSelectedZone;

        AttachToHud(_widget);

        _player.LocalPlayerAttached += OnLocalAttached;
        _player.LocalPlayerDetached += OnLocalDetached;
        _cfg.OnValueChanged(CMUMedicalCCVars.Enabled, OnGateCvarChanged);
        _cfg.OnValueChanged(CMUMedicalCCVars.HitLocationEnabled, OnGateCvarChanged);

        RefreshVisibility();
    }

    public void OnStateExited(GameplayState state)
    {
        if (_widget == null)
            return;

        _player.LocalPlayerAttached -= OnLocalAttached;
        _player.LocalPlayerDetached -= OnLocalDetached;
        _cfg.UnsubValueChanged(CMUMedicalCCVars.Enabled, OnGateCvarChanged);
        _cfg.UnsubValueChanged(CMUMedicalCCVars.HitLocationEnabled, OnGateCvarChanged);

        _widget.ZoneClicked -= OnZoneClicked;
        _widget.Orphan();
        _widget = null;
    }

    private void AttachToHud(BodyZoneTargetWidget widget)
    {
        var screen = UIManager.ActiveScreen;
        var viewport = screen?.GetWidget<MainViewport>();
        var parent = viewport?.Parent ?? (Control?)screen ?? UIManager.RootControl;
        parent.AddChild(widget);

        const float margin = 8f;
        var width = widget.MinSize.X;
        var height = widget.MinSize.Y;

        LayoutContainer.SetAnchorPreset(widget, LayoutContainer.LayoutPreset.BottomRight);
        LayoutContainer.SetMarginLeft(widget, -(margin + width));
        LayoutContainer.SetMarginRight(widget, -margin);
        LayoutContainer.SetMarginTop(widget, -(margin + height));
        LayoutContainer.SetMarginBottom(widget, -margin);
    }

    private void OnLocalAttached(EntityUid uid) => RefreshVisibility();
    private void OnLocalDetached(EntityUid uid) => RefreshVisibility();
    private void OnGateCvarChanged(bool _) => RefreshVisibility();

    private void RefreshVisibility()
    {
        if (_widget == null)
            return;
        _widget.Visible = ShouldShow();
    }

    private bool ShouldShow()
    {
        if (!_cfg.GetCVar(CMUMedicalCCVars.Enabled))
            return false;
        if (!_cfg.GetCVar(CMUMedicalCCVars.HitLocationEnabled))
            return false;
        if (_player.LocalEntity is not { } local)
            return false;
        return _entMan.HasComponent<BodyZoneTargetingComponent>(local);
    }

    private void OnZoneClicked(TargetBodyZone zone)
    {
        SelectZone(zone);
    }

    private void CycleSelectedZone(int direction)
    {
        if (!ShouldShow())
            return;
        if (_player.LocalEntity is not { } local)
            return;
        if (!_entMan.TryGetComponent<BodyZoneTargetingComponent>(local, out var aim))
            return;

        SelectZone(CycleZone(aim.Selected, direction));
    }

    private void SelectZone(TargetBodyZone zone)
    {
        if (_player.LocalEntity is { } local &&
            _entMan.HasComponent<BodyZoneTargetingComponent>(local))
        {
            _bodyZone.SelectZone(local, zone);
        }

        _net.SendSystemNetworkMessage(new BodyZoneTargetSelectedMessage(zone));
    }

    private static TargetBodyZone CycleZone(TargetBodyZone current, int direction)
    {
        var idx = System.Array.IndexOf(CycleOrder, current);
        if (idx < 0)
            idx = System.Array.IndexOf(CycleOrder, TargetBodyZone.Chest);

        var len = CycleOrder.Length;
        return CycleOrder[(idx + direction + len) % len];
    }

    private TargetBodyZone? GetLocalSelectedZone()
    {
        if (_player.LocalEntity is not { } local)
            return null;
        if (!_entMan.TryGetComponent<BodyZoneTargetingComponent>(local, out var aim))
            return null;
        return aim.Selected;
    }
}
