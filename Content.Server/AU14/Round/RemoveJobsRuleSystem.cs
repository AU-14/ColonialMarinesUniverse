using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.Station.Components;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;

namespace Content.Server.AU14.Round;

[UsedImplicitly]
public sealed class RemoveJobsRuleSystem : GameRuleSystem<RemoveJobsRuleComponent>
{
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    protected override void Started(EntityUid uid, RemoveJobsRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var mapId = _gameTicker.DefaultMap;
        var stationUid = _stationSystem.GetStationInMap(mapId);
        if (stationUid == null || !EntityManager.EntityExists(stationUid.Value))
            return;

        var stationJobs = EntityManager.GetComponentOrNull<StationJobsComponent>(stationUid.Value);
        if (stationJobs != null)
        {
            // Only clear jobs that are part of the station's setup (i.e., not jobs added by other gamerules)

                var setupKeys = stationJobs.SetupAvailableJobs.Keys.ToList();
                foreach (var jobKey in setupKeys)
                {
                    _stationJobs.TrySetJobSlot(stationUid.Value, jobKey.ToString(), 0, false, stationJobs);
                }

        }


    }
}
