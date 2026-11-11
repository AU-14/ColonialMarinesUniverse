using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Targeting;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;

namespace Content.Shared.CMU14.Weapons.ATGM;

/// <summary>
///     Spotter lock-on: the spotter clicks an entity with the linked finder, and missiles fired while the lock
///     holds home on that entity. The lock is shown with RMC's targeting laser and reticle.
/// </summary>
/// <remarks>
///     Locks are server-authoritative. The handlers mark the click handled on the client too so nothing else
///     acts on it, but only the server validates, changes <see cref="CMUATGMFinderComponent.LockedTarget"/>
///     and starts the laser; the networked state and popups then reach the client.
///     The lock persists after a shot, so the spotter can keep lasing for follow-up missiles. It ends when the
///     spotter clears it (unique action), drops, stows or deselects the finder, moves (RMC cancels the laser),
///     the laser times out, the finder is unlinked, or the target is gone.
///     While a missile guided through the finder is in flight the spotter can neither lock a new target nor
///     clear the lock (FLOW-07); losing the finder still cuts the wire and the lock as above, and the missile
///     keeps the target it copied at launch.
/// </remarks>
public sealed partial class CMUATGMSystem
{
    private static readonly TimeSpan LockValidationInterval = TimeSpan.FromSeconds(0.5);

    private TimeSpan _nextLockValidation;

    private void InitializeLock()
    {
        SubscribeLocalEvent<CMUATGMFinderComponent, AfterInteractEvent>(OnFinderAfterInteract);
        SubscribeLocalEvent<CMUATGMFinderComponent, UniqueActionEvent>(OnFinderUniqueAction);
        SubscribeLocalEvent<CMUATGMFinderComponent, GotUnequippedHandEvent>(OnFinderUnequipped);
        SubscribeLocalEvent<CMUATGMFinderComponent, HandDeselectedEvent>(OnFinderDeselected);
        SubscribeLocalEvent<CMUATGMFinderComponent, TargetingCancelledEvent>(OnFinderTargetingCancelled);
        SubscribeLocalEvent<CMUATGMFinderComponent, TargetingFinishedEvent>(OnFinderTargetingFinished);
    }

    public override void Update(float frameTime)
    {
        // Lock targets can be deleted at any time; check the few finders periodically instead of
        // subscribing to every entity's termination.
        if (_net.IsClient || _timing.CurTime < _nextLockValidation)
            return;

        _nextLockValidation = _timing.CurTime + LockValidationInterval;
        var query = EntityQueryEnumerator<CMUATGMFinderComponent>();
        while (query.MoveNext(out var uid, out var finder))
        {
            if (finder.LockedTarget != null)
                TryGetLock((uid, finder), out _);
        }
    }

    private void OnFinderAfterInteract(Entity<CMUATGMFinderComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // Clicking empty ground while steering by cursor is normal; only a clicked entity is a lock attempt.
        if (_net.IsClient || args.Target == null)
            return;

        TryLock(ent, args.User, args.Target);
    }

    private void OnFinderUniqueAction(Entity<CMUATGMFinderComponent> ent, ref UniqueActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (_net.IsClient || !_hands.IsHolding(args.UserUid, ent))
            return;

        if (IsMissileInFlight(ent))
        {
            _popup.PopupEntity(Loc.GetString("cmu-atgm-lock-in-flight"), args.UserUid, args.UserUid, PopupType.SmallCaution);
            return;
        }

        var message = ClearLock(ent) ? "cmu-atgm-lock-cleared" : "cmu-atgm-lock-none";
        _popup.PopupEntity(Loc.GetString(message), args.UserUid, args.UserUid);
    }

    private void OnFinderUnequipped(Entity<CMUATGMFinderComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (_net.IsServer)
            ClearLockPopup(ent, args.User);
    }

    private void OnFinderDeselected(Entity<CMUATGMFinderComponent> ent, ref HandDeselectedEvent args)
    {
        if (_net.IsServer)
            ClearLockPopup(ent, args.User);
    }

