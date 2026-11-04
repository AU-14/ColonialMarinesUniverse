using System;
using System.Linq;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Callsigns;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Vehicle;

// CMU14: Shared interior maps must use the supplying vehicle's faction.
public sealed partial class VehicleSystem
{
    [Dependency] private AccessReaderSystem _interiorAccess = default!;
    [Dependency] private SharedMarineAnnounceSystem _interiorAnnouncements = default!;
    [Dependency] private SharedOverwatchConsoleSystem _interiorOverwatch = default!;
    [Dependency] private SharedTacticalMapSystem _interiorTacticalMap = default!;
    [Dependency] private IPrototypeManager _interiorPrototypes = default!;

    /// <summary>
    /// Remember ownership at spawn, before the vehicle can leave its supplying ship.
    /// Do not change ownership when a stored vehicle is raised on another ship.
    /// </summary>
    public void CaptureInteriorFaction(Entity<VehicleEnterComponent> vehicle)
    {
        if (_net.IsClient || vehicle.Comp.InteriorFaction != null)
            return;

        var parent = vehicle.Owner;
        while (TryComp(parent, out TransformComponent? transform))
        {
            if (TryComp(parent, out ShipFactionComponent? ship) &&
                ship.Faction?.ToLowerInvariant() is Team.GovFor or Team.OpFor)
            {
                vehicle.Comp.InteriorFaction = ship.Faction.ToLowerInvariant();
                return;
            }

            parent = transform.ParentUid;
        }
    }

    private void ConfigureInteriorFaction(Entity<VehicleEnterComponent> vehicle, MapId interiorMap)
    {
        // Also handle mapped vehicles whose ship faction was assigned after MapInit.
        CaptureInteriorFaction(vehicle);
        var faction = vehicle.Comp.InteriorFaction?.ToLowerInvariant();
        if (faction is not (Team.GovFor or Team.OpFor))
            return;

        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var transform))
        {
            if (transform.MapID != interiorMap)
                continue;

            if (TryComp(uid, out AccessReaderComponent? access))
            {
                var groups = access.AccessLists.Select(group => group
                    .Select(id => GetInteriorAccess(id, faction)).ToHashSet()).ToList();
                _interiorAccess.TrySetAccesses((uid, access), groups, updateOriginal: true);
            }

            if (TryComp(uid, out MarineCommunicationsComputerComponent? announcements))
                _interiorAnnouncements.SetComputerFaction((uid, announcements), faction);

            if (TryComp(uid, out OverwatchConsoleComponent? overwatch))
                _interiorOverwatch.SetGroup((uid, overwatch), faction.ToUpperInvariant());

            if (TryComp(uid, out TacticalMapComputerComponent? tactical))
                _interiorTacticalMap.SetComputerFaction((uid, tactical), faction);

            if (TryComp(uid, out AU14CallsignConsoleComponent? callsigns))
            {
                callsigns.Faction = faction;
                Dirty(uid, callsigns);
            }
        }
    }

    private ProtoId<AccessLevelPrototype> GetInteriorAccess(ProtoId<AccessLevelPrototype> access, string faction)
    {
        var source = faction == Team.OpFor ? "AU14AccessGovfor" : "AU14AccessOpfor";
        if (!access.Id.StartsWith(source, StringComparison.Ordinal))
            return access;

        var target = faction == Team.OpFor ? "AU14AccessOpfor" : "AU14AccessGovfor";
        var replacement = new ProtoId<AccessLevelPrototype>(target + access.Id[source.Length..]);
        return _interiorPrototypes.HasIndex(replacement) ? replacement : access;
    }
}
