using Content.Shared._CMU14.BalanceRating;

namespace Content.Client._CMU14.BalanceRating;

public sealed partial class CMUBalanceRatingClientSystem : EntitySystem
{
    public void SendRating(long pollId, byte rating)
    {
        if (rating is < 1 or > 5)
            return;

        RaiseNetworkEvent(new CMUBalanceRatingResponseEvent(pollId, rating));
    }
}
