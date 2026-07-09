using System;
using System.Collections.Generic;
using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Treatment.FieldCare;

[RegisterComponent]
public sealed partial class CMUMedicalIngredientComponent : Component
{
    [DataField(required: true)]
    public CMUFieldTreatmentFamily Family;

    [DataField(required: true)]
    public EntProtoId GauzeProduct;

    [DataField(required: true)]
    public EntProtoId TraumaProduct;

    [DataField]
    public EntProtoId<SkillDefinitionComponent> Skill = "RMCSkillMedical";
}

[RegisterComponent]
public sealed partial class CMUMedicalMixingBaseComponent : Component
{
    [DataField(required: true)]
    public CMUFieldTreatmentBaseKind Kind;

    [DataField]
    public bool ControlsBleeding = true;

    [DataField]
    public bool StopsArterialBleeding;

    [DataField]
    public TimeSpan BleedControlDelay;

    [DataField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> InstantBleedControlSkills = new();
}
