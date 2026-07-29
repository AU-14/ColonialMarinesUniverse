using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.FactionRoster;

[Serializable, NetSerializable]
public enum FactionRosterConsoleUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FactionRosterEntry
{
    public string Name = string.Empty;
    public string Job = string.Empty;

    public string ShortExamine = string.Empty;
    public string FullDescription = string.Empty;
    public string MedicalRecord = string.Empty;
    public string CriminalRecord = string.Empty;
    public string GeneralRecord = string.Empty;
    public string Height = string.Empty;
    public int Weight;
    public string SkinToneName = string.Empty;
    public string HairColorName = string.Empty;
    public int Age;
    public string Build = string.Empty;
    public string EyeColorName = string.Empty;
    public string? Allegiance;
    public string? Origin;
}

[Serializable, NetSerializable]
public sealed class FactionRosterConsoleBuiState : BoundUserInterfaceState
{
    public List<FactionRosterEntry> Entries;

    public FactionRosterConsoleBuiState(List<FactionRosterEntry> entries)
    {
        Entries = entries;
    }
}

[Serializable, NetSerializable]
public sealed class FactionRosterConsoleRefreshBuiMsg : BoundUserInterfaceMessage
{
}
