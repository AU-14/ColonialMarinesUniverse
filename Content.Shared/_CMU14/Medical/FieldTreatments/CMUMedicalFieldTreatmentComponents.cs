using System;
using System.Collections.Generic;
using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.FieldTreatments;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class CMUMedicalIngredientComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public CMUFieldTreatmentFamily Family;

    [DataField(required: true), AutoNetworkedField]
    public EntProtoId GauzeProduct;

    [DataField(required: true), AutoNetworkedField]
    public EntProtoId TraumaProduct;

    [DataField, AutoNetworkedField]
    public EntProtoId<SkillDefinitionComponent> Skill = "RMCSkillMedical";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class CMUMedicalMixingBaseComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public CMUFieldTreatmentBaseKind Kind;

    [DataField, AutoNetworkedField]
    public bool ControlsBleeding = true;

    [DataField, AutoNetworkedField]
    public bool StopsArterialBleeding;

    [DataField, AutoNetworkedField]
    public TimeSpan BleedControlDelay;

    [DataField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> InstantBleedControlSkills = new();
}
