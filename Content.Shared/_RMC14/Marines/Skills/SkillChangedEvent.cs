using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Marines.Skills;

/// <summary>
/// Raised on an entity when a specific skill level is changed.
/// </summary>
[ByRefEvent]
public readonly record struct SkillChangedEvent(
    EntityUid Uid,
    EntProtoId<SkillDefinitionComponent> Skill,
    int NewLevel
);
