using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Fireman;
using Content.Shared.DoAfter;
using Content.Shared.Popups;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Fireman;

public sealed partial class FiremanCarrySystem
{
    // CanThrow lives on the carried mob and humans have it off, so preds couldn't throw who they were carrying.
    // false means nothing changed and there's nothing to put back
    public bool TryAllowCarriedThrow(EntityUid carrier, EntityUid carried)
    {
        if (!TryComp(carried, out FiremanCarriableComponent? carriable) ||
            carriable.CanThrow ||
            !carriable.BeingCarried ||
            !TryComp(carrier, out CanFiremanCarryComponent? carry) ||
            carry.Carrying != carried)
        {
            return false;
        }

        carriable.CanThrow = true;
        Dirty(carried, carriable);
        return true;
    }

    public void SetCarriedThrowable(EntityUid carried, bool canThrow)
    {
        if (!TryComp(carried, out FiremanCarriableComponent? carriable) || carriable.CanThrow == canThrow)
            return;

        carriable.CanThrow = canThrow;
        Dirty(carried, carriable);
    }

    public bool IsAggressivelyGrabbed(EntityUid target)
    {
        return IsBeingAggressivelyGrabbed(target);
    }

    /// <summary>
    /// FLOW-02: whether <paramref name="carrier"/> could aggressively grab <paramref name="target"/> right now, by the
    /// same rules as the second Ctrl-click, without any popup. Does not check the upgrade delay.
    /// </summary>
    public bool CanAggressiveGrab(Entity<CanFiremanCarryComponent?> carrier, EntityUid target)
    {
        if (!Resolve(carrier, ref carrier.Comp, false) || carrier.Comp.AggressiveGrab)
            return false;

        return ValidateAggressiveGrab((carrier.Owner, carrier.Comp), target, popup: false);
    }

    /// <summary>
    /// FLOW-02: sets how long after a pull starts the grabber may upgrade it (the Early Warning Joe's Machine Grip sets
    /// zero and restores the original on stand-down). The field is restricted to this system.
    /// </summary>
    public void SetAggressiveGrabDelay(Entity<CanFiremanCarryComponent?> carrier, TimeSpan delay)
    {
        if (!Resolve(carrier, ref carrier.Comp, false) || carrier.Comp.AggressiveGrabDelay == delay)
            return;

        carrier.Comp.AggressiveGrabDelay = delay;
        Dirty(carrier);
    }

    /// <summary>
    /// FLOW-02: how much longer than <see cref="FiremanCarriableComponent.Delay"/> breaking free of
    /// <paramref name="target"/>'s current puller takes. 1 without a <see cref="CMUGrabEscapeMultiplierComponent"/>.
    /// </summary>
    public float GetGrabEscapeMultiplier(EntityUid target)
    {
        if (!_rmcPulling.IsBeingPulled(target, out var puller) ||
            !TryComp(puller, out CMUGrabEscapeMultiplierComponent? escape))
        {
            return 1f;
        }

        return Math.Max(0f, escape.Multiplier);
    }

    /// <summary>
    /// FLOW-02: a hold restarts the victim's struggle. When <paramref name="target"/> is mid break-free do-after, that
    /// do-after is cancelled and a fresh one of the full length is started, as if it had just begun to struggle.
    /// Returns false when the target was not struggling.
    /// </summary>
    public bool RestartBreakFree(EntityUid target)
    {
        if (!TryComp(target, out FiremanCarriableComponent? carriable) ||
            !carriable.BreakingFree ||
            !TryComp(target, out DoAfterComponent? doAfters))
        {
            return false;
        }

        DoAfterId? struggle = null;
        foreach (var doAfter in doAfters.DoAfters.Values)
        {
            if (doAfter.Cancelled || doAfter.Completed || doAfter.Args.Event is not BreakFiremanCarryDoAfterEvent)
                continue;

            struggle = doAfter.Id;
            break;
        }

        if (struggle == null)
            return false;

        // the cancellation runs OnCarriableBreakCarryDoAfter, which clears BreakingFree
        _doAfter.Cancel(struggle, doAfters);

        if (!carriable.BeingCarried && !IsBeingAggressivelyGrabbed(target))
            return true;

        var ev = new BreakFiremanCarryDoAfterEvent();
        var restart = new DoAfterArgs(EntityManager, target, carriable.Delay * GetGrabEscapeMultiplier(target), ev, target);
        if (!_doAfter.TryStartDoAfter(restart))
            return true;

        carriable.BreakingFree = true;
        Dirty(target, carriable);
        _popup.PopupEntity(Loc.GetString("cmu-grab-struggle-reset"), target, target, PopupType.MediumCaution);
        return true;
    }

    /// <summary>
    /// FLOW-02: the CMU way through the xeno gate in ValidateAggressiveGrab, for a grabber with a
    /// <see cref="CMUXenoGrabberComponent"/>. <paramref name="onCooldown"/> is set when only the grab cooldown refused it.
    /// </summary>
    private bool CMUCanGrabXeno(EntityUid carrier, EntityUid target, out bool onCooldown)
    {
        onCooldown = false;
        if (!TryComp(carrier, out CMUXenoGrabberComponent? grabber) || !IsGrabbableXenoBody(grabber, target))
            return false;

        if (_timing.CurTime < grabber.NextGrab)
        {
            onCooldown = true;
            return false;
        }

        return true;
    }

    /// <summary>
    /// FLOW-02: whether <paramref name="grabber"/>'s owner may grab <paramref name="target"/> by caste alone (no cooldown):
    /// a carriable xeno no higher than <see cref="CMUXenoGrabberComponent.MaxTier"/> and no larger than
    /// <see cref="CMUXenoGrabberComponent.MaxSize"/> (so T3s, not the heavies), passing the optional lists.
    /// The pull-paralyze exemption asks the same question, so the pull and the grab agree.
    /// </summary>
    public bool IsGrabbableXenoBody(CMUXenoGrabberComponent grabber, EntityUid target)
    {
        // the grab blocks the xeno's movement and standing through FiremanCarriable; without it the grab does nothing
        if (!TryComp(target, out XenoComponent? xeno) || !HasComp<FiremanCarriableComponent>(target))
            return false;

        if (xeno.Tier > grabber.MaxTier)
            return false;

        if (TryComp(target, out RMCSizeComponent? size) && size.Size > grabber.MaxSize)
            return false;

        if (grabber.Whitelist != null && !_whitelist.IsValid(grabber.Whitelist, target))
            return false;

        return !_whitelist.IsWhitelistPass(grabber.Blacklist, target);
    }
}
