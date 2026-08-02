using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Yautja;

public enum YautjaCrewMonitoringLocationKind : byte
{
    Unknown,
    MainShip,
    HuntingGround,
}

public static class YautjaCrewMonitoringMetadata
{
    public static LocId GetAssignment(YautjaRank rank, bool isBadBlood)
    {
        return isBadBlood
            ? "cmu-yautja-rank-badblood"
            : YautjaRankMetadata.For(rank).LocalizedName;
    }

    public static int SumDamageGroup(DamageSpecifier damage, IReadOnlyList<string> damageTypes)
    {
        var total = FixedPoint2.Zero;

        foreach (var damageType in damageTypes)
        {
            if (!damage.DamageDict.TryGetValue(damageType, out var value) || value <= FixedPoint2.Zero)
                continue;

            total += value;
        }

        return total.Int();
    }
}
