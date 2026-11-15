using Content.Client.UserInterface.Systems.Hands.Controls;
using Content.Shared.CMU14.Surrender;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.UserInterface.Systems.Hands;

public sealed partial class HandsUIController
{
    [UISystemDependency] private SpriteSystem _cmuSurrenderSprites = default!;

    private bool CMUIsSurrendering()
    {
        return _entities.HasComponent<CMUSurrenderComponent>(_player.LocalEntity);
    }

    private void CMUUpdateSurrenderHand(HandButton hand, bool surrendering)
    {
        var raised = surrendering && hand.Entity == null && !hand.Blocked;
        hand.CMUSetSurrendering(raised, _cmuSurrenderSprites);
    }
}
