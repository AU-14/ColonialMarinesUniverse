using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Roles;

public enum SupplementTarget
{
    Survivors,
    Colonists,
}

/// <summary>
///     Bonus items handed to spawn paths when their party spawns. Skill-gated
///     entries only apply to survivors meeting the skill level.
/// </summary>
[Prototype]
public sealed partial class SurvivorSupplementPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     Only the highest-priority mode-matching supplement applies; ID breaks ties.
    /// </summary>
    [DataField]
    public int Priority { get; private set; }

    /// <summary>
    ///     Which spawn path this applies to: survivors are threat party members,
    ///     colonists are round-start civilian job spawns.
    /// </summary>
    [DataField]
    public SupplementTarget Target { get; private set; } = SupplementTarget.Survivors;

    /// <summary>Game preset ids this applies in; empty means every mode.</summary>
    [DataField]
    public List<string> WhitelistedGamemodes { get; private set; } = new();

    [DataField]
    public List<string> BlacklistedGamemodes { get; private set; } = new();

    [DataField]
    public List<SupplementEntry> Entries { get; private set; } = new();
}

[DataDefinition]
public sealed partial class SupplementEntry
{
    /// <summary>Skill gate; null means the entry applies to everyone.</summary>
    [DataField]
    public EntProtoId<SkillDefinitionComponent>? Skill { get; private set; }

    [DataField]
    public int Level { get; private set; } = 1;

    [DataField]
    public List<EntProtoId> Items { get; private set; } = new();
}
