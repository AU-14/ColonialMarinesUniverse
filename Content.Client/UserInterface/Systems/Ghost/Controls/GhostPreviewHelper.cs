using Robust.Client.Player;

namespace Content.Client.UserInterface.Systems.Ghost.Controls;

internal static class GhostPreviewHelper
{
    public static bool CanUseLiveSprite(
        IEntityManager entityManager,
        IPlayerManager playerManager,
        EntityUid target)
    {
        if (playerManager.LocalEntity is not { } local)
            return false;

        if (!entityManager.TryGetComponent(local, out TransformComponent? localXform) ||
            !entityManager.TryGetComponent(target, out TransformComponent? targetXform))
        {
            return false;
        }

        return localXform.MapID == targetXform.MapID;
    }
}