    private void OnFinderTargetingCancelled(Entity<CMUATGMFinderComponent> ent, ref TargetingCancelledEvent args)
    {
        // RMC removes the laser itself right after raising this (e.g. the spotter moved).
        if (_net.IsClient || ent.Comp.LockedTarget is not { } target)
            return;

        ent.Comp.LockedTarget = null;
        Dirty(ent);
        if (_hands.IsHeld(ent.Owner, out var holder))
            _popup.PopupEntity(Loc.GetString("cmu-atgm-lock-broken", ("target", target)), holder.Value, holder.Value, PopupType.SmallCaution);
    }

    private void OnFinderTargetingFinished(Entity<CMUATGMFinderComponent> ent, ref TargetingFinishedEvent args)
    {
        if (_net.IsClient)
            return;

        // The laser ran its full duration. RMC only drops the countdown, so stop the laser here.
        if (TryComp(ent, out TargetingComponent? targeting))
            _targeting.StopTargeting((ent, targeting), args.Target);

        if (ent.Comp.LockedTarget != args.Target)
            return;

        ent.Comp.LockedTarget = null;
        Dirty(ent);
        if (_hands.IsHeld(ent.Owner, out var holder))
            _popup.PopupEntity(Loc.GetString("cmu-atgm-lock-expired", ("target", args.Target)), holder.Value, holder.Value, PopupType.SmallCaution);
    }

    /// <summary>
    ///     Tries to lock <paramref name="finder"/> onto <paramref name="clicked"/> for <paramref name="user"/>,
    ///     popping up why not. Server only. A click on part of a vehicle locks the whole vehicle.
    /// </summary>
    public bool TryLock(Entity<CMUATGMFinderComponent> finder, EntityUid user, EntityUid? clicked)
    {
        if (!CanLock(finder, user, clicked, out var target, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), user, user, PopupType.SmallCaution);
            return false;
        }

        ClearLock(finder);
        finder.Comp.LockedTarget = target;
        finder.Comp.NextLock = _timing.CurTime + finder.Comp.LockCooldown;
        Dirty(finder);

