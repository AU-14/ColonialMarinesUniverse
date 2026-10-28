using Content.Shared.Interaction;

namespace Content.Client.ContextMenu.UI;

public sealed partial class EntityMenuElement
{
    private (EntityUid Player, EntityUid Target)? _cmuExaminePair;
    private SharedInteractionSystem.Ignored? _cmuExaminePredicate;

    public SharedInteractionSystem.Ignored CMUGetExaminePredicate(EntityUid player, EntityUid target)
    {
        if (_cmuExaminePair == (player, target))
            return _cmuExaminePredicate!;

        _cmuExaminePair = (player, target);
        // Capture immutable IDs, never a mutable controller-wide "current target".
        // Visibility and LOS are still evaluated on every frame.
        return _cmuExaminePredicate = CMUCreateExaminePredicate(player, target);
    }

    private static SharedInteractionSystem.Ignored CMUCreateExaminePredicate(EntityUid player, EntityUid target) =>
        entity => entity == player || entity == target;
}
