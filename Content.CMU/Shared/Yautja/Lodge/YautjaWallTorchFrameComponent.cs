using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja.Lodge;

// unlit torch item, use it on a wall to mount the real torch
[RegisterComponent]
public sealed partial class YautjaWallTorchFrameComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Mounted;

    // how far off the wall's centre the torch sits, just past the wall's edge so its light isn't inside the occluder
    [DataField]
    public float Offset = 0.55f;
}
