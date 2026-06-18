using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Human.Effects;

public sealed partial class SemiPermanentInjuryTriggerSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedStatusEffectsSystem _status = default!;

    private static readonly EntProtoId Whiplash = "StatusEffectCMUWhiplash";
    private static readonly FixedPoint2 SevereRegionDamageThreshold = FixedPoint2.New(90);

    private readonly Dictionary<EntityUid, bool[]> _severeRegionState = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanMedicalLedgerChangedEvent>(OnLedgerChanged);
        SubscribeLocalEvent<HumanMedicalShutdownEvent>(OnMedicalShutdown);
    }

    private void OnLedgerChanged(ref HumanMedicalLedgerChangedEvent args)
    {
        if (!IsEnabled())
            return;

        if (!TryComp(args.Body, out HumanMedicalComponent? medical))
            return;

        var ent = (args.Body, medical);

        if (args.Result.DirtyFlags.HasFlag(MedicalDirtyFlags.Organs))
            ApplyBrainInjuryTrigger(ent);

        if (args.Result.DirtyFlags.HasFlag(MedicalDirtyFlags.Regions))
            ApplyRegionRecoveryTriggers(ent);
    }

    private void OnMedicalShutdown(ref HumanMedicalShutdownEvent args)
    {
        _severeRegionState.Remove(args.Body);
    }

    private bool IsEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled)
            && _cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled);
    }

    private void ApplyBrainInjuryTrigger(Entity<HumanMedicalComponent> ent)
    {
        var brain = HumanMedicalLedger.GetOrgan(ent.Comp, OrganSlot.Brain);
        if (!brain.Symptomatic || brain.Damage < FixedPoint2.New(5))
            return;

        _status.TrySetStatusEffectDuration(ent.Owner, Whiplash, TimeSpan.FromMinutes(5));
    }

    private void ApplyRegionRecoveryTriggers(Entity<HumanMedicalComponent> ent)
    {
        if (!_severeRegionState.TryGetValue(ent.Owner, out var previous))
        {
            previous = new bool[HumanMedicalComponent.RegionSlotCount];
            _severeRegionState[ent.Owner] = previous;
            SeedRegionSeverity(ent.Comp, previous);
            return;
        }

        foreach (var region in ent.Comp.Regions)
        {
            if (region.Region == BodyRegion.None)
                continue;

            var index = (int) region.Region;
            if (index <= 0 || index >= previous.Length)
                continue;

            var severe = IsSevere(region);
            if (previous[index] && !severe && ResolveNerveStatus(region.Region) is { } statusId)
                _status.TrySetStatusEffectDuration(ent.Owner, statusId, TimeSpan.FromMinutes(30));

            previous[index] = severe;
        }
    }

    private static void SeedRegionSeverity(HumanMedicalComponent medical, bool[] previous)
    {
        foreach (var region in medical.Regions)
        {
            var index = (int) region.Region;
            if (index <= 0 || index >= previous.Length)
                continue;

            previous[index] = IsSevere(region);
        }
    }

    private static bool IsSevere(RegionState region)
    {
        return region.BruteDamage + region.BurnDamage >= SevereRegionDamageThreshold;
    }

    private static EntProtoId? ResolveNerveStatus(BodyRegion region) => region switch
    {
        BodyRegion.LeftArm or BodyRegion.RightArm => "StatusEffectCMUNerveDamageArm",
        BodyRegion.LeftHand or BodyRegion.RightHand => "StatusEffectCMUNerveDamageHand",
        BodyRegion.LeftLeg or BodyRegion.RightLeg => "StatusEffectCMUNerveDamageLeg",
        BodyRegion.LeftFoot or BodyRegion.RightFoot => "StatusEffectCMUNerveDamageFoot",
        _ => null,
    };
}
