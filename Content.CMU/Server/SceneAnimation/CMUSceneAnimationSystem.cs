using System.Numerics;
using System.Linq;
using Content.Server.Humanoid;
using Content.Server.Physics.Controllers;
using Content.Shared.Clothing.Components;
using Content.Shared.CMU14.SceneAnimation;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Gibbing;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.SSDIndicator;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Item;
using Content.Shared._RMC14.Vendors;
using Content.Shared._RMC14.Vehicle;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.SceneAnimation;

/// <summary>
/// Runs mapper-authored background traffic without AI or player operators.
/// </summary>
public sealed partial class CMUSceneAnimationSystem : EntitySystem
{
    private const float CivilianVehicleContactRadius = 1f;
    private const string ApparelVendor = "AU14USCMclothingequipmentvendor";
    private const string RequisitionsVendor = "AU14USCMRequisitionVendor";
    private const string SceneMarine = "CMUSceneMarine";
    private const string StandardIssueRifle = "RMCWeaponRifleM54CMK1Unloaded";
    private const string TwoPointSling = "AU14AttachmentTwoPointSling";

    private enum MarineWeaponCarryStyle : byte
    {
        ArmorStorage,
        OneHanded,
        Wielded,
    }

    private static readonly HashSet<string> ColdWeatherMasks =
    [
        "CMMaskCoif",
        "RMCMaskScarfBlack",
        "RMCMaskScarfGray",
        "RMCMaskScarfTan",
        "RMCMaskTornScarfBlack",
        "RMCMaskTornScarfSnow",
        "RMCMaskSmockBlack",
    ];

    private static readonly HashSet<string> ColdWeatherGloves =
    [
        "AU14PVEHandsInsulatedBlackMarine",
    ];

    private static readonly string[] CivilianVehiclePropPrefixes =
    [
        "RMCPropVehicleMonoSupron",
        "RMCPropVehicleAmbulance",
        "RMCPropVehicleLoaderTruck",
        "RMCPropVehicleBoxVan",
        "RMCPropVehicleLongTruck",
        "RMCPropVehicleSmallTruck",
    ];

    private static readonly HashSet<string> AllowedSceneSpawns =
    [
        SceneMarine,
        "VehicleHumvee",
        "VehicleHumveeMedical",
        "VehicleHumveeTransport",
        "VehicleHumveeARC",
        "VehicleAPC",
        "VehicleAPCMed",
        "VehicleAPCCommand",
        "AU14VehicleCivArmoredTruck",
        "AU14VehicleCivTruck",
        "AU14VehicleCivVan",
        "VehicleTank",
        "VehicleTankTWE",
    ];

    private static readonly Vector2[] TankRiderPositions =
    [
        new(-0.82f, -0.65f),
        new(0.82f, -0.65f),
        new(-0.82f, 0.75f),
        new(0.82f, 0.75f),
    ];

    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AttachableHolderSystem _attachable = default!;
    [Dependency] private HumanoidOrganAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemCamouflageSystem _camouflage = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private MarkingManager _markings = default!;
    [Dependency] private MoverController _moverController = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedWieldableSystem _wieldable = default!;
    [Dependency] private VehicleRideSurfaceSystem _rideSurface = default!;

