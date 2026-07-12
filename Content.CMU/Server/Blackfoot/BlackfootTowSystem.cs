using System.Numerics;
using Content.Shared._CMU14.Blackfoot;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Maths;

namespace Content.Server._CMU14.Blackfoot;

public sealed partial class BlackfootTowSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private PullingSystem _pulling = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlackfootTowComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<BlackfootTowComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<BlackfootTowComponent, ComponentShutdown>(OnTowShutdown);
    }

    private void OnActivate(Entity<BlackfootTowComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (IsGhost(args.User))
            return;

        if (ent.Comp.CanTow)
        {
            args.Handled = true;

            if (ent.Comp.TowedEntity is { } towed && Exists(towed))
            {
                Popup(args.User, "Use the Detach tug verb to release the tow gear.", PopupType.SmallCaution);
                return;
            }

            if (!TryFindTowTarget(ent, out var target, out var reason))
            {
                Popup(args.User, reason, PopupType.SmallCaution);
                return;
            }

            Attach(ent, target, args.User);
            return;
        }

        if (!ent.Comp.CanBeTowed ||
            ent.Comp.TowVehicle is not { } tug ||
            !TryComp(tug, out BlackfootTowComponent? tugTow))
        {
            return;
        }

        args.Handled = true;
        Popup(args.User, "Use the Detach tug verb to release the tow gear.", PopupType.SmallCaution);
    }

    private void OnGetAlternativeVerbs(Entity<BlackfootTowComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract ||
            !args.CanAccess ||
            args.Using != null ||
            IsGhost(args.User))
        {
            return;
        }

        var user = args.User;
        if (ent.Comp.CanTow)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = ent.Comp.TowedEntity is { } towed && Exists(towed) ? "Detach tug" : "Attach tug",
                Priority = 2,
                Act = () => ToggleTug(ent.Owner, user),
            });
            return;
        }

        if (!ent.Comp.CanBeTowed)
            return;

        if (ent.Comp.TowVehicle is { } tug && TryComp(tug, out BlackfootTowComponent? tugTow))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = "Detach tug",
                Priority = 2,
                Act = () => Detach((tug, tugTow), user),
            });
            return;
        }

        if (!TryFindTowTug(ent, out var nearbyTug, out _))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = "Attach tug",
            Priority = 2,
            Act = () => Attach(nearbyTug, ent, user),
        });
    }

    private void ToggleTug(EntityUid tugUid, EntityUid user)
    {
        if (IsGhost(user))
            return;

        if (!TryComp(tugUid, out BlackfootTowComponent? tugTow))
            return;

        Entity<BlackfootTowComponent> tug = (tugUid, tugTow);
        if (tugTow.TowedEntity is { } towed && Exists(towed))
        {
            Detach(tug, user);
            return;
        }

        if (!TryFindTowTarget(tug, out var target, out var reason))
        {
            Popup(user, reason, PopupType.SmallCaution);
            return;
        }

        Attach(tug, target, user);
    }

    private void OnTowShutdown(Entity<BlackfootTowComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.TowedEntity is { } towed &&
            TryComp(towed, out BlackfootTowComponent? towedTow) &&
            towedTow.TowVehicle == ent.Owner)
        {
            towedTow.TowVehicle = null;
            Dirty(towed, towedTow);
        }

        if (ent.Comp.TowVehicle is { } tug &&
            TryComp(tug, out BlackfootTowComponent? tugTow) &&
            tugTow.TowedEntity == ent.Owner)
        {
            tugTow.TowedEntity = null;
            Dirty(tug, tugTow);
        }
    }

    private bool TryFindTowTarget(
        Entity<BlackfootTowComponent> tug,
        out Entity<BlackfootTowComponent> target,
        out string reason)
    {
        target = default;
        reason = "No towable Blackfoot is close enough.";

        var tugXform = Transform(tug);
        var tugMap = tugXform.MapUid;
        if (tugMap == null)
        {
            reason = "The tug is not on a valid map.";
            return false;
        }

        var tugPosition = _transform.GetWorldPosition(tugXform);
        var rangeSq = tug.Comp.AttachRange * tug.Comp.AttachRange;
        var bestDistance = float.MaxValue;
        var foundBlocked = false;
        var query = EntityQueryEnumerator<BlackfootTowComponent, BlackfootFlightComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var tow, out var flight, out var xform))
        {
            if (uid == tug.Owner ||
                !tow.CanBeTowed ||
                xform.MapUid != tugMap)
            {
                continue;
            }

            var (targetPosition, targetRotation) = _transform.GetWorldPositionRotation(xform);
            var attachPosition = targetPosition + targetRotation.RotateVec(tug.Comp.AttachOffset);
            var distance = Vector2.DistanceSquared(tugPosition, attachPosition);
            if (distance > rangeSq ||
                distance >= bestDistance)
            {
                continue;
            }

            if (!CanTowState(flight.State, tow, tug.Comp, out reason))
            {
                foundBlocked = true;
                continue;
            }

            if (tow.TowVehicle != null)
            {
                foundBlocked = true;
                reason = "That Blackfoot is already attached to towing gear.";
                continue;
            }

            bestDistance = distance;
            target = (uid, tow);
        }

        if (target.Owner != default)
            return true;

        if (!foundBlocked)
            reason = "No towable Blackfoot is close enough.";

        return false;
    }

    private bool TryFindTowTug(
        Entity<BlackfootTowComponent> target,
        out Entity<BlackfootTowComponent> tug,
        out string reason)
    {
        tug = default;
        reason = "No Blackfoot aerospace tug is parked under the cockpit.";

        if (!TryComp(target, out BlackfootFlightComponent? flight))
        {
            reason = "That cannot be moved with the Blackfoot tug.";
            return false;
        }

        var targetXform = Transform(target);
        var targetMap = targetXform.MapUid;
        if (targetMap == null)
        {
            reason = "The Blackfoot is not on a valid map.";
            return false;
        }

        var (targetPosition, targetRotation) = _transform.GetWorldPositionRotation(targetXform);
        var bestDistance = float.MaxValue;
        var foundBlocked = false;
        var query = EntityQueryEnumerator<BlackfootTowComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var tow, out var xform))
        {
            if (uid == target.Owner ||
                !tow.CanTow ||
                tow.TowedEntity != null ||
                xform.MapUid != targetMap)
            {
                continue;
            }

            var attachPosition = targetPosition + targetRotation.RotateVec(tow.AttachOffset);
            var distance = Vector2.DistanceSquared(_transform.GetWorldPosition(xform), attachPosition);
            if (distance > tow.AttachRange * tow.AttachRange ||
                distance >= bestDistance)
            {
                continue;
            }

            if (!CanTowState(flight.State, target.Comp, tow, out reason))
            {
                foundBlocked = true;
                continue;
            }

            bestDistance = distance;
            tug = (uid, tow);
        }

        if (tug.Owner != default)
            return true;

        if (!foundBlocked)
            reason = "No Blackfoot aerospace tug is parked under the cockpit.";

        return false;
    }

    private static bool CanTowState(
        BlackfootFlightState state,
        BlackfootTowComponent targetTow,
        BlackfootTowComponent tugTow,
        out string reason)
    {
        reason = string.Empty;

        switch (state)
        {
            case BlackfootFlightState.Stowed when targetTow.AllowStowedTowing:
            case BlackfootFlightState.Grounded:
            case BlackfootFlightState.Crashed when targetTow.AllowCrashedTowing:
                return true;
            case BlackfootFlightState.TakingOff:
            case BlackfootFlightState.VTOL:
            case BlackfootFlightState.Flight:
            case BlackfootFlightState.Landing:
                if (targetTow.AllowAirborneTowing && tugTow.AllowAirborneTowing)
                    return true;

                reason = "The tug cannot attach while the Blackfoot is airborne.";
                return false;
            case BlackfootFlightState.Idling:
                reason = "Shut the Blackfoot engines down before attaching towing gear.";
                return false;
            case BlackfootFlightState.Stowed:
                reason = "This Blackfoot cannot be towed while stowed.";
                return false;
            case BlackfootFlightState.Crashed:
                reason = "This Blackfoot cannot be towed while crashed.";
                return false;
            default:
                reason = "The Blackfoot cannot be towed in its current state.";
                return false;
        }
    }

    private void Attach(Entity<BlackfootTowComponent> tug, Entity<BlackfootTowComponent> target, EntityUid user)
    {
        if (IsGhost(user))
            return;

        tug.Comp.TowedEntity = target.Owner;
        target.Comp.TowVehicle = tug.Owner;
        Dirty(tug);
        Dirty(target);

        SetTugPullable(tug, false);
        _transform.SetParent(tug.Owner, target.Owner);
        _transform.SetLocalPosition(tug.Owner, tug.Comp.AttachOffset);
        _transform.SetLocalRotation(tug.Owner, Angle.FromDegrees(tug.Comp.AttachRotationDegrees));
        SetTugCollision(tug.Owner, false);

        Popup(user, "Tug attached. The Blackfoot pilot can taxi the aircraft.");
    }

    private void Detach(Entity<BlackfootTowComponent> tug, EntityUid user)
    {
        if (IsGhost(user))
            return;

        var target = tug.Comp.TowedEntity;
        tug.Comp.TowedEntity = null;
        Dirty(tug);

        if (target is { } towed && TryComp(towed, out BlackfootTowComponent? towedTow))
        {
            towedTow.TowVehicle = null;
            Dirty(towed, towedTow);

            var targetXform = Transform(towed);
            var tugXform = Transform(tug.Owner);
            if (targetXform.ParentUid.IsValid())
            {
                _transform.SetParent(tug.Owner, tugXform, targetXform.ParentUid);
                _transform.SetLocalPosition(
                    tug.Owner,
                    targetXform.LocalPosition + targetXform.LocalRotation.RotateVec(tug.Comp.AttachOffset),
                    tugXform);
            }
            else
            {
                _transform.AttachToGridOrMap(tug.Owner, tugXform);
            }
        }
        else
        {
            _transform.AttachToGridOrMap(tug.Owner);
        }

        SetTugCollision(tug.Owner, true);
        SetTugPullable(tug, true);
        Popup(user, "Tug detached.");
    }

    private void SetTugCollision(EntityUid tug, bool canCollide)
    {
        if (TryComp(tug, out PhysicsComponent? physics))
            _physics.SetCanCollide(tug, canCollide, force: true, body: physics);
    }

    private void SetTugPullable(Entity<BlackfootTowComponent> tug, bool canPull)
    {
        if (canPull)
        {
            if (tug.Comp.RestorePullableOnDetach && !HasComp<PullableComponent>(tug.Owner))
                EnsureComp<PullableComponent>(tug.Owner);

            tug.Comp.RestorePullableOnDetach = false;
            return;
        }

        if (!TryComp(tug.Owner, out PullableComponent? pullable))
        {
            tug.Comp.RestorePullableOnDetach = false;
            return;
        }

        tug.Comp.RestorePullableOnDetach = true;
        _pulling.TryStopPull(tug.Owner, pullable);
        RemComp<PullableComponent>(tug.Owner);
    }

    private void Popup(EntityUid user, string message, PopupType type = PopupType.Small)
    {
        _popup.PopupCursor(message, user, type);
    }

    private bool IsGhost(EntityUid user)
    {
        return HasComp<GhostComponent>(user);
    }
}
