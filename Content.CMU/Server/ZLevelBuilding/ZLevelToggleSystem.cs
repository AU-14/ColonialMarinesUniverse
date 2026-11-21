// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.IO;
using System.Linq;
using Content.Server.CMU14.Construction.CustomConstruction;
using Content.Server.GameTicking;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.Administration;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): the "Z-Level Toggles" admin tool in the construction menu's Tools panel.
///
/// Lets a permitted admin allow/deny z-level building (stairs, support beams, digging, and everything else
/// gated by <see cref="ZBuildableMapComponent"/>) PER MAP PROTOTYPE - e.g. keep it on for planet maps but off
/// for ships so nobody digs an underground cave below a spaceship. Choices are keyed by GameMapPrototype id,
/// persisted in the server's user-data folder across rounds and restarts, and applied both to maps as they
/// load (<see cref="PostGameMapLoad"/>) and live to already-loaded maps when a toggle is flipped mid-round.
///
/// Mapper-authored opt-outs (a ZBuildableMap { enabled: false } on the map prototype itself) still work; this
/// tool simply overrides the component's Enabled at runtime for maps the admin has denied.
/// </summary>
public sealed partial class ZLevelToggleSystem : EntitySystem
{
    [Dependency] private  IPrototypeManager _prototype = default!;
    [Dependency] private  IResourceManager _resource = default!;
    [Dependency] private  SharedMapSystem _mapManager = default!;
    [Dependency] private  CustomConstructionMenuSystem _menu = default!;
    [Dependency] private  SharedPopupSystem _popup = default!;
    [Dependency] private  ISharedAdminLogManager _adminLog = default!;
    [Dependency] private  CMUZLevelsSystem _zLevels = default!;

    /// <summary>Persisted denial list (one GameMapPrototype id per line) in the server's user-data folder.</summary>
    private static readonly ResPath SaveFile = new("/au14_zlevel_disabled.txt");

    /// <summary>GameMapPrototype ids on which z-level building is denied.</summary>
    private readonly HashSet<string> _disabled = new();

    // Which map ENTITIES each game-map prototype produced this round, so a mid-round toggle applies live.
    // Round-scoped; cleared on restart.
    private readonly Dictionary<string, HashSet<EntityUid>> _loadedMaps = new();

    // Maps whose z-network is denied this round, so levels attached to that network later are denied too.
    private readonly HashSet<EntityUid> _deniedMaps = new();

    // Levels that had no ZBuildableMapComponent until a denial added one.
    private readonly HashSet<EntityUid> _addedSettings = new();

    // Levels a denial switched off (as opposed to a mapper's own enabled: false), so they can be restored on leave.
    private readonly HashSet<EntityUid> _deniedLevels = new();

