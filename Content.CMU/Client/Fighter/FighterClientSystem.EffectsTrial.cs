using Content.Shared.CMU14.Fighter;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterClientSystem
{
    private readonly HashSet<uint> _effectsCaptured = [];
    private readonly HashSet<EntityUid> _strikeCaptured = [];
    private readonly HashSet<EntityUid> _impactCaptured = [];
    private bool _effectsSecondPass;

    private void CaptureEffectsTrial(FighterAircraftComponent aircraft, FighterSeatComponent seat)
    {
        if (seat.Aircraft is not { } uid || !TryComp(uid, out FighterEffectsComponent? effects)) return;
        var role = seat.Pilot ? "pilot" : "officer";
        foreach (var cue in effects.Cues)
        {
            var age = FighterEffects.Age(effects, cue, _timing.CurTime);
            if (_effectsCaptured.Contains(cue.Sequence) || age < (cue.Kind == FighterEffectKind.Flares ? .65f : .2f) ||
                !FighterEffects.Active(effects, cue, _timing.CurTime)) continue;
            _effectsCaptured.Add(cue.Sequence);
            if (seat.Pilot && cue.Kind is FighterEffectKind.Gau or FighterEffectKind.Rocket or FighterEffectKind.Missile or FighterEffectKind.Flares or FighterEffectKind.Hit)
            {
                _display?.ShowOptics(false);
                _display?.ShowRoute(false);
            }
            CaptureTrial($"effects-{role}-{cue.Kind.ToString().ToLowerInvariant()}");
            Log.Info($"Fighter effect capture: kind={cue.Kind}, index={cue.Index}, age={age:F2}s, sequence={cue.Sequence}.");
        }
        if (seat.Pilot) return;
        var query = EntityQueryEnumerator<FighterStrikeVisualComponent, TransformComponent>();
        while (query.MoveNext(out var visual, out var strike, out var xform))
        {
            if (xform.MapUid == aircraft.TerrainMap && !_impactCaptured.Contains(visual) && strike.Impacts.Count > 0)
            {
                var hit = strike.Impacts[0];
                var age = (_timing.CurTime - strike.ImpactAt - hit.At).TotalSeconds;
                if (age is >= .2 and < 2)
                {
                    _impactCaptured.Add(visual);
                    CaptureTrial($"effects-impact-{strike.Kind.ToString().ToLowerInvariant()}");
                    Log.Info($"Fighter ground impact capture: kind={strike.Kind}, water={hit.Water}, age={age:F2}s, count={strike.Impacts.Count}.");
                }
            }
            if (_strikeCaptured.Contains(visual) || xform.MapUid != aircraft.TerrainMap ||
                !FighterEffects.TryDescent(strike, _timing.CurTime, out var progress, out _) || progress < .3f) continue;
            _strikeCaptured.Add(visual);
            CaptureTrial($"effects-ground-{strike.Kind.ToString().ToLowerInvariant()}");
        }
    }
}
