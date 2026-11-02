using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.TacticalMap.Scout;

[Serializable, NetSerializable]
public enum CMUScoutContactType : byte
{
    Unknown,
    Infantry,
    Vehicle,
    Emplacement,
}

[Serializable, NetSerializable]
public readonly record struct CMUScoutReportInfo(
    string Reporter,
    CMUScoutContactType ContactType,
    TimeSpan ReportedAt,
    TimeSpan ExpiresAt)
{
    public static string ContactName(CMUScoutContactType type) => Loc.GetString(type switch
    {
        CMUScoutContactType.Infantry => "cmu-scout-contact-infantry",
        CMUScoutContactType.Vehicle => "cmu-scout-contact-vehicle",
        CMUScoutContactType.Emplacement => "cmu-scout-contact-emplacement",
        _ => "cmu-scout-contact-unknown",
    });
}

public sealed partial class CMUScoutReportActionEvent : WorldTargetActionEvent
{
    [DataField]
    public CMUScoutContactType ContactType;
}
