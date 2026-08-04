using System.Numerics;
using Content.Server._RMC14.Scoping;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Scoping;
using Content.Shared._RMC14.Weapons.Ranged.Vulture;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._RMC14.Weapons.Ranged.Vulture;

public sealed partial class VultureAimSystem : SharedVultureAimSystem
{
    private const string SpotterScopeTag = "RMCVultureSpotterScope";
    private static readonly TimeSpan UpdateEvery = TimeSpan.FromSeconds(0.1);

    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private AttachableHolderSystem _attachableHolder = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ScopeSystem _scope = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScopingComponent, ScopedEvent>(OnScoped);
    }

    public override void Update(float frameTime)
    {
        if (_nextUpdate > Timing.CurTime)
            return;

        _nextUpdate = Timing.CurTime + UpdateEvery;
        ValidateMountedSpotterScopes();

        var query = EntityQueryEnumerator<VultureAimComponent>();
        while (query.MoveNext(out var uid, out var aim))
        {
            var ent = (uid, aim);
            if (!ValidateSniper(ent))
                continue;

            if (aim.BreathActive(Timing.CurTime))
            {
                aim.SwayOffset = Vector2i.Zero;
                aim.DriftOffset = Vector2.Zero;
                aim.DriftTarget = Vector2.Zero;
            }
            else
            {
                aim.SwayOffset = Vector2i.Zero;
                Actions.SetToggled(aim.BreathAction, false);
                UpdateDrift(aim);
            }

            ValidateOrFindSpotter(ent);
            UpdateAimCoordinates(ent);
            MoveRelays(ent);
            Dirty(uid, aim);
        }
    }

    private void UpdateDrift(VultureAimComponent aim)
    {
        if (aim.NextSwayAt <= Timing.CurTime)
        {
            aim.DriftTarget = PickDriftTarget(aim.DriftRadius, aim.DriftOffset);
            aim.NextSwayAt = Timing.CurTime + aim.SwayEvery;
        }

        var delta = aim.DriftTarget - aim.DriftOffset;
        if (delta == Vector2.Zero)
            return;

        var maxDistance = aim.DriftSpeed * (float) UpdateEvery.TotalSeconds;
        if (delta.LengthSquared() <= maxDistance * maxDistance)
        {
            aim.DriftOffset = aim.DriftTarget;
            return;
        }

        aim.DriftOffset += Vector2.Normalize(delta) * maxDistance;
    }

    private Vector2 PickDriftTarget(float radius, Vector2 current)
    {
        var target = current;
        for (var i = 0; i < 4; i++)
        {
            target = new Vector2(
                _random.NextFloat(-radius, radius),
                _random.NextFloat(-radius, radius));

            if ((target - current).LengthSquared() >= 0.5f)
                return target;
        }

        return target;
    }

    private void OnScoped(Entity<ScopingComponent> scoped, ref ScopedEvent ev)
    {
        if (TryGetVultureRifle(ev.Scope.Owner, out var rifle))
        {
            if (ev.Scope.Comp.ScopingDirection is not { } direction)
                return;

            if (!HasDeployedBipod(rifle))
            {
                _popup.PopupClient(
                    Loc.GetString("rmc-vulture-bipod-required", ("gun", rifle.Owner)),
                    ev.User,
                    ev.User,
                    PopupType.LargeCaution);
                _scope.Unscope(ev.Scope);
                ClearAim(rifle);
                return;
            }

            ResetAim(rifle, ev.User, ev.Scope.Owner, direction);
            ValidateOrFindSpotter(rifle);
            MoveRelays(rifle);
            return;
        }

        if (!TryGetMountedSpotterTripod(ev.Scope.Owner, out var tripod))
            return;

        if (!IsSpotterAtTripod(ev.User, tripod))
        {
            _scope.Unscope(ev.Scope);
            return;
        }

        if (TryFindLinkedRifle(tripod, out var linked))
        {
            SetSpotter(linked, ev.User, ev.Scope.Owner, tripod);
            MoveRelays(linked);
        }
    }

    private bool ValidateSniper(Entity<VultureAimComponent> ent)
    {
        if (ent.Comp.Sniper is not { } sniper ||
            ent.Comp.RifleScope is not { } scope ||
            !TryComp(scope, out ScopeComponent? scopeComp) ||
            scopeComp.User != sniper ||
            !TryComp(sniper, out ScopingComponent? scoping) ||
            scoping.Scope != scope ||
            scopeComp.ScopingDirection is not { } direction)
        {
            if (ent.Comp.Sniper != null)
                ClearAim(ent);

            return false;
        }

        if (!HasDeployedBipod(ent))
        {
            _scope.Unscope((scope, scopeComp));
            ClearAim(ent);
            return false;
        }

        ent.Comp.Direction = direction;
        return true;
    }

    private void ValidateOrFindSpotter(Entity<VultureAimComponent> ent)
    {
        if (ent.Comp.Spotter is { } spotter &&
            ent.Comp.SpotterScope is { } spotterScope &&
            ent.Comp.SpotterTripod is { } tripod &&
            IsValidSpotter(ent, spotter, spotterScope, tripod))
        {
            return;
        }

        if (ent.Comp.Spotter != null)
        {
            ClearSpotter(ent);
        }

        TryFindSpotter(ent);
    }

    private bool IsValidSpotter(Entity<VultureAimComponent> ent, EntityUid spotter, EntityUid scope, EntityUid tripod)
    {
        return !TerminatingOrDeleted(spotter) &&
               !TerminatingOrDeleted(scope) &&
               !TerminatingOrDeleted(tripod) &&
               TryComp(scope, out ScopeComponent? scopeComp) &&
               scopeComp.User == spotter &&
               TryGetMountedSpotterTripod(scope, out var mountedTripod) &&
               mountedTripod == tripod &&
               IsSpotterAtTripod(spotter, tripod) &&
               IsTripodLinkedToSniper(ent, tripod);
    }

    private void TryFindSpotter(Entity<VultureAimComponent> ent)
    {
        var query = EntityQueryEnumerator<ScopeComponent>();
        while (query.MoveNext(out var scopeUid, out var scope))
        {
            if (scope.User is not { } spotter ||
                !TryGetMountedSpotterTripod(scopeUid, out var tripod) ||
                !IsSpotterAtTripod(spotter, tripod) ||
                !IsTripodLinkedToSniper(ent, tripod))
            {
                continue;
            }

            SetSpotter(ent, spotter, scopeUid, tripod);
            return;
        }
    }

    private bool TryFindLinkedRifle(EntityUid tripod, out Entity<VultureAimComponent> rifle)
    {
        var query = EntityQueryEnumerator<VultureAimComponent>();
        while (query.MoveNext(out var uid, out var aim))
        {
            if (aim.Sniper is not { } sniper ||
                !_transform.InRange(Transform(sniper).Coordinates, Transform(tripod).Coordinates, aim.SpotterLinkRange))
            {
                continue;
            }

            rifle = (uid, aim);
            return true;
        }

        rifle = default;
        return false;
    }

    private void ValidateMountedSpotterScopes()
    {
        var query = EntityQueryEnumerator<ScopeComponent>();
        while (query.MoveNext(out var scopeUid, out var scope))
        {
            if (scope.User is not { } user ||
                !TryGetMountedSpotterTripod(scopeUid, out var tripod) ||
                IsSpotterAtTripod(user, tripod))
            {
                continue;
            }

            _scope.Unscope((scopeUid, scope));
        }
    }

    private bool IsTripodLinkedToSniper(Entity<VultureAimComponent> ent, EntityUid tripod)
    {
        return ent.Comp.Sniper is { } sniper &&
               _transform.InRange(Transform(sniper).Coordinates, Transform(tripod).Coordinates, ent.Comp.SpotterLinkRange);
    }

    private bool IsSpotterAtTripod(EntityUid spotter, EntityUid tripod)
    {
        return !TerminatingOrDeleted(spotter) &&
               !TerminatingOrDeleted(tripod) &&
               _transform.InRange(
                   Transform(spotter).Coordinates,
                   Transform(tripod).Coordinates,
                   SharedInteractionSystem.InteractionRange);
    }

    private bool TryGetVultureRifle(EntityUid scope, out Entity<VultureAimComponent> rifle)
    {
        if (_container.TryGetContainingContainer((scope, null), out var container) &&
            TryComp(container.Owner, out VultureAimComponent? aim))
        {
            rifle = (container.Owner, aim);
            return true;
        }

        rifle = default;
        return false;
    }

    private bool HasDeployedBipod(Entity<VultureAimComponent> ent)
    {
        if (!TryComp(ent.Owner, out VultureRifleComponent? vulture) ||
            !TryComp(ent.Owner, out AttachableHolderComponent? holder))
        {
            return false;
        }

        if (!_attachableHolder.TryGetAttachable((ent.Owner, holder), vulture.BipodSlot, out var attachable))
            return false;

        return TryComp(attachable.Owner, out AttachableToggleableComponent? toggleable) && toggleable.Active;
    }

    private bool TryGetMountedSpotterTripod(EntityUid scope, out EntityUid tripod)
    {
        if (!_tag.HasTag(scope, SpotterScopeTag) ||
            !_container.TryGetContainingContainer((scope, null), out var container) ||
            !HasComp<VultureSpotterTripodComponent>(container.Owner))
        {
            tripod = default;
            return false;
        }

        tripod = container.Owner;
        return true;
    }

    private void MoveRelays(Entity<VultureAimComponent> ent)
    {
        if (ent.Comp.ViewCenter is not { } center)
            return;

        MoveRelay(ent.Comp.RifleScope, center);
        MoveRelay(ent.Comp.SpotterScope, center);
    }

    private void MoveRelay(EntityUid? scopeUid, MapCoordinates center)
    {
        if (scopeUid is not { } uid ||
            !TryComp(uid, out ScopeComponent? scope) ||
            scope.RelayEntity is not { } relay ||
            TerminatingOrDeleted(relay))
        {
            return;
        }

        _transform.SetMapCoordinates(relay, center);
    }
}
