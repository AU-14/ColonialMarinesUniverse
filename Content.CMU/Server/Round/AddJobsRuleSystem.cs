using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.Station.Components;
using Content.Shared.AU14.util;
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

    protected override void Started(EntityUid uid, AddJobsRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        PlatoonPrototype? platoon = null;
        if (!string.IsNullOrEmpty(component.ShipFaction))
        {
            var protoManager = IoCManager.Resolve<IPrototypeManager>();
            // Try to get the selected platoon for the faction (case-insensitive)
            foreach (var proto in protoManager.EnumeratePrototypes<PlatoonPrototype>())
            {
                // Platoon IDs are not strictly tied to govfor/opfor, so match by ShipFaction if possible
                if (proto.ID.Equals(component.ShipFaction, StringComparison.OrdinalIgnoreCase))
                {
                    platoon = proto;
                    break;
                }
            }
        }

        if (component.Jobs == null)
            return;

        Dictionary<ProtoId<JobPrototype>, int>? slotOverride = null;
        if (platoon != null)
        {
            if (!string.IsNullOrEmpty(component.ShipFaction))
            {
                if (component.ShipFaction.Equals("govfor", StringComparison.OrdinalIgnoreCase))
                    slotOverride = platoon.JobSlotOverrideGovfor;
                else if (component.ShipFaction.Equals("opfor", StringComparison.OrdinalIgnoreCase))
                    slotOverride = platoon.JobSlotOverrideOpfor;
            }
        }

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

                // If slotOverride is present, remove all jobs not in slotOverride
                if (slotOverride != null && slotOverride.Count > 0)
                {
                    var jobsToRemove = station.JobList.Keys.Where(jobId => !slotOverride.ContainsKey(jobId)).ToList();
                    foreach (var jobId in jobsToRemove)
                    {
                        _stationJobs.TryAdjustJobSlot(stationUid, jobId.ToString(), 0, true, false, station);
                    }
                }

                foreach (var entry in component.Jobs)
                {
                    var jobId = entry.Key;
                    var amount = entry.Value;
                    // Only allow jobs in slotOverride if present
                    if (slotOverride != null && slotOverride.Count > 0 && !slotOverride.ContainsKey(jobId))
                        continue;
                    if (slotOverride != null && slotOverride.TryGetValue(jobId, out var overrideAmount))
                        amount = overrideAmount;
                    _stationJobs.TryAdjustJobSlot(stationUid, jobId.ToString(), amount, true, false, station);
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

                // If slotOverride is present, remove all jobs not in slotOverride
                if (slotOverride != null && slotOverride.Count > 0)
                {
                    var jobsToRemove = station.JobList.Keys.Where(jobId => !slotOverride.ContainsKey(jobId)).ToList();
                    foreach (var jobId in jobsToRemove)
                    {
                        _stationJobs.TryAdjustJobSlot(stationUid, jobId.ToString(), 0, true, false, station);
                    }
                }

                foreach (var entry in component.Jobs)
                {
                    var jobId = entry.Key;
                    var amount = entry.Value;
                    // Only allow jobs in slotOverride if present
                    if (slotOverride != null && slotOverride.Count > 0 && !slotOverride.ContainsKey(jobId))
                        continue;
                    if (slotOverride != null && slotOverride.TryGetValue(jobId, out var overrideAmount))
                        amount = overrideAmount;
                    _stationJobs.TryAdjustJobSlot(stationUid, jobId.ToString(), amount, true, false, station);
                }
            }
        }
    }
}
