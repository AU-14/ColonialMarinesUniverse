using Content.Shared.Actions;

namespace Content.Shared._CMU14.Xenonids.ParalyzingSlash;

public sealed partial class CMUXenoParalyzingSlashActionEvent : InstantActionEvent
{
    [DataField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public bool SuperSlow;

}