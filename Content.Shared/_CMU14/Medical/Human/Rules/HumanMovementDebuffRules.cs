using System;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Rules;

public readonly record struct EmbeddedObjectMovementInput(
    int Count,
    FixedPoint2 MoveDamage = default,
    FixedPoint2 MoveDamagePerCount = default,
    bool CanExplode = false,
    float ExplosionChance = 0f);

public readonly record struct SplintBreakInput(
    BodyRegion Region,
    FixedPoint2 NonBurnDamage,
    bool Splinted,
    bool Indestructible = false);

public readonly record struct SplintBreakResult(
    bool ShouldBreak,
    int ChancePercent);

public readonly record struct HumanMovementDebuffRng(
    float OrganDamageRoll = 1f,
    float InternalBleedRoll = 1f,
    float CrippledLegsRoll = 1f,
    float OrganDamageAmountRoll = 0f,
    float InternalBleedAmountRoll = 0f,
    float OrganSlotRoll = 0f,
    float BrokenRegionRoll = 0f);

public readonly record struct HumanMovementComplicationResult(
    MedicalTransaction? Transaction,
    bool BonesMoved,
    BodyRegion BonesMovedRegion,
    bool BonesCut,
    BodyRegion BonesCutRegion,
    bool CrippledLegsShouldDrop);

public static class HumanMovementDebuffRules
{
    public const float ShrapnelJostlePopupChance = 0.30f;
    public const float UnsplintedFractureOrganDamageChance = 0.15f;
    public const float UnsplintedFractureInternalBleedChance = 0.02f;
    public const float CrippledLegsDropChance = 0.02f;
    public const float DefaultEmbeddedObjectMovementDamagePerCount = 0.5f;
    public const float InternalBleedMovementRateMultiplier = 0.1f;

    private static readonly OrganSlot[] HeadOrganTargets =
    {
        OrganSlot.Brain,
        OrganSlot.Eyes,
    };

    private static readonly OrganSlot[] ChestOrganTargets =
    {
        OrganSlot.Heart,
        OrganSlot.LeftLung,
        OrganSlot.RightLung,
        OrganSlot.Liver,
    };

    private static readonly OrganSlot[] GroinOrganTargets =
    {
        OrganSlot.Kidneys,
    };

    public static FixedPoint2 GetEmbeddedObjectMovementDamage(EmbeddedObjectMovementInput input)
    {
        if (input.Count <= 0)
            return FixedPoint2.Zero;

        if (input.MoveDamage > FixedPoint2.Zero)
            return input.MoveDamage;

        var perCount = input.MoveDamagePerCount > FixedPoint2.Zero
            ? input.MoveDamagePerCount
            : FixedPoint2.New(DefaultEmbeddedObjectMovementDamagePerCount);

        return FixedPoint2.New(perCount.Float() * input.Count);
    }

    public static bool ShouldEmbeddedObjectExplode(EmbeddedObjectMovementInput input, float roll)
    {
        if (!input.CanExplode || input.ExplosionChance <= 0f)
            return false;

        return Math.Clamp(roll, 0f, 1f) < Math.Clamp(input.ExplosionChance, 0f, 1f);
    }

    public static SplintBreakResult EvaluateSplintBreak(SplintBreakInput input, float roll)
    {
        if (!input.Splinted ||
            input.Indestructible ||
            input.NonBurnDamage <= FixedPoint2.New(5))
        {
            return new SplintBreakResult(false, 0);
        }

        var chance = Math.Clamp((int) MathF.Round(50f + input.NonBurnDamage.Float() * 2.5f), 0, 100);
        return new SplintBreakResult(Math.Clamp(roll, 0f, 1f) < chance / 100f, chance);
    }

    public static HumanMovementComplicationResult EvaluateUnsplintedFractureMovement(
        HumanMedicalComponent medical,
        HumanMovementDebuffRng rng)
    {
        MedicalTransaction? transaction = null;
        var bonesMoved = TryPickUnsplintedBrokenRegion(
            medical,
            rng.BrokenRegionRoll,
            out var bonesMovedRegion);
        var bonesCut = false;
        var bonesCutRegion = BodyRegion.None;

        if (Math.Clamp(rng.OrganDamageRoll, 0f, 1f) < UnsplintedFractureOrganDamageChance &&
            TryPickCoreBrokenRegion(medical, rng.BrokenRegionRoll, out var organRegion) &&
            TryPickOrganTarget(medical, organRegion, rng.OrganSlotRoll, out var organ))
        {
            transaction = EnsureTransaction(transaction, organRegion);
            transaction.Add(MedicalEffect.AddOrganDamage(
                organ,
                RollMovementComplicationDamage(rng.OrganDamageAmountRoll)));
            bonesMoved = true;
            bonesMovedRegion = organRegion;
        }

        if (Math.Clamp(rng.InternalBleedRoll, 0f, 1f) < UnsplintedFractureInternalBleedChance &&
            TryPickUnsplintedBrokenRegion(medical, rng.BrokenRegionRoll, out var bleedRegion))
        {
            var damage = RollMovementComplicationDamage(rng.InternalBleedAmountRoll);
            transaction = EnsureTransaction(transaction, bleedRegion);
            transaction.Add(MedicalEffect.AddInjury(
                bleedRegion,
                InjuryKind.InternalBleed,
                InjuryStage.InternalBleed,
                damage));
            transaction.Add(MedicalEffect.AddBleedSource(
                bleedRegion,
                BleedKind.Internal,
                FixedPoint2.New(damage.Float() * InternalBleedMovementRateMultiplier)));
            bonesCut = true;
            bonesCutRegion = bleedRegion;
        }

        var crippledLegs = HasBothCrippledLegSides(medical) &&
            Math.Clamp(rng.CrippledLegsRoll, 0f, 1f) < CrippledLegsDropChance;

        return new HumanMovementComplicationResult(
            transaction,
            bonesMoved,
            bonesMovedRegion,
            bonesCut,
            bonesCutRegion,
            crippledLegs);
    }

