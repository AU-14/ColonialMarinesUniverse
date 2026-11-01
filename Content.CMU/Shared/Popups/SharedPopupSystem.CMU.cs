namespace Content.Shared.Popups;

public abstract partial class SharedPopupSystem
{
    /// <summary>
    /// Shows a popup above the recipient, visible only to that recipient.
    /// </summary>
    public void PopupSelf(string? message, EntityUid? recipient, PopupType type = PopupType.Small)
    {
        if (recipient is { } uid)
            PopupEntity(message, uid, recipient, type);
    }

    /// <summary>
    /// Broadcasts a popup while respecting the actor's preference for action notifications.
    /// </summary>
    public void PopupBroadcast(string? message, EntityUid uid, EntityUid? recipient, PopupType type = PopupType.Small)
    {
        if (recipient.HasValue && !ShouldBroadcastToOthers(recipient.Value))
            return;

        PopupEntity(message, uid, type);
    }
}
