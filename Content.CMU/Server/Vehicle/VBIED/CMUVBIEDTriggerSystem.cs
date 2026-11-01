using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Administration.Logs;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.CMU14.Vehicle.VBIED;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Trigger.Systems;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Vehicle.VBIED;

/// <summary>
/// Rigs IEDs into civilian vehicles and sets them off: on a crash, near someone with hostile IFF, or once
/// the vehicle has taken enough damage either way. The bomb goes off where the vehicle is and, if the
/// vehicle has an interior, inside it too.
/// </summary>
public sealed partial class CMUVBIEDTriggerSystem : EntitySystem
{
    public const string ImpactReason = "impact";
    public const string ProximityReason = "proximity";
    public const string DamageReason = "damage";

    private static readonly TimeSpan ProximityInterval = TimeSpan.FromSeconds(0.5);

    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private GunIFFSystem _gunIFF = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    private readonly List<EntityUid> _detonating = new();
    private readonly HashSet<EntProtoId<IFFFactionComponent>> _factions = new();
    private readonly HashSet<Entity<VehicleInteriorComponent>> _interiors = new();
    private readonly HashSet<Entity<UserIFFComponent>> _nearby = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVBIEDRiggableComponent, CMUVBIEDRigDoAfterEvent>(OnRigDoAfter);
        SubscribeLocalEvent<CMUVBIEDRiggableComponent, EntRemovedFromContainerMessage>(OnRemovedFromContainer);
        SubscribeLocalEvent<GridVehicleMoverComponent, CMUVBIEDInspectDoAfterEvent>(OnInspectDoAfter);
        SubscribeLocalEvent<CMUVBIEDRiggedComponent, CMUVehicleImpactEvent>(OnImpact);
        SubscribeLocalEvent<CMUVBIEDRiggedComponent, CMUVehicleHardpointDamagedEvent>(OnHardpointDamaged);
        SubscribeLocalEvent<CMUVBIEDComponent, CMExplosiveTriggeredEvent>(OnExplosiveTriggered);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        _detonating.Clear();
        var query = EntityQueryEnumerator<CMUVBIEDRiggedComponent>();
        while (query.MoveNext(out var uid, out var rigged))
        {
            if (rigged.Detonation == null &&
                rigged.Trigger == CMUVBIEDTrigger.Proximity &&
                now >= rigged.NextProximityCheck)
            {
                rigged.NextProximityCheck = now + ProximityInterval;
                if (IsHostileNearby(uid, rigged))
                    rigged.Detonation = ProximityReason;
            }

            if (rigged.Detonation != null)
                _detonating.Add(uid);
        }

