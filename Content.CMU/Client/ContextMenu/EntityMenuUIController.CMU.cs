using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Map;

namespace Content.Client.ContextMenu.UI;

public sealed partial class EntityMenuUIController
{
    private List<EntityUid>? _cmuVisibilitySnapshot = new();

    private void CMUUpdateVisibility(EntityUid player, MenuVisibility visibility, ExaminerComponent? examiner)
    {
        // Removing a row can collapse its group or close the menu. Borrow the
        // snapshot exclusively so callbacks (including reentrant updates) cannot mutate it.
        var snapshot = _cmuVisibilitySnapshot ?? new List<EntityUid>();
        _cmuVisibilitySnapshot = null;
        try
        {
            snapshot.AddRange(Elements.Keys);
            foreach (var entity in snapshot)
            {
                if (!Elements.TryGetValue(entity, out var element))
                    continue;

                if (!_xformQuery.TryGetComponent(entity, out var xform) ||
                    (visibility & MenuVisibility.Invisible) == 0 &&
                    _spriteQuery.TryGetComponent(entity, out var sprite) && !sprite.Visible)
                {
                    RemoveEntity(entity);
                    continue;
                }

                if ((visibility & MenuVisibility.NoFov) != 0)
                    continue;

                var pos = new MapCoordinates(_xform.GetWorldPosition(xform, _xformQuery), xform.MapID);
                if (!_examineSystem.CanExamine(player, pos, element.CMUGetExaminePredicate(player, entity), entity, examiner))
                    RemoveEntity(entity);
            }
        }
        finally
        {
            snapshot.Clear();
            _cmuVisibilitySnapshot ??= snapshot;
        }
    }
}
