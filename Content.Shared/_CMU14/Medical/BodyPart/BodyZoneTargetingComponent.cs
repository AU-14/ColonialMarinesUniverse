using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.BodyPart;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedBodyZoneTargetingSystem))]
public sealed partial class BodyZoneTargetingComponent : Component
{
    [DataField, AutoNetworkedField]
    public TargetBodyZone Selected = TargetBodyZone.Chest;

    /// <summary>
    ///     Zero is the sentinel for "never clicked" — resolver returns null so a
    ///     fresh entity doesn't auto-target chest before its first pick.
    /// </summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan LastSelectedAt;

    [DataField, AutoNetworkedField]
    public float MeleeAccuracy = 0.65f;

    [DataField, AutoNetworkedField]
    public float RangedBaseAccuracy = 0.15f;

    [DataField, AutoNetworkedField]
    public float RangedSkillBonus = 0.15f;

    [DataField, AutoNetworkedField]
    public float MeleeRangeTiles = 1.5f;

    [DataField, AutoNetworkedField]
    public EntProtoId<SkillDefinitionComponent> RangedSkill = "RMCSkillFirearms";
}
