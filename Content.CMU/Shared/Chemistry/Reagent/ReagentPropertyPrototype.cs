/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.

using Content.Shared._RMC14.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Content.Shared._RMC14.Chemistry.Effects;

namespace Content.Shared._CMU14.Chemistry.Reagent;


[Flags]
public enum ReagentPropertyTypeEnum
{
    All = 0,
    Medicine = 1,
    Toxicant = 2,
    Stimulant = 4,
    Reactant = 8,
    Irritant = 16,
    Metabolite = 32,
    Anomalous = 64,
    Unadjustable = 128,
    Catalyst = 256,
    Combustible = 512
}
public enum ReagentPropertyRarityEnum
{
    Disabled = 0, // Doesn't spawn
    Common = 1,
    Uncommon = 2,
    Rare = 3,
    Legendary = 4,
    Admin = 5 // Doesn't spawn naturally (redundant?)
}

public enum ReagentPropertyHintEnum
{
    Negative = 0,
    Neutral = 1,
    Positive = 2,
    Rare = 3,
    Legendary = 4,
    Disabled = 5
}

[Prototype, DataDefinition, Virtual]
public partial class ReagentPropertyPrototype : IPrototype, IInheritingPrototype, ICMSpecific
{
    [ViewVariables, IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    private LocId Name { get; set; }

    [ViewVariables(VVAccess.ReadOnly)]
    public string LocalizedName => Loc.GetString(Name);

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public bool GenerationDisabled = false;

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public bool Starter = false;

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public string Code = "000";

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<ReagentPropertyPrototype>))]
    public string[]? Parents { get; private set; }

    [ViewVariables(VVAccess.ReadOnly), DataField(required:true)]
    public ReagentPropertyTypeEnum Category = ReagentPropertyTypeEnum.All;

    [ViewVariables(VVAccess.ReadOnly), DataField(required: true)]
    public ReagentPropertyRarityEnum Rarity = ReagentPropertyRarityEnum.Disabled;

    [ViewVariables(VVAccess.ReadOnly), DataField(required: true)]
    public ReagentPropertyHintEnum Hint = ReagentPropertyHintEnum.Disabled;

    // VVLOW 0.1
    // VLOW 0.25
    // LOW 0.5
    // MEDIUMLOW 1.5
    // MEDIUM 2
    // HIGH 3
    // VHIGH 5
    // VVHIGH 6
    // HIGHEXTREMEINTER 7.5
    // EXTREME 10
    [ViewVariables(VVAccess.ReadOnly), DataField(required: true)]
    public float Potency = 1f;

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public int Level = 1;

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public int Value = 0;

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public bool CostPenalty = true;

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    [DataField("desc", required: true)]
    private LocId Description { get; set; }

    [ViewVariables(VVAccess.ReadOnly)]
    public string LocalizedDescription => Loc.GetString(Description);

    [DataField]
    public bool IsCM { get; set; }
}
