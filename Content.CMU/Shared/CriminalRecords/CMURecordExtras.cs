using Content.Shared.Humanoid;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CriminalRecords;

/// <summary>
///     How someone looked when they arrived this round, for the mugshot on their record: drawn in their underwear, from
///     their character's body and appearance.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class CMURecordPortrait
{
    /// <summary>Only used to draw the right body; the record doesn't list it.</summary>
    [DataField]
    public string Species = string.Empty;

    [DataField]
    public Sex Sex = Sex.Male;

    [DataField]
    public HumanoidCharacterAppearance Appearance = new();
}

/// <summary>A warrant issued against someone from a records console.</summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial record CMURecordWarrant
{
    [DataField]
    public string Charge = string.Empty;

    [DataField]
    public string IssuedBy = string.Empty;

    [DataField]
    public TimeSpan IssuedAt;
}

/// <summary>An arrest logged on someone's record: what for, how long they're held and who arrested them.</summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial record CMURecordArrest
{
    [DataField]
    public string Charges = string.Empty;

    [DataField]
    public int SentenceMinutes;

    [DataField]
    public string Officer = string.Empty;

    [DataField]
    public TimeSpan ArrestedAt;
}

[Serializable, NetSerializable]
public sealed class CMURecordIssueWarrantMsg(string charge) : BoundUserInterfaceMessage
{
    public readonly string Charge = charge;
}

[Serializable, NetSerializable]
public sealed class CMURecordRevokeWarrantMsg(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public sealed class CMURecordLogArrestMsg(string charges, int sentenceMinutes) : BoundUserInterfaceMessage
{
    public readonly string Charges = charges;
    public readonly int SentenceMinutes = sentenceMinutes;
}

[Serializable, NetSerializable]
public sealed class CMURecordDeleteArrestMsg(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

/// <summary>Asks for the selected record's personnel file, the same dossier the personnel roster shows.</summary>
[Serializable, NetSerializable]
public sealed class CMURecordOpenPersonnelMsg : BoundUserInterfaceMessage;

/// <summary>The personnel file asked for, sent back to whoever asked.</summary>
[Serializable, NetSerializable]
public sealed class CMURecordPersonnelMsg(Content.Shared.CMU14.FactionRoster.FactionRosterEntry entry) : BoundUserInterfaceMessage
{
    public readonly Content.Shared.CMU14.FactionRoster.FactionRosterEntry Entry = entry;
}

/// <summary>Prints the selected criminal record on paper at the console.</summary>
[Serializable, NetSerializable]
public sealed class CMURecordPrintRecordMsg : BoundUserInterfaceMessage;

/// <summary>Prints a photo of the selected record's person, with their name and description, at the console.</summary>
[Serializable, NetSerializable]
public sealed class CMURecordPrintPhotoMsg : BoundUserInterfaceMessage;
