using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Ears;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Stomach;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Shrapnel;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Synth;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Injuries.Pain;

public readonly record struct CMUPainSourceSnapshot(FixedPoint2 Target, FixedPoint2 RiseRate);

public sealed partial class SharedPainSourceProfileSystem : EntitySystem
{
    [Dependency] private SharedFractureSystem _fracture = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;

    private const float SourceStackMultiplier = 0.30f;
    private const float PainTargetCap = 95f;
    private const float PainRiseRateCap = 4.0f;
    private const float PainRiseRatePerTarget = 0.05f;

    public CMUPainSourceSnapshot ComputePainSourceProfile(EntityUid body)
    {
        if (HasComp<SynthComponent>(body))
            return new CMUPainSourceSnapshot(FixedPoint2.Zero, FixedPoint2.Zero);

        var sourceCount = 0;
        var highest = 0f;
        var total = 0f;
        var riseRate = 0f;

        foreach (var (partUid, _) in _medicalIndex.GetBodyParts(body))
        {
            if (TryComp<FractureComponent>(partUid, out var frac))
                AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate,
                    FracturePainTarget(_fracture.GetEffectiveSeverity((partUid, frac))));

            if (TryComp<BodyPartHealthComponent>(partUid, out var ph) &&
                ph.Max > FixedPoint2.Zero)
            {
                var current = ph.Current;
                var max = ph.Max;
                var fraction = current.Float() / max.Float();
                if (fraction < 0.10f)
                    AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate, 30f);
                else if (fraction < 0.25f)
                    AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate, 15f);
            }

            if (TryComp<BodyPartWoundComponent>(partUid, out var pw))
            {
                foreach (var entry in _woundLedger.GetEntries(pw))
                {
                    if (entry.Wound.Treated)
                        continue;

                    AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate,
                        WoundPainTarget(entry.Size, entry.Wound.Damage.Float()));
                }
            }

            if (HasComp<CMUEscharComponent>(partUid))
                AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate, 55f);

            if (HasComp<InternalBleedingComponent>(partUid))
                AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate, 35f);

            if (TryComp<CMUShrapnelComponent>(partUid, out var shrapnel))
                AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate,
                    SharedCMUShrapnelSystem.GetPainTarget(shrapnel));
        }

        foreach (var organ in _medicalIndex.GetOrgans(body))
        {
            if (!TryComp<OrganHealthComponent>(organ.Owner, out var oh))
                continue;

            AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate,
                OrganPainTarget(organ.Owner, oh.Stage));
        }

        if (sourceCount == 0)
            return new CMUPainSourceSnapshot(FixedPoint2.Zero, FixedPoint2.Zero);

        var target = MathF.Min(PainTargetCap, highest + SourceStackMultiplier * (total - highest));
        return new CMUPainSourceSnapshot(
            (FixedPoint2) target,
            (FixedPoint2) MathF.Min(PainRiseRateCap, riseRate));
    }

    public FixedPoint2 ComputeAccumulationRate(EntityUid body)
    {
        return ComputePainSourceProfile(body).RiseRate;
    }

    private static void AddPainSource(
        ref int count,
        ref float highest,
        ref float total,
        ref float riseRate,
        float target)
    {
        if (target <= 0f)
            return;

        count++;
        highest = MathF.Max(highest, target);
        total += target;
        riseRate += target * PainRiseRatePerTarget;
    }

    private static float FracturePainTarget(FractureSeverity sev)
    {
        return sev switch
        {
            FractureSeverity.Hairline => 10f,
            FractureSeverity.Simple => 25f,
            FractureSeverity.Compound => 45f,
            FractureSeverity.Shattered => 65f,
            _ => 0f,
        };
    }

    private static float WoundPainTarget(WoundSize size, float damage)
    {
        return WoundSizeProfile.PainTarget(size, damage);
    }

    private float OrganPainTarget(EntityUid organ, OrganDamageStage stage)
    {
        if (HasComp<HeartComponent>(organ) ||
            HasComp<LungsComponent>(organ) ||
            HasComp<CMUBrainComponent>(organ))
        {
            return VitalOrganPainTarget(stage);
        }

        if (HasComp<LiverComponent>(organ) ||
            HasComp<KidneysComponent>(organ))
        {
            return MetabolicOrganPainTarget(stage);
        }

        if (HasComp<CMUStomachComponent>(organ))
            return StomachPainTarget(stage);

        if (HasComp<EyesComponent>(organ) ||
            HasComp<EarsComponent>(organ))
        {
            return SensoryOrganPainTarget(stage);
        }

        return FallbackOrganPainTarget(stage);
    }

    private static float VitalOrganPainTarget(OrganDamageStage stage)
    {
        return stage switch
        {
            OrganDamageStage.Bruised => 10f,
            OrganDamageStage.Damaged => 32f,
            OrganDamageStage.Failing => 50f,
            OrganDamageStage.Dead => 65f,
            _ => 0f,
        };
    }

    private static float MetabolicOrganPainTarget(OrganDamageStage stage)
    {
        return stage switch
        {
            OrganDamageStage.Bruised => 6f,
            OrganDamageStage.Damaged => 20f,
            OrganDamageStage.Failing => 35f,
            OrganDamageStage.Dead => 50f,
            _ => 0f,
        };
    }

    private static float StomachPainTarget(OrganDamageStage stage)
    {
        return stage switch
        {
            OrganDamageStage.Bruised => 4f,
            OrganDamageStage.Damaged => 12f,
            OrganDamageStage.Failing => 24f,
            OrganDamageStage.Dead => 35f,
            _ => 0f,
        };
    }

    private static float SensoryOrganPainTarget(OrganDamageStage stage)
    {
        return stage switch
        {
            OrganDamageStage.Bruised => 2f,
            OrganDamageStage.Damaged => 8f,
            OrganDamageStage.Failing => 16f,
            OrganDamageStage.Dead => 25f,
            _ => 0f,
        };
    }

    private static float FallbackOrganPainTarget(OrganDamageStage stage)
    {
        return stage switch
        {
            OrganDamageStage.Bruised => 10f,
            OrganDamageStage.Damaged => 25f,
            OrganDamageStage.Failing => 45f,
            OrganDamageStage.Dead => 65f,
            _ => 0f,
        };
    }

}
