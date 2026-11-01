using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Xenomorphs.Reaper;

/// <summary>Tracks the current death, independently of whether the xeno has a player or ghost.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUReaperCorpseComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan DiedAt;
}
