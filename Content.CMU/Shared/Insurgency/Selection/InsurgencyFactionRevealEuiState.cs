using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Insurgency.Selection;

/// <summary>
///     State for the faction reveal popup shown to every cell member once a faction is applied. Plain
///     presentation data: the title, the roleplay style to play, the flavour description, and the flag
///     entity / status icon ids so the client can render their sprites if the author set them.
/// </summary>
[Serializable, NetSerializable]
public sealed class InsurgencyFactionRevealEuiState : EuiStateBase
{
    public string Title { get; }
    public string Description { get; }
    public string Roleplay { get; }
    public string? FlagEntity { get; }
    public string? StatusIcon { get; }

    public InsurgencyFactionRevealEuiState(string title, string description, string roleplay, string? flagEntity, string? statusIcon)
    {
        Title = title;
        Description = description;
        Roleplay = roleplay;
        FlagEntity = flagEntity;
        StatusIcon = statusIcon;
    }
}
