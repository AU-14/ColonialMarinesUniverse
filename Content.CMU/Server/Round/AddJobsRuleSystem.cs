using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.Station.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using JetBrains.Annotations;

namespace Content.Server.AU14.Round;

[UsedImplicitly]
public sealed class AddJobsRuleSystem : GameRuleSystem<AddJobsRuleComponent>
{
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    protected override void Started(EntityUid uid, AddJobsRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        if (component.Jobs == null)
            return;
        foreach (var station in EntityManager.EntityQuery<StationJobsComponent>(true))
        {
            foreach (var entry in component.Jobs)
            {
                var jobId = entry.Key;
                var amount = entry.Value;
                _stationJobs.TryAdjustJobSlot(station.Owner, jobId, amount, true, false, station);
            }
        }
    }
}
