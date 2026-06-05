using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Examine;

[Serializable, NetSerializable]
public sealed partial class CMUDetailedPhysicalExamineDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed class CMUInspectInjuriesResponseEvent : EntityEventArgs
{
    public readonly NetEntity Target;
    public readonly string TargetName;
    public readonly string Markup;
    public readonly ExternalBleedTier Bleeding;

    public CMUInspectInjuriesResponseEvent(NetEntity target, string targetName, string markup, ExternalBleedTier bleeding)
    {
        Target = target;
        TargetName = targetName;
        Markup = markup;
        Bleeding = bleeding;
    }
}
