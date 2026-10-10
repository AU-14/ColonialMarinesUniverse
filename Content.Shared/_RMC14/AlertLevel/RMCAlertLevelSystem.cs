using Content.Shared._RMC14.ARES;
using Content.Shared._RMC14.ARES.Logs;
using Content.Shared._RMC14.Doors;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.Marines; // CMU14
using Content.Shared.CMU14.ZLevels.Core.EntitySystems; // CMU14
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Ghost.Components;
using Content.Shared.Lock;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map; // CMU14
using Robust.Shared.Map.Components; // CMU14: MapComponent moved namespaces upstream
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.AlertLevel;

public sealed partial class RMCAlertLevelSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private ARESCoreSystem _aresCore = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!; // CMU14

    private EntityQuery<GhostComponent> _ghostQuery;

    private static readonly EntProtoId<ARESLogTypeComponent> LogCat = "ARESTabAnnouncementLogs";

    public override void Initialize()
    {
        SubscribeLocalEvent<DropshipHijackLandedEvent>(OnDropshipHijackLanded);
        SubscribeLocalEvent<WarshipComponent, ComponentInit>(OnWarshipInit); // CMU14

        _ghostQuery = GetEntityQuery<GhostComponent>();
    }

    // CMU14 method
    private void OnWarshipInit(Entity<WarshipComponent> ent, ref ComponentInit args)
    {
        // WarshipComponent sits on the ship's grid, the alert level belongs on its map so map
        // scoped reads hit. A grid tagged off the primary deck still arms the primary.
        if (!_net.IsServer)
            return;

        // CMU14: EntityUid == is a custom operator, so == null does not narrow for the compiler
        if (Transform(ent).MapUid is not { } map)
            return;

        if (_zLevels.TryGetZNetwork(map, out var network)
            && _zLevels.TryGetMapAtDepth(network.Value, 0, out var primary))
            map = primary;

        EnsureComp<RMCAlertLevelComponent>(map);
    }

    private void OnDropshipHijackLanded(ref DropshipHijackLandedEvent ev)
    {
        // TODO RMC14 is this real
        // Set(RMCAlertLevels.Red);
    }

    // CMU14 Per-warship alert levels Begin
    private bool TryGetAlertLevel(EntityUid? context, out Entity<RMCAlertLevelComponent> alert)
    {
        // A context entity resolves to its own ship's map first
        if (context != null
            && EntityManager.TransformQuery.CompOrNull(context.Value)?.MapUid is { } map
            && TryComp<RMCAlertLevelComponent>(map, out var shipAlert))
        {
            alert = (map, shipAlert);
            return true;
        }

        // A multi-z warship keeps its component on the primary deck, so other decks resolve through the z-network
        if (context != null
            && EntityManager.TransformQuery.CompOrNull(context.Value)?.MapUid is { } networkMap)
        {
            foreach (var member in _zLevels.GetAllNetworkMaps(networkMap))
            {
                if (member == networkMap
                    || !TryComp<RMCAlertLevelComponent>(member, out var networkAlert))
                    continue;

                alert = (member, networkAlert);
                return true;
            }
        }

        // Lowest uid wins so two warships never race the enumeration order
        EntityUid? best = null;
        alert = default;
        var query = EntityQueryEnumerator<RMCAlertLevelComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (best == null || uid.CompareTo(best.Value) < 0)
            {
                best = uid;
                alert = (uid, comp);
            }
        }

        return best != null;
    }

    private Entity<RMCAlertLevelComponent> EnsureAlertLevel(EntityUid? context)
    {
        if (TryGetAlertLevel(context, out var alert))
            return alert;

        var uid = Spawn();
        var comp = EnsureComp<RMCAlertLevelComponent>(uid);
        return (uid, comp);
    }

    public RMCAlertLevels? Get(EntityUid? context = null)
    {
        if (!TryGetAlertLevel(context, out var alert))
            return null;

        return alert.Comp.Level;
    }

    public bool IsRedOrDeltaAlert(EntityUid? context = null)
        => Get(context) is { } level && level >= RMCAlertLevels.Red;
    // CMU14 End

    public void Set(RMCAlertLevels level, EntityUid? user, bool playSound = true, bool sendAnnouncement = true,
        EntityUid? context = null) // CMU14: context scopes the ship without naming the map as the actor
    {
        var ent = EnsureAlertLevel(context ?? user); // CMU14
        if (ent.Comp.Level == level)
            return;

        var (sound, message, announcement) = level switch
        {
            RMCAlertLevels.Green => (ent.Comp.GreenSound, ent.Comp.GreenMessage, null),
            RMCAlertLevels.Blue when ent.Comp.Level < RMCAlertLevels.Blue => (ent.Comp.BlueElevatedSound, ent.Comp.BlueElevatedMessage, null),
            RMCAlertLevels.Blue when ent.Comp.Level > RMCAlertLevels.Blue => (ent.Comp.BlueLoweredSound, ent.Comp.BlueLoweredMessage, null),
            RMCAlertLevels.Red when ent.Comp.Level < RMCAlertLevels.Red => (ent.Comp.RedElevatedSound, ent.Comp.RedElevatedMessage, null),
            RMCAlertLevels.Red when ent.Comp.Level > RMCAlertLevels.Red => (ent.Comp.RedLoweredSound, ent.Comp.RedLoweredMessage, null),
            // CMU14 Begin: darkened ship
            RMCAlertLevels.Black when ent.Comp.Level < RMCAlertLevels.Black => (ent.Comp.BlackElevatedSound, ent.Comp.BlackElevatedMessage, null),
            RMCAlertLevels.Black when ent.Comp.Level > RMCAlertLevels.Black => (ent.Comp.BlackLoweredSound, ent.Comp.BlackLoweredMessage, null),
            // CMU14 End
            RMCAlertLevels.Delta => (ent.Comp.DeltaSound, ent.Comp.DeltaAnnouncement, ent.Comp.DeltaAnnouncement),
            _ => (null, null, null),
        };

        ent.Comp.Level = level;
        Dirty(ent);

        // CMU14
        if (user != null)
            _adminLog.Add(LogType.RMCAlertLevel, $"{ToPrettyString(user)} set alert level to {level}");
        else
            _adminLog.Add(LogType.RMCAlertLevel, $"Alert level set to {level}");

        var transformQuery = EntityManager.TransformQuery;
        // CMU14: side effects stay on the warship whose level changed, ghosts still hear everything
        // The holder is the armed warship map, free fallback entities stay global
        var shipMap = HasComp<MapComponent>(ent) ? ent.Owner : (EntityUid?) null;

        if (user != null && shipMap != null)
            _aresCore.CreateARESLog(shipMap.Value, LogCat, (string) $"{Name(user.Value)} set the alert level to: {level}");

        var filter = Filter.Empty()
            .AddWhereAttachedEntity(entity =>
            {
                if (shipMap != null && _zLevels.IsSameZNetwork(transformQuery.CompOrNull(entity)?.MapUid, shipMap.Value)) // CMU14
                    return true;

                if (_ghostQuery.HasComp(entity))
                    return true;

                return false;
            });

        // Play alarm sound if playSound == true
        if (playSound && _net.IsServer)
        {
            _audio.PlayGlobal(sound, filter, true);
        }

        // Send announcement if sendAnnouncement == true
        if (sendAnnouncement)
        {
            // CMU14: chat carries the announcement like the other priority broadcasts.
            // MarineCommon radio is unused on this fork so only ghosts would ever hear it.
            var locId = announcement ?? message;
            if (locId != null)
            {
                var text = Loc.GetString(locId.Value);
                _marineAnnounce.AnnounceToMarines(text, filter: filter);
                _marineAnnounce.AnnounceAlertLevel(level, text, filter);
            }
        }

        var unlockQuery = EntityQueryEnumerator<RMCUnlockOnAlertLevelComponent, LockComponent, TransformComponent>();
        while (unlockQuery.MoveNext(out var uid, out var unlock, out var lockComp, out var unlockXform))
        {
            if (shipMap != null && !_zLevels.IsSameZNetwork(unlockXform.MapUid, shipMap.Value)) // CMU14
                continue;

            if (unlock.Level <= level)
            {
                _lock.Unlock(uid, null, lockComp);
            }
            else
            {
                if (TryComp<EntityStorageComponent>(uid, out var entityStorageComp))
                    _entityStorage.CloseStorage((uid, entityStorageComp)); // Close a locker before locking it.
                _lock.Lock(uid, null, lockComp);
            }
        }

        var openQuery = EntityQueryEnumerator<RMCOpenOnAlertLevelComponent, DoorComponent, RMCPodDoorComponent, TransformComponent>();
        while (openQuery.MoveNext(out var uid, out var unlock, out var door, out var podDoor, out var openXform))
        {
            if (unlock.Id != podDoor.Id)
                continue;

            if (shipMap != null && !_zLevels.IsSameZNetwork(openXform.MapUid, shipMap.Value)) // CMU14
                continue;

            if (unlock.Level <= level)
                _door.TryOpen(uid, door);
            else
                _door.TryClose(uid, door);
        }

        var displays = EntityQueryEnumerator<RMCAlertLevelDisplayComponent, TransformComponent>();
        while (displays.MoveNext(out var uid, out _, out var displayXform))
        {
            if (shipMap != null && !_zLevels.IsSameZNetwork(displayXform.MapUid, shipMap.Value)) // CMU14
                continue;

            _appearance.SetData(uid, RMCAlertLevelsVisuals.Alert, level);
        }

        var ev = new RMCAlertLevelChangedEvent(ent.Comp.Level, shipMap); // CMU14
        RaiseLocalEvent(ent, ref ev, true);
    }
}
