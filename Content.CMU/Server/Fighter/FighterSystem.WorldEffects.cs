using Content.Shared.CMU14.Fighter;
using Robust.Server.GameStates;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private FighterAudioSystem _fighterAudio = default!;
    [Dependency] private PvsOverrideSystem _fighterPvs = default!;
    private readonly Dictionary<EntityUid, (EntityUid Audio, bool Taxi)> _groundEngines = [];

    private void OnAircraftShutdown(Entity<FighterAircraftComponent> aircraft, ref ComponentShutdown args)
    {
        _aircraftNetworkState.Remove(aircraft);
        StopGroundEngine(aircraft);
        ClearFlyby(aircraft.Comp);
    }

    private void StopGroundEngine(EntityUid aircraft)
    {
        if (_groundEngines.Remove(aircraft, out var sound)) _audio.Stop(sound.Audio);
    }

    private void ClearFlyby(FighterAircraftComponent aircraft)
    {
        aircraft.FlybyAudio = _audio.Stop(aircraft.FlybyAudio);
        if (aircraft.Flyby is { } effect && !TerminatingOrDeleted(effect)) QueueDel(effect);
        aircraft.Flyby = null;
    }

    private void UpdateWorldEffects(Entity<FighterAircraftComponent> aircraft)
    {
        var a = aircraft.Comp;
        if (a.GroundEntity is { } ground && !TerminatingOrDeleted(ground) && TryComp(ground, out FighterGroundComponent? grounded) &&
            grounded.State == FighterGroundState.Grounded &&
                (CompOrNull<FighterSeatComponent>(a.FrontSeat)?.Occupant != null ||
                 CompOrNull<FighterSeatComponent>(a.RearSeat)?.Occupant != null))
        {
            var definition = Airframe(aircraft);
            if (!grounded.EngineRunning)
            {
                grounded.EngineRunning = true;
                _fighterAudio.PlayGround(FighterAudioSystem.Engine(definition, "startup"), Transform(ground).Coordinates, 30, -8);
            }
            var taxi = grounded.TaxiInput != FighterInput.None;
            if (_groundEngines.TryGetValue(aircraft, out var current) && current.Taxi != taxi)
                StopGroundEngine(aircraft);
            if ((!_groundEngines.TryGetValue(aircraft, out var stream) || TerminatingOrDeleted(stream.Audio)) &&
                _audio.PlayPvs(FighterAudioSystem.Engine(definition, taxi ? "taxi" : "idle"), ground,
                    FighterAudioSystem.Exterior(18, -12).WithReferenceDistance(2.5f).WithLoop(true)) is { } idle)
            {
                // This is the crew's own engine, inside the airframe. Surface
                // occlusion rays through its nested seats can yield NaN. Keep
                // distance attenuation without muffling the engine through itself.
                idle.Component.Flags |= AudioFlags.NoOcclusion;
                Dirty(idle.Entity, idle.Component);
                _groundEngines[aircraft] = (idle.Entity, taxi);
            }
        }
        else
        {
            StopGroundEngine(aircraft);
            if (a.GroundEntity is { } hull && TryComp(hull, out FighterGroundComponent? stopped) &&
                stopped.State == FighterGroundState.Grounded && stopped.EngineRunning)
            {
                stopped.EngineRunning = false;
                _fighterAudio.PlayGround(FighterAudioSystem.Engine(Airframe(aircraft), "shutdown"), Transform(hull).Coordinates, 30, -8);
            }
        }

        var airborne = FighterFlight.InAirspace(a);
        // Holding still has an engine inside the cockpit, but no jet over the battlefield.
        if (a.Hull is { } cabin) _ambient.SetAmbience(cabin, airborne);
        if (!airborne || !a.Flying || !a.Battlefield.Enlarged(40).Contains(a.Position) || TerminatingOrDeleted(a.TerrainMap))
        {
            ClearFlyby(a);
            return;
        }
        if (a.Flyby is not { } uid || TerminatingOrDeleted(uid))
        {
            var definition = Airframe(aircraft);
            uid = Spawn(definition.Flyby, new EntityCoordinates(a.TerrainMap, a.Position));
            SetAirframe(uid, definition.ID);
            a.Flyby = uid;
            // One tiny presentation entity per aircraft. Without this, the roar
            // cuts in only when its source enters the ordinary sprite PVS radius.
            _fighterPvs.AddGlobalOverride(uid);
            a.FlybyAudio = _audio.PlayPvs(FighterAudioSystem.Engine(definition, "exterior"), uid,
                FighterAudioSystem.Exterior(75, -2).WithLoop(true))?.Entity;
            if (a.FlybyAudio is { } audio) _fighterPvs.AddGlobalOverride(audio);
        }
        _transform.SetCoordinates(uid, new EntityCoordinates(a.TerrainMap, a.Position));
        _transform.SetWorldRotation(uid, new Angle(-a.Heading));
        var flyby = Comp<FighterFlybyComponent>(uid);
        flyby.Height = a.Height;
        flyby.Crashing = a.GroundState == FighterGroundState.Crashing;
        Dirty(uid, flyby);
        var altitude = Math.Clamp((a.Height - FighterFlight.MinimumHeight) /
            (FighterFlight.MaximumHeight - FighterFlight.MinimumHeight), 0, 1);
        _audio.SetVolume(a.FlybyAudio, -2 - altitude * 12);
    }

    private void PlayGroundAirEffect(EntityUid aircraft, FighterEffectKind kind)
    {
        if (!TryComp(aircraft, out FighterAircraftComponent? a) || a.Flyby is not { } flyby || TerminatingOrDeleted(flyby)) return;
        SoundSpecifier? sound = kind switch
        {
            FighterEffectKind.Pass => FighterAudioSystem.Engine(Airframe(aircraft), a.Height < FighterFlight.CloudBase ? "pass" : "pass-high"),
            FighterEffectKind.Interceptor => new SoundPathSpecifier("/Audio/CMU14/Fighter/missile-release.ogg"),
            FighterEffectKind.Flares => new SoundPathSpecifier("/Audio/CMU14/Fighter/countermeasures.ogg"),
            FighterEffectKind.Hit or FighterEffectKind.Evaded => new SoundPathSpecifier("/Audio/CMU14/Fighter/airburst.ogg"),
            _ => null,
        };
        if (sound == null) return;
        var volume = kind == FighterEffectKind.Pass && a.Height >= FighterFlight.CloudBase ? -10 : -5;
        _fighterAudio.PlayGround(sound, Transform(flyby).Coordinates, 65, volume);
        if (kind is not (FighterEffectKind.Flares or FighterEffectKind.Hit or FighterEffectKind.Evaded)) return;
        // Leave the burst at its release point while the aircraft continues.
        var burst = Spawn("CMUFighterAirBurst", Transform(flyby).Coordinates);
        var effects = Comp<FighterEffectsComponent>(burst);
        FighterEffects.Add(effects, kind, _timing.CurTime, duration: 3);
        Dirty(burst, effects);
    }
}
