using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.Station.Components;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;

namespace Content.Server.AU14.Round;

[UsedImplicitly]
public sealed partial class RemoveJobsRuleSystem : GameRuleSystem<RemoveJobsRuleComponent>
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSystem _stationSystem = default!;

    protected override void Started(EntityUid uid, RemoveJobsRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var mapId = _gameTicker.DefaultMap;
        var stationUid = _stationSystem.GetStationInMap(mapId);
        if (stationUid == null || !Exists(stationUid.Value))
            return;

        var stationJobs = EntityManager.GetComponentOrNull<StationJobsComponent>(stationUid.Value);
        if (stationJobs != null)
        {
            // Only clear jobs that are part of the station's setup (i.e., not jobs added by other gamerules)
            var setupKeys = stationJobs.SetupAvailableJobs.Keys.ToList();
            foreach (var jobKey in setupKeys)
            {
                // Clear both the active job list and the round-start setup slots.
                _stationJobs.TrySetJobSlotAndRoundStart(stationUid.Value,
                    jobKey,
                    0,
                    false,
                    stationJobs);
            }
        }
    }
}
