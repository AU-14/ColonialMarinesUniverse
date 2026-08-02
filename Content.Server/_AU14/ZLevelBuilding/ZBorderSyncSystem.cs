// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Server._AU14.Construction.CustomConstruction;
using Content.Server.AU14.Round;
using Content.Shared._AU14.Administration;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared._RMC14.Rules;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._AU14.ZLevelBuilding;

/// <summary>
/// The "Z-Sync Lists" admin tool: controls WHICH wall prototypes get mirrored across z-levels as map
/// borders. Whitelist = reflected; blacklist overrides (for walls that inherit the invincible border
/// parent but are ordinary structures, e.g. dropship walls). Lists are scoped per map: the GLOBAL scope
/// applies on every map, and each planet map prototype can carry its own extra entries which only apply
/// when that planet is the round's map. Save files from before scoping existed load into the global scope
/// unchanged. On first boot the implicit rule (the CMBaseWallInvincible family) is materialized into
/// explicit global whitelist entries so admins can see and edit it through the same menu. Persisted
/// across rounds in the server user-data folder.
/// </summary>
public sealed class ZBorderSyncSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceManager _resource = default!;
    [Dependency] private readonly CustomConstructionMenuSystem _menu = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly AuRoundSystem _auRound = default!;
    [Dependency] private readonly RMCPlanetSystem _planets = default!;

    private static readonly ResPath SaveFile = new("/au14_zborder_sync.txt");

    /// <summary>The scope key for entries that apply on every map.</summary>
    public const string GlobalScope = "";

    // The abstract roots whose descendants are border walls by default (seeded into the whitelist).
    private static readonly string[] DefaultBorderParents = { "CMBaseWallInvincible", "RMCBaseWallInvincibleNoIcon" };

    private sealed class ScopeLists
    {
        public readonly HashSet<string> Whitelist = new(StringComparer.Ordinal);
        public readonly HashSet<string> Blacklist = new(StringComparer.Ordinal);
    }

    // Keyed by scope: GlobalScope ("") or a planet map prototype id.
    private readonly Dictionary<string, ScopeLists> _scopes = new(StringComparer.Ordinal);
    private Dictionary<string, List<string>>? _descendantsByParent;
    private Dictionary<string, List<string>>? _nonAbstractByName;

    public event Action? ListsChanged;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestOpenZBorderSyncEvent>(OnRequestOpen);
        SubscribeNetworkEvent<ModifyZBorderSyncEvent>(OnModify);
        SubscribeNetworkEvent<PickZBorderSyncEntityEvent>(OnPickEntity);
        LoadLists();
    }

    private ScopeLists GetScope(string scope)
    {
        if (!_scopes.TryGetValue(scope, out var lists))
            _scopes[scope] = lists = new ScopeLists();

        return lists;
    }

    /// <summary>Whether this prototype should be mirrored across z-levels as a map border, considering the
    /// global lists plus the lists scoped to the round's current map. Scope keys are GameMapPrototype ids;
    /// the selected planet's MapId and its planet prototype id are both accepted so either keying works.</summary>
    public bool ShouldReflect(string protoId)
    {
        var currentGameMap = _auRound.GetSelectedPlanet()?.MapId;
        var currentPlanet = _auRound.GetSelectedPlanetId();

        foreach (var (scope, lists) in _scopes)
        {
            if (scope != GlobalScope && scope != currentGameMap && scope != currentPlanet)
                continue;

            if (PrototypeOrParentListed(protoId, lists.Blacklist))
                return false;
        }

        foreach (var (scope, lists) in _scopes)
        {
            if (scope != GlobalScope && scope != currentGameMap && scope != currentPlanet)
                continue;

            if (PrototypeOrParentListed(protoId, lists.Whitelist))
                return true;
        }

        return false;
    }

    private bool PrototypeOrParentListed(string protoId, HashSet<string> list)
    {
        if (list.Count == 0)
            return false;

        if (list.Contains(protoId))
            return true;

        if (!_prototype.HasIndex<EntityPrototype>(protoId))
            return false;

        foreach (var (parentId, _) in _prototype.EnumerateAllParents<EntityPrototype>(protoId, includeSelf: false))
        {
            if (list.Contains(parentId))
                return true;
        }

        return false;
    }

    private void OnRequestOpen(RequestOpenZBorderSyncEvent msg, EntitySessionEventArgs args)
    {
        if (!_menu.CanUseTool(args.SenderSession, AU14ToolPermissions.ZSync))
            return;

        RaiseNetworkEvent(BuildOpenEvent(), args.SenderSession);
    }

    private OpenZBorderSyncEvent BuildOpenEvent()
    {
        var ev = new OpenZBorderSyncEvent();
        foreach (var (scope, lists) in _scopes)
        {
            if (lists.Whitelist.Count > 0)
                ev.Whitelists[scope] = lists.Whitelist.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            if (lists.Blacklist.Count > 0)
                ev.Blacklists[scope] = lists.Blacklist.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Every map prototype is offered, planets or not, in rotation or not - the scope picker should
        // cover anything the server can ever load.
        foreach (var map in _prototype.EnumeratePrototypes<Content.Server.Maps.GameMapPrototype>()
                     .OrderBy(p => p.MapName, StringComparer.OrdinalIgnoreCase))
        {
            var name = string.IsNullOrWhiteSpace(map.MapName) ? map.ID : map.MapName;
            ev.Maps.Add(new ZSyncMapOption { Id = map.ID, Name = name });
        }

        return ev;
    }

    private void OnModify(ModifyZBorderSyncEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!_menu.CanUseTool(session, AU14ToolPermissions.ZSync))
            return;

        // Empty scope list = the global scope.
        var scopes = msg.MapIds.Count == 0 ? new List<string> { GlobalScope } : msg.MapIds;

        var changed = 0;
        foreach (var scope in scopes)
        {
            if (scope != GlobalScope &&
                !_prototype.HasIndex<Content.Server.Maps.GameMapPrototype>(scope) &&
                !_prototype.HasIndex<EntityPrototype>(scope))
                continue;

            changed += ApplyListChange(scope, msg.ProtoIds, msg.Blacklist, msg.Add);
        }

        if (changed > 0)
        {
            SaveLists();
            ListsChanged?.Invoke();
        }

        var listName = msg.Blacklist ? "blacklist" : "whitelist";
        var scopeName = msg.MapIds.Count == 0 ? "global" : string.Join(", ", msg.MapIds);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{session.Name} {(msg.Add ? "added" : "removed")} {changed} prototypes {(msg.Add ? "to" : "from")} the z-border {listName} (scope: {scopeName})");

        if (session.AttachedEntity is { } ent)
        {
            _popup.PopupEntity(Loc.GetString("au-zsync-changed", ("count", changed), ("list", listName)),
                ent, ent, PopupType.Medium);
        }

        // Push the fresh lists back so the open window refreshes in place.
        RaiseNetworkEvent(BuildOpenEvent(), session);
    }

    private void OnPickEntity(PickZBorderSyncEntityEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!_menu.CanUseTool(session, AU14ToolPermissions.ZSync))
            return;

        if (!TryGetEntity(msg.Entity, out var uid) || MetaData(uid.Value).EntityPrototype is not { } proto)
            return;

        // In-world picks always go to the global scope.
        var changed = ApplyListChange(GlobalScope, new List<string> { proto.ID }, msg.Blacklist, add: true);
        if (changed > 0)
        {
            SaveLists();
            ListsChanged?.Invoke();
        }

        var listName = msg.Blacklist ? "blacklist" : "whitelist";
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{session.Name} picked {proto.ID} in-round for the z-border {listName} ({changed} changed)");

        if (session.AttachedEntity is { } ent)
        {
            _popup.PopupEntity(Loc.GetString("au-zsync-picked", ("proto", proto.ID), ("list", listName)),
                ent, ent, PopupType.Medium);
        }

        RaiseNetworkEvent(BuildOpenEvent(), session);
    }

    private int ApplyListChange(string scope, List<string> protoIds, bool blacklist, bool add)
    {
        if (blacklist)
            protoIds = ExpandBlacklistPrototypeIds(protoIds);

        var lists = GetScope(scope);
        var list = blacklist ? lists.Blacklist : lists.Whitelist;
        var oppositeList = blacklist ? lists.Whitelist : lists.Blacklist;
        var changed = 0;
        foreach (var id in protoIds)
        {
            if (!_prototype.HasIndex<EntityPrototype>(id))
                continue;

            if (add)
            {
                if (list.Add(id))
                    changed++;

                if (oppositeList.Remove(id))
                    changed++;
            }
            else if (list.Remove(id))
            {
                changed++;
            }
        }

        return changed + RemoveBlacklistedWhitelistEntries();
    }

    private List<string> ExpandBlacklistPrototypeIds(List<string> protoIds)
    {
        EnsurePrototypeExpansionCache();
        var expanded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in protoIds)
        {
            if (!_prototype.TryIndex<EntityPrototype>(id, out var proto))
                continue;

            expanded.Add(id);
            AddDescendants(id, expanded);

            if (string.IsNullOrWhiteSpace(proto.Name))
                continue;

            if (!_nonAbstractByName!.TryGetValue(proto.Name, out var sameName))
                continue;

            foreach (var otherId in sameName)
            {
                expanded.Add(otherId);
                AddDescendants(otherId, expanded);
            }
        }

        return expanded.ToList();
    }

    private void AddDescendants(string parentId, HashSet<string> expanded)
    {
        EnsurePrototypeExpansionCache();

        if (!_descendantsByParent!.TryGetValue(parentId, out var descendants))
            return;

        foreach (var id in descendants)
            expanded.Add(id);
    }

    private void EnsurePrototypeExpansionCache()
    {
        if (_descendantsByParent != null && _nonAbstractByName != null)
            return;

        var descendantsByParent = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var nonAbstractByName = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var proto in _prototype.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract)
                continue;

            if (!string.IsNullOrWhiteSpace(proto.Name))
            {
                if (!nonAbstractByName.TryGetValue(proto.Name, out var sameName))
                    nonAbstractByName[proto.Name] = sameName = new List<string>();

                sameName.Add(proto.ID);
            }

            foreach (var (ancestorId, _) in _prototype.EnumerateAllParents<EntityPrototype>(proto.ID, includeSelf: false))
            {
                if (!descendantsByParent.TryGetValue(ancestorId, out var descendants))
                    descendantsByParent[ancestorId] = descendants = new List<string>();

                descendants.Add(proto.ID);
            }
        }

        _descendantsByParent = descendantsByParent;
        _nonAbstractByName = nonAbstractByName;
    }

    /// <summary>
    /// Loads the lists from user data. Line formats: "w:proto" / "b:proto" are GLOBAL entries (this is
    /// also the pre-scoping format, so old saves migrate into the global scope automatically) and
    /// "w:mapProtoId:proto" / "b:mapProtoId:proto" are per-map entries. A missing file seeds the global
    /// whitelist with every non-abstract descendant of the invincible border-wall family (the previously
    /// hard-coded rule, made editable).
    /// </summary>
    private void LoadLists()
    {
        _scopes.Clear();

        try
        {
            if (_resource.UserData.Exists(SaveFile))
            {
                using (var reader = _resource.UserData.OpenText(SaveFile))
                {
                    while (reader.ReadLine() is { } line)
                    {
                        bool blacklist;
                        if (line.StartsWith("w:", StringComparison.Ordinal))
                            blacklist = false;
                        else if (line.StartsWith("b:", StringComparison.Ordinal))
                            blacklist = true;
                        else
                            continue;

                        var rest = line[2..].Trim();
                        var scope = GlobalScope;

                        // Two segments = per-map entry; one segment = global (incl. legacy pre-scoping saves).
                        var sep = rest.IndexOf(':');
                        if (sep > 0 && sep < rest.Length - 1)
                        {
                            scope = rest[..sep];
                            rest = rest[(sep + 1)..];
                        }

                        var lists = GetScope(scope);
                        (blacklist ? lists.Blacklist : lists.Whitelist).Add(rest);
                    }
                }

                ExpandLoadedBlacklists();
                if (RemoveBlacklistedWhitelistEntries() > 0)
                    SaveLists();

                return;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load z-border sync lists: {e}");
        }

        SeedDefaults();
        SaveLists();
    }

    private void ExpandLoadedBlacklists()
    {
        var changed = false;
        foreach (var lists in _scopes.Values)
        {
            var expanded = ExpandBlacklistPrototypeIds(lists.Blacklist.ToList());
            if (expanded.Count == lists.Blacklist.Count)
                continue;

            lists.Blacklist.Clear();
            foreach (var id in expanded)
                lists.Blacklist.Add(id);

            changed = true;
        }

        if (changed)
            SaveLists();
    }

    private void SeedDefaults()
    {
        EnsurePrototypeExpansionCache();

        var global = GetScope(GlobalScope);
        foreach (var parent in DefaultBorderParents)
        {
            if (!_descendantsByParent!.TryGetValue(parent, out var descendants))
                continue;

            foreach (var id in descendants)
                global.Whitelist.Add(id);
        }

        Log.Info($"Seeded z-border sync whitelist with {global.Whitelist.Count} invincible border-wall prototypes.");
    }

    /// <summary>Removes whitelist entries overridden by a blacklist entry in the same scope or globally.</summary>
    private int RemoveBlacklistedWhitelistEntries()
    {
        var removed = 0;
        var globalBlack = GetScope(GlobalScope).Blacklist;
        foreach (var (scope, lists) in _scopes)
        {
            removed += lists.Whitelist.RemoveWhere(id =>
                PrototypeOrParentListed(id, lists.Blacklist) ||
                (scope != GlobalScope && PrototypeOrParentListed(id, globalBlack)));
        }

        return removed;
    }

    private void SaveLists()
    {
        try
        {
            using var writer = _resource.UserData.OpenWriteText(SaveFile);
            foreach (var (scope, lists) in _scopes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var prefix = scope == GlobalScope ? string.Empty : $"{scope}:";
                foreach (var id in lists.Whitelist.OrderBy(s => s, StringComparer.Ordinal))
                    writer.WriteLine($"w:{prefix}{id}");
                foreach (var id in lists.Blacklist.OrderBy(s => s, StringComparer.Ordinal))
                    writer.WriteLine($"b:{prefix}{id}");
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save z-border sync lists: {e}");
        }
    }
}