    private readonly Dictionary<string, List<EntProtoId>> _apparelSections =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<EntProtoId> _rifles = [];
    private bool _vendorPoolsLoaded;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(VehicleRideSurfaceSystem));
        UpdatesBefore.Add(typeof(MoverController));

        SubscribeLocalEvent<CMUSceneSpawnMarkerComponent, MapInitEvent>(OnSpawnerMapInit);
        SubscribeLocalEvent<CMUSceneMoverComponent, ComponentShutdown>(OnMoverShutdown);
        SubscribeLocalEvent<CMUSceneMoverComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnSpawnerMapInit(Entity<CMUSceneSpawnMarkerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextSpawnAt = _timing.CurTime + RandomInterval(ent.Comp.MinSpawnInterval, ent.Comp.MaxSpawnInterval);
    }

    private void OnMoverShutdown(Entity<CMUSceneMoverComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp(ent.Comp.Spawner, out CMUSceneSpawnMarkerComponent? spawner))
            spawner.ActiveGroupMembers.Remove(ent.Owner);
    }

    private void OnPreventCollide(Entity<CMUSceneMoverComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.GibMobsOnContact && HasComp<MobStateComponent>(args.OtherEntity))
            TryGibTouchedMob(ent, args.OtherEntity);

        if (HasComp<CMUSceneMoverComponent>(args.OtherEntity) ||
            ent.Comp.IgnoreMobCollisions && HasComp<MobStateComponent>(args.OtherEntity))
        {
            args.Cancelled = true;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateSpawners();
        UpdateMovers(frameTime);
        UpdateTankRiders();
    }

    private void UpdateSpawners()
    {
        var query = EntityQueryEnumerator<CMUSceneSpawnMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawner, out var xform))
        {
            spawner.ActiveGroupMembers.RemoveWhere(uid => TerminatingOrDeleted(uid));

            if (spawner.CurrentGroup is { } current)
            {
                if (spawner.GroupMembersRemaining > 0)
                {
                    if (_timing.CurTime >= spawner.NextSpawnAt && HasMemberClearance(spawner, current))
                        SpawnGroupMember(uid, spawner, current, xform);

                    continue;
                }

                // A wave does not begin its 3-10 second cooldown until every member has completed the route.
                if (spawner.WaitForGroupClear && spawner.ActiveGroupMembers.Count > 0)
                    continue;

                spawner.CurrentGroup = null;
                spawner.FixedGroupPrototype = null;
                spawner.LastGroupMember = null;
                spawner.NextSpawnAt = _timing.CurTime + RandomInterval(spawner.MinSpawnInterval, spawner.MaxSpawnInterval);
                continue;
            }

            if (_timing.CurTime < spawner.NextSpawnAt)
                continue;

            BeginWave(spawner);
        }
    }

    private void BeginWave(CMUSceneSpawnMarkerComponent spawner)
    {
        var groups = spawner.SpawnGroups
            .Where(group => group.Spawns.Any(IsAllowedSpawn))
            .ToList();
        if (groups.Count == 0)
        {
            spawner.NextSpawnAt = _timing.CurTime + TimeSpan.FromSeconds(10);
            return;
        }

        var group = _random.Pick(groups);
        var min = Math.Max(1, Math.Min(group.MinSize, group.MaxSize));
        var max = Math.Max(min, Math.Max(group.MinSize, group.MaxSize));
        spawner.CurrentGroup = group;
        spawner.GroupMembersRemaining = _random.Next(min, max + 1);
        spawner.FixedGroupPrototype = group.MixPrototypes ? null : PickAllowed(group.Spawns, null);
        spawner.LastGroupPrototype = null;
        spawner.LastGroupMember = null;
        spawner.NextSpawnAt = _timing.CurTime;
    }

    private bool HasMemberClearance(CMUSceneSpawnMarkerComponent spawner, CMUSceneSpawnGroup group)
    {
        if (group.Marine || group.MemberClearance <= 0f || spawner.LastGroupMember is not { } last)
            return true;

        if (TerminatingOrDeleted(last) || !TryComp(last, out CMUSceneMoverComponent? mover))
            return true;

        var coordinates = _transform.GetMapCoordinates(last);
        if (coordinates.MapId != mover.StartMap)
            return true;

        return Vector2.DistanceSquared(coordinates.Position, mover.StartPosition) >=
               group.MemberClearance * group.MemberClearance;
    }

    private void SpawnGroupMember(
        EntityUid marker,
        CMUSceneSpawnMarkerComponent spawner,
        CMUSceneSpawnGroup group,
        TransformComponent markerXform)
    {
        var prototype = spawner.FixedGroupPrototype ?? PickAllowed(group.Spawns, spawner.LastGroupPrototype);
        if (prototype is not { } spawn || !_prototypes.HasIndex<EntityPrototype>(spawn))
        {
            spawner.GroupMembersRemaining--;
            ScheduleNextMember(spawner);
            return;
        }

        var direction = markerXform.LocalRotation.GetCardinalDir().ToIntVec();
        if (group.HorizontalOnly)
            direction = new Vector2i(direction.X < 0 ? -1 : 1, 0);

        if (group.HorizontalOnly && direction.X > 0)
            spawn = GetOppositeFacingVehicleProp(spawn);
        var offset = Vector2.Zero;
        if (group.Marine)
        {
            var forward = new Vector2(direction.X, direction.Y);
            var right = new Vector2(forward.Y, -forward.X);
            offset = right * _random.NextFloat(-spawner.ActorLateralVariance, spawner.ActorLateralVariance) +
                     forward * _random.NextFloat(-spawner.ActorLongitudinalVariance, spawner.ActorLongitudinalVariance);
        }

        var spawned = Spawn(spawn, markerXform.Coordinates.Offset(offset));
        _transform.SetLocalRotation(spawned, markerXform.LocalRotation);

        var start = _transform.GetMapCoordinates(spawned);
        var mover = EnsureComp<CMUSceneMoverComponent>(spawned);
        mover.Spawner = marker;
        mover.Direction = direction;
        mover.StartPosition = start.Position;
        mover.StartMap = start.MapId;
        mover.IsVehicle = !group.Marine;
        mover.IsTank = group.Tank;
        mover.IgnoreMobCollisions = true;
        mover.GibMobsOnContact = IsCivilianVehicleProp(spawn.Id);
        mover.DirectMoveSpeed = Math.Max(0f, group.DirectMoveSpeed);
        mover.MaxTravelDistance = Math.Max(1f, group.MaxTravelDistance);

        if (group.Marine)
        {
            ConfigureMarine(spawned, coldWeather: true);
        }
        else
        {
            ConfigureVehicle(spawned, spawn.Id, group, spawner);
        }

        spawner.ActiveGroupMembers.Add(spawned);
        spawner.LastGroupMember = spawned;
        spawner.LastGroupPrototype = spawn;
        spawner.GroupMembersRemaining--;
        ScheduleNextMember(spawner);
    }

    private void ScheduleNextMember(CMUSceneSpawnMarkerComponent spawner)
    {
        spawner.NextSpawnAt = _timing.CurTime +
                              RandomInterval(spawner.MinGroupMemberInterval, spawner.MaxGroupMemberInterval);
    }

    private EntProtoId? PickAllowed(IReadOnlyList<EntProtoId> prototypes, EntProtoId? avoid)
    {
        var valid = prototypes.Where(IsAllowedSpawn).ToList();
        if (valid.Count == 0)
            return null;

        if (valid.Count > 1 && avoid is { } previous)
            valid.RemoveAll(id => id == previous);

        return _random.Pick(valid);
    }

    private bool IsAllowedSpawn(EntProtoId prototype)
        => (AllowedSceneSpawns.Contains(prototype.Id) || IsCivilianVehicleProp(prototype.Id)) &&
           _prototypes.HasIndex<EntityPrototype>(prototype);

    private static bool IsCivilianVehicleProp(string prototype)
    {
        if (prototype.Contains("Destroyed", StringComparison.OrdinalIgnoreCase) ||
            prototype.Contains("Damaged", StringComparison.OrdinalIgnoreCase) ||
            prototype.Contains("Broken", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return CivilianVehiclePropPrefixes.Any(prefix =>
            prototype.StartsWith(prefix, StringComparison.Ordinal));
    }

    private EntProtoId GetOppositeFacingVehicleProp(EntProtoId prototype)
    {
        if (!IsCivilianVehicleProp(prototype.Id))
            return prototype;

        var suffix = prototype.Id.StartsWith("RMCPropVehicleMonoSupron", StringComparison.Ordinal)
            ? "Flipped"
            : "F";
        EntProtoId westbound = prototype.Id + suffix;
        return IsAllowedSpawn(westbound) ? westbound : prototype;
    }

    private void UpdateMovers(float frameTime)
    {
        var deleteMarkers = new List<(MapCoordinates Coordinates, float Radius)>();
        var markerQuery = EntityQueryEnumerator<CMUSceneDeleteMarkerComponent, TransformComponent>();
        while (markerQuery.MoveNext(out _, out var marker, out var markerXform))
            deleteMarkers.Add((_transform.GetMapCoordinates(markerXform), marker.Radius));

        var toDelete = new List<EntityUid>();
        var query = EntityQueryEnumerator<CMUSceneMoverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mover, out var xform))
        {
            var coordinates = _transform.GetMapCoordinates(xform);

            if (mover.DirectMoveSpeed > 0f)
            {
                var previousPosition = coordinates.Position;
                var movement = new Vector2(mover.Direction.X, mover.Direction.Y) * mover.DirectMoveSpeed * frameTime;
                var position = coordinates.Position + movement;
                _transform.SetWorldPosition((uid, xform), position);
                coordinates = new MapCoordinates(position, coordinates.MapId);

                if (mover.GibMobsOnContact)
                    GibMobsAlongPath(uid, coordinates.MapId, previousPosition, position);
            }

            var finished = coordinates.MapId != mover.StartMap ||
                           Vector2.DistanceSquared(coordinates.Position, mover.StartPosition) >=
                           mover.MaxTravelDistance * mover.MaxTravelDistance;

            if (!finished)
            {
                foreach (var marker in deleteMarkers)
                {
                    if (coordinates.MapId == marker.Coordinates.MapId &&
                        Vector2.DistanceSquared(coordinates.Position, marker.Coordinates.Position) <=
                        marker.Radius * marker.Radius)
                    {
                        finished = true;
                        break;
                    }
                }
            }

            if (finished)
            {
                toDelete.Add(uid);
                continue;
            }

            if (!mover.IsVehicle)
            {
                // Scene marines are unpossessed NPCs, so guarantee the mover exists instead of relying on
                // player attachment to create it.
                var input = EnsureComp<InputMoverComponent>(uid);
                SetMarineWalkInput((uid, input), mover.Direction);
            }
        }

        foreach (var uid in toDelete)
            DeleteSceneEntity(uid);
    }

    private void GibMobsAlongPath(EntityUid vehicle, MapId mapId, Vector2 from, Vector2 to)
    {
        var midpoint = (from + to) * 0.5f;
        var queryRadius = Vector2.Distance(from, to) * 0.5f + CivilianVehicleContactRadius;
        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(
                     new MapCoordinates(midpoint, mapId),
                     queryRadius))
        {
            if (SegmentDistance(target, from, to) <= CivilianVehicleContactRadius)
                TryGibTouchedMob(vehicle, target);
        }
    }

    private void TryGibTouchedMob(EntityUid vehicle, EntityUid target)
    {
        if (target == vehicle || TerminatingOrDeleted(target))
            return;

        _gibbing.Gib(target, user: vehicle);
    }

    private float SegmentDistance(EntityUid target, Vector2 from, Vector2 to)
    {
        var point = _transform.GetMapCoordinates(target).Position;
        var segment = to - from;
        var amount = segment.LengthSquared() < 0.0001f
            ? 0f
            : Math.Clamp(Vector2.Dot(point - from, segment) / segment.LengthSquared(), 0f, 1f);
        return Vector2.Distance(point, from + amount * segment);
    }

    private void SetMarineWalkInput(Entity<InputMoverComponent> marine, Vector2i direction)
    {
        var expected = MoveButtons.Walk;
        if (direction.X > 0)
            expected |= MoveButtons.Right;
        else if (direction.X < 0)
            expected |= MoveButtons.Left;

        if (direction.Y > 0)
            expected |= MoveButtons.Up;
        else if (direction.Y < 0)
            expected |= MoveButtons.Down;

        if (marine.Comp.HeldMoveButtons == expected)
            return;

        // Set the same input state a player would produce so MoverController can apply normal walking movement.
        _moverController.SetVelocityDirection(marine, Direction.North, 0, (expected & MoveButtons.Up) != 0);
        _moverController.SetVelocityDirection(marine, Direction.South, 0, (expected & MoveButtons.Down) != 0);
        _moverController.SetVelocityDirection(marine, Direction.East, 0, (expected & MoveButtons.Right) != 0);
        _moverController.SetVelocityDirection(marine, Direction.West, 0, (expected & MoveButtons.Left) != 0);
        _moverController.SetSprinting(marine, 0, true);
    }

    private void DeleteSceneEntity(EntityUid uid)
    {
        if (TryComp(uid, out CMUSceneMoverComponent? mover))
        {
            foreach (var linked in mover.LinkedEntities)
            {
                if (!TerminatingOrDeleted(linked))
                    QueueDel(linked);
            }
        }

        QueueDel(uid);
    }

    private void ConfigureVehicle(
        EntityUid vehicle,
        string prototype,
        CMUSceneSpawnGroup group,
        CMUSceneSpawnMarkerComponent spawner)
    {
        switch (prototype)
        {
            case "VehicleHumvee":
            case "VehicleHumveeMedical":
            case "VehicleHumveeTransport":
            case "VehicleHumveeARC":
                InstallHardpoint(vehicle, "wheel-1", "VehicleHumveeWheel");
                break;
            case "VehicleAPC":
            case "VehicleAPCMed":
            case "VehicleAPCCommand":
                InstallHardpoint(vehicle, "wheel-1", "RMCAPCWheel");
                if (_random.Prob(0.5f))
                    InstallHardpoint(vehicle, "primary", "RMCAPCDualCannon");
                else
                    InstallHardpoint(vehicle, "support", "RMCAPCFlareLauncher");
                break;
            case "AU14VehicleCivArmoredTruck":
            case "AU14VehicleCivTruck":
            case "AU14VehicleCivVan":
                InstallHardpoint(vehicle, "wheel-1", "RMCVanWheel");
                break;
            case "VehicleTank":
                InstallHardpoint(vehicle, "wheel-1", "VehicleTankTreads");
                TrySpawnTankRiders(vehicle, spawner);
                break;
            case "VehicleTankTWE":
                InstallHardpoint(vehicle, "wheel-1", "VehicleTankTreadsTWE");
                TrySpawnTankRiders(vehicle, spawner);
                break;
        }
    }

    private void InstallHardpoint(EntityUid vehicle, string slot, EntProtoId prototype)
    {
        if (!_prototypes.HasIndex<EntityPrototype>(prototype) ||
            _itemSlots.GetItemOrNull(vehicle, slot) != null)
        {
            return;
        }

        var item = Spawn(prototype, Transform(vehicle).Coordinates);
        if (!_itemSlots.TryInsert(vehicle, slot, item, null, excludeUserAudio: true))
            QueueDel(item);
    }

    private void TrySpawnTankRiders(EntityUid tank, CMUSceneSpawnMarkerComponent spawner)
    {
        if (!_random.Prob(Math.Clamp(spawner.TankRiderChance, 0f, 1f)) ||
            !TryComp(tank, out CMUSceneMoverComponent? mover))
        {
            return;
        }

        var count = _random.Next(2, TankRiderPositions.Length + 1);
        var poses = TankRiderPositions.OrderBy(_ => _random.Next()).Take(count);
        foreach (var localPosition in poses)
        {
            var rider = Spawn(SceneMarine, Transform(tank).Coordinates);
            ConfigureMarine(rider, coldWeather: false);

            if (!_rideSurface.TryPlaceRider(rider, tank, localPosition))
            {
                QueueDel(rider);
                continue;
            }

            var riderComp = EnsureComp<CMUSceneTankRiderComponent>(rider);
            riderComp.Tank = tank;
            riderComp.LocalPosition = localPosition;
            mover.LinkedEntities.Add(rider);
        }
    }

    private void UpdateTankRiders()
    {
        var query = EntityQueryEnumerator<CMUSceneTankRiderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var rider, out var riderXform))
        {
            if (TerminatingOrDeleted(rider.Tank) || !TryComp(rider.Tank, out TransformComponent? tankXform))
            {
                QueueDel(uid);
                continue;
            }

            var (tankPosition, tankRotation) = _transform.GetWorldPositionRotation(tankXform);
            var targetPosition = tankPosition + tankRotation.RotateVec(rider.LocalPosition);
            _transform.SetMapCoordinates((uid, riderXform), new MapCoordinates(targetPosition, tankXform.MapID));

            var outward = rider.LocalPosition.LengthSquared() > 0.001f
                ? Vector2.Normalize(rider.LocalPosition)
                : Vector2.UnitY;
            _transform.SetWorldRotation(riderXform, Angle.FromWorldVec(tankRotation.RotateVec(outward)));
        }
    }

    private void ConfigureMarine(EntityUid marine, bool coldWeather)
    {
        RemComp<SSDIndicatorComponent>(marine);
        EnsureVendorPools();
        ApplyRegulationHair(marine);

        var camouflage = coldWeather
            ? CamouflageType.Snow
            : _random.Prob(0.5f) ? CamouflageType.Jungle : CamouflageType.Desert;

        EquipRandomSection(marine, "Fatigues", "jumpsuit", camouflage);
        EquipRandomSection(marine, "Footwear", "shoes", camouflage);
        EquipRandomSection(marine,
            "Gloves",
            "gloves",
            camouflage,
            coldWeather ? ColdWeatherGloves : null);
        EquipRandomSection(marine, "Headgear", "head", camouflage);
        EquipRandomSection(marine, "Armor", "outerClothing", camouflage);
        EquipRandomSection(marine, "Backpack", "back", camouflage);
        EquipRandomSection(marine, "Belts", "belt", camouflage);

        if (coldWeather)
            EquipRandomSection(marine, "Mask", "mask", camouflage, ColdWeatherMasks);
        else if (_random.Prob(0.35f))
            EquipRandomSection(marine, "Mask", "mask", camouflage);
        if (_random.Prob(0.35f))
            EquipRandomSection(marine, "Glasses & Goggles", "eyes", camouflage);

        EquipRifle(marine, camouflage);
    }

    private void EquipRandomSection(
        EntityUid marine,
        string section,
        string slot,
        CamouflageType camouflage,
        IReadOnlySet<string>? allowed = null)
    {
        if (!_apparelSections.TryGetValue(section, out var entries) || entries.Count == 0)
            return;

        var candidates = allowed == null
            ? entries
            : entries.Where(entry => allowed.Contains(entry.Id)).ToList();
        if (candidates.Count == 0)
            return;

        var prototype = _random.Pick(candidates);
        var item = Spawn(prototype, Transform(marine).Coordinates);
        ApplyCamouflage(item, camouflage);

        if (!_inventory.TryEquip(marine,
                item,
                slot,
                silent: true,
                force: true,
                checkDoafter: false,
                doRangeCheck: false))
        {
            QueueDel(item);
        }
    }

    private void EquipRifle(EntityUid marine, CamouflageType camouflage)
    {
        if (_rifles.Count == 0)
            return;

        var rifle = Spawn(_random.Pick(_rifles), Transform(marine).Coordinates);
        ApplyCamouflage(rifle, camouflage);

        var carryStyle = (MarineWeaponCarryStyle) _random.Next(0, 3);
        if (carryStyle == MarineWeaponCarryStyle.ArmorStorage &&
            TryStoreRifleOnArmor(marine, rifle, camouflage))
        {
            return;
        }

        if (_hands.TryForcePickupAnyHand(marine, rifle, checkActionBlocker: false))
        {
            if (carryStyle == MarineWeaponCarryStyle.Wielded &&
                TryComp(rifle, out WieldableComponent? wieldable))
            {
                _wieldable.TryWield((rifle, wieldable), marine);
            }

            return;
        }

        // A hand should normally always be available, but armor storage is a safe fallback if it is not.
        if (!TryStoreRifleOnArmor(marine, rifle, camouflage))
            QueueDel(rifle);
    }

    private bool TryStoreRifleOnArmor(EntityUid marine, EntityUid rifle, CamouflageType camouflage)
    {
        var sling = Spawn(TwoPointSling, Transform(marine).Coordinates);
        ApplyCamouflage(sling, camouflage);
        if (!TryComp(rifle, out AttachableHolderComponent? holder) ||
            !_attachable.Attach((rifle, holder), sling, marine, "rmc-aslot-rail"))
        {
            QueueDel(sling);
        }

        return _inventory.TryEquip(marine,
                rifle,
                "suitstorage",
                silent: true,
                force: true,
                checkDoafter: false,
                doRangeCheck: false);
    }

    private void ApplyCamouflage(EntityUid item, CamouflageType camouflage)
    {
        if (TryComp(item, out ItemCamouflageComponent? itemCamouflage))
            _camouflage.SetCamouflage((item, itemCamouflage), camouflage);
    }

    private void ApplyRegulationHair(EntityUid marine)
    {
        if (!TryComp(marine, out HumanoidProfileComponent? profile) ||
            !_humanoidAppearance.TryGetMarkings(
                marine,
                HumanoidVisualLayers.Hair,
                out var organ,
                out var markingData,
                out _))
        {
            return;
        }

        var validHair = new List<MarkingPrototype>();
        foreach (var hairId in HairStyles.RegulationHairStyles)
        {
            if (_prototypes.TryIndex<MarkingPrototype>(hairId, out var hair) &&
                hair.BodyPart == HumanoidVisualLayers.Hair &&
                _markings.CanBeApplied(markingData.Group, profile.Sex, hair))
            {
                validHair.Add(hair);
            }
        }

        if (validHair.Count == 0)
            return;

        var color = _random.Pick(HairStyles.RegulationHairColors).Color;
        var style = _random.Pick(validHair);
        _humanoidAppearance.SetMarkings(
            marine,
            organ,
            HumanoidVisualLayers.Hair,
            [style.AsMarking().WithColor(color)]);

        if (!_humanoidAppearance.TryGetMarkings(
                marine,
                HumanoidVisualLayers.FacialHair,
                out var facialOrgan,
                out var facialData,
                out _))
        {
            return;
        }

        if (!_random.Prob(0.3f))
        {
            _humanoidAppearance.SetMarkings(marine, facialOrgan, HumanoidVisualLayers.FacialHair, []);
            return;
        }

        var validFacialHair = new List<MarkingPrototype>();
        foreach (var hairId in HairStyles.RegulationFacialHairStyles)
        {
            if (_prototypes.TryIndex<MarkingPrototype>(hairId, out var hair) &&
                hair.BodyPart == HumanoidVisualLayers.FacialHair &&
                _markings.CanBeApplied(facialData.Group, profile.Sex, hair))
            {
                validFacialHair.Add(hair);
            }
        }

        if (validFacialHair.Count > 0)
        {
            var facial = _random.Pick(validFacialHair);
            _humanoidAppearance.SetMarkings(
                marine,
                facialOrgan,
                HumanoidVisualLayers.FacialHair,
                [facial.AsMarking().WithColor(color)]);
        }
    }

    private void EnsureVendorPools()
    {
        if (_vendorPoolsLoaded)
            return;

        _vendorPoolsLoaded = true;
        if (TryGetVendor(ApparelVendor, out var apparel))
        {
            foreach (var section in apparel.Sections)
            {
                var entries = section.Entries
                    .Select(entry => entry.Id)
                    .Where(id => _prototypes.HasIndex<EntityPrototype>(id))
                    .ToList();
                if (entries.Count > 0)
                    _apparelSections[section.Name] = entries;
            }
        }

        if (TryGetVendor(RequisitionsVendor, out var requisitions))
        {
            foreach (var section in requisitions.Sections)
            {
                if (!section.Name.Contains("firearm", StringComparison.OrdinalIgnoreCase) &&
                    !section.Name.Contains("primar", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var entry in section.Entries)
                {
                    if (entry.Id.Id.Contains("Rifle", StringComparison.OrdinalIgnoreCase) &&
                        _prototypes.TryIndex<EntityPrototype>(entry.Id, out var riflePrototype) &&
                        riflePrototype.TryGetComponent<WieldableComponent>(out _, _componentFactory))
                    {
                        _rifles.Add(entry.Id);
                    }
                }
            }
        }

        if (_rifles.Count == 0 && _prototypes.HasIndex<EntityPrototype>(StandardIssueRifle))
            _rifles.Add(StandardIssueRifle);
    }

    private bool TryGetVendor(string id, out CMAutomatedVendorComponent vendor)
    {
        if (_prototypes.TryIndex<EntityPrototype>(id, out var prototype) &&
            prototype.TryGetComponent<CMAutomatedVendorComponent>(out var component, _componentFactory))
        {
            vendor = component;
            return true;
        }

        vendor = default!;
        return false;
    }

    private TimeSpan RandomInterval(TimeSpan min, TimeSpan max)
    {
        var low = Math.Min(min.TotalSeconds, max.TotalSeconds);
        var high = Math.Max(min.TotalSeconds, max.TotalSeconds);
        return TimeSpan.FromSeconds(_random.NextDouble(low, high));
    }
}
