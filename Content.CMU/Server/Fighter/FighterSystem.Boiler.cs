using System.Numerics;
using Content.Server.CMU14.Dropship.Integrity;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.CMU14.Dropship.Integrity;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Spawners;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private FighterBoilerAirDefenseSystem _boilerAirDefense = default!;
    [Dependency] private DropshipIntegritySystem _boilerGunshipIntegrity = default!;
    [Dependency] private XenoPlasmaSystem _boilerPlasma = default!;
    private readonly List<EntityUid> _boilerTargets = [];
    private static readonly TimeSpan BoilerFlightTime = TimeSpan.FromSeconds(5);
    private static readonly SoundSpecifier BoilerPlasmaSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/blobattack.ogg");
    private static readonly SoundSpecifier BoilerPrepareSound = new SoundCollectionSpecifier("XenoDrool");

    private void CancelBoilerWindup(FighterBoilerAirDefenseComponent boiler)
    {
        boiler.WindupAudio = _audio.Stop(boiler.WindupAudio);
        if (boiler.WindupVisual is { } visual && !TerminatingOrDeleted(visual)) QueueDel(visual);
        boiler.WindupVisual = null;
        boiler.Target = null;
        boiler.LaunchAt = default;
    }

    private void OnBoilerStopped(Entity<FighterBoilerAirDefenseComponent> ent, ref FighterBoilerAirDefenseStoppedEvent args) => CancelBoilerWindup(ent.Comp);

    private void UpdateBoilerAirDefense(bool updateRadar)
    {
        var now = _timing.CurTime;
        var shots = EntityQueryEnumerator<CMUBoilerGunshipShotComponent>();
        while (shots.MoveNext(out var shot, out var pending))
        {
            if (now < pending.ImpactAt)
                continue;

            if (TryGetBoilerGunship(pending.Target, pending.Terrain, out var hull))
                _boilerGunshipIntegrity.DamageIntegrity((pending.Target, hull), pending.Damage);
            QueueDel(shot);
        }

        _boilerTargets.Clear();
        foreach (var aircraft in _combatAircraft)
            _boilerTargets.Add(aircraft.Owner);
        var gunships = EntityQueryEnumerator<DropshipIntegrityComponent, DropshipTacticalHoverComponent>();
        while (gunships.MoveNext(out var ship, out _, out _))
            _boilerTargets.Add(ship);

        var query = EntityQueryEnumerator<FighterBoilerAirDefenseComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var boiler, out var xform))
        {
            if (!boiler.Watching || !_boilerAirDefense.CanWatch(uid) || xform.MapUid is not { } terrain)
            {
                _boilerAirDefense.SetWatching((uid, boiler), false);
                if (boiler.Target != null) CancelBoilerWindup(boiler);
                continue;
            }
            while (TryComp(terrain, out CMUZLevelMapComponent? level) && level.Depth > 0 && level.MapBelow is { } below)
                terrain = below;
            var position = _transform.GetWorldPosition(uid);
            var loaded = TryComp(uid, out XenoPlasmaComponent? plasma) && plasma.Plasma >= boiler.PlasmaCost;
            if (updateRadar)
                SendAirspaceRadar(uid, uid, terrain, position, true, loaded, boiler.ReadyAt, boiler.LaunchAt, true, boiler.Target, organic: true);
            _areas.CanOrbitalBombard(new EntityCoordinates(xform.MapUid.Value, position), out var roofed);
            if (now < boiler.ReadyAt || !loaded || roofed)
            {
                if (boiler.Target != null) CancelBoilerWindup(boiler);
                continue;
            }
            var retained = false;
            foreach (var target in _boilerTargets)
            {
                if (boiler.Target is { } locked && locked != target ||
                    !CanAcquireBoilerTarget(target, terrain, position)) continue;
                if (boiler.Target == null)
                {
                    boiler.Target = target;
                    boiler.StartedPosition = position;
                    boiler.LaunchAt = now + boiler.AcquisitionTime;
                    var visual = Spawn("CMUFighterBoilerPlasma", xform.Coordinates);
                    boiler.WindupVisual = visual;
                    var effect = Comp<FighterPlasmaVisualComponent>(visual);
                    effect.StartedAt = now;
                    effect.ExpiresAt = boiler.LaunchAt + TimeSpan.FromSeconds(3);
                    Dirty(visual, effect);
                    boiler.WindupAudio = _audio.PlayPvs(BoilerPrepareSound, uid)?.Entity;
                }
                if (Vector2.DistanceSquared(position, boiler.StartedPosition) > .2f * .2f ||
                    boiler.WindupVisual is not { } launch || !TryComp(launch, out FighterPlasmaVisualComponent? bolt) ||
                    Transform(launch).MapUid != xform.MapUid) break;
                retained = true;
                if (now < boiler.LaunchAt) break;
                if (!_boilerPlasma.TryRemovePlasma(uid, boiler.PlasmaCost))
                {
                    retained = false;
                    break;
                }
                boiler.ReadyAt = now + boiler.Cooldown;
                if (TryComp(target, out FighterAircraftComponent? aircraft) &&
                    TryComp(target, out FighterAirCombatComponent? combat) &&
                    TryComp(target, out FighterWeaponsComponent? weapons))
                {
                    BeginInterception((target, aircraft, combat, weapons), position, BoilerFlightTime, fromGround: true, plasma: true);
                    bolt.Direction = -combat.IncomingDirection;
                }
                else
                {
                    var shot = AddComp<CMUBoilerGunshipShotComponent>(launch);
                    shot.Target = target;
                    shot.Terrain = terrain;
                    shot.Damage = boiler.GunshipDamage;
                    shot.ImpactAt = now + BoilerFlightTime;
                    var direction = _transform.GetWorldPosition(target) - position;
                    bolt.Direction = direction.LengthSquared() > 0f ? Vector2.Normalize(direction) : Vector2.UnitY;
                }
                bolt.Launched = true;
                bolt.StartedAt = now;
                bolt.ExpiresAt = now + TimeSpan.FromSeconds(3);
                // Keep the existing ground visual, but retain gunship shots until their damage resolves.
                Comp<TimedDespawnComponent>(launch).Lifetime = HasComp<CMUBoilerGunshipShotComponent>(launch)
                    ? (float) BoilerFlightTime.TotalSeconds + 1
                    : 3;
                Dirty(launch, bolt);
                boiler.WindupVisual = null;
                CancelBoilerWindup(boiler);
                _fighterAudio.PlayGround(BoilerPlasmaSound, xform.Coordinates, 45, 0);
                break;
            }
            if (!retained && boiler.Target != null) CancelBoilerWindup(boiler);
        }
    }

    private bool CanAcquireBoilerTarget(EntityUid target, EntityUid terrain, Vector2 position)
    {
        if (TryComp(target, out FighterAircraftComponent? aircraft) &&
            TryComp(target, out FighterAirCombatComponent? combat) &&
            TryComp(target, out FighterWeaponsComponent? weapons))
            return InGroundWeaponSector(terrain, position, (target, aircraft, combat, weapons));

        if (!TryGetBoilerGunship(target, terrain, out _) || !TryGetAirspaceChart(terrain, out var chart))
            return false;

        var sector = FighterAirCombat.SectorAt(chart.Bounds, position);
        return sector >= 0 && sector == FighterAirCombat.SectorAt(chart.Bounds, _transform.GetWorldPosition(target));
    }

    private bool TryGetBoilerGunship(EntityUid target, EntityUid terrain, out DropshipIntegrityComponent hull)
    {
        hull = default!;
        if (TerminatingOrDeleted(target) ||
            !TryComp(target, out DropshipTacticalHoverComponent? hover) || hover.GroundMap is not { } ground ||
            !TryComp(target, out DropshipIntegrityComponent? integrity) || integrity.Crashing || integrity.Wrecked ||
            TryComp(target, out FTLComponent? ftl) && ftl.State is FTLState.Starting or FTLState.Travelling or FTLState.Arriving)
            return false;

        while (TryComp(ground, out CMUZLevelMapComponent? level) && level.Depth > 0 && level.MapBelow is { } below)
            ground = below;
        if (ground != terrain)
            return false;

        hull = integrity;
        return true;
    }
}
