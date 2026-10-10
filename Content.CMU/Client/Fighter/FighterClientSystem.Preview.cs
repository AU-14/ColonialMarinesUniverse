using Content.Shared.CMU14.Fighter;
using Robust.Shared.Configuration;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterClientSystem
{
    private TimeSpan _nextPreviewFrame;
    private int _previewFrame;

    // Opt-in capture of real gameplay frames for the PR demonstration.
    private void CaptureWeaponPreview(FighterAircraftComponent aircraft, FighterSeatComponent seat)
    {
        if (!_configuration.GetCVar(FighterPreviewCVars.Record) || _timing.RealTime < _nextPreviewFrame ||
            seat.Aircraft is not { } uid || !TryComp(uid, out FighterEffectsComponent? effects))
            return;

        foreach (var cue in effects.Cues)
        {
            var age = FighterEffects.Age(effects, cue, _timing.CurTime);
            if (cue.Kind is not (FighterEffectKind.Gau or FighterEffectKind.Missile) || age < 0 || age > 7)
                continue;
            _nextPreviewFrame = _timing.RealTime + TimeSpan.FromMilliseconds(100);
            CaptureTrial($"preview-{(seat.Pilot ? "pilot" : "officer")}-{cue.Kind.ToString().ToLowerInvariant()}-{_previewFrame++:D5}");
            return;
        }
    }
}

[CVarDefs]
public sealed class FighterPreviewCVars
{
    public static readonly CVarDef<bool> Record = CVarDef.Create("fighter.record_preview", false, CVar.CLIENTONLY);
}
