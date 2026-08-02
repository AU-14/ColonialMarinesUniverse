using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    private int _nextIntelBlipKey = -1;

    private static readonly Dictionary<string, SpriteSpecifier.Rsi> FactionSignalIcon = new()
    {
        ["govfor"] = new SpriteSpecifier.Rsi(
            new ResPath("/Textures/_AU14/Interface/au14govforjobicons.rsi"),
            "rifleman"),

        ["opfor"] = new SpriteSpecifier.Rsi(
            new ResPath("/Textures/_AU14/Interface/au14opforjobicons.rsi"),
            "rifleman"),

        ["clf"] = new SpriteSpecifier.Rsi(
            new ResPath("/Textures/_AU14/Interface/au14colonyjobicons.rsi"),
            "colonist")
    };

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
