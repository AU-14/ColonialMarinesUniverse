using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Shared.Audio;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    private void PlayEffect(EntityUid aircraft, FighterEffectKind kind, int index = -1, Vector2 direction = default, float duration = 1)
    {
        var effects = EnsureComp<FighterEffectsComponent>(aircraft);
        FighterEffects.Add(effects, kind, _timing.CurTime, index, direction, duration);
        Dirty(aircraft, effects);
        PlayGroundAirEffect(aircraft, kind);
        // Ground ordnance supplies its own cockpit and impact audio. Physical
        // feedback replaces generic chimes and radar/return beeps.
        var sound = kind switch
        {
            FighterEffectKind.Interceptor => "/Audio/CMU14/Fighter/missile-release.ogg",
            FighterEffectKind.Flares => "/Audio/CMU14/Fighter/countermeasures.ogg",
            FighterEffectKind.Hit => "/Audio/CMU14/Fighter/airframe-hit.ogg",
            FighterEffectKind.Evaded => "/Audio/CMU14/Fighter/airburst.ogg",
            FighterEffectKind.Repaired or FighterEffectKind.Laser or FighterEffectKind.LaserLock => "/Audio/CMU14/Fighter/control-servo.ogg",
            FighterEffectKind.Overheat => "/Audio/CMU14/Fighter/pressure-vent.ogg",
            _ => null,
        };
        if (sound != null) _audio.PlayPvs(new SoundPathSpecifier(sound), aircraft,
            AudioParams.Default.WithVolume(kind == FighterEffectKind.Hit ? -4 : -10));
    }

    private void PlayPhaseEffect(EntityUid uid, FighterPhase phase) => PlayEffect(uid, phase switch
    {
        FighterPhase.Approach => FighterEffectKind.Launch,
        FighterPhase.Pass => FighterEffectKind.Pass,
        FighterPhase.Return => FighterEffectKind.Return,
        _ => FighterEffectKind.Holding,
    }, duration: 2);
}
