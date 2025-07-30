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
    [Dependency] private readonly AuRoundSystem _auRoundSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        // Removed duplicate event subscription to GameRuleAddedEvent
    }

    protected override void Started(EntityUid uid, AddJobsRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        if (component.Jobs == null)
            return;

        if (component.AddToShip)
        {
            string? shipFaction = component.ShipFaction;
            foreach (var station in EntityManager.EntityQuery<StationJobsComponent>(true))
            {
                var stationUid = station.Owner;

                if (EntityManager.TryGetComponent<ShipFactionComponent>(stationUid, out var shipComp))
                {
                    if (!string.IsNullOrEmpty(shipFaction) && !string.Equals(shipComp.Faction, shipFaction, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                else if (!string.IsNullOrEmpty(shipFaction))
                {
                    // If a specific faction is requested, add ShipFactionComponent if missing
                    shipComp = AddComp<ShipFactionComponent>(stationUid);
                    shipComp.Faction = shipFaction;
                }
                foreach (var entry in component.Jobs)
                {
                    var jobId = entry.Key;
                    var amount = entry.Value;
                    _stationJobs.TryAdjustJobSlot(stationUid, jobId, amount, true, false, station);
                }
                // If a specific ship was targeted, only add jobs to the first match
                // Ensure the ship gets the BecomesStationComponent
                if (!EntityManager.HasComponent<BecomesStationComponent>(stationUid))
                {
                    AddComp<BecomesStationComponent>(stationUid);
                }
                if (!string.IsNullOrEmpty(shipFaction))
                    return;
            }
        }
        else
        {
            // Default: add jobs to all stations
            foreach (var station in EntityManager.EntityQuery<StationJobsComponent>(true))
            {
                var stationUid = station.Owner; // Replace with correct UID if available
                foreach (var entry in component.Jobs)
                {
                    var jobId = entry.Key;
                    var amount = entry.Value;
                    _stationJobs.TryAdjustJobSlot(stationUid, jobId, amount, true, false, station);
                }
            }
        }
    }
}
