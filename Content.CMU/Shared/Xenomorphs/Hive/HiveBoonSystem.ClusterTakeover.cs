using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Xenonids.Weeds;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Xenonids.ManageHive.Boons;

public sealed partial class HiveBoonSystem
{
    // the takeover only ran when the weeds reached the tower or the tower broke. a cluster planted early
    // never got another look once the round passed the takeover time, so it had to be rebuilt by hand
    public void RetryClusterTowerTakeovers()
    {
        if (_gameTicker.RoundDuration() < CommunicationTowerXenoTakeoverTime)
            return;

        var towers = EntityQueryEnumerator<CommunicationsTowerComponent>();
        while (towers.MoveNext(out var uid, out var tower))
        {
            if (tower.XenoControlled || tower.State != CommunicationsTowerState.Broken)
                continue;

            var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(uid);
            while (anchored.MoveNext(out var anchoredUid))
            {
                if (!TryComp(anchoredUid, out XenoWeedsComponent? weeds) ||
                    !TryComp(weeds.Source, out HiveClusterComponent? cluster))
                {
                    continue;
                }

                ReplaceCluster((weeds.Source.Value, cluster), (uid, tower));
                break;
            }
        }
    }
}
