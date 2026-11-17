using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Camera;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Optical Zoom (playtest 3), copied from <c>YautjaMaskSystem.SetZoom</c> so it behaves like the
/// bio-mask: the zoom is server-authoritative, the offset is fixed toward the facing direction when
/// toggled, and the PVS range grows with the view so the far edge does not pop in. Removing the
/// component (stand-down) puts the eye back.
/// </summary>
public sealed class EWJoeOpticalZoomSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContentEyeSystem _contentEye = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeOpticalZoomComponent, EWJoeOpticalZoomActionEvent>(OnToggle);
        SubscribeLocalEvent<EWJoeOpticalZoomComponent, GetEyeOffsetEvent>(OnGetEyeOffset);
        SubscribeLocalEvent<EWJoeOpticalZoomComponent, GetEyePvsScaleEvent>(OnGetEyePvsScale);
        SubscribeLocalEvent<EWJoeOpticalZoomComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnToggle(Entity<EWJoeOpticalZoomComponent> ent, ref EWJoeOpticalZoomActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Action = args.Action.Owner;

        if (_net.IsClient)
            return;

        SetZoom(ent, !ent.Comp.Zoomed);
    }

    public void SetZoom(Entity<EWJoeOpticalZoomComponent> ent, bool zoomed, bool feedback = true)
    {
        if (_net.IsClient || ent.Comp.Zoomed == zoomed)
            return;

        ent.Comp.Zoomed = zoomed;
        ent.Comp.Offset = zoomed ? GetOffset(ent) : Vector2.Zero;
        Dirty(ent);
        _actions.SetToggled(ent.Comp.Action, zoomed);

        var eye = EnsureComp<ContentEyeComponent>(ent);
        if (zoomed)
            _contentEye.SetZoom(ent, Vector2.One * ent.Comp.ZoomLevel, true, eye);
        else
            _contentEye.ResetZoom(ent, eye);

        RefreshEye(ent);

        if (feedback)
            _popup.PopupEntity(Loc.GetString(zoomed ? "cmu-ew-joe-optics-on" : "cmu-ew-joe-optics-off"), ent, ent);
    }

    private Vector2 GetOffset(Entity<EWJoeOpticalZoomComponent> ent)
    {
        var direction = Transform(ent).LocalRotation.GetCardinalDir();
        return direction.ToVec() * ((ent.Comp.ZoomOffset * ent.Comp.ZoomLevel - 1) / 2);
    }

    private void OnGetEyeOffset(Entity<EWJoeOpticalZoomComponent> ent, ref GetEyeOffsetEvent args)
    {
        if (ent.Comp.Zoomed && ent.Comp.LifeStage < ComponentLifeStage.Stopping)
            args.Offset += ent.Comp.Offset;
    }

    /// <summary>Same estimate XenoZoom uses: scale PVS with the widened view, never down.</summary>
    private void OnGetEyePvsScale(Entity<EWJoeOpticalZoomComponent> ent, ref GetEyePvsScaleEvent args)
    {
        if (!ent.Comp.Zoomed || ent.Comp.LifeStage >= ComponentLifeStage.Stopping)
            return;

        const float pvsOverheadEstimate = 0.2f;
        var scale = (ent.Comp.ZoomLevel + pvsOverheadEstimate) / (1 + pvsOverheadEstimate);
        args.Scale += Math.Max(0, scale - 1);
    }

    private void OnShutdown(Entity<EWJoeOpticalZoomComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent) || !ent.Comp.Zoomed || _net.IsClient)
            return;

        if (TryComp(ent, out ContentEyeComponent? eye))
            _contentEye.ResetZoom(ent, eye);

        RefreshEye(ent);
    }

    private void RefreshEye(EntityUid uid)
    {
        if (TryComp(uid, out EyeComponent? eyeComponent))
            _contentEye.UpdateEyeOffset((uid, eyeComponent));

        _contentEye.UpdatePvsScale(uid);
    }
}
