using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CharacterDescription;

[Serializable, NetSerializable]
public sealed class EditCharacterDescriptionEuiState : EuiStateBase
{
    public string ShortExamine = string.Empty;
    public string FullDescription = string.Empty;
    public int MaxShortExamine;
    public int MaxFullDescription;
}

/// <summary>
/// Sent by the client to replace this round's short examine text and full description.
/// </summary>
[Serializable, NetSerializable]
public sealed class EditCharacterDescriptionSaveMessage(string shortExamine, string fullDescription) : EuiMessageBase
{
    public readonly string ShortExamine = shortExamine;
    public readonly string FullDescription = fullDescription;
}
