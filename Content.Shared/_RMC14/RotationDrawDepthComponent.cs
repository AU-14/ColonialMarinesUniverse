using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using DrawDepthTag = Robust.Shared.GameObjects.DrawDepth;

namespace Content.Shared._RMC14;

[RegisterComponent]
public sealed partial class RotationDrawDepthComponent : Component
{
    [DataField(customTypeSerializer: typeof(ConstantSerializer<DrawDepthTag>))]
    public int DefaultDrawDepth;

    [DataField(customTypeSerializer: typeof(ConstantSerializer<DrawDepthTag>))]
    public int SouthDrawDepth;
}
