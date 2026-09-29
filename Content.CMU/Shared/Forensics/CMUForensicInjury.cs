using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Forensics;

/// <summary>
/// One line of a forensic injury report: where it is, what it is, and how bad.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public partial record struct CMUForensicInjury
{
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CMUForensicInjurySeverity Severity { get; set; }

    public CMUForensicInjury(string location, string description, CMUForensicInjurySeverity severity)
    {
        Location = location;
        Description = description;
        Severity = severity;
    }
}

[Serializable, NetSerializable]
public enum CMUForensicInjurySeverity : byte
{
    Minor,
    Moderate,
    Severe,
    Critical,
    Missing,
}
