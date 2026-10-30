using Content.Shared.Actions.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared._RMC14.Xenonids.Rest;

// CMU14 class: rest toggle for mobs that use the xeno Rest action without being xenos (Wendigos).
// XenoRestingComponent is access-restricted to this system, so the toggle lives here and mirrors OnXenoRestAction.
public sealed partial class XenoRestSystem
{
    /// <summary>
    /// Toggles resting on a non-xeno the same way the xeno Rest action does. Returns false if it was refused.
    /// </summary>
    public bool CMUToggleRest(EntityUid uid, Entity<ActionComponent>? action)
    {
        if (_timing.ApplyingState)
            return false;

        var attempt = new XenoRestAttemptEvent();
        RaiseLocalEvent(uid, ref attempt);
        if (attempt.Cancelled)
            return false;

        var resting = !HasComp<XenoRestingComponent>(uid);
        if (resting)
            AddComp<XenoRestingComponent>(uid);
        else
            RemComp<XenoRestingComponent>(uid);

        _appearance.SetData(uid, XenoVisualLayers.Base, resting ? XenoRestState.Resting : XenoRestState.NotResting);
        if (action is { } act)
            _actions.SetToggled(act.AsNullable(), resting);

        _actionBlocker.UpdateCanMove(uid);

        var ev = new XenoRestEvent(resting);
        RaiseLocalEvent(uid, ref ev);
        return true;
    }
}
