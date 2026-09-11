using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    private int _nextIntelBlipKey = -1;

    private static readonly Dictionary<string, SpriteSpecifier.Rsi> FactionSignalIcon = new()
    {
        ["govfor"] = new SpriteSpecifier.Rsi(
            new ResPath("/Textures/CMU14/Interface/au14govforjobicons.rsi"),
            "rifleman"),

        ["opfor"] = new SpriteSpecifier.Rsi(
            new ResPath("/Textures/CMU14/Interface/au14opforjobicons.rsi"),
            "rifleman"),

        ["clf"] = new SpriteSpecifier.Rsi(
            new ResPath("/Textures/CMU14/Interface/au14colonyjobicons.rsi"),
            "colonist")
    };

    private static readonly SpriteSpecifier.Rsi YautjaPreyIcon = new(
        new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"),
        "enemy_blip");

    public bool TrySetYautjaPreyBlip(EntityUid source, out EntityUid gridId)
    {
        gridId = default;
        if (!_transformQuery.TryComp(source, out var xform)
            || xform.GridUid is not { } sourceGrid
            || !_mapGridQuery.TryComp(sourceGrid, out var gridComp)
            || !_tacticalMapQuery.TryComp(sourceGrid, out var tacticalMap)
            || !_transform.TryGetGridTilePosition((source, xform), out var indices, gridComp))
        {
            return false;
        }

        var sourceBlip = FindBlipInMap(source.Id, tacticalMap);
        var blip = sourceBlip is { } existing
            ? existing with { Indices = indices }
            : new TacticalMapBlip(
                indices,
                YautjaPreyIcon,
                Color.FromHex("#FFB347"),
                TacticalMapBlipStatus.Alive,
                null,
                false);

        tacticalMap.YautjaBlips[source.Id] = blip;
        tacticalMap.NextUpdatePerFaction["YAUTJA"] = TimeSpan.Zero;
        tacticalMap.MapDirty = true;
        gridId = sourceGrid;
        return true;
    }

    public void RemoveYautjaPreyBlip(EntityUid source, EntityUid? gridId = null)
    {
        if (gridId is { } mapUid
            && TryComp(mapUid, out TacticalMapComponent? tacticalMap))
        {
            if (tacticalMap.YautjaBlips.Remove(source.Id))
            {
                tacticalMap.NextUpdatePerFaction["YAUTJA"] = TimeSpan.Zero;
                tacticalMap.MapDirty = true;
            }
            return;
        }

        var maps = EntityQueryEnumerator<TacticalMapComponent>();
        while (maps.MoveNext(out var map))
        {
            if (map.YautjaBlips.Remove(source.Id))
            {
                map.NextUpdatePerFaction["YAUTJA"] = TimeSpan.Zero;
                map.MapDirty = true;
            }
        }
    }

    public (EntityUid GridId, int Key)? CreateFactionIntelBlip(
        EntityUid source,
        string sourceFactionLower,
        string viewerFactionUpper)
    {
        if (!_transformQuery.TryComp(source, out var xform) ||
            xform.GridUid is not { } gridId ||
            !_mapGridQuery.TryComp(gridId, out var gridComp) ||
            !_tacticalMapQuery.TryComp(gridId, out var tacticalMap) ||
            !_transform.TryGetGridTilePosition((source, xform), out var indices, gridComp))
        {
            return null;
        }

        FactionSignalIcon.TryGetValue(sourceFactionLower, out var icon);

        var blip = new TacticalMapBlip(
            indices,
            icon,
            Color.FromHex("#FF6B6B"),
            TacticalMapBlipStatus.Alive,
            null,
            false);

        var key = _nextIntelBlipKey--;

        if (!TryGetBlipDicts(tacticalMap, viewerFactionUpper, out var live, out _))
            return null;

        // live dict only: DF fixes are realtime SIGINT for the ops consoles (tacmap
        // computers and overwatch poll the live blips), not part of the snapshot map
        // updates handed to every rifleman. a manual update pulled while the fix is
        // up still captures it, which is fine - that update reflects current intel
        live[key] = blip;

        tacticalMap.MapDirty = true;
        return (gridId, key);
    }

    public void EraseFactionIntelBlip(EntityUid gridId, int key, string viewerFactionUpper)
    {
        if (!TryComp(gridId, out TacticalMapComponent? tacticalMap))
            return;

        if (!TryGetBlipDicts(tacticalMap, viewerFactionUpper, out var live, out var snapshot))
            return;

        var removed = live.Remove(key);
        removed |= snapshot.Remove(key);

        if (removed)
            tacticalMap.MapDirty = true;
    }

    private static bool TryGetBlipDicts(
        TacticalMapComponent map,
        string factionUpper,
        out Dictionary<int, TacticalMapBlip> live,
        out Dictionary<int, TacticalMapBlip> snapshot)
    {
        switch (factionUpper)
        {
            case "OPFOR":
                live = map.OpforBlips;
                snapshot = map.LastUpdateOpforBlips;
                return true;

            case "GOVFOR":
                live = map.GovforBlips;
                snapshot = map.LastUpdateGovforBlips;
                return true;

            case "CLF":
                live = map.ClfBlips;
                snapshot = map.LastUpdateClfBlips;
                return true;

            default:
                live = null!;
                snapshot = null!;
                return false;
        }
    }
}