    public override void Initialize()
    {
        base.Initialize();

        LoadDisabledSet();

        SubscribeNetworkEvent<RequestOpenZLevelTogglesEvent>(OnRequestOpen);
        SubscribeNetworkEvent<SetZLevelToggleEvent>(OnSetToggle);

        // After the z-network is built, so a denial reaches every authored level of the map.
        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad, after: new[] { typeof(CMUZLevelsSystem) });
        SubscribeLocalEvent<CMUZLevelNetworkUpdatedEvent>(OnNetworkUpdated);
        SubscribeLocalEvent<CMUZLevelMapLeftNetworkEvent>(OnMapLeftNetwork);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _loadedMaps.Clear();
            _deniedMaps.Clear();
            _addedSettings.Clear();
            _deniedLevels.Clear();
        });
    }

    /// <summary>
    /// Allows or denies z-level building on <paramref name="mapUid"/> and on every level of its z-network. A denial
    /// also covers levels attached to that network later this round. Not persisted; the per-prototype admin tool
    /// uses this for every map it applies to.
    /// </summary>
    public void SetNetworkEnabled(EntityUid mapUid, bool enabled)
    {
        if (enabled)
            _deniedMaps.Remove(mapUid);
        else
            _deniedMaps.Add(mapUid);

        ApplyToMap(mapUid, enabled);
    }

    /// <summary>Re-applies a recorded denial to levels newly attached to a denied network.</summary>
    private void OnNetworkUpdated(ref CMUZLevelNetworkUpdatedEvent args)
    {
        if (_deniedMaps.Count == 0)
            return;

        _deniedMaps.RemoveWhere(uid => TerminatingOrDeleted(uid));
        foreach (var denied in _deniedMaps)
        {
            if (_zLevels.TryGetZNetwork(denied, out var network) && network.Value.Owner == args.Network.Owner)
                ApplyToMap(denied, false);
        }
    }

    /// <summary>A game map finished loading: remember its map entity and apply a persisted denial, if any.</summary>
    private void OnPostGameMapLoad(PostGameMapLoad ev)
    {
        if (!_mapManager.MapExists(ev.Map))
            return;

        var mapUid = _mapManager.GetMapOrInvalid(ev.Map);
        if (!mapUid.IsValid())
            return;

        var set = _loadedMaps.TryGetValue(ev.GameMap.ID, out var existing)
            ? existing
            : _loadedMaps[ev.GameMap.ID] = new HashSet<EntityUid>();
        set.Add(mapUid);

        if (_disabled.Contains(ev.GameMap.ID))
            SetNetworkEnabled(mapUid, false);
    }

    private void OnRequestOpen(RequestOpenZLevelTogglesEvent msg, EntitySessionEventArgs args)
    {
        if (!_menu.CanUseTool(args.SenderSession, AU14ToolPermissions.ZLevelToggles))
            return;

        var ev = new OpenZLevelTogglesEvent();
        foreach (var proto in _prototype.EnumeratePrototypes<GameMapPrototype>().OrderBy(p => p.MapName))
        {
            ev.Maps.Add(new ZLevelToggleEntry
            {
                MapProtoId = proto.ID,
                MapName = proto.MapName,
                Enabled = !_disabled.Contains(proto.ID),
                Loaded = _loadedMaps.ContainsKey(proto.ID),
            });
        }

        RaiseNetworkEvent(ev, args.SenderSession);
    }

    private void OnSetToggle(SetZLevelToggleEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!_menu.CanUseTool(session, AU14ToolPermissions.ZLevelToggles))
            return;

        if (!_prototype.HasIndex<GameMapPrototype>(msg.MapProtoId))
            return;

        var changed = msg.Enabled ? _disabled.Remove(msg.MapProtoId) : _disabled.Add(msg.MapProtoId);
        if (!changed)
            return;

        SaveDisabledSet();

        // Apply live to every currently-loaded map of that prototype (component is networked, so the client
        // ghost condition updates too).
        if (_loadedMaps.TryGetValue(msg.MapProtoId, out var maps))
        {
            foreach (var mapUid in maps)
            {
                if (!Deleted(mapUid))
                    SetNetworkEnabled(mapUid, msg.Enabled);
            }
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{session.Name} set z-level building on map '{msg.MapProtoId}' to {(msg.Enabled ? "ALLOWED" : "DENIED")}.");
        _popup.PopupCursor(
            Loc.GetString(msg.Enabled ? "au-zlevel-toggle-enabled" : "au-zlevel-toggle-disabled", ("map", msg.MapProtoId)),
            session);
    }

    /// <summary>Sets z-building on <paramref name="mapUid"/> and on every other level of its z-network.</summary>
    private void ApplyToMap(EntityUid mapUid, bool enabled)
    {
        foreach (var level in _zLevels.GetAllNetworkMaps(mapUid))
        {
            if (TerminatingOrDeleted(level))
                continue;

            // A level without settings reads them from the level above it (GetSourceMapForStone), so a component
            // added only to deny it is removed again on allow rather than left to shadow those settings.
            ZBuildableMapComponent? comp;
            if (enabled)
            {
                _deniedLevels.Remove(level);
                if (_addedSettings.Remove(level))
                {
                    RemComp<ZBuildableMapComponent>(level);
                    continue;
                }

                if (!TryComp(level, out comp))
                    continue;
            }
            else if (!TryComp(level, out comp))
            {
                comp = AddComp<ZBuildableMapComponent>(level);
                _addedSettings.Add(level);
            }

            if (comp.Enabled == enabled)
                continue;

            comp.Enabled = enabled;
            Dirty(level, comp);
            if (!enabled)
                _deniedLevels.Add(level);
        }
    }

    /// <summary>
    /// A level that leaves a denied network is no longer reached by an allow on that network, so restore it now.
    /// A map that was itself denied stays denied.
    /// </summary>
    private void OnMapLeftNetwork(ref CMUZLevelMapLeftNetworkEvent args)
    {
        var map = args.Map;
        if (TerminatingOrDeleted(map))
        {
            _deniedLevels.Remove(map);
            _addedSettings.Remove(map);
            return;
        }

        if (_deniedMaps.Contains(map) || !_deniedLevels.Remove(map))
            return;

        if (_addedSettings.Remove(map))
        {
            RemComp<ZBuildableMapComponent>(map);
            return;
        }

        if (TryComp(map, out ZBuildableMapComponent? comp) && !comp.Enabled)
        {
            comp.Enabled = true;
            Dirty(map, comp);
        }
    }

    // -------------------------------------------------------------------------
    // Persistence (server user-data file; survives rounds and restarts)
    // -------------------------------------------------------------------------

    private void LoadDisabledSet()
    {
        try
        {
            if (!_resource.UserData.Exists(SaveFile))
                return;

            using var reader = _resource.UserData.OpenText(SaveFile);
            while (reader.ReadLine() is { } line)
            {
                line = line.Trim();
                if (line.Length > 0)
                    _disabled.Add(line);
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load z-level toggles: {e}");
        }
    }

    private void SaveDisabledSet()
    {
        try
        {
            using var writer = _resource.UserData.OpenWriteText(SaveFile);
            foreach (var id in _disabled.OrderBy(i => i))
                writer.WriteLine(id);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save z-level toggles: {e}");
        }
    }
}
