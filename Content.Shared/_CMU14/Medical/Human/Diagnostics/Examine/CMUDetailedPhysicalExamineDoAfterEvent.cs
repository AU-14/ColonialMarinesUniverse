using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Diagnostics.Examine;

[Serializable, NetSerializable]
public sealed partial class CMUDetailedPhysicalExamineDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed class CMUInspectInjuriesResponseEvent : EntityEventArgs
{
    public readonly NetEntity Target;
    public readonly string TargetName;
    public readonly string Markup;
    public readonly BleedSeverity Bleeding;

    public CMUInspectInjuriesResponseEvent(NetEntity target, string targetName, string markup, BleedSeverity bleeding)
    {
        Target = target;
        TargetName = targetName;
        Markup = markup;
        Bleeding = bleeding;
    }
}
