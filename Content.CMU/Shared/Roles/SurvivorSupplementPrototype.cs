using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Roles;

/// <summary>
///     Bonus items handed to Distress Signal roundstart survivors when their party spawns.
///     Skill-gated entries only apply to survivors meeting the skill level.
/// </summary>
[Prototype("survivorSupplement")]
public sealed partial class SurvivorSupplementPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     Supplements conflict: only the highest-priority mode-matching one applies per survivor.
    ///     ID breaks ties so the pick stays deterministic.
    /// </summary>
    [DataField]
    public int Priority { get; private set; }

    /// <summary>
    ///     Game preset ids this supplement applies in. Empty means every mode.
    /// </summary>
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
    /// <summary>
    ///     Skill gate; null means the entry applies to every survivor.
    /// </summary>
    [DataField]
    public EntProtoId<SkillDefinitionComponent>? Skill { get; private set; }

    [DataField]
    public int Level { get; private set; } = 1;

    [DataField]
    public List<EntProtoId> Items { get; private set; } = new();
}
