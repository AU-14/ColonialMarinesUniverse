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
    private readonly Dictionary<EntityUid, EntityUid> _groundEngines = [];

    private void OnAircraftShutdown(Entity<FighterAircraftComponent> aircraft, ref ComponentShutdown args)
    {
        StopGroundEngine(aircraft);
        ClearFlyby(aircraft.Comp);
    }

    private void StopGroundEngine(EntityUid aircraft)
    {
        if (_groundEngines.Remove(aircraft, out var sound)) _audio.Stop(sound);
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
            if ((!_groundEngines.TryGetValue(aircraft, out var stream) || TerminatingOrDeleted(stream)) &&
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/CMU14/Fighter/jet-idle.ogg"), ground,
                    FighterAudioSystem.Exterior(18, -12).WithReferenceDistance(2.5f).WithLoop(true)) is { } idle)
            {
                // This is the crew's own engine, inside the airframe. Surface
                // occlusion rays through its nested seats can yield NaN. Keep
                // distance attenuation without muffling the engine through itself.
                idle.Component.Flags |= AudioFlags.NoOcclusion;
                Dirty(idle.Entity, idle.Component);
                _groundEngines[aircraft] = idle.Entity;
            }
        }
        else StopGroundEngine(aircraft);

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
            uid = Spawn("CMUFighterFlyby", new EntityCoordinates(a.TerrainMap, a.Position));
            a.Flyby = uid;
            // One tiny presentation entity per aircraft. Without this, the roar
            // cuts in only when its source enters the ordinary sprite PVS radius.
            _fighterPvs.AddGlobalOverride(uid);
            a.FlybyAudio = _audio.PlayPvs(new SoundPathSpecifier("/Audio/CMU14/Fighter/jet-exterior.ogg"), uid,
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
        var path = kind switch
        {
            FighterEffectKind.Pass => a.Height < FighterFlight.CloudBase
                ? "/Audio/CMU14/Fighter/jet-pass.ogg"
                : "/Audio/CMU14/Fighter/jet-pass-high.ogg",
            FighterEffectKind.Interceptor => "/Audio/CMU14/Fighter/missile-release.ogg",
            FighterEffectKind.Flares => "/Audio/CMU14/Fighter/countermeasures.ogg",
            FighterEffectKind.Hit or FighterEffectKind.Evaded => "/Audio/CMU14/Fighter/airburst.ogg",
            _ => null,
        };
        if (path == null) return;
        var volume = kind == FighterEffectKind.Pass && a.Height >= FighterFlight.CloudBase ? -10 : -5;
        _fighterAudio.PlayGround(new SoundPathSpecifier(path), Transform(flyby).Coordinates, 65, volume);
        if (kind is not (FighterEffectKind.Flares or FighterEffectKind.Hit or FighterEffectKind.Evaded)) return;
        // Leave the burst at its release point while the aircraft continues.
        var burst = Spawn("CMUFighterAirBurst", Transform(flyby).Coordinates);
        var effects = Comp<FighterEffectsComponent>(burst);
        FighterEffects.Add(effects, kind, _timing.CurTime, duration: 3);
        Dirty(burst, effects);
    }
}
