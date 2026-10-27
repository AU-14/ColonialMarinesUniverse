namespace Content.Client.Chat.UI;

public abstract partial class SpeechBubble
{
    // Shared with the owner so hidden bubbles and deleted senders cannot retain controls indefinitely.
    internal bool CMUHasExpired => Disposed || _dead ||
                                  _entityManager.Deleted(_senderEntity) || _timing.RealTime >= _deathTime;

    internal void CMURetire()
    {
        // Cancel any pending deferred death notification and release the owner's subscription.
        _dead = true;
        OnDied = null;
        if (!Disposed)
            Orphan();
    }
}
