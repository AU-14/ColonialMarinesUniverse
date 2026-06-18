using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Targeting.Events;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Effects;
using Content.Shared._CMU14.Medical.Human.Damage;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Random;
using HumanOrganSlot = Content.Shared._CMU14.Medical.Human.Data.OrganSlot;

namespace Content.Shared._CMU14.Medical.Human.Systems;

public sealed partial class HumanMedicalDamageSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedHumanMedicalSystem _humanMedical = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;

    private readonly Dictionary<EntityUid, ResolvedHit> _pendingHits = new();
    private const float SplintBreakPainPulse = 8f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanMedicalComponent, HitLocationResolvedEvent>(OnHitLocationResolved);
        SubscribeLocalEvent<HumanMedicalComponent, DamageChangedEvent>(OnDamageChanged);
    }

    public static bool CanProcessBody(
        bool hasHumanLedger,
        bool isSynth,
        bool isXeno)
    {
        return hasHumanLedger && !isSynth && !isXeno;
    }

    public static BodyRegion ResolveBodyRegion(
        BodyPartType partType,
        AnatomyRegionComponent? anatomy)
    {
        if (anatomy is { Region: not BodyRegion.None })
            return anatomy.Region;

        return partType switch
        {
            BodyPartType.Head => BodyRegion.Head,
            BodyPartType.Torso => BodyRegion.Chest,
            BodyPartType.Arm => BodyRegion.LeftArm,
            BodyPartType.Hand => BodyRegion.LeftHand,
            BodyPartType.Leg => BodyRegion.LeftLeg,
            BodyPartType.Foot => BodyRegion.LeftFoot,
            _ => BodyRegion.Chest,
        };
    }

    public static MedicalTransactionResult TryApplyDamageToLedger(
        HumanMedicalComponent medical,
        BodyRegion region,
        DamageSpecifier damage,
        CMUTraumaContactResult trauma,
        MedicalRngContext rng,
        bool isSynth = false,
        bool isXeno = false)
    {
        if (!CanProcessBody(hasHumanLedger: true, isSynth, isXeno))
        {
            return new MedicalTransactionResult(
                false,
                medical.Revision,
                MedicalDirtyFlags.None,
                "Body is not eligible for the human medical ledger.");
        }

        HumanMedicalLedger.EnsureInitialized(medical);

        var brute = ExtractBruteDamage(damage);
        var burn = ExtractBurnDamage(damage);
        if (brute <= FixedPoint2.Zero && burn <= FixedPoint2.Zero)
        {
            return new MedicalTransactionResult(
                false,
                medical.Revision,
                MedicalDirtyFlags.None,
                "Damage has no tracked human medical component.");
        }

        var regionState = HumanMedicalLedger.GetRegion(medical, region);
        if (regionState.Presence == LimbPresence.Prosthetic)
            return ApplyProstheticDamageToLedger(medical, region, brute, burn);

        if (regionState.Presence != LimbPresence.Present)
        {
            return new MedicalTransactionResult(
                false,
                medical.Revision,
                MedicalDirtyFlags.None,
                "Target region is not a biological limb.");
        }

        var context = new MedicalDamageContext(
            GetPrimaryInjuryKind(damage, brute, burn),
            trauma.BoneContact,
            trauma.OrganContact,
            trauma.VascularContact,
            HumanOrganSlot.None,
            trauma.OrganPassThrough,
            FixedPoint2.New(trauma.InternalBleedRate));
        var transaction = MedicalDamageRules.CreateDamageTransaction(
            region,
            brute,
            burn,
            context,
            rng,
            organs: medical.Organs);

        return HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }

    private void OnHitLocationResolved(Entity<HumanMedicalComponent> ent, ref HitLocationResolvedEvent args)
    {
        _pendingHits[ent.Owner] = new ResolvedHit(args.ResolvedPart, args.ResolvedPartEntity, args.ResolvedRegion);
    }

    private void OnDamageChanged(Entity<HumanMedicalComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta is not { } damage ||
            !CanProcessBody(
                hasHumanLedger: true,
                HasComp<SynthComponent>(ent.Owner),
                HasComp<XenoComponent>(ent.Owner)))
        {
            _pendingHits.Remove(ent.Owner);
            return;
        }

        var hit = ConsumeResolvedHit(ent.Owner);
        var region = hit.Region != BodyRegion.None
            ? hit.Region
            : ResolveBodyRegion(
                hit.PartType,
                hit.Part is { } partUid && TryComp<AnatomyRegionComponent>(partUid, out var anatomy)
                    ? anatomy
                    : null);
        var trauma = CreateTrauma(region, hit.PartType, damage, args.Impact);
        var rng = new MedicalRngContext(
            BoneRoll: _random.NextFloat(),
            OrganRoll: _random.NextFloat(),
            VascularRoll: _random.NextFloat());

        HumanMedicalLedger.EnsureInitialized(ent.Comp);

        var brute = ExtractBruteDamage(damage);
        var burn = ExtractBurnDamage(damage);
        if (brute <= FixedPoint2.Zero && burn <= FixedPoint2.Zero)
            return;

        var previousRegionState = HumanMedicalLedger.GetRegion(ent.Comp, region);
        if (previousRegionState.Presence == LimbPresence.Prosthetic)
        {
            var prostheticResult = ApplyProstheticDamageToLedger(ent.Comp, region, brute, burn);
            if (prostheticResult.Applied)
            {
                _humanMedical.RefreshActiveMarkers(ent.Owner, ent.Comp);
                var currentRegionState = HumanMedicalLedger.GetRegion(ent.Comp, region);
                var ev = new HumanMedicalDamageAppliedEvent(
                    ent.Owner,
                    region,
                    hit.PartType,
                    hit.Part,
                    brute,
                    burn,
                    previousRegionState,
                    currentRegionState,
                    args.Tool,
                    args.Impact);
                RaiseLocalEvent(ent.Owner, ref ev, broadcast: true);
            }

            return;
        }

        if (previousRegionState.Presence != LimbPresence.Present)
            return;

        var splintBreak = HumanMovementDebuffRules.EvaluateSplintBreak(
            new SplintBreakInput(
                region,
                brute,
                previousRegionState.Skeletal.Splinted),
            _random.NextFloat());

        var context = new MedicalDamageContext(
            GetPrimaryInjuryKind(damage, brute, burn),
            trauma.BoneContact,
            trauma.OrganContact,
            trauma.VascularContact,
            HumanOrganSlot.None,
            trauma.OrganPassThrough,
            FixedPoint2.New(trauma.InternalBleedRate));
        var transaction = MedicalDamageRules.CreateDamageTransaction(
            region,
            brute,
            burn,
            context,
            rng,
            organs: ent.Comp.Organs);

        var result = _humanMedical.ApplyTransaction(ent, transaction);
        if (result.Applied)
        {
            TryBreakSplint(ent, region, splintBreak);
            var currentRegionState = HumanMedicalLedger.GetRegion(ent.Comp, region);
            var ev = new HumanMedicalDamageAppliedEvent(
                ent.Owner,
                region,
                hit.PartType,
                hit.Part,
                brute,
                burn,
                previousRegionState,
                currentRegionState,
                args.Tool,
                args.Impact);
            RaiseLocalEvent(ent.Owner, ref ev, broadcast: true);
        }
    }

    private static MedicalTransactionResult ApplyProstheticDamageToLedger(
        HumanMedicalComponent medical,
        BodyRegion region,
        FixedPoint2 brute,
        FixedPoint2 burn)
    {
        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.AddRegionDamage(region, brute, burn));
        return HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }

    private void TryBreakSplint(
        Entity<HumanMedicalComponent> ent,
        BodyRegion region,
        SplintBreakResult splintBreak)
    {
        if (!splintBreak.ShouldBreak)
            return;

        var regionState = HumanMedicalLedger.GetRegion(ent.Comp, region);
        if (!regionState.Skeletal.Splinted)
            return;

        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.SetSkeletalState(
            region,
            regionState.Skeletal.Broken,
            splinted: false));

        var result = _humanMedical.ApplyTransaction(ent, transaction);
        if (!result.Applied)
            return;

        _pain.AddPainPulse(ent.Owner, FixedPoint2.New(SplintBreakPainPulse));
        var ev = new HumanSplintBrokenEvent(ent.Owner, region);
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    private ResolvedHit ConsumeResolvedHit(EntityUid body)
    {
        if (!_pendingHits.Remove(body, out var hit))
            return new ResolvedHit(BodyPartType.Torso, null, BodyRegion.None);

        return hit;
    }

    private CMUTraumaContactResult CreateTrauma(
        BodyRegion region,
        BodyPartType partType,
        DamageSpecifier damage,
        DamageImpact impact)
    {
        var brute = ExtractBruteDamage(damage);
        return CMUTraumaContactModel.Create(
            InferMechanism(damage, brute, ExtractBurnDamage(damage)),
            impact,
            partType,
            brute,
            HasOrgans(region),
            _random.NextFloat(),
            CMUTraumaContactSettings.Default);
    }

    private static FixedPoint2 ExtractBruteDamage(DamageSpecifier damage)
    {
        return GetPositiveDamageType(damage, "Blunt") +
               GetPositiveDamageType(damage, "Slash") +
               GetPositiveDamageType(damage, "Piercing");
    }

    private static FixedPoint2 ExtractBurnDamage(DamageSpecifier damage)
    {
        return GetPositiveDamageType(damage, "Heat") +
               GetPositiveDamageType(damage, "Cold") +
               GetPositiveDamageType(damage, "Shock") +
               GetPositiveDamageType(damage, "Caustic");
    }

    private static InjuryKind GetPrimaryInjuryKind(
        DamageSpecifier damage,
        FixedPoint2 brute,
        FixedPoint2 burn)
    {
        if (burn > brute)
            return InjuryKind.Burn;

        if (GetPositiveDamageType(damage, "Piercing") > FixedPoint2.Zero)
            return InjuryKind.Puncture;

        if (GetPositiveDamageType(damage, "Slash") > FixedPoint2.Zero)
            return InjuryKind.Cut;

        return InjuryKind.Bruise;
    }

    private static CMUTraumaMechanism InferMechanism(
        DamageSpecifier damage,
        FixedPoint2 brute,
        FixedPoint2 burn)
    {
        if (burn > brute)
            return CMUTraumaMechanism.Generic;

        var piercing = GetPositiveDamageType(damage, "Piercing");
        var slash = GetPositiveDamageType(damage, "Slash");
        var blunt = GetPositiveDamageType(damage, "Blunt");

        if (piercing > FixedPoint2.Zero && piercing >= slash && piercing >= blunt)
            return CMUTraumaMechanism.Pierce;
        if (slash > FixedPoint2.Zero && slash >= blunt)
            return CMUTraumaMechanism.Slash;
        if (blunt > FixedPoint2.Zero)
            return CMUTraumaMechanism.Blunt;

        return CMUTraumaMechanism.Generic;
    }

    private static bool HasOrgans(BodyRegion region)
    {
        return OrganRules.HasDamageTargets(region);
    }

    private static FixedPoint2 GetPositiveDamageType(DamageSpecifier damage, string type)
    {
        return damage.DamageDict.TryGetValue(type, out var value) && value > FixedPoint2.Zero
            ? value
            : FixedPoint2.Zero;
    }

    private readonly record struct ResolvedHit(
        BodyPartType PartType,
        EntityUid? Part,
        BodyRegion Region);
}

[ByRefEvent]
public readonly record struct HumanMedicalDamageAppliedEvent(
    EntityUid Body,
    BodyRegion Region,
    BodyPartType PartType,
    EntityUid? Part,
    FixedPoint2 BruteDelta,
    FixedPoint2 BurnDelta,
    RegionState PreviousRegion,
    RegionState CurrentRegion,
    EntityUid? Tool,
    DamageImpact Impact);
