using System.Numerics;
using Content.Server._RMC14.Explosion;
using Content.Server.CMU14.Blackfoot;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Blackfoot;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Blackfoot;

public sealed partial class CMUBlackfootDestructionSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private RMCExplosionSystem _explosion = default!;
    [Dependency] private BlackfootFlightSystem _flight = default!;
    [Dependency] private SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCCameraShakeSystem _shake = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleSystem _vehicles = default!;

    public bool IsFalling(EntityUid blackfoot)
    {
        return HasComp<CMUBlackfootFallingComponent>(blackfoot);
    }

    /// <summary>False when the Blackfoot has no destruction component, so the caller keeps its own crash.</summary>
    public bool TryBeginFall(EntityUid blackfoot)
    {
        if (!TryComp(blackfoot, out CMUBlackfootDestructionComponent? config))
            return false;

        if (IsFalling(blackfoot))
            return true;

        var falling = AddComp<CMUBlackfootFallingComponent>(blackfoot);
        falling.CrashAt = _timing.CurTime + config.FallDelay;

        // the airframe is gone for good, whatever is left of its parts
        _hardpoints.CMUDestroyBeyondRepair(blackfoot);

        // the crew sits in the interior, so only the pilot is warned
        if (TryComp(blackfoot, out VehicleComponent? vehicle) && vehicle.Operator is { } pilot)
            _popup.PopupEntity(Loc.GetString("cmu-blackfoot-critical-hull-failure"), pilot, pilot, PopupType.LargeCaution);

        return true;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<
            CMUBlackfootFallingComponent,
            CMUBlackfootDestructionComponent,
            BlackfootFlightComponent>();
        while (query.MoveNext(out var uid, out var falling, out var config, out var flight))
        {
            falling.AngularVelocityDegrees += config.SpinAcceleration * frameTime;
            _transform.SetWorldRotation(uid,
                _transform.GetWorldRotation(uid) + Angle.FromDegrees(falling.AngularVelocityDegrees * frameTime));

            if (now >= falling.CrashAt)
                Crash((uid, flight), config);
        }
    }

    private void Crash(Entity<BlackfootFlightComponent> blackfoot, CMUBlackfootDestructionComponent config)
    {
        RemCompDeferred<CMUBlackfootFallingComponent>(blackfoot);

        // moves the wreck to the ground level first
        _flight.Crash(blackfoot);

        var coordinates = _transform.GetMapCoordinates(blackfoot);
        if (Transform(blackfoot).MapUid is not { } map)
            return;

        _explosion.QueueExplosion(
            coordinates,
            config.ExplosionType,
            config.ExplosionTotalIntensity,
            config.ExplosionSlope,
            config.ExplosionMaxTileIntensity,
            blackfoot,
            canCreateVacuum: false);

        _flammable.SpawnFireDiamond(config.FireProto, _transform.ToCoordinates(coordinates), config.FireRange);

        for (var i = 0; i < config.CrashEffectCount; i++)
        {
            SpawnEffect(config.CrashEffect, map, coordinates.Position, config.CrashEffectSpread);
        }

        _audio.PlayPvs(config.CrashSound, blackfoot, AudioParams.Default.WithMaxDistance(config.SoundRange));
        _shake.ShakeCamera(Filter.Empty().AddInRange(coordinates, config.CameraShakeRange), 3, 1);

        _vehicles.EjectOccupants(blackfoot, config.EjectionDistance, config.EjectionSpeed, cookOff: true);
    }

    private void SpawnEffect(EntProtoId prototype, EntityUid map, Vector2 center, float spread)
    {
        var offset = new Vector2(_random.NextFloat(-spread, spread), _random.NextFloat(-spread, spread));
        Spawn(prototype, new EntityCoordinates(map, center + offset));
    }
}
