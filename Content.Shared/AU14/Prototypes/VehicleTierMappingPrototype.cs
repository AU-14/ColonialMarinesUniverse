using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.AU14.Prototypes;

[Prototype]
public sealed class VehicleTierMappingPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField("entity", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Entity { get; private set; } = string.Empty;

    [DataField("tier")]
    public int Tier { get; private set; } = 1;
}
