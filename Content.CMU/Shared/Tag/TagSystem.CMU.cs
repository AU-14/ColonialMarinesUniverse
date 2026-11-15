namespace Content.Shared.Tag;

public sealed partial class TagSystem
{
    private void CMUNotifyTagsChanged(EntityUid uid)
    {
        var ev = new CMUTagsChangedEvent();
        RaiseLocalEvent(uid, ref ev);
    }
}

/// <summary>Local notification after TagSystem changes an entity's tags; never sent over the network.</summary>
[ByRefEvent]
public readonly record struct CMUTagsChangedEvent;
