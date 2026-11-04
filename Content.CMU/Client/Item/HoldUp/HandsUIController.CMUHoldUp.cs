using Content.Shared.CMU14.Item.HoldUp;

namespace Content.Client.UserInterface.Systems.Hands;

public sealed partial class HandsUIController
{
    private void CMUHoldUpItem(string handName)
    {
        if (!_handsSystem.TryGetPlayerHands(out var hands) ||
            !_handsSystem.TryGetHeldItem(hands.Value.AsNullable(), handName, out _))
        {
            return;
        }

        _entities.EntityNetManager.SendSystemNetworkMessage(new CMUHoldUpItemEvent(handName));
    }
}
