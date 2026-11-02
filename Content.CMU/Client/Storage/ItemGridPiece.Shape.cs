using Content.Client.Items.Systems;
using Content.Shared.Item;
using Content.Shared.Storage;

namespace Content.Client.UserInterface.Systems.Storage.Controls;

public sealed partial class ItemGridPiece
{
    private readonly List<Box2i> _cmuSourceShape = new();
    private IReadOnlyList<Box2i>? _cmuDrawShape;
    private Angle _cmuShapeRotation;

    private IReadOnlyList<Box2i> CMUGetDrawShape(Entity<StorageComponent?> storage, Entity<ItemComponent?> item)
    {
        var items = _entityManager.System<ItemSystem>();
        var source = items.GetItemShape(storage, item);
        var changed = _cmuDrawShape == null || _cmuShapeRotation != Location.Rotation ||
                      source.Count != _cmuSourceShape.Count;

        // Shapes and storage overrides can change in place (including empty magazines).
        for (var i = 0; !changed && i < source.Count; i++)
            changed = source[i] != _cmuSourceShape[i];

        if (!changed)
            return _cmuDrawShape!;

        _cmuSourceShape.Clear();
        for (var i = 0; i < source.Count; i++)
            _cmuSourceShape.Add(source[i]);

        _cmuShapeRotation = Location.Rotation;
        _cmuDrawShape = Location.Rotation == Angle.Zero
            ? _cmuSourceShape
            : items.GetAdjustedItemShape(storage, item, Location.Rotation, Vector2i.Zero);
        return _cmuDrawShape;
    }
}
