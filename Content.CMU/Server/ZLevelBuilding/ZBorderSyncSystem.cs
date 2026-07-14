// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Server._AU14.Construction.CustomConstruction;
using Content.Shared._AU14.ZLevelBuilding;
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
/// parent but are ordinary structures, e.g. dropship walls). On first boot the implicit rule (the
/// CMBaseWallInvincible family) is materialized into explicit whitelist entries so admins can see and
/// edit it through the same menu. Persisted across rounds in the server user-data folder.
/// </summary>
public sealed class ZBorderSyncSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceManager _resource = default!;
    [Dependency] private readonly CustomConstructionMenuSystem _menu = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;

    private static readonly ResPath SaveFile = new("/au14_zborder_sync.txt");

    // The abstract roots whose descendants are border walls by default (seeded into the whitelist).
    private static readonly string[] DefaultBorderParents = { "CMBaseWallInvincible", "RMCBaseWallInvincibleNoIcon" };

    private readonly HashSet<string> _whitelist = new(StringComparer.Ordinal);
    private readonly HashSet<string> _blacklist = new(StringComparer.Ordinal);
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

    /// <summary>Whether this prototype should be mirrored across z-levels as a map border.</summary>
    public bool ShouldReflect(string protoId)
    {
        if (PrototypeOrParentListed(protoId, _blacklist))
            return false;

        return PrototypeOrParentListed(protoId, _whitelist);
    }

    private bool PrototypeOrParentListed(string protoId, HashSet<string> list)
    {
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
        if (!_menu.CanEditConstructionMenu(args.SenderSession))
            return;

        RaiseNetworkEvent(BuildOpenEvent(), args.SenderSession);
    }

    private OpenZBorderSyncEvent BuildOpenEvent() => new()
    {
        Whitelist = _whitelist.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList(),
        Blacklist = _blacklist.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList(),
    };

    private void OnModify(ModifyZBorderSyncEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!_menu.CanEditConstructionMenu(session))
            return;

        var changed = ApplyListChange(msg.ProtoIds, msg.Blacklist, msg.Add);

        if (changed > 0)
        {
            SaveLists();
            ListsChanged?.Invoke();
        }

        var listName = msg.Blacklist ? "blacklist" : "whitelist";
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{session.Name} {(msg.Add ? "added" : "removed")} {changed} prototypes {(msg.Add ? "to" : "from")} the z-border {listName}");

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
        if (!_menu.CanEditConstructionMenu(session))
            return;

        if (!TryGetEntity(msg.Entity, out var uid) || MetaData(uid.Value).EntityPrototype is not { } proto)
            return;

        var changed = ApplyListChange(new List<string> { proto.ID }, msg.Blacklist, add: true);
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

    private int ApplyListChange(List<string> protoIds, bool blacklist, bool add)
    {
        if (blacklist)
            protoIds = ExpandBlacklistPrototypeIds(protoIds);

        var list = blacklist ? _blacklist : _whitelist;
        var oppositeList = blacklist ? _whitelist : _blacklist;
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

    /// <summary>Loads the lists from user data; a missing file seeds the whitelist with every non-abstract
    /// descendant of the invincible border-wall family (the previously hard-coded rule, made editable).</summary>
    private void LoadLists()
    {
        _whitelist.Clear();
        _blacklist.Clear();

        try
        {
            if (_resource.UserData.Exists(SaveFile))
            {
                using (var reader = _resource.UserData.OpenText(SaveFile))
                {
                    while (reader.ReadLine() is { } line)
                    {
                        if (line.StartsWith("w:", StringComparison.Ordinal))
                            _whitelist.Add(line[2..].Trim());
                        else if (line.StartsWith("b:", StringComparison.Ordinal))
                            _blacklist.Add(line[2..].Trim());
                    }
                }

                ExpandLoadedBlacklist();
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

    private void ExpandLoadedBlacklist()
    {
        var expanded = ExpandBlacklistPrototypeIds(_blacklist.ToList());
        if (expanded.Count == _blacklist.Count)
            return;

        _blacklist.Clear();
        foreach (var id in expanded)
            _blacklist.Add(id);

        SaveLists();
    }

    private void SeedDefaults()
    {
        EnsurePrototypeExpansionCache();

        foreach (var parent in DefaultBorderParents)
        {
            if (!_descendantsByParent!.TryGetValue(parent, out var descendants))
                continue;

            foreach (var id in descendants)
                _whitelist.Add(id);
        }

        Log.Info($"Seeded z-border sync whitelist with {_whitelist.Count} invincible border-wall prototypes.");
    }

    private int RemoveBlacklistedWhitelistEntries()
    {
        return _whitelist.RemoveWhere(id => PrototypeOrParentListed(id, _blacklist));
    }

    private void SaveLists()
    {
        try
        {
            using var writer = _resource.UserData.OpenWriteText(SaveFile);
            foreach (var id in _whitelist.OrderBy(s => s, StringComparer.Ordinal))
                writer.WriteLine($"w:{id}");
            foreach (var id in _blacklist.OrderBy(s => s, StringComparer.Ordinal))
                writer.WriteLine($"b:{id}");
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save z-border sync lists: {e}");
        }
    }
}
