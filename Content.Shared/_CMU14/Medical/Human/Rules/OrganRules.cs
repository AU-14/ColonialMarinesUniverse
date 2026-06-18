using System;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Rules;

public static class OrganRules
{
    private static readonly OrganSlot[] HeadDamageTargets =
    {
        OrganSlot.Brain,
        OrganSlot.Eyes,
        OrganSlot.Ears,
    };

    private static readonly OrganSlot[] ChestDamageTargets =
    {
        OrganSlot.Heart,
        OrganSlot.LeftLung,
        OrganSlot.RightLung,
        OrganSlot.Liver,
        OrganSlot.Stomach,
    };

    private static readonly OrganSlot[] GroinDamageTargets =
    {
        OrganSlot.Kidneys,
    };

    public static OrganDamageStatus GetStatus(FixedPoint2 damage)
    {
        if (damage <= FixedPoint2.Zero)
            return OrganDamageStatus.None;

        if (damage < FixedPoint2.New(10))
            return OrganDamageStatus.LittleBruised;

        if (damage < FixedPoint2.New(30))
            return OrganDamageStatus.Bruised;

        return OrganDamageStatus.Broken;
    }

    public static bool HasDamageTargets(BodyRegion region)
    {
        return GetDamageTargets(region).Length > 0;
    }

    public static bool TryPickDamageTarget(
        BodyRegion region,
        OrganState[]? organs,
        float roll,
        out OrganSlot slot)
    {
        var targets = GetDamageTargets(region);
        slot = OrganSlot.None;
        if (targets.Length == 0)
            return false;

        var count = 0;
        for (var i = 0; i < targets.Length; i++)
        {
            if (IsEligibleTarget(targets[i], organs))
                count++;
        }

        if (count <= 0)
            return false;

        var selected = PickCandidateIndex(count, roll);
        for (var i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            if (!IsEligibleTarget(target, organs))
                continue;

            if (selected-- != 0)
                continue;

            slot = target;
            return true;
        }

        return false;
    }

    private static OrganSlot[] GetDamageTargets(BodyRegion region)
    {
        return region switch
        {
            BodyRegion.Head => HeadDamageTargets,
            BodyRegion.Chest => ChestDamageTargets,
            BodyRegion.Groin => GroinDamageTargets,
            _ => Array.Empty<OrganSlot>(),
        };
    }

    private static bool IsEligibleTarget(OrganSlot slot, OrganState[]? organs)
    {
        if (organs == null)
            return slot != OrganSlot.None;

        var index = (int) slot;
        if (index <= 0 || index >= organs.Length)
            return false;

        var organ = organs[index];
        return organ.Slot == slot && !organ.Missing;
    }

    private static int PickCandidateIndex(int count, float roll)
    {
        return Math.Clamp((int) MathF.Floor(Math.Clamp(roll, 0f, 0.9999f) * count), 0, count - 1);
    }
}
