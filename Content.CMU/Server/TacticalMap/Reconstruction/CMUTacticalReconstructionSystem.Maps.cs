using System.Linq;
using Content.Shared.CMU14;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUTacticalReconstructionSystem
{
    private readonly record struct MapTargets(EntityUid? Planet, EntityUid? Ship, bool AboardShip);

    private EntityUid? MapRoot(EntityUid? map)
    {
        if (map is not { } uid || TerminatingOrDeleted(uid)) return null;
        if (TryComp<CMUZLevelMapComponent>(uid, out var level) &&
            TryComp<CMUZLevelsNetworkComponent>(level.NetworkUid, out var network))
        {
            IReadOnlyDictionary<int, EntityUid?> levels = network.ZLevels;
            if (levels.TryGetValue(0, out var ground) && ground != null) return ground;
            return network.ZLevels.OrderBy(p => p.Key).Select(p => p.Value).FirstOrDefault(p => p != null);
        }
        return uid;
    }

    private MapTargets GetMapTargets(EntityUid actor, EntityUid? boundMap, string faction)
    {
        var location = MapRoot(Transform(actor).MapUid);
        var aboardShip = HasComp<ShipFactionComponent>(location);
        var planet = MapRoot(boundMap);
        if (HasComp<ShipFactionComponent>(planet)) planet = null;
        if (planet == null && EntityManager.System<Content.Server._RMC14.TacticalMap.TacticalMapSystem>().TryGetTacticalMap(out var tactical))
            planet = MapRoot(tactical.Owner);
        if (planet == null && !aboardShip) planet = location;

        EntityUid? ship = aboardShip ? location : null;
        if (ship == null)
        {
            var ships = EntityQueryEnumerator<ShipFactionComponent>();
            while (ships.MoveNext(out var uid, out var candidate))
            {
                if (faction == null || SharedTacticalMapSystem.NormalizeMapFaction(candidate.Faction) != faction) continue;
                ship = MapRoot(Transform(uid).MapUid);
                if (ship != null) break;
            }
        }
        return new MapTargets(planet, ship, aboardShip);
    }

    private bool TryMapLayout(EntityUid root, out EntityUid networkUid, out int min, out EntityUid?[] maps)
    {
        networkUid = root;
        min = 0;
        maps = [];
        if (TerminatingOrDeleted(root)) return false;
        if (!TryComp<CMUZLevelMapComponent>(root, out var level))
        {
            if (!HasComp<MapGridComponent>(root)) return false;
            maps = [root];
            return true;
        }
        if (!TryComp<CMUZLevelsNetworkComponent>(level.NetworkUid, out var network) || network.ZLevels.Count == 0) return false;
        min = network.ZLevels.Keys.Min();
        var count = (long) network.ZLevels.Keys.Max() - min + 1;
        if (count <= 0 || count > CMUReconGeometry.MaxLevels) return false;
        networkUid = level.NetworkUid;
        maps = new EntityUid?[(int) count];
        foreach (var (depth, map) in network.ZLevels)
            if (map is { } uid && !TerminatingOrDeleted(uid) && HasComp<MapGridComponent>(uid)) maps[depth - min] = uid;
        return true;
    }

    private void OnClassic(Entity<CMUTacticalReconstructionComponent> ent, ref CMUReconClassicMessage args)
    {
        if (!CanUse(ent, args.Actor) || !_ui.IsUiOpen(ent.Owner, Key, args.Actor)) return;
        if (_ui.TryOpenUi(ent.Owner, TacticalMapComputerUi.Key, args.Actor))
            _ui.CloseUi(ent.Owner, Key, args.Actor);
    }
}
