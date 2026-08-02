using Content.Shared._RMC14.Construction.Prototypes;
using Content.Shared._RMC14.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Construction.Prototypes;

public sealed partial class ConstructionPrototype : IInheritingPrototype, ICMSpecific
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<ConstructionPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    [DataField]
    public bool IsCM { get; private set; }

    [DataField("rmcPrototype")]
    public ProtoId<RMCConstructionPrototype>? RMCPrototype { get; private set; }

    [DataField]
    public Color IconColor = Color.FromHex("#ffffff");

    /// <summary>
    /// AU14: which spawnlist (top-level group in the improved construction menu's left tree) this
    /// recipe belongs to. Empty is treated as the default "AU14" spawnlist. Set by the in-game
    /// construction-menu editor on generated entries.
    /// </summary>
    [DataField]
    public string Spawnlist = string.Empty;
}