    public static bool HasUnsplintedFractureRisk(HumanMedicalComponent medical)
    {
        for (var i = 1; i < medical.Regions.Length; i++)
        {
            var region = medical.Regions[i];
            if (IsUnsplintedBroken(region))
                return true;
        }

        return false;
    }

    public static bool HasBothCrippledLegSides(HumanMedicalComponent medical)
    {
        return IsCrippledLegSide(
                medical.Regions[(int) BodyRegion.LeftLeg],
                medical.Regions[(int) BodyRegion.LeftFoot])
            && IsCrippledLegSide(
                medical.Regions[(int) BodyRegion.RightLeg],
                medical.Regions[(int) BodyRegion.RightFoot]);
    }

    private static MedicalTransaction EnsureTransaction(MedicalTransaction? transaction, BodyRegion region)
    {
        return transaction ?? new MedicalTransaction(region);
    }

    private static FixedPoint2 RollMovementComplicationDamage(float roll)
    {
        return FixedPoint2.New(3f + Math.Clamp(roll, 0f, 1f) * 2f);
    }

    private static bool TryPickCoreBrokenRegion(
        HumanMedicalComponent medical,
        float roll,
        out BodyRegion region)
    {
        region = BodyRegion.None;
        var head = IsUnsplintedBroken(medical, BodyRegion.Head);
        var chest = IsUnsplintedBroken(medical, BodyRegion.Chest);
        var groin = IsUnsplintedBroken(medical, BodyRegion.Groin);
        var count = 0;

        if (head)
            count++;
        if (chest)
            count++;
        if (groin)
            count++;

        if (count <= 0)
            return false;

        var selected = PickCandidateIndex(count, roll);
        if (head && selected-- == 0)
        {
            region = BodyRegion.Head;
            return true;
        }

        if (chest && selected-- == 0)
        {
            region = BodyRegion.Chest;
            return true;
        }

        if (groin && selected == 0)
        {
            region = BodyRegion.Groin;
            return true;
        }

        return false;
    }

    private static bool TryPickUnsplintedBrokenRegion(
        HumanMedicalComponent medical,
        float roll,
        out BodyRegion region)
    {
        region = BodyRegion.None;
        var count = 0;

        for (var i = 1; i < medical.Regions.Length; i++)
        {
            if (IsUnsplintedBroken(medical.Regions[i]))
                count++;
        }

        if (count <= 0)
            return false;

        var selected = PickCandidateIndex(count, roll);
        for (var i = 1; i < medical.Regions.Length; i++)
        {
            var candidate = medical.Regions[i];
            if (!IsUnsplintedBroken(candidate))
                continue;

            if (selected-- != 0)
                continue;

            region = candidate.Region;
            return true;
        }

        return false;
    }

    private static bool TryPickOrganTarget(
        HumanMedicalComponent medical,
        BodyRegion region,
        float roll,
        out OrganSlot slot)
    {
        var targets = GetOrganTargets(region);
        slot = OrganSlot.None;
        if (targets.Length == 0)
            return false;

        var count = 0;
        for (var i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            var index = (int) target;
            if (index <= 0 || index >= medical.Organs.Length)
                continue;

            var organ = medical.Organs[index];
            if (organ.Missing)
                continue;

            count++;
        }

        if (count <= 0)
            return false;

        var selected = PickCandidateIndex(count, roll);
        for (var i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            var index = (int) target;
            if (index <= 0 || index >= medical.Organs.Length)
                continue;

            var organ = medical.Organs[index];
            if (organ.Missing)
                continue;

            if (selected-- != 0)
                continue;

            slot = target;
            return true;
        }

        return false;
    }

    private static int PickCandidateIndex(int count, float roll)
    {
        return Math.Clamp((int) MathF.Floor(Math.Clamp(roll, 0f, 0.9999f) * count), 0, count - 1);
    }

    private static OrganSlot[] GetOrganTargets(BodyRegion region)
    {
        return region switch
        {
            BodyRegion.Head => HeadOrganTargets,
            BodyRegion.Chest => ChestOrganTargets,
            BodyRegion.Groin => GroinOrganTargets,
            _ => Array.Empty<OrganSlot>(),
        };
    }

    private static bool IsUnsplintedBroken(RegionState region)
    {
        return region.Region != BodyRegion.None &&
            region.Presence == LimbPresence.Present &&
            region.Skeletal.Broken &&
            !region.Skeletal.Splinted;
    }

    private static bool IsUnsplintedBroken(HumanMedicalComponent medical, BodyRegion region)
    {
        var index = (int) region;
        return index > 0 &&
            index < medical.Regions.Length &&
            IsUnsplintedBroken(medical.Regions[index]);
    }

    private static bool IsCrippledLegSide(RegionState leg, RegionState foot)
    {
        return IsCrippledLegRegion(leg) || IsCrippledLegRegion(foot);
    }

    private static bool IsCrippledLegRegion(RegionState region)
    {
        return region.Presence is LimbPresence.Missing or LimbPresence.Detached ||
            IsUnsplintedBroken(region);
    }
}
