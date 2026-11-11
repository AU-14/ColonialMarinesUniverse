using Robust.Shared.Map;

namespace Content.Client.UserInterface.Controls;

public abstract partial class SlotControl
{
    private EntityUid? _cmuHoverEntity;

    public EntityUid CMUGetHoverEntity()
    {
        if (_cmuHoverEntity is { } existing &&
            !_entities.Deleted(existing) && !_entities.IsQueuedForDeletion(existing))
            return existing;

        _cmuHoverEntity = _entities.SpawnEntity("hoverentity", MapCoordinates.Nullspace);
        return _cmuHoverEntity.Value;
    }

    private void CMUClearHover()
    {
        // The sprite view only borrows entities; the slot owns this preview even
        // when the sprite has been removed or deferred deletion has started.
        if (_cmuHoverEntity is { } entity && !_entities.Deleted(entity))
            _entities.QueueDeleteEntity(entity);

        _cmuHoverEntity = null;
        HoverSpriteView.SetEntity(null);
    }

    protected override void ExitedTree()
    {
        ClearHover();
        MouseIsHovering = false;
        base.ExitedTree();
    }

    [Obsolete("Controls should only be removed from UI tree instead of being disposed")]
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ClearHover();
        base.Dispose(disposing);
    }
}
