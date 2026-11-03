using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.Dropship.Integrity;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.Dropship.Integrity;
using Content.Shared.Database;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Fighter;

public sealed partial class CMUManpadLowAltitudeMissileSystem : EntitySystem
{
    private static readonly EntProtoId LaunchVisual = "CMUFighterManpadLaunch";

    private const float MinFlightDistance = 2f;

    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private FighterAudioSystem _fighterAudio = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private DropshipIntegritySystem _integrity = default!;
    [Dependency] private CMUManpadLowAltitudeLockSystem _lock = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private CMUManpadLowAltitudeTargetingSystem _targeting = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUManpadLowAltitudeComponent, CMUManpadLowAltitudeLaunchRequestEvent>(OnLaunchRequested);
    }

    private void OnLaunchRequested(
        Entity<CMUManpadLowAltitudeComponent> launcher,
        ref CMUManpadLowAltitudeLaunchRequestEvent args)
    {
        var xform = Transform(launcher);
        if (xform.MapUid is not { } map
            || !TryComp(launcher, out CMUManpadLowAltitudeAimingComponent? aiming)
            || aiming.Target is not { } target
            || !_targeting.CanTrack(launcher, target)
            || !TryConsumeMissile(launcher, args.User))
        {
            return;
        }

        var origin = _transform.GetWorldPosition(xform);
        var launchSite = new EntityCoordinates(map, origin + GetMuzzleOffset(args.User));
        var flight = SpawnLaunchVisual(launcher.Comp, args.User, target, launchSite, origin);
        AddMissile(flight, launcher.Comp, args.User, target);

        _fighterAudio.PlayGround(launcher.Comp.LaunchSound, launchSite, launcher.Comp.SoundRange, 0);
        _audio.PlayPvs(launcher.Comp.IncomingSound, target, AudioParams.Default.WithVolume(-6));

        _adminLogger.Add(
            LogType.Action,
            LogImpact.Medium,
            $"{ToPrettyString(args.User):user} launched a missile from {ToPrettyString(launcher):launcher} " +
            $"at {launcher.Comp.TargetKind} {ToPrettyString(target):target}");

        _lock.ResetLock(launcher, aiming);
    }

    private EntityUid SpawnLaunchVisual(
        CMUManpadLowAltitudeComponent launcher,
        EntityUid user,
        EntityUid target,
        EntityCoordinates launchSite,
        Vector2 origin)
    {
        var now = _timing.CurTime;
        var hullCenter = _targeting.GetTargetCenter(target);
        var toTarget = hullCenter - origin;
        var flightSeconds = (float) launcher.MissileFlightTime.TotalSeconds;

        var flight = Spawn(LaunchVisual, launchSite);
        var visual = Comp<FighterManpadVisualComponent>(flight);
        visual.Launched = true;
        visual.Direction = toTarget.LengthSquared() > 0.001f ? Vector2.Normalize(toTarget) : Vector2.UnitY;
        visual.TubeDirection = _transform.GetWorldRotation(user).GetCardinalDir().ToVec();
        visual.StartedAt = now;
        visual.FlightSeconds = flightSeconds;
        if (toTarget.LengthSquared() >= MinFlightDistance * MinFlightDistance)
        {
            visual.Destination = hullCenter;
            visual.Target = target;
        }

        var lifetime = MathF.Max(FighterManpadVisualComponent.Lifetime, flightSeconds + 1f);
        visual.ExpiresAt = now + TimeSpan.FromSeconds(lifetime);
        Comp<TimedDespawnComponent>(flight).Lifetime = lifetime;
        Dirty(flight, visual);
        return flight;
    }

    private void AddMissile(
        EntityUid flight,
        CMUManpadLowAltitudeComponent launcher,
        EntityUid user,
        EntityUid target)
    {
        var missile = AddComp<CMUManpadLowAltitudeMissileComponent>(flight);
        missile.Shooter = user;
        missile.Target = target;
        missile.ImpactAt = _timing.CurTime + launcher.MissileFlightTime;
        missile.Hit = launcher.TargetKind == CMUManpadLowAltitudeTargetKind.Blackfoot
            ? launcher.BlackfootHit
            : launcher.GunshipHit;
        missile.ImpactSound = launcher.ImpactSound;
        missile.SoundRange = launcher.SoundRange;
    }

    private bool TryConsumeMissile(Entity<CMUManpadLowAltitudeComponent> launcher, EntityUid user)
    {
        var take = new TakeAmmoEvent(1, [], Transform(launcher).Coordinates, user);
        RaiseLocalEvent(launcher, take);
        if (take.Ammo.Count == 0)
            return false;

        foreach (var (cartridge, _) in take.Ammo)
        {
            if (cartridge is { } round)
                Del(round);
        }

        _gun.UpdateAmmoCount(launcher);
        return true;
    }

    private Vector2 GetMuzzleOffset(EntityUid user)
    {
        var leftHand = _hands.TryGetHand(user, _hands.GetActiveHand(user), out var hand)
                       && hand.Value.Location == HandLocation.Left;
        return _transform.GetWorldRotation(user).GetCardinalDir() switch
        {
            Direction.South => new Vector2(leftHand ? .16f : -.2f, .35f),
            Direction.North => new Vector2(leftHand ? -.2f : .16f, .35f),
            Direction.East => new Vector2(.48f, .16f),
            _ => new Vector2(-.48f, .16f),
        };
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUManpadLowAltitudeMissileComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var missile, out var xform))
        {
            if (now < missile.ImpactAt)
                continue;

            Impact(missile, xform.MapUid);

            RemCompDeferred<CMUManpadLowAltitudeMissileComponent>(uid);
        }
    }

    private void Impact(CMUManpadLowAltitudeMissileComponent missile, EntityUid? shooterMap)
    {
        if (!_targeting.TryGetKind(missile.Target, out var kind)
            || Transform(missile.Target).MapUid is not { } targetMap)
        {
            return;
        }

        switch (kind)
        {
            case CMUManpadLowAltitudeTargetKind.Gunship:
                DamageGunship(missile);
                break;
            case CMUManpadLowAltitudeTargetKind.Blackfoot:
                DamageBlackfoot(missile);
                break;
        }

        SpawnImpactEffects(missile, targetMap, shooterMap);
    }

    private void DamageGunship(CMUManpadLowAltitudeMissileComponent missile)
    {
        var integrity = Comp<DropshipIntegrityComponent>(missile.Target);
        _integrity.DamageIntegrity((missile.Target, integrity), missile.Hit.Damage);
        LogHit(missile, CMUManpadLowAltitudeTargetKind.Gunship, missile.Hit.Damage);
        _audio.PlayPvs(integrity.ImpactSound, missile.Target);
    }

    private void DamageBlackfoot(CMUManpadLowAltitudeMissileComponent missile)
    {
        var dealt = _hardpoints.CMUDamageVehicleHullExact(missile.Target, missile.Hit.Damage);
        LogHit(missile, CMUManpadLowAltitudeTargetKind.Blackfoot, dealt);
    }

    private void LogHit(CMUManpadLowAltitudeMissileComponent missile, CMUManpadLowAltitudeTargetKind kind, float damage)
    {
        _adminLogger.Add(
            LogType.Damaged,
            LogImpact.High,
            $"{ToPrettyString(missile.Shooter):user}'s missile hit {kind} {ToPrettyString(missile.Target):target} " +
            $"for {damage} hull damage");
    }

    private void SpawnImpactEffects(CMUManpadLowAltitudeMissileComponent missile, EntityUid targetMap, EntityUid? shooterMap)
    {
        var position = _targeting.GetTargetCenter(missile.Target);
        var hit = missile.Hit;
        for (var i = 0; i < hit.EffectCount; i++)
        {
            var offset = new Vector2(
                _random.NextFloat(-hit.EffectSpread, hit.EffectSpread),
                _random.NextFloat(-hit.EffectSpread, hit.EffectSpread));
            Spawn(hit.Effect, new EntityCoordinates(targetMap, position + offset));
        }

        if (missile.ImpactSound is not { } sound)
            return;

        _fighterAudio.PlayGround(sound, new EntityCoordinates(targetMap, position), missile.SoundRange, 0);
        if (shooterMap is { } groundMap)
            _fighterAudio.PlayGround(sound, new EntityCoordinates(groundMap, position), missile.SoundRange, 0);
    }
}
