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

    public static bool IsHumanStock(EntityPrototype? proto)
    {
        return proto != null
            && (proto.ID == HumanMeatPrototype || proto.ID.StartsWith(HumanOrganPrefix, StringComparison.Ordinal));
    }
}