        foreach (var uid in _detonating)
        {
            Detonate(uid);
        }
    }

    /// <summary>
    /// Whether an IED is rigged inside the vehicle.
    /// </summary>
    public bool IsRigged(EntityUid vehicle)
    {
        return HasComp<CMUVBIEDRiggedComponent>(vehicle) ||
               _container.TryGetContainer(vehicle, CMUVBIEDRiggableComponent.ContainerId, out var container) &&
               container.ContainedEntities.Count > 0;
    }

    /// <summary>
    /// Rigs the IED inside the vehicle, as if <paramref name="user"/> had finished doing it.
    /// </summary>
    public bool TryRig(EntityUid vehicle, EntityUid ied, EntityUid user, CMUVBIEDTrigger trigger)
    {
        if (!HasComp<CMUVBIEDRiggableComponent>(vehicle) || !HasComp<CMUVBIEDComponent>(ied))
            return false;

        if (IsRigged(vehicle))
        {
            _popup.PopupEntity(Loc.GetString("cmu-vbied-rig-already", ("vehicle", vehicle)), vehicle, user,
                PopupType.SmallCaution);
            return false;
        }

        var slot = _container.EnsureContainer<ContainerSlot>(vehicle, CMUVBIEDRiggableComponent.ContainerId);
        if (!_container.Insert(ied, slot))
            return false;

        var rigged = EnsureComp<CMUVBIEDRiggedComponent>(vehicle);
        rigged.IED = ied;
        rigged.Trigger = trigger;
        rigged.Planter = user;
        rigged.DamageTaken = 0f;
        rigged.Detonation = null;
        rigged.NextProximityCheck = _timing.CurTime + ProximityInterval;
        _gunIFF.TryGetFactions((user, null), rigged.Factions);

        var message = trigger == CMUVBIEDTrigger.Impact ? "cmu-vbied-rig-done-impact" : "cmu-vbied-rig-done-proximity";
        _popup.PopupEntity(Loc.GetString(message, ("vehicle", vehicle)), vehicle, user);
        _adminLog.Add(LogType.Explosion, LogImpact.High,
            $"{ToPrettyString(user):user} rigged {ToPrettyString(ied):entity} inside {ToPrettyString(vehicle):target} to detonate on {trigger}");
        return true;
    }

    private void OnRigDoAfter(Entity<CMUVBIEDRiggableComponent> ent, ref CMUVBIEDRigDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } ied)
            return;

        args.Handled = true;
        if (!_hands.IsHolding(args.User, ied))
            return;

        TryRig(ent, ied, args.User, args.Trigger);
    }

    private void OnInspectDoAfter(Entity<GridVehicleMoverComponent> ent, ref CMUVBIEDInspectDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (IsRigged(ent))
        {
            _popup.PopupEntity(Loc.GetString("cmu-vbied-inspect-found", ("vehicle", ent.Owner)), ent, args.User,
                PopupType.LargeCaution);
            _adminLog.Add(LogType.Explosion, LogImpact.Medium,
                $"{ToPrettyString(args.User):user} found the IED rigged inside {ToPrettyString(ent):target}");
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-vbied-inspect-clear", ("vehicle", ent.Owner)), ent, args.User);
    }

    private void OnImpact(Entity<CMUVBIEDRiggedComponent> ent, ref CMUVehicleImpactEvent args)
    {
        if (ent.Comp.Trigger == CMUVBIEDTrigger.Impact)
            ent.Comp.Detonation ??= ImpactReason;
    }

    private void OnHardpointDamaged(Entity<CMUVBIEDRiggedComponent> ent, ref CMUVehicleHardpointDamagedEvent args)
    {
        ent.Comp.DamageTaken += args.Amount;
        if (TryComp(ent.Comp.IED, out CMUVBIEDComponent? vbied) && ent.Comp.DamageTaken >= vbied.DamageThreshold)
            ent.Comp.Detonation ??= DamageReason;
    }

    /// <summary>
    /// A rigged IED set off some other way, by its phone or a timer already running, still blasts the
    /// interior.
    /// </summary>
    private void OnExplosiveTriggered(Entity<CMUVBIEDComponent> ent, ref CMExplosiveTriggeredEvent args)
    {
        if (!_container.TryGetContainingContainer(ent.Owner, out var container) ||
            container.ID != CMUVBIEDRiggableComponent.ContainerId ||
            !TryComp(container.Owner, out CMUVBIEDRiggedComponent? rigged))
        {
            return;
        }

        var vehicle = container.Owner;
        RemComp(vehicle, rigged);
        QueueInteriorBlast(vehicle, ent.Comp);
        _adminLog.Add(LogType.Explosion, LogImpact.High,
            $"{ToPrettyString(ent):entity} rigged inside {ToPrettyString(vehicle):target} went off by other means");
    }

    private void OnRemovedFromContainer(Entity<CMUVBIEDRiggableComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != CMUVBIEDRiggableComponent.ContainerId || TerminatingOrDeleted(ent))
            return;

        if (TryComp(ent, out CMUVBIEDRiggedComponent? rigged) && rigged.IED == args.Entity)
            RemComp(ent, rigged);
    }

    private void Detonate(EntityUid vehicle)
    {
        if (!TryComp(vehicle, out CMUVBIEDRiggedComponent? rigged))
            return;

        var ied = rigged.IED;
        var planter = rigged.Planter is { } p && Exists(p) ? p : (EntityUid?) null;
        var reason = rigged.Detonation;
        RemComp(vehicle, rigged);

        if (TerminatingOrDeleted(ied) ||
            !TryComp(ied, out CMUVBIEDComponent? vbied) ||
            !_container.TryGetContainingContainer(ied, out var container) ||
            container.Owner != vehicle)
        {
            return;
        }

        _adminLog.Add(LogType.Explosion, LogImpact.Extreme,
            $"{ToPrettyString(ied):entity} rigged inside {ToPrettyString(vehicle):target} by {ToPrettyString(planter):user} detonated on {reason}");

        // The explosion queue runs in order, so the interior goes up with the outside rather than after it.
        QueueInteriorBlast(vehicle, vbied);
        _trigger.Trigger(ied, planter, "timer");
    }

    private void QueueInteriorBlast(EntityUid vehicle, CMUVBIEDComponent vbied)
    {
        if (!TryComp(vehicle, out VehicleInteriorComponent? interior) ||
            interior.EntryParent == EntityUid.Invalid ||
            !Exists(interior.EntryParent))
        {
            return;
        }

        _explosion.QueueExplosion(
            _transform.ToMapCoordinates(interior.Entry),
            vbied.InteriorExplosionType.Id,
            vbied.InteriorTotalIntensity,
            vbied.InteriorIntensitySlope,
            vbied.InteriorMaxIntensity,
            vehicle,
            maxTileBreak: 0,
            canCreateVacuum: false,
            addLog: false);
    }

    private bool IsHostileNearby(EntityUid vehicle, CMUVBIEDRiggedComponent rigged)
    {
        if (!TryComp(rigged.IED, out CMUVBIEDComponent? vbied))
            return false;

        var coordinates = _transform.GetMapCoordinates(vehicle);
        _nearby.Clear();
        _lookup.GetEntitiesInRange(coordinates, vbied.ProximityRange, _nearby);
        foreach (var target in _nearby)
        {
            if (IsHostile(target, rigged))
                return true;
        }

        // Anyone inside a vehicle close by counts as close by, starting with this one.
        _interiors.Clear();
        _lookup.GetEntitiesInRange(coordinates, vbied.ProximityRange, _interiors);
        if (TryComp(vehicle, out VehicleInteriorComponent? own))
            _interiors.Add((vehicle, own));

        foreach (var other in _interiors)
        {
            foreach (var passenger in other.Comp.Passengers)
            {
                if (IsHostile(passenger, rigged))
                    return true;
            }
        }

        return false;
    }

    private bool IsHostile(Entity<UserIFFComponent?> target, CMUVBIEDRiggedComponent rigged)
    {
        if (target.Owner == rigged.Planter ||
            !TryComp(target, out MobStateComponent? mobState) ||
            _mobState.IsDead(target, mobState) ||
            !_gunIFF.TryGetFactions(target, _factions))
        {
            return false;
        }

        return !_factions.Overlaps(rigged.Factions);
    }
}
