using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Round.Antags.Cannibal;

/// <summary>
/// Identifies human stock (carved human meat and human organs) by prototype.
/// Shared by the cannibal meal tracking and the Wendigo procedure so the two cannot drift.
/// </summary>
public static class CMUHumanMeat
{
    public const string HumanMeatPrototype = "FoodMeatHuman";
    public const string HumanOrganPrefix = "OrganHuman";

    public const string CMUHumanPartPrefix = "CMUPartHuman";
    public const string CMUHumanOrganPrefix = "CMUOrganHuman";

    public static bool IsHumanStock(EntityPrototype? proto)
    {
        return proto != null
            && (proto.ID == HumanMeatPrototype || proto.ID.StartsWith(HumanOrganPrefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// <see cref="IsHumanStock"/> plus the CMU human anatomy (CMUPartHuman* limbs and CMUOrganHuman* organs) that
    /// CMU humans actually carry. Used by the Wendigo flesh heal; the cannibal and lab procedure keep IsHumanStock.
    /// </summary>
    public static bool IsHumanFlesh(EntityPrototype? proto)
    {
        if (proto == null)
            return false;

        return IsHumanStock(proto)
            || proto.ID.StartsWith(CMUHumanPartPrefix, StringComparison.Ordinal)
            || proto.ID.StartsWith(CMUHumanOrganPrefix, StringComparison.Ordinal);
    }
}
