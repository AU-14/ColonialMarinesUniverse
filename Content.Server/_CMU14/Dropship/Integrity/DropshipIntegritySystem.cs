using System.Linq;
using System.Numerics;
using Content.Server._RMC14.Explosion;
using Content.Server.Explosion.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Destructible;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Shared._CMU14.Destruction;
using Content.Shared._CMU14.Dropship.Integrity;
using Content.Server._CMU14.Destruction;
using Content.Shared._CMU14.Dropship.GunshipControls;
using Content.Shared._CMU14.Dropship.TacticalLand;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Repairable;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.CCVar;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared._RMC14.Explosion;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Station.Components;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Prometheus;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Dropship.Integrity;

public sealed partial class DropshipIntegritySystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCExplosionSystem _explosion = default!;
    [Dependency] private RMCRepairableSystem _repairable = default!;
    [Dependency] private SharedDropshipSystem _dropships = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private ProjectileGrenadeSystem _projectileGrenades = default!;
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private DestructionMomentumSystem _destructionMomentum = default!;
    [Dependency] private IConfigurationManager _configuration = default!;

    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly EntProtoId WarningSignPrototype = "CMUHolographicWarningSign";
    private static readonly EntProtoId CrashExplosion = "CMUDropshipCrashM15Explosion";
    private static readonly TimeSpan ImpactAdoptionGuardTime = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ImpactAdoptionCheckInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan HullInitializationScanInterval = TimeSpan.FromMilliseconds(250);
    private const byte HullInitializationFollowupScans = 4;
    private readonly Dictionary<EntityUid, PendingImpactAdoption> _pendingImpactAdoptions = new();
    private readonly Dictionary<EntityUid, HullExplosionDamageState> _hullExplosionDamage = new();
    private readonly HashSet<EntityUid> _emptyFlightObstructions = new();
    private readonly List<EntityUid> _finishedImpactAdoptions = new();
    private bool _gunshipOverhaulEnabled;
    private static readonly Counter GunshipFlightImpactsMetric = Metrics.CreateCounter(
        "cmu_gunship_flight_impacts_total",
        "Gunship free-flight collision batches processed.");
    private static readonly Histogram GunshipImpactContactsMetric = Metrics.CreateHistogram(
        "cmu_gunship_impact_contacts",
        "Simultaneous obstruction contacts in a gunship impact batch.",
        new HistogramConfiguration { Buckets = Histogram.LinearBuckets(0, 1, 12) });

    public override void Initialize()
    {
        Subs.CVar(_configuration,
            CCVars.CMUEnableGunshipOverhaul,
            enabled => _gunshipOverhaulEnabled = enabled,
            true);

        SubscribeLocalEvent<DropshipComponent, ComponentStartup>(OnDropshipStartup);
        SubscribeLocalEvent<GunshipControlsComponent, ComponentStartup>(OnGunshipControlsStartup);
        SubscribeLocalEvent<GunshipPilotSeatComponent, ComponentStartup>(OnGunshipPilotSeatStartup);
        SubscribeLocalEvent<StationMemberComponent, ComponentStartup>(OnStationMemberStartup);
        SubscribeLocalEvent<DropshipHullComponent, ProjectileHitTargetEvent>(OnProjectileHit);
        SubscribeLocalEvent<DropshipHullComponent, BeforeDamageChangedEvent>(OnBeforeHullDamageChanged);
        SubscribeLocalEvent<DropshipHullComponent, ExplosionReceivedEvent>(OnExplosionReceived);
        SubscribeLocalEvent<DropshipHullComponent, DamageChangedEvent>(OnStructuralDamageChanged);
        SubscribeLocalEvent<DropshipHullComponent, InteractUsingEvent>(OnHullInteractUsing);
        SubscribeLocalEvent<DropshipHullComponent, DropshipIntegrityRepairDoAfterEvent>(OnRepairDoAfter);
        SubscribeLocalEvent<DropshipHullComponent, DropshipMalfunctionRepairDoAfterEvent>(OnMalfunctionRepairDoAfter);
        SubscribeLocalEvent<DropshipHullComponent, ExaminedEvent>(OnHullExamined);
    }

    private void OnDropshipStartup(Entity<DropshipComponent> ent, ref ComponentStartup args)
    {
        // Navigation computers can cause DropshipComponent to be ensured on their
        // parent grid. Only gunships that explicitly opt in through their pilot
        // seat or controls console should receive hull integrity and crash behavior.
        if (!_gunshipOverhaulEnabled ||
            !IsDropshipIntegrityGrid(ent.Owner) ||
            !HasGunshipControls(ent.Owner))
        {
            RemoveDropshipIntegrity(ent.Owner);
            return;
        }

        InitializeDropshipIntegrity(ent.Owner);
    }

    private void OnGunshipControlsStartup(Entity<GunshipControlsComponent> ent, ref ComponentStartup args)
    {
        TryInitializeControlledGunship(ent.Owner);
    }

    private void OnGunshipPilotSeatStartup(Entity<GunshipPilotSeatComponent> ent, ref ComponentStartup args)
    {
        TryInitializeControlledGunship(ent.Owner);
    }

    private void TryInitializeControlledGunship(EntityUid controls)
    {
        if (!_gunshipOverhaulEnabled ||
            Transform(controls).GridUid is not { } grid ||
            !HasComp<DropshipComponent>(grid) ||
            !IsDropshipIntegrityGrid(grid))
        {
            return;
        }

        InitializeDropshipIntegrity(grid);
    }

    private void InitializeDropshipIntegrity(EntityUid grid)
    {
        var integrity = EnsureComp<DropshipIntegrityComponent>(grid);
        integrity.Integrity = Math.Clamp(integrity.Integrity, 0f, integrity.MaxIntegrity);
        if (TryComp(grid, out DropshipComponent? dropship))
            integrity.Wrecked |= dropship.Crashed;
        integrity.FlightState = ResolveFlightState(grid, integrity);

        Dirty(grid, integrity);
        MarkInitialHull(grid);
        integrity.HullInitializationScansRemaining = HullInitializationFollowupScans;
        integrity.NextHullInitializationScan = _timing.CurTime + HullInitializationScanInterval;
    }

    private bool HasGunshipControls(EntityUid grid)
    {
        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<GunshipControlsComponent>(child) || HasComp<GunshipPilotSeatComponent>(child))
                return true;
        }

        return false;
    }

    private void OnStationMemberStartup(Entity<StationMemberComponent> ent, ref ComponentStartup args)
    {
        // Station membership is often assigned after other grid components have
        // started. Clean up any false-positive integrity pool created earlier in
        // that startup sequence.
        RemoveDropshipIntegrity(ent.Owner);
    }

    private bool IsDropshipIntegrityGrid(EntityUid grid)
    {
        return HasComp<ShuttleComponent>(grid) &&
               HasComp<MapGridComponent>(grid) &&
               !HasComp<BecomesStationComponent>(grid) &&
               !HasComp<StationMemberComponent>(grid);
    }

    private void RemoveDropshipIntegrity(EntityUid grid)
    {
        if (!HasComp<DropshipIntegrityComponent>(grid))
            return;

        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
            RemComp<DropshipHullComponent>(child);

        RemComp<DropshipIntegrityComponent>(grid);
    }

    private void OnStructuralDamageChanged(Entity<DropshipHullComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is null || !Transform(ent).Anchored)
            return;

        if (!TryGetDropship(ent.Owner, out var dropship))
            return;

        var amount = args.DamageDelta.GetTotal().Float();
        if (amount > 0f)
            DamageIntegrity(dropship, amount);
    }

    private void OnBeforeHullDamageChanged(Entity<DropshipHullComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Source is { } source &&
            HasComp<XenoAcidProjectileComponent>(source) &&
            TryGetDropship(ent.Owner, out var dropship))
        {
            args.Damage *= dropship.Comp.XenoAcidProjectileDamageMultiplier;
        }
    }

    /// <summary>
    /// Intact dropship walls deliberately have no Damageable component, so an
    /// ordinary projectile impact cannot raise DamageChangedEvent for them.
    /// Forward that impact into the grid's shared integrity pool. Damageable
    /// hull pieces (doors, wreck walls, and similar structures) remain on the
    /// normal damage event path to avoid counting the same shot twice.
    /// </summary>
    private void OnProjectileHit(Entity<DropshipHullComponent> target, ref ProjectileHitTargetEvent args)
    {
        if (HasComp<DamageableComponent>(target) ||
            !Transform(target).Anchored ||
            !TryGetDropship(target, out var dropship))
        {
            return;
        }

        var multiplier = HasComp<XenoAcidProjectileComponent>(args.Projectile)
            ? dropship.Comp.XenoAcidProjectileDamageMultiplier
            : 1f;
        var amount = args.Damage.GetTotal().Float() * multiplier;
        if (amount > 0f)
            DamageIntegrity(dropship, amount);
    }

    /// <summary>
    /// Intact hull walls are intentionally not Damageable, but explosions still
    /// discover them through their collision broadphase and raise this event.
    /// Forward the strongest wall exposure from each blast into the shared pool.
    /// Damageable wreck pieces remain on the normal DamageChangedEvent path.
    /// </summary>
    private void OnExplosionReceived(Entity<DropshipHullComponent> target, ref ExplosionReceivedEvent args)
    {
        if (HasComp<DamageableComponent>(target) ||
            !Transform(target).Anchored ||
            !TryGetDropship(target, out var dropship))
        {
            return;
        }

        var amount = args.Damage.GetTotal().Float();
        if (amount <= 0f)
            return;

        if (_hullExplosionDamage.TryGetValue(dropship.Owner, out var previous) &&
            previous.Tick == _timing.CurTick &&
            previous.Epicenter == args.Epicenter)
        {
            if (amount <= previous.Damage)
                return;

            var additionalDamage = amount - previous.Damage;
            _hullExplosionDamage[dropship.Owner] = new HullExplosionDamageState(
                _timing.CurTick,
                args.Epicenter,
                amount);
            DamageIntegrity(dropship, additionalDamage);
            return;
        }

        _hullExplosionDamage[dropship.Owner] = new HullExplosionDamageState(
            _timing.CurTick,
            args.Epicenter,
            amount);
        DamageIntegrity(dropship, amount);
    }

    /// <summary>
    /// Applies an impact and returns the impact-speed budget left after breaking
    /// every obstruction. A zero result means the ship must stop.
    /// </summary>
    public float ApplyFlightImpact(
        EntityUid dropship,
        IReadOnlyCollection<EntityUid> obstructions,
        float speed,
        EntityUid? terrainGrid = null)
    {
        // Grid traversal can adopt an overlapping anchored obstruction before
        // damage is applied. Snapshot the ship first and prefer the terrain
        // grid supplied by the collision query over the obstruction's current
        // (possibly already changed) parent.
        HashSet<EntityUid> originalChildren;
        if (TryComp(dropship, out DropshipTacticalHoverComponent? hover) &&
            hover.FlightGridChildrenInitialized)
        {
            originalChildren = hover.FlightGridChildren;
        }
        else
        {
            originalChildren = new HashSet<EntityUid>();
            var children = Transform(dropship).ChildEnumerator;
            while (children.MoveNext(out var child))
                originalChildren.Add(child);
        }

        Entity<MapGridComponent>? obstructionGrid = null;
        if (terrainGrid is { } explicitGround &&
            explicitGround != dropship &&
            TryComp(explicitGround, out MapGridComponent? explicitGroundGrid))
        {
            obstructionGrid = (explicitGround, explicitGroundGrid);
        }

        if (obstructionGrid == null)
        {
            foreach (var obstruction in obstructions)
            {
                if (!TryComp(obstruction, out TransformComponent? obstructionXform) ||
                    obstructionXform.GridUid is not { } grid ||
                    grid == dropship ||
                    !TryComp(grid, out MapGridComponent? gridComp))
                {
                    continue;
                }

                obstructionGrid = (grid, gridComp);
                break;
            }
        }

        // The Z-level map entity is also its terrain grid and remains the
        // fallback for callers which cannot provide an authoritative grid.
        if (obstructionGrid == null &&
            Transform(dropship).MapUid is { } map &&
            map != dropship &&
            TryComp(map, out MapGridComponent? mapGrid))
        {
            obstructionGrid = (map, mapGrid);
        }

        if (!TryComp(dropship, out DropshipIntegrityComponent? integrity) ||
            integrity.Crashing || integrity.Wrecked || speed < integrity.MinimumDamagingImpactSpeed)
        {
            GuardImpactAdoptions(dropship, obstructionGrid, originalChildren, obstructions);
            return 0f;
        }

        if (_timing.CurTime >= integrity.NextImpactSound)
        {
            _audio.PlayPvs(integrity.ImpactSound, dropship);
            integrity.NextImpactSound = _timing.CurTime + integrity.ImpactSoundCooldown;
        }

        if (integrity.ObstacleDamageMultiplier <= 0f)
        {
            GuardImpactAdoptions(dropship, obstructionGrid, originalChildren, obstructions);
            return 0f;
        }

        // Swept-motion callers supply geometric contact order as a list.
        // Preserve it so damage is resolved front-to-back; unordered callers
        // retain a stable entity-id fallback for deterministic replays.
        var activeObstructions = obstructions
            .Where(obstruction => !TerminatingOrDeleted(obstruction));
        var orderedObstructions = obstructions is IReadOnlyList<EntityUid>
            ? activeObstructions.ToArray()
            : activeObstructions.OrderBy(obstruction => obstruction.Id).ToArray();
        GunshipFlightImpactsMetric.Inc();
        GunshipImpactContactsMetric.Observe(orderedObstructions.Length);
        var remainingSpeed = speed;
        var removedEveryObstruction = true;
        var removedAnyObstruction = false;
        if (!TryApplyProportionalImpactBatch(
                dropship,
                orderedObstructions,
                speed,
                integrity.ObstacleDamageMultiplier,
                out remainingSpeed,
                out removedEveryObstruction,
                out removedAnyObstruction))
        {
            foreach (var obstruction in orderedObstructions)
            {
                if (remainingSpeed <= 0f)
                {
                    removedEveryObstruction = false;
                    break;
                }

                if (TrySmashFlightObstacle(
                        obstruction,
                        dropship,
                        integrity.ObstacleDamageMultiplier,
                        ref remainingSpeed))
                {
                    removedAnyObstruction = true;
                    continue;
                }

                if (!_destructionMomentum.TryGetBreakCost(obstruction,
                        remainingSpeed,
                        integrity.ObstacleDamageMultiplier,
                        out var breakCost))
                {
                    // Legacy fallback for explicitly smashable or unusual
                    // damageables without a resolvable destruction threshold.
                    var remainingDamage = remainingSpeed * remainingSpeed * integrity.ObstacleDamageMultiplier;
                    ApplyObstacleDamage(obstruction, remainingDamage, dropship);
                    remainingSpeed = 0f;
                    removedEveryObstruction = false;
                    break;
                }

                var rawDamage = breakCost * breakCost * integrity.ObstacleDamageMultiplier;
                ApplyObstacleDamage(obstruction, rawDamage, dropship);
                remainingSpeed = ImpactEnergySolver.GetRemainingSpeed(remainingSpeed, breakCost);
                removedAnyObstruction = true;
            }
        }

        GuardImpactAdoptions(dropship, obstructionGrid, originalChildren, obstructions);

        var resultSpeed = removedEveryObstruction && removedAnyObstruction
            ? remainingSpeed
            : 0f;

        // Self-damage is based on the squared-speed budget actually spent on
        // the collision. A light obstruction should not deal the same damage
        // as an indestructible wall hit at the same incoming speed.
        var spentSpeedSquared = MathF.Max(0f, speed * speed - resultSpeed * resultSpeed);
        DamageIntegrity((dropship, integrity), spentSpeedSquared * integrity.ImpactDamageMultiplier);

        return integrity.Crashing || integrity.Wrecked
            ? 0f
            : resultSpeed;
    }

    private bool TryApplyProportionalImpactBatch(
        EntityUid dropship,
        IReadOnlyList<EntityUid> obstructions,
        float speed,
        float damageMultiplier,
        out float remainingSpeed,
        out bool removedEveryObstruction,
        out bool removedAnyObstruction)
    {
        remainingSpeed = speed;
        removedEveryObstruction = true;
        removedAnyObstruction = false;
        if (obstructions.Count == 0)
            return true;

        var requiredSpeeds = new float[obstructions.Count];
        for (var i = 0; i < obstructions.Count; i++)
        {
            if (!_destructionMomentum.TryGetRequiredBreakSpeed(
                    obstructions[i],
                    damageMultiplier,
                    out requiredSpeeds[i]))
            {
                return false;
            }
        }

        var allocation = ImpactEnergySolver.AllocateBatch(speed, requiredSpeeds);
        for (var i = 0; i < obstructions.Count; i++)
        {
            var obstruction = obstructions[i];
            var requiredSpeed = requiredSpeeds[i];
            var rawDamage = requiredSpeed * requiredSpeed * damageMultiplier * allocation.AppliedFraction;

            var smashed = false;
            if (allocation.CanClearAll &&
                TryComp(obstruction, out VehicleSmashableComponent? smashable) &&
                smashable is { DeleteOnHit: true })
            {
                var ignoredRemainingSpeed = speed;
                smashed = TrySmashFlightObstacle(
                    obstruction,
                    dropship,
                    damageMultiplier,
                    ref ignoredRemainingSpeed);
            }

            if (!smashed)
                ApplyObstacleDamage(obstruction, rawDamage, dropship);
        }

        remainingSpeed = allocation.RemainingSpeed;
        removedEveryObstruction = allocation.CanClearAll;
        removedAnyObstruction = allocation.CanClearAll;
        return true;
    }

    /// <summary>
    /// Uses the same explicit destruction contract as ordinary RMC vehicles.
    /// Platform edges commonly change into a non-colliding broken construction
    /// node under generic damage; VehicleSmashable instead means that a vehicle
    /// impact should remove the structure completely.
    /// </summary>
    private bool TrySmashFlightObstacle(
        EntityUid obstruction,
        EntityUid dropship,
        float damageMultiplier,
        ref float remainingSpeed)
    {
        if (!TryComp(obstruction, out VehicleSmashableComponent? smashable) ||
            !smashable.DeleteOnHit ||
            smashable.RequiredVehicleTag is { } requiredTag && !_tag.HasTag(dropship, requiredTag))
        {
            return false;
        }

        if (smashable.SmashSound != null)
            _audio.PlayPvs(smashable.SmashSound, Transform(obstruction).Coordinates);

        // Snapshot the physical cost before applying the guaranteed smash;
        // afterward the entity may already be terminating or at its threshold.
        var hasBreakCost = _destructionMomentum.TryGetBreakCost(
            obstruction,
            remainingSpeed,
            damageMultiplier,
            out var breakCost);

        var damage = new DamageSpecifier
        {
            DamageDict =
            {
                ["Blunt"] = FixedPoint2.New(smashable.DamageOnHit),
            },
        };
        _damageable.TryChangeDamage(obstruction, damage, true, origin: dropship, tool: dropship);

        if (TryComp(obstruction, out PhysicsComponent? physics))
            _physics.SetCanCollide(obstruction, false, force: true, body: physics);

        if (!TerminatingOrDeleted(obstruction))
            _destructible.DestroyEntity(obstruction);

        // Dropships preserve momentum by spending only the speed required to
        // remove the obstacle. The ordinary vehicle slowdown multiplier is
        // multiplicative and used to halve speed once per entity; across a row
        // of railings or platform edges that compounded to effectively zero.
        if (hasBreakCost)
        {
            remainingSpeed = ImpactEnergySolver.GetRemainingSpeed(remainingSpeed, breakCost);
        }
        else
        {
            // Some explicitly smashable props have no damage/destruction
            // threshold from which a physical cost can be derived.
            remainingSpeed *= Math.Clamp(smashable.SlowdownMultiplier, 0f, 1f);
        }

        return true;
    }

    private void GuardImpactAdoptions(
        EntityUid dropship,
        Entity<MapGridComponent>? ground,
        HashSet<EntityUid> originalChildren,
        IReadOnlyCollection<EntityUid> obstructions)
    {
        if (ground is not { } impactGround)
            return;

        var forcedGround = obstructions.ToHashSet();
        var terrainAnchors = TryComp(dropship, out DropshipTacticalHoverComponent? hover)
            ? new Dictionary<EntityUid, DropshipTerrainAnchorPose>(hover.FlightTerrainAnchors)
            : new Dictionary<EntityUid, DropshipTerrainAnchorPose>();
        RestoreImpactAdoptions(dropship, impactGround, originalChildren, forcedGround,
            terrainAnchors: terrainAnchors);
        _pendingImpactAdoptions[dropship] = new PendingImpactAdoption(
            impactGround.Owner,
            originalChildren,
            forcedGround,
            terrainAnchors,
            _timing.CurTime + _timing.TickPeriod,
            _timing.CurTime + ImpactAdoptionGuardTime);
    }

    /// <summary>
    /// Guards ordinary free-flight movement against adopting anchored terrain
    /// whose fixtures are not considered a blocking impact.
    /// </summary>
    public void GuardFlightAdoptions(
        EntityUid dropship,
        EntityUid terrainGrid,
        HashSet<EntityUid> originalChildren,
        IReadOnlyDictionary<EntityUid, DropshipTerrainAnchorPose> terrainAnchors)
    {
        if (terrainGrid == dropship ||
            !TryComp(terrainGrid, out MapGridComponent? groundGrid))
        {
            return;
        }

        // In ordinary unobstructed flight the grid still has exactly its
        // original children. Avoid copying the candidate poses and scanning
        // the entire ship when there is nothing to restore. Transform
        // reparenting is synchronous, so an adopted terrain entity has already
        // increased this count by the time this guard runs.
        if (!_pendingImpactAdoptions.ContainsKey(dropship) &&
            Transform(dropship).ChildCount == originalChildren.Count &&
            !HasAdoptedTerrainCandidate(dropship, terrainAnchors))
        {
            return;
        }

        // A blocking impact in the same movement tick may already have
        // scheduled a stricter guard with explicit obstruction entities and
        // impact tiles. Do not replace that information with the general
        // free-flight guard.
        if (_pendingImpactAdoptions.TryGetValue(dropship, out var pending) &&
            pending.Ground == terrainGrid &&
            _timing.CurTime < pending.Expires)
        {
            foreach (var (entity, pose) in terrainAnchors)
                pending.TerrainAnchors.TryAdd(entity, pose);

            RestoreImpactAdoptions(
                dropship,
                (terrainGrid, groundGrid),
                originalChildren,
                pending.ForcedGround,
                terrainAnchors: pending.TerrainAnchors);
            return;
        }

        var preservedAnchors = new Dictionary<EntityUid, DropshipTerrainAnchorPose>(terrainAnchors);
        RestoreImpactAdoptions(
            dropship,
            (terrainGrid, groundGrid),
            originalChildren,
            _emptyFlightObstructions,
            terrainAnchors: preservedAnchors);
        _pendingImpactAdoptions[dropship] = new PendingImpactAdoption(
            terrainGrid,
            originalChildren,
            _emptyFlightObstructions,
            preservedAnchors,
            _timing.CurTime + TimeSpan.FromMilliseconds(100),
            _timing.CurTime + ImpactAdoptionGuardTime);
    }

    private void ApplyObstacleDamage(EntityUid obstruction, float rawDamage, EntityUid dropship)
    {
        if (TerminatingOrDeleted(obstruction) || rawDamage <= 0f || !HasComp<DamageableComponent>(obstruction))
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = FixedPoint2.New(rawDamage);
        _damageable.TryChangeDamage(obstruction, damage, origin: dropship);
    }

    private void RestoreImpactAdoptions(
        EntityUid dropship,
        Entity<MapGridComponent> ground,
        HashSet<EntityUid> originalChildren,
        HashSet<EntityUid> forcedGround,
        IReadOnlyDictionary<EntityUid, DropshipTerrainAnchorPose>? terrainAnchors = null)
    {
        if (Transform(dropship).ChildCount == originalChildren.Count &&
            !HasAdoptedTerrainCandidate(dropship, forcedGround) &&
            (terrainAnchors == null || !HasAdoptedTerrainCandidate(dropship, terrainAnchors)))
            return;

        var adopted = new List<EntityUid>();
        var children = Transform(dropship).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (originalChildren.Contains(child) && !forcedGround.Contains(child))
                continue;

            // Entities added to the dropship legitimately during flight are
            // not terrain. Only restore a foreign child when it was captured
            // on the ground grid or explicitly identified as an obstruction.
            if (!forcedGround.Contains(child) &&
                (terrainAnchors == null || !terrainAnchors.ContainsKey(child)))
            {
                continue;
            }

            adopted.Add(child);
        }

        var groundXform = Transform(ground);
        var groundRotation = _transform.GetWorldRotation(ground);
        foreach (var child in adopted)
        {
            if (TerminatingOrDeleted(child) || !TryComp(child, out TransformComponent? childXform))
                continue;

            Vector2 localPosition;
            Angle localRotation;
            if (terrainAnchors != null && terrainAnchors.TryGetValue(child, out var terrainPose))
            {
                localPosition = terrainPose.Position;
                localRotation = terrainPose.Rotation;
            }
            else
            {
                var worldPosition = _transform.GetWorldPosition(childXform);
                var worldRotation = _transform.GetWorldRotation(childXform);
                localPosition = Vector2.Transform(worldPosition, groundXform.InvLocalMatrix);
                localRotation = worldRotation - groundRotation;
            }
            var wasAnchored = childXform.Anchored;

            _transform.SetCoordinates(
                child,
                childXform,
                new EntityCoordinates(ground, localPosition),
                localRotation);

            if (!wasAnchored || TerminatingOrDeleted(child))
                continue;

            var tile = _map.TileIndicesFor(ground, ground.Comp, new EntityCoordinates(ground, localPosition));
            _transform.AnchorEntity((child, childXform), ground, tile);
        }
    }

    private bool HasAdoptedTerrainCandidate<T>(EntityUid dropship, IEnumerable<KeyValuePair<EntityUid, T>> candidates)
    {
        foreach (var (candidate, _) in candidates)
        {
            if (TryComp(candidate, out TransformComponent? xform) && xform.ParentUid == dropship)
                return true;
        }

        return false;
    }

    private bool HasAdoptedTerrainCandidate(EntityUid dropship, IEnumerable<EntityUid> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (TryComp(candidate, out TransformComponent? xform) && xform.ParentUid == dropship)
                return true;
        }

        return false;
    }

    public void DamageIntegrity(Entity<DropshipIntegrityComponent> dropship, float amount)
    {
        if (amount <= 0f || dropship.Comp.Crashing || dropship.Comp.Wrecked)
            return;

        var previousIntegrity = dropship.Comp.Integrity;
        dropship.Comp.Integrity = Math.Max(0f, dropship.Comp.Integrity - amount);
        TryTriggerMalfunctions(dropship, previousIntegrity);
        Dirty(dropship);

        if (dropship.Comp.Integrity <= 0f)
            BeginCrash(dropship);
    }

    private void BeginCrash(Entity<DropshipIntegrityComponent> integrity)
    {
        if (!TryComp(integrity.Owner, out DropshipComponent? dropship) || integrity.Comp.Crashing || integrity.Comp.Wrecked)
            return;

        integrity.Comp.Crashing = true;
        integrity.Comp.CrashAt = _timing.CurTime + integrity.Comp.CrashWarningTime;
        _dropships.SetDropshipCrashed((integrity.Owner, dropship), true);
        Dirty(integrity);

        var xform = Transform(integrity.Owner);
        integrity.Comp.CrashMap = xform.MapUid;
        if (TryComp(integrity.Owner, out DropshipTacticalHoverComponent? hover))
        {
            var crashStarted = new GunshipCrashStartedEvent();
            RaiseLocalEvent(integrity.Owner, ref crashStarted);

            if (xform.MapUid is { } currentMap &&
                _zLevels.TryMapOffset(currentMap, hover.GroundMapOffset, out var groundMap))
            {
                integrity.Comp.CrashMap = groundMap.Value.Owner;
            }
            else if (hover.GroundMap is { } fallbackGround)
            {
                integrity.Comp.CrashMap = fallbackGround;
            }

            hover.GunshipLinearVelocity = Vector2.Zero;
        }

        if (integrity.Comp.CrashMap is { } warningMap && TryComp(warningMap, out MapComponent? map))
        {
            SpawnCrashWarning(warningMap,
                _transform.GetWorldPosition(integrity.Owner),
                integrity.Owner,
                (float) integrity.Comp.CrashWarningTime.TotalSeconds);
        }

        _popup.PopupEntity(Loc.GetString("cmu-gunship-critical-hull-failure"), integrity.Owner, PopupType.LargeCaution);
    }

    private void SpawnCrashWarning(EntityUid map, Vector2 position, EntityUid dropship, float lifetime)
    {
        if (!TryComp(dropship, out MapGridComponent? grid))
            return;

        var tiles = _map.GetAllTiles(dropship, grid).Select(tile => tile.GridIndices).ToHashSet();
        var rotation = _transform.GetWorldRotation(dropship);
        foreach (var tile in tiles)
        {
            if (tiles.Contains(tile + Vector2i.Up) &&
                tiles.Contains(tile + Vector2i.Down) &&
                tiles.Contains(tile + Vector2i.Left) &&
                tiles.Contains(tile + Vector2i.Right))
            {
                continue;
            }

            var localCenter = _map.TileCenterToVector(dropship, grid, tile);
            var warning = Spawn(WarningSignPrototype,
                new EntityCoordinates(map, position + rotation.RotateVec(localCenter)));
            EnsureComp<TimedDespawnComponent>(warning).Lifetime = lifetime;
        }
    }

    public override void Update(float frameTime)
    {
        ProcessPendingImpactAdoptions();

        var query = EntityQueryEnumerator<DropshipIntegrityComponent>();
        while (query.MoveNext(out var uid, out var integrity))
        {
            var flightState = ResolveFlightState(uid, integrity);
            if (integrity.FlightState != flightState)
            {
                integrity.FlightState = flightState;
                Dirty(uid, integrity);
            }

            if (integrity.HullInitializationScansRemaining > 0 &&
                _timing.CurTime >= integrity.NextHullInitializationScan)
            {
                MarkInitialHull(uid);
                integrity.HullInitializationScansRemaining--;
                integrity.NextHullInitializationScan = _timing.CurTime + HullInitializationScanInterval;
            }

            if (integrity.CrashAftermathAt is { } aftermathAt && _timing.CurTime >= aftermathAt)
            {
                integrity.CrashAftermathAt = null;
                if (TryComp(uid, out MapGridComponent? wreckGrid))
                {
                    ConvertCrashWalls((uid, wreckGrid));
                    SpawnCrashExplosions((uid, wreckGrid));
                    SpawnConsoleShrapnel((uid, wreckGrid));
                }
            }

            if (!integrity.Crashing)
                continue;

            if (TryComp(uid, out DropshipTacticalHoverComponent? hover))
            {
                hover.GunshipAngularVelocityDegrees += integrity.CrashSpinAccelerationDegrees * frameTime;
                _transform.SetWorldRotation(uid,
                    _transform.GetWorldRotation(uid) + Angle.FromDegrees(hover.GunshipAngularVelocityDegrees * frameTime));
            }

            if (_timing.CurTime >= integrity.CrashAt)
                FinishCrash((uid, integrity));
        }
    }

    private void ProcessPendingImpactAdoptions()
    {
        if (_pendingImpactAdoptions.Count == 0)
            return;

        var finished = _finishedImpactAdoptions;
        finished.Clear();
        foreach (var (dropship, pending) in _pendingImpactAdoptions)
        {
            if (TerminatingOrDeleted(dropship) ||
                _timing.CurTime >= pending.Expires ||
                !TryComp(pending.Ground, out MapGridComponent? groundGrid))
            {
                finished.Add(dropship);
                continue;
            }

            if (_timing.CurTime < pending.NextCheck)
                continue;

            RestoreImpactAdoptions(
                dropship,
                (pending.Ground, groundGrid),
                pending.OriginalChildren,
                pending.ForcedGround,
                terrainAnchors: pending.TerrainAnchors);
            pending.NextCheck = _timing.CurTime + ImpactAdoptionCheckInterval;
        }

        foreach (var dropship in finished)
            _pendingImpactAdoptions.Remove(dropship);
    }

    private sealed class PendingImpactAdoption(
        EntityUid ground,
        HashSet<EntityUid> originalChildren,
        HashSet<EntityUid> forcedGround,
        Dictionary<EntityUid, DropshipTerrainAnchorPose> terrainAnchors,
        TimeSpan nextCheck,
        TimeSpan expires)
    {
        public readonly EntityUid Ground = ground;
        public readonly HashSet<EntityUid> OriginalChildren = originalChildren;
        public readonly HashSet<EntityUid> ForcedGround = forcedGround;
        public readonly Dictionary<EntityUid, DropshipTerrainAnchorPose> TerrainAnchors = terrainAnchors;
        public TimeSpan NextCheck = nextCheck;
        public readonly TimeSpan Expires = expires;
    }

    private readonly record struct HullExplosionDamageState(
        GameTick Tick,
        MapCoordinates Epicenter,
        float Damage);

    private void MarkInitialHull(EntityUid dropship)
    {
        var children = Transform(dropship).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            // Intact dropship walls are deliberately invincible and only gain
            // Damageable when the ship becomes a wreck. Still expose the
            // shared hull status and repair interaction on every wall.
            if (!HasComp<DamageableComponent>(child) && !_tag.HasTag(child, WallTag))
                continue;

            var childTransform = Transform(child);
            if (!childTransform.Anchored || childTransform.GridUid != dropship)
                continue;

            EnsureComp<DropshipHullComponent>(child);
        }
    }

    private void FinishCrash(Entity<DropshipIntegrityComponent> integrity)
    {
        if (!TryComp(integrity.Owner, out MapGridComponent? shipGrid))
            return;

        var position = _transform.GetWorldPosition(integrity.Owner);
        var rotation = _transform.GetWorldRotation(integrity.Owner);
        if (integrity.Comp.CrashMap is { } targetMap &&
            targetMap != integrity.Owner &&
            TryComp(targetMap, out MapComponent? mapComp) &&
            TryComp(targetMap, out MapGridComponent? groundGrid))
        {
            ClearCrashFootprint((integrity.Owner, shipGrid), (targetMap, groundGrid), position, rotation);
            _transform.SetMapCoordinates(integrity.Owner, new MapCoordinates(position, mapComp.MapId));
            _transform.SetWorldRotation(integrity.Owner, rotation);
        }

        if (TryComp(integrity.Owner, out DropshipTacticalHoverComponent? hover))
        {
            hover.GunshipLinearVelocity = Vector2.Zero;
            hover.GunshipAngularVelocityDegrees = 0f;
            RemComp<DropshipTacticalHoverComponent>(integrity.Owner);
        }

        integrity.Comp.Crashing = false;
        integrity.Comp.Wrecked = true;
        integrity.Comp.Integrity = 0f;
        Dirty(integrity);

        if (TryComp(integrity.Owner, out DropshipComponent? dropship))
            _audio.PlayPvs(dropship.CrashSound, integrity.Owner);

        // Keep the recursive map transfer and tactical-hover component removal
        // in a separate game state from the large batch of wreck components,
        // triggered explosions, and shrapnel entities.
        integrity.Comp.CrashAftermathAt = _timing.CurTime + _timing.TickPeriod;
    }

    private void ConvertCrashWalls(Entity<MapGridComponent> ship)
    {
        var children = Transform(ship.Owner).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!TryComp(child, out DropshipCrashDestructibleWallComponent? wreckWall))
                continue;

            EntityManager.AddComponents(child, wreckWall.Components);
            EnsureComp<DropshipHullComponent>(child);
        }
    }

    private void ClearCrashFootprint(
        Entity<MapGridComponent> ship,
        Entity<MapGridComponent> ground,
        Vector2 position,
        Angle rotation)
    {
        var delete = new HashSet<EntityUid>();
        foreach (var tile in _map.GetAllTiles(ship.Owner, ship.Comp))
        {
            var localCenter = _map.TileCenterToVector(ship.Owner, ship.Comp, tile.GridIndices);
            var sample = position + rotation.RotateVec(localCenter);
            if (!_map.TryGetTileRef(ground.Owner, ground.Comp, sample, out var targetTile))
                continue;

            foreach (var anchored in _map.GetAnchoredEntities(ground.Owner, ground.Comp, targetTile.GridIndices))
            {
                // Tactical destinations are control markers, not physical
                // obstructions. Deleting one leaves navigation state holding a
                // stale entity reference and can break later dropship updates.
                if (anchored != ship.Owner &&
                    !HasComp<MapComponent>(anchored) &&
                    !HasComp<MapGridComponent>(anchored) &&
                    !HasComp<DropshipDestinationComponent>(anchored))
                {
                    delete.Add(anchored);
                }
            }
        }

        foreach (var uid in delete)
        {
            if (!TerminatingOrDeleted(uid))
                QueueDel(uid);
        }
    }

    private void SpawnCrashExplosions(Entity<MapGridComponent> ship)
    {
        var tiles = _map.GetAllTiles(ship.Owner, ship.Comp).ToList();
        if (tiles.Count == 0)
            return;

        var mapId = Transform(ship.Owner).MapID;
        for (var i = 0; i < 2; i++)
        {
            var tile = _random.Pick(tiles);
            var local = _map.TileCenterToVector(ship.Owner, ship.Comp, tile.GridIndices);
            var world = _transform.GetWorldPosition(ship.Owner) + _transform.GetWorldRotation(ship.Owner).RotateVec(local);
            QueueCrashExplosion(new MapCoordinates(world, mapId), ship.Owner);
        }

        var radius = MathF.Max(ship.Comp.LocalAABB.Width, ship.Comp.LocalAABB.Height) * 0.65f;
        for (var i = 0; i < 2; i++)
        {
            var angle = _random.NextAngle();
            var world = _transform.GetWorldPosition(ship.Owner) + angle.ToVec() * radius;
            QueueCrashExplosion(new MapCoordinates(world, mapId), ship.Owner);
        }
    }

    private void QueueCrashExplosion(
        MapCoordinates coordinates,
        EntityUid cause)
    {
        // Match the M15's RMC blast once. The spawned helper below supplies
        // only its fragmentation payload, avoiding the previous duplicate
        // explosion at every crash site.
        _explosion.QueueExplosion(
            coordinates,
            "RMC",
            240f,
            6f,
            20f,
            cause,
            tileBreakScale: 3f,
            canCreateVacuum: false);

        var effect = Spawn(CrashExplosion, coordinates);
        _trigger.Trigger(effect, cause);
        QueueDel(effect);
    }

    private void SpawnConsoleShrapnel(Entity<MapGridComponent> ship)
    {
        var consoles = new List<EntityUid>();
        var children = Transform(ship.Owner).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<GunshipControlsComponent>(child))
                consoles.Add(child);
        }

        foreach (var console in consoles)
        {
            if (TerminatingOrDeleted(console))
                continue;

            var effect = Spawn(CrashExplosion, _transform.GetMapCoordinates(console));
            if (TryComp(effect, out ProjectileGrenadeComponent? projectileGrenade))
                _projectileGrenades.SetPayloadCount((effect, projectileGrenade), _random.Next(18, 46));

            _trigger.Trigger(effect, ship.Owner);
            QueueDel(effect);
        }
    }

    private void OnHullInteractUsing(Entity<DropshipHullComponent> target, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryGetDropship(target.Owner, out var integrity))
        {
            return;
        }

        if (TryStartMalfunctionRepair(target, integrity, ref args))
            return;

        if (!_tool.HasQuality(args.Used, WeldingQuality))
            return;

        args.Handled = true;
        if (integrity.Comp.Wrecked || integrity.Comp.Crashing)
        {
            _popup.PopupEntity(Loc.GetString("cmu-gunship-repair-wrecked"), target, args.User, PopupType.SmallCaution);
            return;
        }

        if (!CanRepairDropship(integrity.Owner))
        {
            _popup.PopupEntity(Loc.GetString("cmu-gunship-repair-must-be-landed"), target, args.User, PopupType.SmallCaution);
            return;
        }

        if (IsRepairerInsideDropship(args.User, integrity.Owner))
        {
            _popup.PopupEntity(Loc.GetString("cmu-gunship-repair-must-be-outside"), target, args.User, PopupType.SmallCaution);
            return;
        }

        if (integrity.Comp.Integrity >= integrity.Comp.MaxIntegrity)
        {
            _popup.PopupEntity(Loc.GetString("cmu-gunship-repair-already-complete"), target, args.User, PopupType.SmallCaution);
            return;
        }

        if (!_repairable.UseFuel(args.Used, args.User, integrity.Comp.RepairFuel, true))
            return;

        var ev = new DropshipIntegrityRepairDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, args.User, integrity.Comp.RepairTime, ev, target, target, used: args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cmu-gunship-repair-started"), target, args.User);
    }

    private void OnHullExamined(Entity<DropshipHullComponent> target, ref ExaminedEvent args)
    {
        if (!TryGetDropship(target.Owner, out var integrity))
            return;

        var status = integrity.Comp.Wrecked
            ? "[color=red]WRECKED[/color]"
            : $"{MathF.Ceiling(integrity.Comp.Integrity)}/{MathF.Ceiling(integrity.Comp.MaxIntegrity)}";
        args.PushMarkup(Loc.GetString("cmu-gunship-hull-integrity", ("status", status)));
        PushMalfunctionDiagnostics(integrity, args);
    }

    private void OnRepairDoAfter(Entity<DropshipHullComponent> target, ref DropshipIntegrityRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } tool ||
            !TryGetDropship(target.Owner, out var integrity) || integrity.Comp.Wrecked || integrity.Comp.Crashing ||
            !CanRepairDropship(integrity.Owner) ||
            IsRepairerInsideDropship(args.User, integrity.Owner) ||
            !_repairable.UseFuel(tool, args.User, integrity.Comp.RepairFuel))
        {
            return;
        }

        args.Handled = true;
        integrity.Comp.Integrity = Math.Min(integrity.Comp.MaxIntegrity,
            integrity.Comp.Integrity + integrity.Comp.RepairAmount);
        Dirty(integrity);
        _audio.PlayPvs(integrity.Comp.RepairSound, target);
        _popup.PopupEntity(Loc.GetString("cmu-gunship-repair-restored",
                ("integrity", MathF.Ceiling(integrity.Comp.Integrity)),
                ("maximum", MathF.Ceiling(integrity.Comp.MaxIntegrity))),
            target,
            args.User);
    }

    private bool TryGetDropship(EntityUid structure, out Entity<DropshipIntegrityComponent> dropship)
    {
        dropship = default;
        var xform = Transform(structure);
        if (!xform.Anchored || xform.GridUid is not { } grid ||
            !TryComp(grid, out DropshipIntegrityComponent? integrity))
        {
            return false;
        }

        dropship = (grid, integrity);
        return true;
    }

    private bool IsRepairerInsideDropship(EntityUid user, EntityUid dropship)
    {
        return TryComp(user, out TransformComponent? xform) && xform.GridUid == dropship;
    }

    private bool CanRepairDropship(EntityUid dropship)
    {
        if (!TryComp(dropship, out DropshipIntegrityComponent? integrity))
            return false;

        var state = ResolveFlightState(dropship, integrity);
        if (integrity.FlightState != state)
        {
            integrity.FlightState = state;
            Dirty(dropship, integrity);
        }

        return DropshipRepairEligibility.CanRepair(integrity.FlightState);
    }

    private DropshipFlightState ResolveFlightState(EntityUid dropship, DropshipIntegrityComponent integrity)
    {
        var hovering = TryComp(dropship, out DropshipTacticalHoverComponent? hover);
        var ftlActive = TryComp(dropship, out FTLComponent? ftl) &&
                        ftl.State is FTLState.Starting or FTLState.Travelling or FTLState.Arriving;
        return DropshipRepairEligibility.ResolveState(
            hovering,
            hover?.AltitudeTransitionAt != null,
            ftlActive,
            integrity.Crashing,
            integrity.Wrecked);
    }
}
