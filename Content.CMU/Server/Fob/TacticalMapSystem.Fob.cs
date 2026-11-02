using System.Linq;
using Content.Server.CMU14.Fob;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.GameTicking;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.Fob;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    [Dependency] private CMUFobReserveSystem _fobReserve = default!;

    private readonly HashSet<(EntityUid Source, EntityUid Actor)> _fobMarkerRecipients = new();

    private void InitializeFobMarkers()
    {
        SubscribeLocalEvent<CMUFobMarkersChangedEvent>(OnFobMarkersChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _fobMarkerRecipients.Clear());
    }

    private void OnFobMarkersChanged(ref CMUFobMarkersChangedEvent args)
    {
        // Rebuild existing recipient views immediately, including published snapshots. Never put a private
        // mission in a source faction bucket: sensors can expose those buckets to another faction.
        var users = EntityQueryEnumerator<TacticalMapUserComponent>();
        while (users.MoveNext(out var uid, out var user))
        {
            if (user.Map is { } map && TryComp<TacticalMapComponent>(map, out var tactical))
            {
                ApplyFobMarkers((uid, user), tactical);
                Dirty(uid, user);
            }
        }
        var computers = EntityQueryEnumerator<TacticalMapComputerComponent>();
        while (computers.MoveNext(out var uid, out var computer))
        {
            // Normal publication repopulates this view; resolution must also clear closed UI caches now.
            RemoveFobMarkers(computer.Blips);
            RemoveFobMarkers(computer.SquadBlips);
            if (TryGetTacticalMap(out var map))
                ApplyFobMarkers((uid, computer), map.Comp);
            Dirty(uid, computer);
        }
    }

    private static void RemoveFobMarkers(Dictionary<int, TacticalMapBlip> blips)
    {
        foreach (var id in blips.Where(pair => pair.Value.CMUFobLabel != null).Select(pair => pair.Key).ToArray())
            blips.Remove(id);
    }

    private void ApplyFobMarkers(Entity<TacticalMapUserComponent> user, TacticalMapComponent map)
    {
        if (!_fobReserve.Enabled) return;
        foreach (var blips in new[] { user.Comp.GovforBlips, user.Comp.OpforBlips, user.Comp.MarineBlips,
                     user.Comp.ClfBlips, user.Comp.XenoBlips, user.Comp.XenoStructureBlips, user.Comp.SquadBlips })
            RemoveFobMarkers(blips);
        Dirty(user);
        if (!user.Comp.Govfor || HasComp<XenoComponent>(user) || !TryComp<MarineComponent>(user, out var marine) || marine.Faction != "govfor")
            return;
        AddFobMarkers(map, user.Comp.GovforBlips, "govfor");
        if (user.Comp.HasSquad) AddFobMarkers(map, user.Comp.SquadBlips, "govfor");
    }

    private void ApplyFobMarkers(Entity<TacticalMapComputerComponent> computer, TacticalMapComponent map)
    {
        if (!_fobReserve.Enabled && _fobMarkerRecipients.Count == 0) return;
        // Computer components replicate in PVS. Private missions travel only to authorized UI actors.
        foreach (var actor in _ui.GetActors(computer.Owner, TacticalMapComputerUi.Key))
        {
            var markers = NormalizeMapFaction(computer.Comp.Faction) == GovforFaction
                ? GetFobMarkers(actor) : new Dictionary<int, TacticalMapBlip>();
            var key = (computer.Owner, actor);
            if (markers.Count == 0 && !_fobMarkerRecipients.Remove(key)) continue;
            if (markers.Count > 0) _fobMarkerRecipients.Add(key);
            _ui.ServerSendUiMessage(computer.Owner, TacticalMapComputerUi.Key, new CMUFobMapMarkersMessage(markers), actor);
        }
    }

    /// <summary>Private overlay for an already-authorized UI actor, not a sensor source bucket.</summary>
    public Dictionary<int, TacticalMapBlip> GetFobMarkers(EntityUid actor)
    {
        var result = new Dictionary<int, TacticalMapBlip>();
        if (_fobReserve.Enabled && !HasComp<XenoComponent>(actor)
            && TryComp<MarineComponent>(actor, out var marine) && marine.Faction == "govfor"
            && TryGetTacticalMap(out var map))
            AddFobMarkers(map.Comp, result, "govfor");
        return result;
    }

    private void AddFobMarkers(TacticalMapComponent map, Dictionary<int, TacticalMapBlip> destination, string faction)
    {
        if (!_fobReserve.Enabled || !_fobReserve.TryGetNetwork(faction, out var bank)
            || bank.Comp.State == CMUFobReserveState.Closed || bank.Comp.JobExpiresAt == null)
            return;
        var seconds = (int) bank.Comp.RelayReward.TotalSeconds;
        var label = Loc.GetString("cmu-fob-map-relay", ("time", $"{seconds / 60}:{seconds % 60:00}"));
        foreach (var relay in new[] { bank.Comp.FirstRelay, bank.Comp.SecondRelay })
        {
            if (relay is not { } uid || TerminatingOrDeleted(uid) || Transform(uid).MapUid != bank.Comp.Map
                || Transform(uid).GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp)
                || !TryComp<TacticalMapComponent>(grid, out var source) || source != map
                || !_transform.TryGetGridTilePosition((uid, Transform(uid)), out var indices, gridComp))
                continue;
            destination[uid.Id] = new TacticalMapBlip(indices,
                new SpriteSpecifier.Rsi(new ResPath("/Textures/_CMU14/Structures/Fob/map_blips.rsi"), "hvt"),
                Color.Cyan, TacticalMapBlipStatus.Alive, null, false) { CMUFobLabel = label };
        }
    }
}
