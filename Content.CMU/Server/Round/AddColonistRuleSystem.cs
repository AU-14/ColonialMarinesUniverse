using System.Collections.Generic;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round;

[UsedImplicitly]
public sealed partial class AddColonistRuleSystem : GameRuleSystem<AddColonistRuleComponent>
{
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IRobustRandom _random = default!;

    private ISawmill _logs = default!;

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("addcolonist");
    }

    protected override void Started(EntityUid uid, AddColonistRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        var mapId = _gameTicker.DefaultMap;
        var stationUid = _stationSystem.GetStationInMap(mapId);
        if (stationUid == null || !Exists(stationUid.Value))
            return;

        var stationJobs = EntityManager.GetComponentOrNull<StationJobsComponent>(stationUid.Value);
        if (stationJobs == null)
            return;

        // Slots only ever go to jobs the planet map declares, so undeclared
        // roster entries cannot create spawnless slots.
        var counts = new Dictionary<ProtoId<JobPrototype>, int>(component.Jobs);
        var candidates = component.RandomJobs
            .Where(job => stationJobs.SetupAvailableJobs.ContainsKey(job))
            .ToList();
        if (component.RandomJobs.Count > 0)
        {
            if (candidates.Count == 0)
                _logs.Warning("no randomJobs candidate is declared by this planet's map");
            else
                counts[_random.Pick(candidates)] = 1;
        }

        foreach (var (jobId, count) in counts)
        {
            if (!stationJobs.SetupAvailableJobs.ContainsKey(jobId))
            {
                _logs.Warning($"{jobId} is not declared by this planet's map, skipping");
                continue;
            }

            _stationJobs.TrySetJobSlot(stationUid.Value, jobId.ToString(), count, true, stationJobs);
            _stationJobs.SetRoundStartJobSlot(stationUid.Value, jobId, count, stationJobs);
        }
    }
}
