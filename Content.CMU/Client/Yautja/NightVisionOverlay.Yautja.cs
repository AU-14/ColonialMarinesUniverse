using Content.Shared.CMU14.Yautja;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.NightVision;

public sealed partial class NightVisionOverlay
{
    [Dependency] private IGameTiming _cmuTiming = default!;

    private bool HasCMUComposedPresentation(EntityUid uid)
    {
        if (_entity.TryGetComponent(uid, out CMUYautjaOpticalCloakComponent? cloak) &&
            (cloak.Enabled || (_cmuTiming.CurTime - cloak.StartedAt).TotalSeconds < CMUYautjaPresentation.CloakDuration))
            return true;

        // Wounds persist after the operation stops, including when a new observer enters PVS.
        return _entity.TryGetComponent(uid, out CMUYautjaButcherVisualComponent? butcher) &&
               (butcher.CompletedStage > 0 || butcher.Hunter != null && _cmuTiming.CurTime < butcher.EndsAt);
    }
}
