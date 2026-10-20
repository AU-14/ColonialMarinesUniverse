using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;

namespace Content.Server.CMU14.Round;

[UsedImplicitly]
public sealed partial class AddColonistRuleSystem : GameRuleSystem<AddColonistRuleComponent>
{
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;

    protected override void Started(EntityUid uid, AddColonistRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        var mapId = _gameTicker.DefaultMap;
        var stationUid = _stationSystem.GetStationInMap(mapId);
        if (stationUid == null || !Exists(stationUid.Value))
            return;

        var stationJobs = EntityManager.GetComponentOrNull<StationJobsComponent>(stationUid.Value);
        if (stationJobs == null)
            return;

        if (component.Jobs != null)
            foreach (var jobId in component.Jobs)
                if (!stationJobs.SetupAvailableJobs.ContainsKey(jobId))
                    Logger.GetSawmill("content").Warning($"[AddColonistRuleSystem] {jobId} is not declared by this planet's map, skipping");

        foreach (var (jobId, slots) in stationJobs.SetupAvailableJobs)
        {
            if (component.Jobs != null && !component.Jobs.Contains(jobId))
                continue;

            // RemoveAllJobs zeroes the round-start entry but leaves the map's
            // midround count in [1]; that is the count being restored here.
            var count = slots.Length > 1 ? slots[1] : slots[0];
            if (count < 0)
            {
                _stationJobs.MakeJobUnlimited(stationUid.Value, jobId.ToString(), stationJobs);
                _stationJobs.SetRoundStartJobSlot(stationUid.Value, jobId, -1, stationJobs);
            }
            else
            {
                _stationJobs.TrySetJobSlot(stationUid.Value, jobId.ToString(), count, true, stationJobs);
                _stationJobs.SetRoundStartJobSlot(stationUid.Value, jobId, count, stationJobs);
            }
        }
    }
}
