using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Ghost.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Vehicle.Jeep;

/// <summary>
/// Turns a jeep whose hull damage has brought to zero into a burning wreck: its engine bursts into
/// flames, throwing sparks while the fire crackles, then its riders are thrown clear, a loaded crate
/// goes off the back, the jeep's explosive goes off, and the jeep with every part fitted to it is
/// replaced by a wreck, debris, oil and fire.
/// </summary>
public sealed class CMUJeepWreckSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private CMUVehicleCargoSystem _cargo = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedExplosionSystem _explosion = default!;
    [Dependency] private SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private readonly List<Entity<CMUJeepWreckComponent>> _detonating = new();
    private readonly List<(EntityUid Entity, Vector2 Throw, float Speed)> _thrown = new();
    private readonly List<(MapCoordinates Seat, MapCoordinates Exit)> _exits = new();
    private readonly List<EntityUid> _stragglers = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUJeepWreckComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<CMUJeepWreckComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        ent.Comp.Hit = true;
        if (args.Origin is { } origin)
            ent.Comp.LastAttacker = origin;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        _detonating.Clear();

        var query = EntityQueryEnumerator<CMUJeepWreckComponent, HardpointIntegrityComponent>();
        while (query.MoveNext(out var uid, out var wreck, out var hull))
        {
            if (wreck.DetonateAt is { } detonateAt)
            {
                if (now >= detonateAt)
                    _detonating.Add((uid, wreck));
                else if (now >= wreck.NextSpark)
                    Spark((uid, wreck), now);

                continue;
            }

            // Read here, once the tick's hits have settled: a hit wears each part before the hull is summed
            // again. Collisions wear the hull without touching Damageable, so a drop at the same capacity
            // counts as damage too.
            var damaged = wreck.Hit ||
                          (wreck.LastHull > 0f &&
                           hull.Integrity < wreck.LastHull &&
                           MathF.Abs(hull.MaxIntegrity - wreck.LastMaxHull) < 0.01f);

            wreck.Hit = false;
            wreck.LastHull = hull.Integrity;
            wreck.LastMaxHull = hull.MaxIntegrity;

            if (damaged && hull.MaxIntegrity > 0f && hull.Integrity <= 0f)
                Arm((uid, wreck));
        }

        foreach (var jeep in _detonating)
        {
            Detonate(jeep);
        }
    }

    private void Arm(Entity<CMUJeepWreckComponent> ent)
    {
        var now = _timing.CurTime;
        ent.Comp.DetonateAt = now + ent.Comp.Delay;
        ent.Comp.NextSpark = now + ent.Comp.MinSparkDelay;

        // The engine goes with the hull and catches fire: flames from under the hood over its heavy
        // smoke, a flickering glow and the fire's crackle, all gone with the jeep when it goes up.
        if (TryComp(ent.Owner, out CMUJeepComponent? jeep))
        {
            jeep.EngineIntegrity = 0f;
            jeep.Burning = true;
            Dirty(ent.Owner, jeep);
        }

        SpawnAttachedTo(ent.Comp.FireLightPrototype, new EntityCoordinates(ent.Owner, ent.Comp.EngineBay));
        if (ent.Comp.WarningSound is { } crackle)
            _audio.PlayPvs(crackle, ent.Owner, crackle.Params.WithLoop(true));

        _popup.PopupEntity(Loc.GetString(ent.Comp.WarningPopup, ("vehicle", ent.Owner)),
            ent.Owner,
            Filter.Pvs(ent.Owner),
            true,
            PopupType.LargeCaution);
    }

    /// <summary>
    /// Sparks fly from somewhere in the burning engine bay, and now and then something in it gives
    /// way with a bang.
    /// </summary>
    private void Spark(Entity<CMUJeepWreckComponent> ent, TimeSpan now)
    {
        var wreck = ent.Comp;
        var span = (float) (wreck.MaxSparkDelay - wreck.MinSparkDelay).TotalSeconds;
        wreck.NextSpark = now + wreck.MinSparkDelay + TimeSpan.FromSeconds(_random.NextFloat(0f, span));

        var offset = wreck.EngineBay + new Vector2(_random.NextFloat(-0.35f, 0.35f), _random.NextFloat(-0.3f, 0.3f));
        Spawn(wreck.SparkPrototype, new EntityCoordinates(ent.Owner, offset));
        _audio.PlayPvs(_random.Prob(0.35f) ? wreck.PopSound : wreck.SparkSound, ent.Owner);
    }

    private void Detonate(Entity<CMUJeepWreckComponent> ent)
    {
        var (uid, wreck) = ent;
        if (TerminatingOrDeleted(uid))
            return;

        // A jeep carried off somewhere goes up once it is back on the ground.
        var xform = Transform(uid);
        if (xform.MapID == MapId.Nullspace || _container.IsEntityInContainer(uid))
            return;

        var coordinates = xform.Coordinates;
        var center = _transform.GetMapCoordinates(uid, xform);
        var rotation = xform.LocalRotation.RoundToCardinalAngle();
        // Vehicle space has the tailgate at +y.
        var rear = _transform.GetWorldRotation(xform).RotateVec(Vector2.UnitY);

        _thrown.Clear();
        EjectRiders(uid, wreck, center);
        DropCrate(uid, wreck, rear);

        var user = wreck.LastAttacker is { } attacker && !TerminatingOrDeleted(attacker)
            ? attacker
            : (EntityUid?) null;
        _explosion.TriggerExplosive(uid, delete: false, user: user);

        // The seats, clickable parts, overlay and every fitted part (wheels, spare, jerry can, tools,
        // windshield, headlights, gun mount and gun) go with it.
        Del(uid);

        var hulk = Spawn(wreck.WreckPrototype, coordinates);
        _transform.SetLocalRotation(hulk, rotation);

        SpawnDebris(wreck, coordinates);
        SpawnFire(wreck, coordinates, rotation);

        foreach (var (thrown, direction, speed) in _thrown)
        {
            if (!TerminatingOrDeleted(thrown))
                _throwing.TryThrow(thrown, direction, speed, recoil: false, playSound: false, doSpin: false);
        }

        _thrown.Clear();
    }

    /// <summary>
    /// Unbuckles everyone aboard and sets them down beside their seats, to be thrown outward once the jeep
    /// is gone. They are still close enough to take the blast.
    /// </summary>
    private void EjectRiders(EntityUid jeep, CMUJeepWreckComponent wreck, MapCoordinates center)
    {
        _exits.Clear();
        if (TryComp(jeep, out CMUVehicleSeatsComponent? seats))
        {
            foreach (var seat in seats.SeatEntities)
            {
                if (!TryComp(seat, out CMUVehicleSeatComponent? seatComp))
                    continue;

                var exit = FindExit(jeep, seatComp.Exit);
                _exits.Add((_transform.GetMapCoordinates(seat), exit));

                if (!TryComp(seat, out StrapComponent? strap))
                    continue;

                foreach (var rider in strap.BuckledEntities.ToArray())
                {
                    if (TryComp(rider, out BuckleComponent? buckle))
                        _buckle.Unbuckle((rider, buckle), null);

                    Eject(wreck, rider, exit, center);
                }
            }
        }

        // Unbuckling leaves a rider on the jeep until the seat system sets them down, and anything still
        // on the jeep is deleted with it. Catch anyone who got up this tick.
        _stragglers.Clear();
        var children = Transform(jeep).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<MobStateComponent>(child) && !HasComp<GhostComponent>(child))
                _stragglers.Add(child);
        }

        foreach (var straggler in _stragglers)
        {
            var position = _transform.GetMapCoordinates(straggler);
            var exit = position;
            var nearest = float.MaxValue;
            foreach (var (seat, seatExit) in _exits)
            {
                var distance = (seat.Position - position.Position).LengthSquared();
                if (distance >= nearest)
                    continue;

                nearest = distance;
                exit = seatExit;
            }

            Eject(wreck, straggler, exit, center);
        }
    }

    /// <summary>
    /// The seat's own exit, or the first clear spot on the other side, behind or in front, so nobody is
    /// set down inside a wall the jeep was parked against. The front spot clears the burning engine bay.
    /// </summary>
    private MapCoordinates FindExit(EntityUid jeep, Vector2 exit)
    {
        var candidates = new[] { exit, new Vector2(-exit.X, exit.Y), new Vector2(0f, 1.6f), new Vector2(0f, -1.9f) };
        foreach (var candidate in candidates)
        {
            var coordinates = new EntityCoordinates(jeep, candidate);
            if (_turf.TryGetTileRef(coordinates, out var tile) &&
                !tile.Value.Tile.IsEmpty &&
                !_turf.IsTileBlocked(tile.Value.GridUid,
                    tile.Value.GridIndices,
                    CollisionGroup.Impassable,
                    ignore: uid => uid == jeep))
            {
                return _transform.ToMapCoordinates(coordinates);
            }
        }

        return _transform.ToMapCoordinates(new EntityCoordinates(jeep, exit));
    }

    private void Eject(CMUJeepWreckComponent wreck, EntityUid rider, MapCoordinates exit, MapCoordinates center)
    {
        // Anyone left on the jeep would be deleted with it.
        if (exit.MapId != center.MapId)
        {
            _transform.AttachToGridOrMap(rider);
            return;
        }

        _transform.SetMapCoordinates(rider, exit);

        var direction = exit.Position - center.Position;
        if (direction.LengthSquared() < 0.0001f)
            direction = _random.NextAngle().ToWorldVec();

        _thrown.Add((rider, Vector2.Normalize(direction) * wreck.EjectDistance, wreck.EjectSpeed));
    }

    /// <summary>
    /// A loaded crate is thrown off the back rather than lost with the jeep. Where the bed's drop spot is
    /// blocked, the jeep's removal sets it down where it rode.
    /// </summary>
    private void DropCrate(EntityUid jeep, CMUJeepWreckComponent wreck, Vector2 rear)
    {
        if (!TryComp(jeep, out CMUVehicleCargoComponent? cargo) || cargo.Crate is not { } crate)
            return;

        _cargo.Unload((jeep, cargo), crate);
        _thrown.Add((crate, rear * wreck.CrateThrowDistance, wreck.CrateThrowSpeed));
    }

    private void SpawnDebris(CMUJeepWreckComponent wreck, EntityCoordinates coordinates)
    {
        if (wreck.DebrisPrototypes.Count > 0)
        {
            var count = _random.Next(wreck.MinDebris, wreck.MaxDebris + 1);
            var first = _random.Next(wreck.DebrisPrototypes.Count);
            for (var i = 0; i < count; i++)
            {
                var debris = Spawn(wreck.DebrisPrototypes[(first + i) % wreck.DebrisPrototypes.Count], coordinates);
                _throwing.TryThrow(debris,
                    _random.NextAngle().ToWorldVec(),
                    baseThrowSpeed: _random.NextFloat(4f, 7f),
                    doSpin: true,
                    compensateFriction: true);
            }
        }

        for (var i = 0; i < wreck.OilSplatters; i++)
        {
            var offset = _random.NextAngle().ToWorldVec() * _random.NextFloat(0.25f, 1.5f);
            Spawn(wreck.OilSpawnerPrototype, coordinates.Offset(offset));
        }
    }

    /// <summary>
    /// Sets the wreck's middle and engine bay burning. The rest of a diamond would light the tiles beside
    /// and behind the seats, where the crew and the crate are thrown out.
    /// </summary>
    private void SpawnFire(CMUJeepWreckComponent wreck, EntityCoordinates coordinates, Angle rotation)
    {
        var center = coordinates.SnapToGrid(EntityManager);
        var centerMap = _transform.ToMapCoordinates(center);
        var rear = _transform.ToMapCoordinates(center.Offset(rotation.RotateVec(Vector2.UnitY))).Position -
                   centerMap.Position;

        _flammable.SpawnFireDiamond(wreck.FirePrototype,
            center,
            1,
            duration: wreck.FireDuration,
            canSpawn: target =>
            {
                var map = _transform.ToMapCoordinates(target);
                if (map.MapId != centerMap.MapId)
                    return false;

                var offset = map.Position - centerMap.Position;
                return MathF.Abs(offset.X * rear.Y - offset.Y * rear.X) < 0.5f &&
                       Vector2.Dot(offset, rear) < 0.5f;
            });
    }
}
