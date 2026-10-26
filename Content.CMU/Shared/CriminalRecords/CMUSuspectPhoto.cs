using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CriminalRecords;

/// <summary>
///     A photo of someone printed from a records console: their mugshot from when they arrived, with their name and
///     description. Opening it shows the photo.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUSuspectPhotoComponent : Component
{
    public static readonly EntProtoId Prototype = "CMUSuspectPhoto";

    [DataField, AutoNetworkedField]
    public string Name = string.Empty;

    [DataField, AutoNetworkedField]
    public string Description = string.Empty;

    [DataField, AutoNetworkedField]
    public CMURecordPortrait? Portrait;
}

[Serializable, NetSerializable]
public enum CMUSuspectPhotoUiKey : byte
{
    Key,
}