        _targeting.Target(finder, user, target.Value, finder.Comp.LockDuration, TargetedEffects.Spotted);
        _popup.PopupEntity(Loc.GetString("cmu-atgm-lock-acquired", ("target", target.Value)), user, user);
        return true;
    }

    /// <summary>
    ///     Validates a lock attempt without changing anything.
    /// </summary>
    /// <param name="target">The entity that would be locked: the clicked entity or its root vehicle.</param>
    /// <param name="reason">Locale id explaining why the lock is refused.</param>
    public bool CanLock(
        Entity<CMUATGMFinderComponent> finder,
        EntityUid user,
        EntityUid? clicked,
        [NotNullWhen(true)] out EntityUid? target,
        [NotNullWhen(false)] out string? reason)
    {
        target = null;
        if (clicked is not { } clickedUid || TerminatingOrDeleted(clickedUid))
        {
            reason = "cmu-atgm-lock-no-target";
            return false;
        }

        if (finder.Comp.LinkedLauncher is not { } launcherUid ||
            !TryComp(launcherUid, out CMUATGMLauncherComponent? launcher) ||
            !IsLinked((launcherUid, launcher)))
        {
            reason = "cmu-atgm-lock-not-linked";
            return false;
        }

        if (!_skills.HasSkill(user, launcher.Skill, 1))
        {
            reason = "cmu-atgm-lock-unskilled";
            return false;
        }

        var resolved = ResolveLockTarget(clickedUid);
        EntityUid? mount = TryComp(launcherUid, out MountableWeaponComponent? mountable)
            ? GetEntity(mountable.MountedTo)
            : null;
        if (resolved == user ||
            resolved == finder.Owner ||
            resolved == launcherUid ||
            resolved == mount ||
            IsCrewOrCarried(resolved, user, launcherUid, mount))
        {
            reason = "cmu-atgm-lock-invalid-target";
            return false;
        }

        if (IsMissileInFlight(finder))
        {
            reason = "cmu-atgm-lock-in-flight";
            return false;
        }

        if (_timing.CurTime < finder.Comp.NextLock)
        {
            reason = "cmu-atgm-lock-cooldown";
            return false;
        }

        // The target's own occluder (a wall, a vehicle hull) must not block the view of itself.
        if (!_examine.InRangeUnOccluded(user, resolved, finder.Comp.LockRange, uid => uid == resolved))
        {
            reason = "cmu-atgm-lock-no-sight";
            return false;
        }

        target = resolved;
        reason = null;
        return true;
    }

    /// <summary>
    ///     Whether a missile guided through <paramref name="finder"/> is still in flight. Its
    ///     <see cref="CMUGuidedMissileComponent.Finder"/> is networked, so this holds on the client too. The
    ///     field is cleared when the wire is cut, and the component is removed when the missile detonates.
    /// </summary>
    public bool IsMissileInFlight(EntityUid finder)
    {
        var query = EntityQueryEnumerator<CMUGuidedMissileComponent>();
        while (query.MoveNext(out var uid, out var missile))
        {
            if (missile.Finder == finder && !TerminatingOrDeleted(uid))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     The entity a click on <paramref name="clicked"/> locks: the outermost vehicle it is part of (turret
    ///     overlays, hardpoints and other vehicle children), or the clicked entity itself.
    /// </summary>
    public EntityUid ResolveLockTarget(EntityUid clicked)
    {
        var result = clicked;
        var current = clicked;
        // Bounded walk; transform hierarchies are shallow and must not loop.
        for (var i = 0; i < 32 && current.IsValid(); i++)
        {
            if (HasComp<VehicleComponent>(current))
                result = current;

            current = Transform(current).ParentUid;
        }

        return result;
    }

    /// <summary>
    ///     Gets the finder's lock if its target still exists on the same map, clearing it (with a popup to the
    ///     holder, on the server) otherwise.
    /// </summary>
    public bool TryGetLock(Entity<CMUATGMFinderComponent> finder, [NotNullWhen(true)] out EntityUid? target)
    {
        target = finder.Comp.LockedTarget;
        if (target is not { } uid)
            return false;

        if (!TerminatingOrDeleted(uid) &&
            !EntityManager.IsQueuedForDeletion(uid) &&
            _transform.GetMapId(uid) == _transform.GetMapId(finder.Owner))
        {
            return true;
        }

        target = null;
        if (_net.IsServer && _hands.IsHeld(finder.Owner, out var holder))
            ClearLockPopup(finder, holder.Value);
        else
            ClearLock(finder);

        return false;
    }

    /// <summary>
    ///     Drops the finder's lock and its laser.
    /// </summary>
    /// <returns>Whether there was a lock to drop.</returns>
    public bool ClearLock(Entity<CMUATGMFinderComponent> finder)
    {
        if (finder.Comp.LockedTarget is not { } target)
            return false;

        finder.Comp.LockedTarget = null;
        Dirty(finder);

        if (!TryComp(finder, out TargetingComponent? targeting))
            return true;

        _targeting.StopTargeting((finder, targeting), target);

        // StopTargeting keeps the component (and stale countdowns) while anyone else still lases the same
        // target; drop it so a later lock starts clean.
        if (TryComp(finder, out targeting) && targeting.Targets.Count == 0)
            RemComp<TargetingComponent>(finder);

        return true;
    }

    private void ClearLockPopup(Entity<CMUATGMFinderComponent> finder, EntityUid user)
    {
        if (ClearLock(finder))
            _popup.PopupEntity(Loc.GetString("cmu-atgm-lock-lost"), user, user, PopupType.SmallCaution);
    }

    /// <summary>
    ///     Rejects the crew and anything they carry: the gunner buckled to the tripod, and items in the
    ///     spotter's or anyone's inventory or containers. A locked missile would otherwise home on its own team.
    /// </summary>
    private bool IsCrewOrCarried(EntityUid target, EntityUid user, EntityUid launcher, EntityUid? mount)
    {
        if (mount is { } mountUid &&
            TryComp(mountUid, out WeaponMountComponent? weaponMount) &&
            weaponMount.User == target)
        {
            return true;
        }

        if (!HasComp<VehicleComponent>(target) && _container.IsEntityOrParentInContainer(target))
            return true;

        var parent = Transform(target).ParentUid;
        while (parent.IsValid() && !HasComp<MapGridComponent>(parent) && !HasComp<MapComponent>(parent))
        {
            if (parent == user || parent == launcher || parent == mount)
                return true;

            parent = Transform(parent).ParentUid;
        }

        return false;
    }
}
