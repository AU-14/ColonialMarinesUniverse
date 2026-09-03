using Robust.Shared.Prototypes;

namespace Content.Shared.Camera;

[Prototype("cameraNetwork")]
public sealed partial class CameraNetworkPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public LocId Name { get; private set; } = default!;
    [DataField] public bool Configurable { get; private set; }
}
