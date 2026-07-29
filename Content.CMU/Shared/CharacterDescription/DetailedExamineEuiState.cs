using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.CharacterDescription;

[Serializable, NetSerializable]
public sealed class DetailedExamineEuiState : EuiStateBase
{
    public NetEntity Target;
    public string Name = string.Empty;

    public string FullDescription = string.Empty;
    public string? MedicalRecord;
    public string? CriminalRecord;
    public string? GeneralRecord;
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
