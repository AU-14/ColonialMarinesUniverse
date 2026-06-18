using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Human.Organs.Brain;

public abstract partial class SharedBrainSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected IRobustRandom Rng = default!;
    [Dependency] protected SharedBodySystem Body = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;
    [Dependency] protected RMCUnrevivableSystem Unrevivable = default!;

    private static readonly EntProtoId Concussed = "StatusEffectCMUConcussed";
    private static readonly EntProtoId TraumaticBrainInjury = "StatusEffectCMUTraumaticBrainInjury";
    private static readonly EntProtoId Unconscious = "StatusEffectCMUUnconscious";

    private const float BrainScanInterval = 1f;
    private float _brainScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanMedicalLedgerChangedEvent>(OnLedgerChanged);
        SubscribeLocalEvent<CMUBrainComponent, ComponentStartup>(OnBrainStartup);
        SubscribeLocalEvent<CMUBrainComponent, OrganRemovedFromBodyEvent>(OnBrainRemovedFromBody);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnBrainStartup(Entity<CMUBrainComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextDisorientCheck = Timing.CurTime + TimeSpan.FromMinutes(1);
        ent.Comp.NextUnconsciousCheck = Timing.CurTime + TimeSpan.FromSeconds(60);
    }

    private void OnBrainRemovedFromBody(Entity<CMUBrainComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;
        if (TerminatingOrDeleted(args.OldBody))
            return;

        if (!ent.Comp.PermadeathApplied)
        {
            ent.Comp.PermadeathApplied = true;
            Dirty(ent);
        }

        ApplyPermadeath(args.OldBody);
    }

    private void OnLedgerChanged(ref HumanMedicalLedgerChangedEvent args)
    {
        if (!HumanOrganLedgerUtility.OrgansChanged(args.Result))
            return;

        if (!TryComp(args.Body, out HumanMedicalComponent? medical))
            return;

        SyncBrainState(args.Body, medical);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient || !_medicalEnabled || !_organEnabled)
            return;

        _brainScanAccumulator += frameTime;
        if (_brainScanAccumulator < BrainScanInterval)
            return;

        _brainScanAccumulator = 0f;
        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<HumanMedicalComponent, ActiveOrganSymptomsComponent>();
        while (query.MoveNext(out var uid, out var medical, out _))
        {
            var status = HumanOrganLedgerUtility.EffectiveStatus(medical, OrganSlot.Brain);
            if (status == OrganDamageStatus.None)
                continue;

            foreach (var brain in Body.GetBodyOrganEntityComps<CMUBrainComponent>(uid))
            {
                if (status is OrganDamageStatus.LittleBruised or OrganDamageStatus.Bruised)
                    TickDisorientation(uid, (brain.Owner, brain.Comp1), now);
                else if (status == OrganDamageStatus.Broken)
                    TickFailingUnconscious(uid, (brain.Owner, brain.Comp1), now);
            }
        }
    }

    private void SyncBrainState(EntityUid body, HumanMedicalComponent medical)
    {
        var brain = HumanMedicalLedger.GetOrgan(medical, OrganSlot.Brain);
        if (brain.Missing)
        {
            ApplyPermadeath(body);
            return;
        }

        var status = HumanOrganLedgerUtility.EffectiveStatus(medical, OrganSlot.Brain);
        foreach (var organBrain in Body.GetBodyOrganEntityComps<CMUBrainComponent>(body))
        {
            ApplyBrainStatus(body, (organBrain.Owner, organBrain.Comp1), status);
        }
    }

    private void ApplyBrainStatus(
        EntityUid body,
        Entity<CMUBrainComponent> ent,
        OrganDamageStatus status)
    {
        switch (status)
        {
            case OrganDamageStatus.None:
                ent.Comp.ActionSpeedMultiplier = 1.0f;
                Status.TryRemoveStatusEffect(body, Concussed);
                Status.TryRemoveStatusEffect(body, TraumaticBrainInjury);
                ClearSlurredSpeech(body);
                break;
            case OrganDamageStatus.LittleBruised:
                ent.Comp.ActionSpeedMultiplier = 0.9f;
                Status.TryRemoveStatusEffect(body, TraumaticBrainInjury);
                ClearSlurredSpeech(body);
                Status.TrySetStatusEffectDuration(body, Concussed, duration: null);
                break;
            case OrganDamageStatus.Bruised:
                ent.Comp.ActionSpeedMultiplier = 0.75f;
                Status.TryRemoveStatusEffect(body, TraumaticBrainInjury);
                Status.TrySetStatusEffectDuration(body, Concussed, duration: null);
                ApplySlurredSpeech(body);
                break;
            case OrganDamageStatus.Broken:
                ent.Comp.ActionSpeedMultiplier = 0.5f;
                Status.TrySetStatusEffectDuration(body, TraumaticBrainInjury, duration: null);
                ApplySlurredSpeech(body);
                break;
        }

        Dirty(ent);
    }

    private void TickDisorientation(
        EntityUid body,
        Entity<CMUBrainComponent> ent,
        TimeSpan now)
    {
        if (ent.Comp.NextDisorientCheck > now)
            return;

        ent.Comp.NextDisorientCheck = now + TimeSpan.FromMinutes(1);
        Dirty(ent);

        if (!Rng.Prob(ent.Comp.DisorientationChancePerMinute))
            return;
        if (Unrevivable.IsUnrevivable(body))
            return;

        ApplyDisorientation(body);
    }

    private void TickFailingUnconscious(
        EntityUid body,
        Entity<CMUBrainComponent> ent,
        TimeSpan now)
    {
        if (ent.Comp.NextUnconsciousCheck > now)
            return;

        ent.Comp.NextUnconsciousCheck = now + TimeSpan.FromSeconds(60);
        Dirty(ent);

        if (Unrevivable.IsUnrevivable(body))
            return;

        Status.TrySetStatusEffectDuration(body, Unconscious, TimeSpan.FromSeconds(5));
    }

    protected virtual void ApplyPermadeath(EntityUid body)
    {
    }

    protected virtual void ApplyDisorientation(EntityUid body)
    {
    }

    protected virtual void ApplySlurredSpeech(EntityUid body)
    {
    }

    protected virtual void ClearSlurredSpeech(EntityUid body)
    {
    }

    protected EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
