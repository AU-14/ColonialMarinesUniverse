using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes; // CMU14

namespace Content.Shared._RMC14.Marines.Skills;

[DataRecord, Serializable, NetSerializable] // CMU14
public readonly partial record struct Skill(EntProtoId<SkillDefinitionComponent> Type, int Level);
