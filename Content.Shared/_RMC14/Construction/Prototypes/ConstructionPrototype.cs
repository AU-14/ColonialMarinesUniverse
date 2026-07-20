using Content.Shared._RMC14.Construction.Prototypes;
using Content.Shared._RMC14.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Construction.Prototypes;

public sealed partial class ConstructionPrototype : ICMSpecific
{
    [DataField]
    public bool IsCM { get; private set; }

    [DataField("rmcPrototype")]
    public ProtoId<RMCConstructionPrototype>? RMCPrototype { get; private set; }

    [DataField]
    public Color IconColor = Color.FromHex("#ffffff");
}
