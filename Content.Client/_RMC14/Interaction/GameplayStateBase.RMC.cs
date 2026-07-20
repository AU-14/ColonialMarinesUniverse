using Content.Client._RMC14.Interaction;
using Robust.Shared.Graphics;

namespace Content.Client.Gameplay;

public partial class GameplayStateBase
{
    private bool IsRMCInteractionTransparent(EntityUid target, IEye eye)
    {
        return _entitySystemManager
            .GetEntitySystem<RMCClientInteractionSystem>()
            .IsInteractionTransparency(target, _playerManager.LocalEntity, eye);
    }
}
