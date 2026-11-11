using Content.Shared.Whitelist;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// "Obtain N items": counts distinct whitelisted entities the owner has taken in hand or equipped
/// since the objective was assigned (<see cref="Obtained"/>) plus whatever it currently carries, minus
/// items already spent on an earlier collect objective (<see cref="EWJoeComponent.SpentCollectibles"/>).
/// Acquisition, not possession: an item that cannot be bagged still counts once it was picked up.
/// Whitelist-driven rather than StealTarget-driven so items count without tagging each one with a
/// networked component (Balance H-6). Firearms are offered, not collected (EWJoeOfferConditionComponent).
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

    /// <summary>Whitelisted items the owner has picked up or equipped since assignment.</summary>
    [DataField]
    public HashSet<EntityUid> Obtained = new();

    /// <summary>The ladder credited this objective, or withdrew it: its items are spent either way.</summary>
    [DataField]
    public bool Credited;

    /// <summary>
    /// When true, the objective is refused if fewer than <see cref="Min"/> matching entities exist on
    /// the owner's planet (its z-level network of maps) at assignment, and the target is clamped to what exists. Colony Fall maps carry
    /// only 0-2 intel items (Balance H-6), so the check keeps pools from offering what cannot be found.
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
