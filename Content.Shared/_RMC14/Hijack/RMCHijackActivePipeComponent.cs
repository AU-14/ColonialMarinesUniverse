namespace Content.Shared._RMC14.Hijack;

[RegisterComponent]
public sealed partial class RMCHijackActivePipeComponent : Component
{
    [DataField]
    public EntityUid? Map;

    [DataField] // CMU14: warning lifetime belongs to its queued pipe.
    public EntityUid? Warning;
}
