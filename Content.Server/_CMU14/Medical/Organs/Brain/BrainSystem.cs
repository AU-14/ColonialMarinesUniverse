using Content.Server.Speech.Components;
using Content.Shared._CMU14.Medical.Organs.Brain;
using Content.Shared._RMC14.Medical.HUD;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Organs.Brain;

public sealed class BrainSystem : SharedBrainSystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private static readonly EntProtoId ForcedSleeping = "StatusEffectForcedSleeping";

    /// <summary>
    ///     Server-only because mob-state mutation cannot run on a predicted
    ///     client tick.
    /// </summary>
    protected override void ApplyPermadeath(EntityUid body)
    {
        Status.TrySetStatusEffectDuration(body, ForcedSleeping, duration: null);

        if (TryComp<HolocardStateComponent>(body, out var holocard)
            && holocard.HolocardStatus != HolocardStatus.Permadead)
        {
            holocard.HolocardStatus = HolocardStatus.Permadead;
            Dirty(body, holocard);
        }

        if (TryComp<MobStateComponent>(body, out var mobState)
            && mobState.CurrentState != MobState.Dead)
        {
            _mobState.ChangeMobState(body, MobState.Dead, mobState);
        }
    }

    protected override void ApplySlurredSpeech(EntityUid body)
        => EnsureComp<SlurredAccentComponent>(body);

    protected override void ClearSlurredSpeech(EntityUid body)
        => RemComp<SlurredAccentComponent>(body);
}
