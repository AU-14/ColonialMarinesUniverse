using Content.Shared.Whitelist;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// "Obtain N items": counts whitelisted entities anywhere in the owner's own container tree
/// (hands, inventory, bags). Whitelist-driven rather than StealTarget-driven so firearms count
/// without tagging every gun on the map with a networked component (Balance H-6).
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeCollectConditionComponent : Component
{
    /// <summary>What counts. Components and tags are both honoured.</summary>
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

    /// <summary>
    /// When true, the objective is refused if fewer than <see cref="Min"/> matching entities exist on
    /// the owner's planet (its z-level network of maps) at assignment, and the target is clamped to what exists. Colony Fall maps carry
    /// 0-2 intel items but 6-34 firearms (Balance H-6), so pools can offer both safely.
    /// </summary>
    [DataField]
    public bool VerifyExistence = true;

    /// <summary>Title locale id; receives "count".</summary>
    [DataField(required: true)]
    public LocId Title;

    /// <summary>Description locale id; receives "count".</summary>
    [DataField(required: true)]
    public LocId Description;
}
