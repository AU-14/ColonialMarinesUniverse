using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Anatomy.BodyParts;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMURoboticLimbComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public LocId MaterialName = "cmu-robotic-limb-material-synthetic";

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<HumanoidVisualLayers, ProtoId<HumanoidSpeciesSpriteLayer>> BaseLayers = new();

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId? ChildPrototype;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public string? ChildSlot;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 BruteDamage;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 BurnDamage;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 WelderRepairAmount = 15;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 CableRepairAmount = 15;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 FuelUsed = 5;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<ToolQualityPrototype> RepairQuality = "Welding";

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId<SkillDefinitionComponent> RepairSkill = "RMCSkillEngineer";

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan RepairTime = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan SelfRepairTime = TimeSpan.FromSeconds(30);
}

[RegisterComponent]
[Access(typeof(SharedCMURoboticLimbSystem))]
public sealed partial class CMURoboticLimbOverlayComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public readonly Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo?> OriginalLayers = new();
}
