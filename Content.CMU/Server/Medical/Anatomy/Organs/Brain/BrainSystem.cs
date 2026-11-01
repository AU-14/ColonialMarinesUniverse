using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Injuries.Vision;
using Content.Shared._RMC14.Medical.HUD.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Medical.Anatomy.Organs.Brain;

public sealed partial class BrainSystem : SharedBrainSystem
{
    [Dependency] private CMUTemporaryBlurryVisionSystem _blur = default!;
    [Dependency] private HolocardSystem _holocard = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SleepingSystem _sleeping = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    private static readonly EntProtoId ForcedSleeping = "StatusEffectForcedSleeping";
    private static readonly EntProtoId Woozy = "StatusEffectWoozy";

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
        var hadForcedSleep = Status.HasStatusEffect(body, ForcedSleeping);
        if (Status.TrySetStatusEffectDuration(body, ForcedSleeping, out var effect, duration: null) && !hadForcedSleep)
            EnsureComp<CMUBrainlessSleepComponent>(body).Effect = effect;

        _holocard.SetBrainRemovalAssessment(body, true);

        if (TryComp<MobStateComponent>(body, out var mobState)
            && mobState.CurrentState != MobState.Dead)
        {
            _mobState.ChangeMobState(body, MobState.Dead, mobState);
        }
    }

    // brain removal is reversible now, but its sleep had no end and also re-sleeps on revive,
    // so a patient who got a brain back and was defibbed stayed asleep and prone forever
    protected override void ClearPermadeath(EntityUid body)
    {
        if (!TryComp<CMUBrainlessSleepComponent>(body, out var brainless))
            return;

        var effect = brainless.Effect;
        RemComp<CMUBrainlessSleepComponent>(body);
        if (effect is not { } uid || TerminatingOrDeleted(uid) ||
            !TryComp<StatusEffectComponent>(uid, out var status) || status.AppliedTo != body)
            return;

        // not QueueDel, TryWaking would still see the forced sleep and refuse
        Del(uid);
        _sleeping.TryWaking((body, null));
    }

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
                Status.TryAddStatusEffectDuration(
                    body,
                    Woozy,
                    TimeSpan.FromSeconds(brain.DisorientationDrunkPower));
                break;
        }
    }
}
