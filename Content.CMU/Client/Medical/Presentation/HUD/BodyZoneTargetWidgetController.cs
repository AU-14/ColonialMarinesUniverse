using Content.Client.Gameplay;
using Content.Shared._CMU14.Input;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts.Events;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Input.Binding;
using MainViewport = Content.Client.UserInterface.Controls.MainViewport;
using ClientBodyZoneTargetingSystem = Content.Client._CMU14.Medical.Anatomy.BodyParts.BodyZoneTargetingSystem;

namespace Content.Client._CMU14.Medical.Presentation.HUD;

public sealed partial class BodyZoneTargetWidgetController :
    UIController,
    IOnStateEntered<GameplayState>,
    IOnStateExited<GameplayState>
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IEntityNetworkManager _net = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [UISystemDependency] private ClientBodyZoneTargetingSystem _bodyZone = default!;

    private BodyZoneTargetWidget? _widget;

    private static readonly TargetBodyZone[] CycleOrder =
    {
        TargetBodyZone.Head,
        TargetBodyZone.RightArm,
        TargetBodyZone.RightHand,
        TargetBodyZone.Chest,
        TargetBodyZone.GroinPelvis,
        TargetBodyZone.LeftArm,
        TargetBodyZone.LeftHand,
        TargetBodyZone.RightLeg,
        TargetBodyZone.RightFoot,
        TargetBodyZone.LeftLeg,
        TargetBodyZone.LeftFoot,
    };

    public override void Initialize()
    {
        base.Initialize();

        _input.SetInputCommand(CMUKeyFunctions.CMUCycleBodyZoneTarget,
            InputCmdHandler.FromDelegate(_ => CycleSelectedZone(1), handle: true));
        _input.SetInputCommand(CMUKeyFunctions.CMUCycleBodyZoneTargetReverse,
            InputCmdHandler.FromDelegate(_ => CycleSelectedZone(-1), handle: true));
        _input.SetInputCommand(CMUKeyFunctions.CMUTargetBodyZoneHead,
            InputCmdHandler.FromDelegate(_ => SelectSingleZone(TargetBodyZone.Head), handle: true));
        _input.SetInputCommand(CMUKeyFunctions.CMUTargetBodyZoneTorso,
            InputCmdHandler.FromDelegate(_ => SelectZoneGroup(TargetBodyZone.Chest, TargetBodyZone.GroinPelvis), handle: true));
        _input.SetInputCommand(CMUKeyFunctions.CMUTargetBodyZoneLeftArm,
            InputCmdHandler.FromDelegate(_ => SelectZoneGroup(TargetBodyZone.LeftArm, TargetBodyZone.LeftHand), handle: true));
        _input.SetInputCommand(CMUKeyFunctions.CMUTargetBodyZoneRightArm,
            InputCmdHandler.FromDelegate(_ => SelectZoneGroup(TargetBodyZone.RightArm, TargetBodyZone.RightHand), handle: true));
        _input.SetInputCommand(CMUKeyFunctions.CMUTargetBodyZoneLeftLeg,
            InputCmdHandler.FromDelegate(_ => SelectZoneGroup(TargetBodyZone.LeftLeg, TargetBodyZone.LeftFoot), handle: true));
        _input.SetInputCommand(CMUKeyFunctions.CMUTargetBodyZoneRightLeg,
            InputCmdHandler.FromDelegate(_ => SelectZoneGroup(TargetBodyZone.RightLeg, TargetBodyZone.RightFoot), handle: true));
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

        const float horizontalMargin = 8f;
        const float verticalMargin = 18f;
        var width = widget.MinSize.X;
        var height = widget.MinSize.Y;

        LayoutContainer.SetAnchorPreset(widget, LayoutContainer.LayoutPreset.BottomRight);
        LayoutContainer.SetMarginLeft(widget, -(horizontalMargin + width));
        LayoutContainer.SetMarginRight(widget, -horizontalMargin);
        LayoutContainer.SetMarginTop(widget, -(verticalMargin + height));
        LayoutContainer.SetMarginBottom(widget, -verticalMargin);
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

    private void SelectSingleZone(TargetBodyZone zone)
    {
        if (!ShouldShow())
            return;

        SelectZone(zone);
    }

    private void SelectZoneGroup(TargetBodyZone primary, TargetBodyZone secondary)
    {
        if (!ShouldShow())
            return;
        if (_player.LocalEntity is not { } local)
            return;
        if (!_entMan.TryGetComponent<BodyZoneTargetingComponent>(local, out var aim))
            return;

        var current = aim.LastSelectedAt == default
            ? (TargetBodyZone?) null
            : aim.Selected;

        SelectZone(current == primary ? secondary : primary);
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
