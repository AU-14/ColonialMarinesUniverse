using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private DamageableSystem _crashDamage = default!;

    private void BeginCrash(Entity<FighterAircraftComponent> aircraft, FighterAirCombatComponent combat)
    {
        var a = aircraft.Comp;
        if (a.GroundState is FighterGroundState.Crashing or FighterGroundState.Crashed) return;
        combat.CrashTarget = FindCrashSite(aircraft);
        // A jet returning from an off-map holding pattern re-enters beside the crash site.
        combat.CrashStart = a.Battlefield.Contains(a.Position) ? a.Position :
            combat.CrashTarget - FighterFlight.Forward(a.Heading) * 60;
        combat.CrashHeight = Math.Max(150, a.Height);
        a.ForcedRetreat = a.Flying = true;
        combat.Incoming = false;
        combat.IncomingAudio = _audio.Stop(combat.IncomingAudio);
        combat.RecoveryUntil = default;
        combat.CoveredSectors.Clear();
        if (a.GroundEntity is { } hull && TryComp(hull, out FighterGroundComponent? ground))
        {
            FinishVtolEffects((hull, ground), FighterVtolOutcome.Aborted);
            MoveFighterCrew((hull, ground), aircraft, false);
            MoveFighterMounts((hull, ground), aircraft, false);
            _transform.SetParent(hull, EntityUid.Invalid);
            SetGroundState((hull, ground), aircraft, FighterGroundState.Crashing, combat.CrashDuration);
        }
        else
        {
            a.GroundState = FighterGroundState.Crashing;
            a.GroundStateStartedAt = _timing.CurTime;
            a.GroundStateEndsAt = _timing.CurTime + combat.CrashDuration;
        }
        foreach (var seatUid in new[] { a.FrontSeat, a.RearSeat })
        {
            if (seatUid is not { } uid || !TryComp(uid, out FighterSeatComponent? seat)) continue;
            CancelQueuedFire((uid, seat));
            CancelLaserLock((uid, seat));
            ClearLaser((uid, seat));
            seat.EjectConfirmUntil = a.GroundStateEndsAt;
            Dirty(uid, seat);
            if (seat.Occupant is { } crew)
                _popup.PopupEntity(Loc.GetString("cmu-fighter-crash-warning", ("seconds", (int) combat.CrashDuration.TotalSeconds)),
                    crew, crew, PopupType.LargeCaution);
        }
        PlayEffect(aircraft, FighterEffectKind.Hit, duration: 8);
        Dirty(aircraft);
        Dirty(aircraft, combat);
    }

    private Vector2 FindCrashSite(Entity<FighterAircraftComponent> aircraft)
    {
        var a = aircraft.Comp;
        var positions = new List<Vector2>();
        var network = CompOrNull<CMUZLevelMapComponent>(a.TerrainMap)?.NetworkUid;
        var players = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (players.MoveNext(out var uid, out _, out var mob, out var xform))
        {
            if (mob.CurrentState == MobState.Dead || xform.MapUid is not { } map ||
                map != a.TerrainMap && (network == null || CompOrNull<CMUZLevelMapComponent>(map)?.NetworkUid != network)) continue;
            positions.Add(_transform.GetWorldPosition(uid));
        }
        var center = Vector2.Clamp(a.Position, a.Battlefield.BottomLeft + Vector2.One, a.Battlefield.TopRight - Vector2.One);
        var largest = 0;
        foreach (var candidate in positions)
        {
            var count = 0;
            var sum = Vector2.Zero;
            foreach (var other in positions)
                if (Vector2.DistanceSquared(candidate, other) <= 20 * 20) { count++; sum += other; }
            if (count <= largest) continue;
            largest = count;
            center = sum / count;
        }
        // Land beside the busiest area, on an open footprint rather than inside its occupants or roof.
        for (var radius = 8; radius <= 128; radius += 4)
        for (var side = 0; side < 16; side++)
        {
            var point = center + new Angle(side * Math.PI / 8).RotateVec(new Vector2(radius, 0));
            if (!a.Battlefield.Contains(point) || !HasGroundTile(a.TerrainMap, point)) continue;
            if (a.GroundEntity is { } hull && !GroundSiteClear(hull, new EntityCoordinates(a.TerrainMap, point))) continue;
            return point;
        }
        // Small test/admin maps may have no full landing footprint. Still retain crew on an actual ground tile.
        for (var radius = 0; radius <= 128; radius += 2)
        for (var side = 0; side < 16; side++)
        {
            var point = center + new Angle(side * Math.PI / 8).RotateVec(new Vector2(radius, 0));
            if (HasGroundTile(a.TerrainMap, point)) return point;
        }
        return center;
    }

    private void UpdateCrash(Entity<FighterAircraftComponent> aircraft)
    {
        var a = aircraft.Comp;
        if (!TryComp(aircraft, out FighterAirCombatComponent? combat)) return;
        var progress = FighterVtol.Progress(_timing.CurTime, a.GroundStateStartedAt, a.GroundStateEndsAt);
        a.Position = Vector2.Lerp(combat.CrashStart, combat.CrashTarget, progress);
        a.Height = combat.CrashHeight * (1 - progress * progress);
        var direction = combat.CrashTarget - combat.CrashStart;
        if (direction.LengthSquared() > .001f) a.Heading = MathF.Atan2(direction.X, direction.Y);
        a.Bank = MathF.Sin(progress * 24) * (1 + progress * 3);
        if (_timing.CurTime < a.GroundStateEndsAt) return;

        var destination = new EntityCoordinates(a.TerrainMap, combat.CrashTarget);
        // Force-release and relocate before damaging anyone, so the cockpit map cannot trap their bodies.
        foreach (var seatUid in new[] { a.FrontSeat, a.RearSeat })
        {
            if (CompOrNull<FighterSeatComponent>(seatUid)?.Occupant is not { } crew) continue;
            _buckle.Unbuckle(crew, null);
            _transform.SetCoordinates(crew, destination.Offset(new Vector2(3, 0)));
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = 200;
            damage.DamageDict["Heat"] = 80;
            _crashDamage.TryChangeDamage(crew, damage, ignoreResistances: true);
        }
        a.Flying = false;
        a.Height = a.Speed = 0;
        a.GroundState = FighterGroundState.Crashed;
        if (a.GroundEntity is { } hull && TryComp(hull, out FighterGroundComponent? ground))
        {
            _transform.SetCoordinates(hull, destination);
            _transform.SetWorldRotation(hull, new Angle(Math.PI - a.Heading));
            SetGroundState((hull, ground), aircraft, FighterGroundState.Crashed);
            MoveFighterCrew((hull, ground), aircraft, true);
            MoveFighterMounts((hull, ground), aircraft, true);
            _groundPhysics.SetBodyType(hull, BodyType.Static);
        }
        var burst = Spawn("CMUFighterCrashBurst", destination);
        var effects = Comp<FighterEffectsComponent>(burst);
        FighterEffects.Add(effects, FighterEffectKind.Crash, _timing.CurTime,
            direction: FighterFlight.Forward(a.Heading), duration: FighterEffects.HistorySeconds);
        Dirty(burst, effects);
        _fighterAudio.PlayGround(new SoundPathSpecifier("/Audio/CMU14/Fighter/airburst.ogg"), destination, 75, 0);
        ClearFlyby(a);
        Dirty(aircraft);
    }
}
