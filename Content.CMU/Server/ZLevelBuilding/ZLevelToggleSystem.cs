// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.IO;
using System.Linq;
using Content.Server._AU14.Construction.CustomConstruction;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._AU14.Administration;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._AU14.ZLevelBuilding;

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
public sealed class ZLevelToggleSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceManager _resource = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly CustomConstructionMenuSystem _menu = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;

    /// <summary>Persisted denial list (one GameMapPrototype id per line) in the server's user-data folder.</summary>
    private static readonly ResPath SaveFile = new("/au14_zlevel_disabled.txt");

    /// <summary>GameMapPrototype ids on which z-level building is denied.</summary>
    private readonly HashSet<string> _disabled = new();

    // Which map ENTITIES each game-map prototype produced this round, so a mid-round toggle applies live.
    // Round-scoped; cleared on restart.
    private readonly Dictionary<string, HashSet<EntityUid>> _loadedMaps = new();

    public override void Initialize()
    {
        base.Initialize();

        LoadDisabledSet();

        SubscribeNetworkEvent<RequestOpenZLevelTogglesEvent>(OnRequestOpen);
        SubscribeNetworkEvent<SetZLevelToggleEvent>(OnSetToggle);

        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _loadedMaps.Clear());
    }

    /// <summary>A game map finished loading: remember its map entity and apply a persisted denial, if any.</summary>
    private void OnPostGameMapLoad(PostGameMapLoad ev)
    {
        if (!_mapManager.MapExists(ev.Map))
            return;

        var mapUid = _mapManager.GetMapEntityId(ev.Map);
        if (!mapUid.IsValid())
            return;

        var set = _loadedMaps.TryGetValue(ev.GameMap.ID, out var existing)
            ? existing
            : _loadedMaps[ev.GameMap.ID] = new HashSet<EntityUid>();
        set.Add(mapUid);

        if (_disabled.Contains(ev.GameMap.ID))
            ApplyToMap(mapUid, false);
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
                    ApplyToMap(mapUid, msg.Enabled);
            }
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{session.Name} set z-level building on map '{msg.MapProtoId}' to {(msg.Enabled ? "ALLOWED" : "DENIED")}.");
        _popup.PopupCursor(
            Loc.GetString(msg.Enabled ? "au-zlevel-toggle-enabled" : "au-zlevel-toggle-disabled", ("map", msg.MapProtoId)),
            session);
    }

    private void ApplyToMap(EntityUid mapUid, bool enabled)
    {
        var comp = EnsureComp<ZBuildableMapComponent>(mapUid);
        if (comp.Enabled == enabled)
            return;

        comp.Enabled = enabled;
        Dirty(mapUid, comp);
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
