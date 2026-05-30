using System.Numerics;
using Content.Server._RMC14.Atmos;
using Content.Shared._AU14.Fire;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Fire;

/// <summary>
/// Drives the <see cref="FlamabilityComponent"/> fire spread and damage logic.
///
/// Spread tick interval: base 8 s, scaled by <c>Spread</c>. Each tick:
///   1. Deals Heat damage equal to <c>Rate</c> HP/s.
///   2. Rolls per-neighbour ignition chance against every nearby
///      <see cref="FlamabilityComponent"/> entity within (<c>Range</c> × 3) tiles.
///      On successful ignition, tile fires are queued along the path with a
///      per-step delay so they visually propagate rather than popping all at once.
///      A configurable scatter roll may also spawn additional tile fires in a
///      radius around the igniting entity.
///
/// Ground fire (<see cref="TileFireComponent"/>) also spreads to nearby
/// <see cref="FlamabilityComponent"/> entities using the same radius/chance logic.
///
/// Burn duration: 50–340 s (configurable). After burn-out the entity is marked
/// <c>Burnt</c> and cannot be re-ignited.
/// </summary>
public sealed class AU14FireSpreadSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly RMCFlammableSystem _rmc = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private const float BaseSpreadRadiusTiles = 2.9f;
    private static readonly TimeSpan BaseSpreadInterval = TimeSpan.FromSeconds(98);
    private static readonly TimeSpan DamageInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TileFireStepDelay = TimeSpan.FromSeconds(1.3);

    private static readonly ProtoId<DamageTypePrototype> HeatDamageType = "Heat";
    private static readonly EntProtoId TileFireProto = "AU14TileFire";
    private static readonly EntProtoId FireVisualProto = "AU14FireVisualOverlay";
    private const float TileFireSpawnChance = 0.6f;

    private const float DenseFillRadius = 13f;
    private const int DenseFillThreshold = 6;
    private const float DenseFillChance = 0.3f;

    // Chance per-second that a mob standing on the same tile as a burning entity is ignited.
    // Scaled by the burning entity's Chance field so highly-flammable things spread more aggressively.
    private const float MobIgniteChancePerSecond = 0.015f;
    private const float MobIgniteRadius = 1.2f;

    private const float HeldBurnDamagePerSecond = 10f;
    private static readonly TimeSpan HeldDamageInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<EntityUid, TimeSpan> _tileFireNextSpread = new();
    private readonly List<(TimeSpan SpawnAt, MapCoordinates Pos)> _pendingFires = new();
    private readonly HashSet<EntityUid> _pendingVisualSpawns = new();
    private readonly Dictionary<EntityUid, (EntityUid Holder, TimeSpan NextTick)> _heldBurningItems = new();

    // ── EntitySystem overrides ───────────────────────────────────────────────

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TileFireComponent, EntityTerminatingEvent>(OnTileFireTerminating);
        SubscribeLocalEvent<FlamabilityComponent, ExtinguishEvent>(OnFlamabilityExtinguish);
        SubscribeLocalEvent<FlamabilityComponent, InteractHandEvent>(OnFlamabilityInteractHand);
        SubscribeLocalEvent<FlamabilityComponent, GotEquippedHandEvent>(OnFlamabilityPickedUp);
        SubscribeLocalEvent<FlamabilityComponent, GotUnequippedHandEvent>(OnFlamabilityDropped);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Process pending delayed tile fire spawns (iterate backwards for safe removal).
        for (var i = _pendingFires.Count - 1; i >= 0; i--)
        {
            var (spawnAt, pos) = _pendingFires[i];
            if (now < spawnAt)
                continue;
            Spawn(TileFireProto, pos);
            _pendingFires.RemoveAt(i);
        }

        // Collect burning entities first — processing them directly inside the enumerator is unsafe
        // because ApplyFireDamage can trigger DestructibleSystem, which may spawn debris entities
        // that have FlamabilityComponent, modifying the dictionary mid-iteration.
        var burningEntities = new List<EntityUid>();
        var flamCollect = EntityQueryEnumerator<FlamabilityComponent, TransformComponent>();
        while (flamCollect.MoveNext(out var uid, out var flam, out _))
        {
            if (flam.OnFire)
                burningEntities.Add(uid);
        }

        foreach (var uid in burningEntities)
        {
            if (Deleted(uid) || !TryComp<FlamabilityComponent>(uid, out var flam) || !flam.OnFire)
                continue;

            // Burn-out check — extinguish and mark permanently spent.
            if (now >= flam.BurnEndTime)
            {
                if (flam.DestroyAnyway)
                {
                    QueueDel(uid);
                    continue;
                }
                flam.Burnt = true;
                Extinguish(uid, flam);
                continue;
            }

            if (!TryComp<TransformComponent>(uid, out var xform))
                continue;

            if (now >= flam.NextDamageTime)
            {
                flam.NextDamageTime = now + DamageInterval;
                ApplyFireDamage(uid, flam.Rate);
                if (!Deleted(uid))
                    TryIgniteNearbyMobs(uid, xform, flam.Chance);
            }

            if (!Deleted(uid) && now >= flam.NextSpreadTime)
            {
                var spreadDelay = BaseSpreadInterval / Math.Max(flam.Spread, 0.01f);
                flam.NextSpreadTime = now + spreadDelay;
                TrySpreadFrom(uid, xform, flam.Chance, flam.Range, flam, spreadToFlamability: true);
            }
        }

        // Continuous burn damage to whoever is holding a burning entity.
        var heldToRemove = new List<EntityUid>();
        foreach (var (item, (holder, nextTick)) in _heldBurningItems)
        {
            if (Deleted(item) || Deleted(holder)
                || !TryComp<FlamabilityComponent>(item, out var heldFlam) || !heldFlam.OnFire)
            {
                heldToRemove.Add(item);
                continue;
            }
            if (now < nextTick)
                continue;
            _heldBurningItems[item] = (holder, nextTick + HeldDamageInterval);
            var heldDmgAmount = HeldBurnDamagePerSecond;
            if (_inventory.TryGetSlotEntity(holder, "gloves", out _))
                heldDmgAmount *= 0.5f;
            var heldDmg = new DamageSpecifier();
            heldDmg.DamageDict[HeatDamageType] = heldDmgAmount;
            _damage.TryChangeDamage(holder, heldDmg, ignoreResistances: false, interruptsDoAfters: false);
        }
        foreach (var item in heldToRemove)
            _heldBurningItems.Remove(item);

        var tileQuery = EntityQueryEnumerator<TileFireComponent, TransformComponent>();
        while (tileQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (!_tileFireNextSpread.TryGetValue(uid, out var nextSpread))
                nextSpread = TimeSpan.Zero;

            if (now < nextSpread)
                continue;

            _tileFireNextSpread[uid] = now + BaseSpreadInterval;
            TrySpreadFrom(uid, xform, 1f, 1f, null, spreadToFlamability: false);

            // Dense area gap-filling: if many tile fires are nearby, scatter a few more to cover gaps.
            var worldPos = _transform.GetWorldPosition(xform);
            if (CountNearbyTileFires(worldPos, xform.MapID, DenseFillRadius) >= DenseFillThreshold
                && _random.Prob(DenseFillChance))
            {
                QueueScatterFires(worldPos, DenseFillRadius, 1, 2, xform.MapID, now);
            }
        }

        foreach (var pending in _pendingVisualSpawns)
        {
            if (Deleted(pending) || !TryComp<FlamabilityComponent>(pending, out var flam) || !flam.OnFire || flam.FireVisualEntity != null)
                continue;
            flam.FireVisualEntity = Spawn(FireVisualProto, new EntityCoordinates(pending, Vector2.Zero));
        }
        _pendingVisualSpawns.Clear();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Sets an entity on fire via the FlamabilityComponent system.</summary>
    public bool Ignite(EntityUid uid, FlamabilityComponent? flam = null)
    {
        if (!Resolve(uid, ref flam, false))
            return false;

        if (flam.OnFire || flam.Burnt)
            return false;

        var now = _timing.CurTime;
        flam.OnFire = true;
        flam.NextDamageTime = now + DamageInterval;
        flam.NextSpreadTime = now + BaseSpreadInterval / Math.Max(flam.Spread, 0.01f);

        var burnSeconds = flam.BurnDurationMin.TotalSeconds +
                          (flam.BurnDurationMax - flam.BurnDurationMin).TotalSeconds * _random.NextDouble();
        flam.BurnEndTime = now + TimeSpan.FromSeconds(burnSeconds);

        _pendingVisualSpawns.Add(uid);
        Dirty(uid, flam);
        return true;
    }

    /// <summary>Extinguishes an entity burning via the FlamabilityComponent system.</summary>
    public void Extinguish(EntityUid uid, FlamabilityComponent? flam = null)
    {
        if (!Resolve(uid, ref flam, false))
            return;

        if (!flam.OnFire)
            return;

        flam.OnFire = false;
        flam.CurrentPats = 0;
        _heldBurningItems.Remove(uid);
        if (flam.FireVisualEntity is { } visual)
        {
            QueueDel(visual);
            flam.FireVisualEntity = null;
        }
        Dirty(uid, flam);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void OnTileFireTerminating(EntityUid uid, TileFireComponent comp, EntityTerminatingEvent args)
    {
        _tileFireNextSpread.Remove(uid);
    }

    private void OnFlamabilityExtinguish(EntityUid uid, FlamabilityComponent comp, ExtinguishEvent args)
    {
        Extinguish(uid, comp);
    }

    private void OnFlamabilityPickedUp(EntityUid uid, FlamabilityComponent comp, GotEquippedHandEvent args)
    {
        if (!comp.OnFire)
            return;
        _heldBurningItems[uid] = (args.User, _timing.CurTime + HeldDamageInterval);
    }

    private void OnFlamabilityDropped(EntityUid uid, FlamabilityComponent comp, GotUnequippedHandEvent args)
    {
        _heldBurningItems.Remove(uid);
    }

    private void OnFlamabilityInteractHand(EntityUid uid, FlamabilityComponent comp, InteractHandEvent args)
    {
        if (args.Handled || !comp.OnFire)
            return;

        // Burning items can be picked up; let the pickup system handle the interact event.
        if (HasComp<ItemComponent>(uid))
            return;

        var user = args.User;
        if (!TryComp(user, out TileFirePatterComponent? patter))
            return;

        var time = _timing.CurTime;
        if (time < patter.Last + patter.Cooldown)
            return;

        args.Handled = true;
        patter.Last = time;
        Dirty(user, patter);

        comp.CurrentPats++;
        if (comp.CurrentPats >= comp.PatsToExtinguish)
            Extinguish(uid, comp);
        else
            Dirty(uid, comp);

        _audio.PlayPvs(patter.Sound, user, AudioParams.Default.WithVolume(-8).WithVariation(0.05f));
    }

    private void ApplyFireDamage(EntityUid uid, float rate)
    {
        var dmg = new DamageSpecifier();
        dmg.DamageDict[HeatDamageType] = rate * (float) DamageInterval.TotalSeconds;
        _damage.TryChangeDamage(uid, dmg, ignoreResistances: false, interruptsDoAfters: false);
    }

    private void TryIgniteNearbyMobs(EntityUid source, TransformComponent xform, float sourceChance)
    {
        var worldPos = _transform.GetWorldPosition(xform);
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(xform.MapID, worldPos, MobIgniteRadius, nearby, LookupFlags.Dynamic);

        foreach (var candidate in nearby)
        {
            if (candidate == source)
                continue;

            if (!TryComp<FlammableComponent>(candidate, out var flammable))
                continue;

            // Direct proximity heat damage — like standing on a tile fire.
            var dmg = new DamageSpecifier();
            dmg.DamageDict[HeatDamageType] = 5f;
            _damage.TryChangeDamage(candidate, dmg, ignoreResistances: false, interruptsDoAfters: false);

            if (flammable.OnFire)
                continue;

            if (!_random.Prob(sourceChance * MobIgniteChancePerSecond))
                continue;

            _rmc.Ignite((candidate, flammable), 1, 30, null);
        }
    }

    private void TrySpreadFrom(
        EntityUid source,
        TransformComponent xform,
        float sourceChance,
        float range,
        FlamabilityComponent? sourceFlam,
        bool spreadToFlamability)
    {
        var radius = BaseSpreadRadiusTiles * range;
        var worldPos = _transform.GetWorldPosition(xform);
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(
            xform.MapID,
            worldPos,
            radius,
            nearby,
            LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries);

        var now = _timing.CurTime;

        foreach (var candidate in nearby)
        {
            if (candidate == source)
                continue;

            if (!TryComp<FlamabilityComponent>(candidate, out var candidateFlam))
                continue;

            if (candidateFlam.OnFire || candidateFlam.Burnt)
                continue;

            if (!_random.Prob(sourceChance * candidateFlam.Chance))
                continue;

            Ignite(candidate, candidateFlam);

            if (!spreadToFlamability || !TryComp<TransformComponent>(candidate, out var candXform))
                continue;

            var candPos = _transform.GetWorldPosition(candXform);
            QueueIntermediateFires(worldPos, candPos, xform.MapID, now);

            if (sourceFlam != null && _random.Prob(sourceFlam.ScatterFireChance))
                QueueScatterFires(worldPos, sourceFlam.ScatterFireRadius,
                    sourceFlam.ScatterFireMinCount, sourceFlam.ScatterFireMaxCount,
                    xform.MapID, now);
        }
    }

    private int CountNearbyTileFires(Vector2 worldPos, MapId mapId, float radius)
    {
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(mapId, worldPos, radius, nearby, LookupFlags.Static);
        var count = 0;
        foreach (var ent in nearby)
        {
            if (HasComp<TileFireComponent>(ent))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Adds tile fires along the path between two world positions to the pending queue,
    /// each staggered by <see cref="TileFireStepDelay"/> so they appear to spread.
    /// </summary>
    private void QueueIntermediateFires(Vector2 sourcePos, Vector2 targetPos, MapId mapId, TimeSpan now)
    {
        var delta = targetPos - sourcePos;
        var distance = delta.Length();

        if (distance < 1.5f)
            return;

        var steps = (int) Math.Floor(distance);
        for (var i = 1; i < steps; i++)
        {
            if (!_random.Prob(TileFireSpawnChance))
                continue;

            var t = (float) i / steps;
            var rawPos = sourcePos + delta * t;
            var snapped = new Vector2((float) Math.Round(rawPos.X), (float) Math.Round(rawPos.Y));
            _pendingFires.Add((now + TileFireStepDelay * i, new MapCoordinates(snapped, mapId)));
        }
    }

    /// <summary>
    /// Adds randomly placed tile fires within <paramref name="radius"/> tiles of
    /// <paramref name="origin"/> to the pending queue with a small random delay each.
    /// </summary>
    private void QueueScatterFires(Vector2 origin, float radius, int minCount, int maxCount, MapId mapId, TimeSpan now)
    {
        var count = _random.Next(minCount, maxCount + 1);
        for (var i = 0; i < count; i++)
        {
            var angle = _random.NextFloat() * MathF.PI * 2f;
            var dist = _random.NextFloat() * radius;
            var rawPos = origin + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);
            var snapped = new Vector2((float) Math.Round(rawPos.X), (float) Math.Round(rawPos.Y));
            var delay = TimeSpan.FromSeconds(_random.NextDouble() * 1.0);
            _pendingFires.Add((now + delay, new MapCoordinates(snapped, mapId)));
        }
    }
}
