using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private IRobustRandom _combatRandom = default!;
    private TimeSpan _nextAirCombatUpdate;
    private readonly List<Entity<FighterAircraftComponent, FighterAirCombatComponent, FighterWeaponsComponent>> _combatAircraft = [];

    private void OnCoverSector(FighterCoverSectorEvent ev, EntitySessionEventArgs args) =>
        TryCoverSector(args.SenderSession.AttachedEntity, ev.Sector);

    public bool TryCoverSector(EntityUid? user, int sector)
    {
        if (!TryGetSeat(user, out _, out var aircraft) || aircraft.Comp.ForcedRetreat || aircraft.Comp.GroundState != FighterGroundState.Airborne ||
            !TryComp(aircraft, out FighterAirCombatComponent? combat) ||
            !TryComp(aircraft, out FighterWeaponsComponent? weapons) || string.IsNullOrWhiteSpace(weapons.Faction)) return false;
        var grid = FighterAirCombat.Grid(aircraft.Comp.Battlefield);
        if (sector < 0 || sector >= grid.X * grid.Y) return false;
        if (combat.CoveredSectors.Remove(sector))
        {
            Dirty(aircraft.Owner, combat);
            return true;
        }
        if (combat.CoveredSectors.Count >= combat.MaximumCoveredSectors) return false;
        combat.CoveredSectors.Add(sector);
        combat.CoverageReadyAt = _timing.CurTime + combat.CoverageArmTime;
        Dirty(aircraft.Owner, combat);
        return true;
    }

    public bool TryDeployFlares(EntityUid? user)
    {
        if (!TryGetSeat(user, out var seat, out var aircraft) || !seat.Comp.Pilot ||
            !TryComp(aircraft, out FighterAirCombatComponent? combat) || !combat.Incoming ||
            combat.FlaresUsed || _timing.CurTime >= combat.IncomingAt) return false;
        combat.FlaresUsed = true;
        PlayEffect(aircraft, FighterEffectKind.Flares, duration: 4);
        Dirty(aircraft.Owner, combat);
        return true;
    }

    private bool HasCombatPilot(FighterAircraftComponent aircraft) =>
        aircraft.FrontSeat is { } seat && TryComp(seat, out FighterSeatComponent? pilot) && pilot.Occupant != null;

    private void UpdateAirCombat()
    {
        var now = _timing.CurTime;
        if (now < _nextAirCombatUpdate) return;
        _nextAirCombatUpdate = now + TimeSpan.FromSeconds(.1);
        _combatAircraft.Clear();
        var query = EntityQueryEnumerator<FighterAircraftComponent, FighterAirCombatComponent, FighterWeaponsComponent>();
        while (query.MoveNext(out var uid, out var aircraft, out var combat, out var weapons))
        {
            _combatAircraft.Add((uid, aircraft, combat, weapons));
            if (combat.Incoming && now >= combat.IncomingAt)
            {
                combat.Incoming = false;
                combat.IncomingAudio = _audio.Stop(combat.IncomingAudio);
                var evaded = combat.FlaresUsed && _combatRandom.Prob(Math.Clamp(combat.FlareEvasionChance, 0, 1));
                combat.Result = evaded ? FighterAirResult.Evaded : FighterAirResult.Hit;
                combat.ResultUntil = now + TimeSpan.FromSeconds(4);
                PlayEffect(uid, evaded ? FighterEffectKind.Evaded : FighterEffectKind.Hit,
                    direction: combat.IncomingDirection, duration: evaded ? 2 : 4);
                if (!evaded)
                {
                    aircraft.ForcedRetreat = true;
                    FighterFlight.Abort(aircraft);
                    combat.CoveredSectors.Clear();
                    combat.RecoveryUntil = default;
                    foreach (var seatUid in new[] { aircraft.FrontSeat, aircraft.RearSeat })
                        if (seatUid is { } seat && TryComp(seat, out FighterSeatComponent? occupant))
                        {
                            CancelLaserLock((seat, occupant));
                            ClearLaser((seat, occupant));
                        }
                    Dirty(uid, aircraft);
                }
                Dirty(uid, combat);
                Log.Info($"Fighter interception resolved: {ToPrettyString(uid)}, flares={combat.FlaresUsed}, result={combat.Result}.");
            }
            if (aircraft.ForcedRetreat && aircraft.Phase == FighterPhase.Holding &&
                (aircraft.GroundEntity == null || aircraft.GroundState == FighterGroundState.Grounded))
            {
                if (combat.RecoveryUntil == TimeSpan.Zero)
                {
                    combat.RecoveryUntil = now + combat.RecoveryDuration;
                    Dirty(uid, combat);
                }
                else if (now >= combat.RecoveryUntil)
                {
                    aircraft.ForcedRetreat = false;
                    combat.RecoveryUntil = default;
                    Dirty(uid, aircraft);
                    Dirty(uid, combat);
                    PlayEffect(uid, FighterEffectKind.Repaired, duration: 2);
                }
            }
        }
        foreach (var source in _combatAircraft)
        {
            var (sourceAircraft, sourceCombat, sourceWeapons) = (source.Comp1, source.Comp2, source.Comp3);
            if (sourceAircraft.ForcedRetreat || !FighterFlight.InAttackRun(sourceAircraft) || !HasCombatPilot(sourceAircraft) || sourceCombat.CoveredSectors.Count == 0 ||
                now < sourceCombat.CoverageReadyAt || now < sourceCombat.InterceptReadyAt) continue;
            foreach (var target in _combatAircraft)
            {
                var (targetAircraft, targetCombat) = (target.Comp1, target.Comp2);
                if (source.Owner == target.Owner || sourceAircraft.TerrainMap != targetAircraft.TerrainMap ||
                    !_iff.Hostile(sourceWeapons.Faction, target.Comp3.Faction) ||
                    !targetAircraft.Flying || targetAircraft.ForcedRetreat || targetCombat.Incoming || !HasCombatPilot(targetAircraft)) continue;
                var sector = FighterAirCombat.SectorAt(sourceAircraft.Battlefield, targetAircraft.Position);
                if (sector < 0 || !sourceCombat.CoveredSectors.Contains(sector)) continue;
                sourceCombat.InterceptReadyAt = now + sourceCombat.InterceptCooldown;
                sourceCombat.LastLaunchSector = sector;
                Dirty(source.Owner, sourceCombat);
                BeginInterception(target, sourceAircraft.Position, sourceCombat.MissileFlightTime);
                PlayEffect(source.Owner, FighterEffectKind.Interceptor, sector,
                    direction: -targetCombat.IncomingDirection, duration: (float) sourceCombat.MissileFlightTime.TotalSeconds);
                Log.Info($"Fighter interceptor launched: {ToPrettyString(source.Owner)} -> {ToPrettyString(target.Owner)}, sector {sector}.");
                break;
            }
        }
        UpdateManpads();
    }

    private void BeginInterception(Entity<FighterAircraftComponent, FighterAirCombatComponent, FighterWeaponsComponent> target,
        Vector2 source, TimeSpan flightTime, bool fromGround = false, bool plasma = false)
    {
        var combat = target.Comp2;
        combat.Incoming = true;
        combat.IncomingFromGround = fromGround;
        combat.IncomingPlasma = plasma;
        combat.FlaresUsed = false;
        combat.IncomingAt = _timing.CurTime + flightTime;
        combat.IncomingStartedAt = _timing.CurTime;
        var incoming = source - target.Comp1.Position;
        combat.IncomingDirection = incoming.LengthSquared() > .001f ? Vector2.Normalize(incoming) : Vector2.UnitX;
        combat.IncomingSector = FighterAirCombat.SectorAt(target.Comp1.Battlefield, target.Comp1.Position);
        combat.Result = FighterAirResult.None;
        Dirty(target.Owner, combat);
        combat.IncomingAudio = _audio.PlayPvs(ManpadIncomingSound,
            target.Owner, AudioParams.Default.WithVolume(fromGround ? -7 : 0))?.Entity;
    }
}
