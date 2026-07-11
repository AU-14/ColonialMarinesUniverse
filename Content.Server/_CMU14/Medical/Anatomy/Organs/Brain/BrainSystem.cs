using Content.Server.Speech.Components;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared._CMU14.Medical.Injuries.Vision;
using Content.Shared._RMC14.Medical.HUD;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared.Drunk;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Anatomy.Organs.Brain;

public sealed partial class BrainSystem : SharedBrainSystem
{
    [Dependency] private CMUTemporaryBlurryVisionSystem _blur = default!;
    [Dependency] private SharedDrunkSystem _drunk = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    private static readonly EntProtoId ForcedSleeping = "StatusEffectForcedSleeping";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateServer(frameTime);
    }

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

    protected override void ApplyDisorientation(
        EntityUid body,
        CMUBrainComponent brain,
        OrganDamageStage stage)
    {
        _blur.AddTemporaryBlurModifier(
            body,
            brain.DisorientationBlurDuration,
            brain.DisorientationBlurStrength);
        _popup.PopupEntity(
            Loc.GetString("cmu-medical-brain-disorientation"),
            body,
            body,
            PopupType.MediumCaution);

        switch (Rng.Next(3))
        {
            case 0:
            {
                var dropItems = new DropHandItemsEvent();
                RaiseLocalEvent(body, ref dropItems);
                break;
            }
            case 1:
                _stun.TryKnockdown(body, brain.DisorientationKnockdownDuration, refresh: false);
                break;
            default:
                _drunk.TryApplyDrunkenness(body, brain.DisorientationDrunkPower, applySlur: false);
                break;
        }
    }
}
