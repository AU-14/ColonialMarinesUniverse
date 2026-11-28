using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CorporateWork;

[Serializable, NetSerializable]
public enum CMUReviewDeskUi : byte
{
    Key,
}

/// <summary>
///     One document on the desk: a heading and its fields, already localized.
/// </summary>
[Serializable, NetSerializable]
public sealed record CMUReviewItem(string Title, List<string> Lines);

[Serializable, NetSerializable]
public readonly record struct CMUReviewDeskTierInfo(string Name, int Items, int Rules, int Reward);

[Serializable, NetSerializable]
public readonly record struct CMUReviewDeskResult(int Correct, int Wrong, int Undecided, int Payout);

[Serializable, NetSerializable]
public sealed class CMUReviewDeskBuiState(
    string title,
    string heading,
    string instructions,
    List<string> options,
    List<CMUReviewDeskTierInfo> tiers,
    int? activeTier,
    List<string> rules,
    List<CMUReviewItem> items,
    TimeSpan nextCaseAt,
    CMUReviewDeskResult? lastResult) : BoundUserInterfaceState
{
    public readonly string Title = title;
    public readonly string Heading = heading;
    public readonly string Instructions = instructions;
    public readonly List<string> Options = options;
    public readonly List<CMUReviewDeskTierInfo> Tiers = tiers;
    public readonly int? ActiveTier = activeTier;
    public readonly List<string> Rules = rules;
    public readonly List<CMUReviewItem> Items = items;
    public readonly TimeSpan NextCaseAt = nextCaseAt;
    public readonly CMUReviewDeskResult? LastResult = lastResult;
}

[Serializable, NetSerializable]
public sealed class CMUReviewDeskStartBuiMsg(int tier) : BoundUserInterfaceMessage
{
    public readonly int Tier = tier;
}

/// <summary>
///     The decision for every document, as an index into the options, or -1 for undecided.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUReviewDeskSubmitBuiMsg(int[] decisions) : BoundUserInterfaceMessage
{
    public readonly int[] Decisions = decisions;
}
