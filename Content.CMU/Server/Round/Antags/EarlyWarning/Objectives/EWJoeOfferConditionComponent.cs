using Content.Shared.Whitelist;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// "Offer N items": counts whitelisted items the Joe consumed with the Offer action while this objective
/// was unfinished. An offered item is deleted, so the count is kept here rather than as a set of
/// entities, and an item can never count twice.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeOfferConditionComponent : Component
{
    /// <summary>What may be offered. Components and tags are both honoured.</summary>
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    /// <summary>Inclusive bounds the target count is rolled from.</summary>
    [DataField]
    public int Min = 1;

    [DataField]
    public int Max = 1;

    /// <summary>The rolled target. Set on assignment.</summary>
    [DataField]
    public int Target;

    /// <summary>Items offered toward this objective so far.</summary>
    [DataField]
    public int Offered;

    /// <summary>Title locale id; receives "count".</summary>
    [DataField(required: true)]
    public LocId Title;

    /// <summary>Description locale id; receives "count".</summary>
    [DataField(required: true)]
    public LocId Description;
}
