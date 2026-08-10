using Content.Shared.CMU.Round;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared.AU14.util;

[Prototype]
public sealed partial class PlatoonVendorSetPrototype : IPrototype, IInheritingPrototype
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<PlatoonVendorSetPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    [AlwaysPushInheritance]
    public Dictionary<PlatoonMarkerClass, EntProtoId> Vendors { get; private set; } = new();

    /// <summary>
    /// Final presentation defaults and sparse force overrides keyed by semantic purpose.
    /// </summary>
    [DataField]
    [AlwaysPushInheritance]
    public Dictionary<RoundSetupSlot, ResPath> Presentations { get; private set; } = new();
}
