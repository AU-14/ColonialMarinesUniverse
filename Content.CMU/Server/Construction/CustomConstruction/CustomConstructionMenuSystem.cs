// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.IO;
using System.Linq;
using System.Text;
using Content.Server.Administration.Managers;
using Content.Server.Players.JobWhitelist;
using Content.Shared._AU14.Construction.CustomConstruction;
using Content.Shared.Administration;
using Content.Shared.Administration.Logs;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Construction.CustomConstruction;

/// <summary>
/// Lets a permitted admin add/remove/re-recipe an arbitrary world entity to/from the construction
/// menu via its world right-click context menu (Construction &gt; Add / Change Recipe / Remove), and
/// services the in-menu admin editor button.
///
/// <para>
/// Persistence is "restart-applied": each added item is written as a self-contained construction
/// prototype YAML file under <c>Resources/Prototypes/_AU14/CustomConstruction/Generated/</c>. The
/// entry lives in the codebase as a normal prototype file, so it is loaded on the next restart,
/// committed to git, and shipped to clients via the content pack. Add = write file, Remove = delete
/// file, Change Recipe = rewrite file. A machine-readable header records spawnlist/category/steps so
/// the editor can prefill and the menu can group entries.
/// </para>
///
/// <para>
/// The editor UI itself is a client window (see <see cref="OpenCustomConstructionEditorEvent"/>). The
/// verbs raise that event to the requesting client; the client sends back a
/// <see cref="SubmitCustomConstructionEditorEvent"/> which this system re-validates, logs, and writes.
/// </para>
/// </summary>
public sealed partial class CustomConstructionMenuSystem : EntitySystem
{
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private Administration.AU14ToolPermissionSystem _toolPerms = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private ITileDefinitionManager _tileDefManager = default!;

    /// <summary>
    /// The admin flag required to use the feature. Single permission extension point: swap this (or
    /// override <see cref="CanEditConstructionMenu"/>) to map onto whatever rank you want —
    /// e.g. <see cref="AdminFlags.Moderator"/>, <see cref="AdminFlags.Host"/>. Admin ranks already
    /// compose these flags, so "Moderator / Senior Moderator / Owner" is expressed through admin ranks.
    /// </summary>
    public const AdminFlags RequiredFlag = AdminFlags.Host;

    // AU14: the old JModEditor job-whitelist gate was replaced by per-tool ckey grants (see
    // AU14ToolPermissionSystem) because jobwhitelistadd was reachable by lower admin ranks. Trusted
    // non-admins are now granted per tool through the Tool Permissions window or the toolperm command.

    private const string GeneratedSubDir = "Prototypes/_AU14/CustomConstruction/Generated";
    private const string DefaultSpawnlist = "AU14";
    private const string DefaultCategory = "Custom";

    /// <summary>
    /// Material stack id -> the single-sheet entity refunded per unit when a built structure is deconstructed.
    /// Lets every menu-added structure be taken back down for its materials (drops generally = materials used).
    /// </summary>
    private static readonly Dictionary<string, string> MaterialSheets = new()
    {
        ["CMSteel"] = "CMSheetMetal1",
        ["CMPlasteel"] = "CMSheetPlasteel1",
        ["CMGlass"] = "CMSheetGlass1",
        ["CMGlassReinforced"] = "CMSheetGlassReinforced1",
        ["CMGlassPhoron"] = "CMSheetGlassPhoron1",
        ["CMPhoron"] = "CMSheetPhoron1",
        ["RMCPlastic"] = "RMCSheetPlastic1",
        ["RMCAluminum"] = "RMCSheetAluminum1",
        ["RMCWood"] = "RMCPlankWood1",
        ["RMCSheetCardboard"] = "RMCSheetCardboard1",
    };

    private const string HeaderEntity = "# entity:";
    private const string HeaderSpawnlist = "# spawnlist:";
    private const string HeaderCategory = "# category:";
    private const string HeaderSteps = "# steps:";
    private const string HeaderDeconstruct = "# deconstruct:";
    private const string HeaderHealth = "# health:";

    /// <summary>The generic "under construction" frame every multi-step recipe's intermediate node parents
    /// (see construction_frame.yml). Keeps the real entity from appearing until the last step is done.</summary>
    private const string ConstructionFrameProto = "AU14CustomConstructionFrame";

    private string? _generatedDir;

    public override void Initialize()
    {
        base.Initialize();

        _generatedDir = ResolveGeneratedDir();

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeNetworkEvent<SubmitCustomConstructionEditorEvent>(OnSubmit);
        SubscribeNetworkEvent<RequestOpenCustomConstructionEditorEvent>(OnRequestOpen);
        SubscribeNetworkEvent<RemoveCustomConstructionEntryEvent>(OnRemoveEntryNetwork);
        SubscribeNetworkEvent<RemoveCustomConstructionGroupEvent>(OnRemoveGroup);
        SubscribeNetworkEvent<HideConstructionRecipeEvent>(OnHideRecipe);

        // The "Mass Entity Editor" batch tool (see the .Mass.cs partial).
        InitializeMass();

        // The "Spawnlist Delete" tool (see the .SpawnlistDelete.cs partial).
        InitializeSpawnlistDelete();

        // The "Tiles" and "Lathe" sibling editors (see the .Tiles.cs / .Lathe.cs partials).
        SubscribeNetworkEvent<RequestOpenCustomTileEditorEvent>(OnRequestOpenTile);
        SubscribeNetworkEvent<SubmitCustomTileEditorEvent>(OnSubmitTile);
        SubscribeNetworkEvent<RequestOpenCustomLatheEditorEvent>(OnRequestOpenLathe);
        SubscribeNetworkEvent<SubmitCustomLatheEditorEvent>(OnSubmitLathe);
        SubscribeNetworkEvent<RemoveCustomLatheRecipeEvent>(OnRemoveLatheRecipe);

        // These packs are referenced by static lathe prototypes. Keep empty fallbacks available even
        // when Generated/ is intentionally not part of the repo and the DB has no custom recipes.
        EnsureLathePackFallbacks();

        // The database is the durable store (the Docker filesystem is wiped on redeploy): put back
        // any stored entry whose generated file is gone, and hot-load its prototypes for this boot.
        RestoreFromDatabase();

        // Safety net: drop any previously-generated entries that reference things that no longer exist
        // (e.g. a recipe saved with an invalid material before validation existed). Prevents broken
        // "can't build / no steps" menu entries from lingering.
        ValidateExistingEntries();

        // Drop hide-tombstones for generated entries that no longer exist after this restart.
        PruneStaleHiddenRecipes();
    }

    /// <summary>
    /// Permission gate. Override/replace to restrict to specific user types. Currently backed by the
    /// existing admin flag system (<see cref="RequiredFlag"/>).
    /// </summary>
    public bool CanEditConstructionMenu(ICommonSession session)
    {
        return CanUseTool(session, Content.Shared._AU14.Administration.AU14ToolPermissions.Construction);
    }

    /// <summary>Per-tool gate: a Host-flagged admin, or a ckey granted this specific tool through the
    /// Tool Permissions system (see <see cref="Administration.AU14ToolPermissionSystem"/>).</summary>
    public bool CanUseTool(ICommonSession session, string tool)
    {
        return _adminManager.HasAdminFlag(session, RequiredFlag) || _toolPerms.HasGrant(session, tool);
    }

    // -------------------------------------------------------------------------
    // Verbs
    // -------------------------------------------------------------------------

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var session = actor.PlayerSession;
        if (!CanEditConstructionMenu(session))
            return;

        if (MetaData(args.Target).EntityPrototype is not { } clicked)
            return;

        // Right-clicking a BUILT custom item gives the generated child prototype (AU14CustomEntity_<key>),
        // not the original. Resolve back to the original entity so Add/Change/Remove operate on it.
        var proto = ResolveOriginalProto(clicked);

        var entries = GetEntriesFor(proto.ID);

        // Add is always available — an entity can have multiple entries (different recipe/spawnlist/category).
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("construction-menu-verb-add"),
            Category = VerbCategory.Construction,
            Act = () => OpenEditorFor(session, proto, entryKey: null),
            Impact = LogImpact.Medium,
            Message = Loc.GetString("construction-menu-verb-add-message"),
        });

        if (entries.Count == 0)
        {
            // Greyed-out hint that there's nothing to change/remove yet.
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("construction-menu-verb-change-recipe"),
                Category = VerbCategory.Construction,
                Disabled = true,
                Act = null,
                Message = Loc.GetString("construction-menu-verb-change-recipe-disabled"),
            });
            return;
        }

        // One verb per existing entry. With multiple entries we disambiguate by "spawnlist / category" so
        // the admin picks which one to edit/remove straight from the context menu (no extra dialog needed).
        var multiple = entries.Count > 1;
        foreach (var entry in entries)
        {
            var key = entry.Key;
            var suffix = multiple ? $" — {entry.Info.Spawnlist} / {entry.Info.Category}" : string.Empty;

            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("construction-menu-verb-change-recipe") + suffix,
                Category = VerbCategory.Construction,
                Act = () => OpenEditorFor(session, proto, key),
                Impact = LogImpact.Medium,
                Message = Loc.GetString("construction-menu-verb-change-recipe-message"),
            });

            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("construction-menu-verb-remove") + suffix,
                Category = VerbCategory.Construction,
                Act = () => RemoveEntry(session, args.User, proto, key),
                Impact = LogImpact.Medium,
                Message = Loc.GetString("construction-menu-verb-remove-message"),
            });
        }
    }

    // -------------------------------------------------------------------------
    // Editor open / submit
    // -------------------------------------------------------------------------

    private void OpenEditorFor(ICommonSession session, EntityPrototype proto, string? entryKey)
    {
        if (_generatedDir == null)
        {
            if (session.AttachedEntity is { } ent)
                _popup.PopupEntity(Loc.GetString("construction-menu-verb-no-resources"), ent, ent, PopupType.MediumCaution);
            return;
        }

        var isEdit = !string.IsNullOrEmpty(entryKey);
        var current = isEdit ? ReadHeaders(FilePathForKey(entryKey!)) : null;
        var ev = new OpenCustomConstructionEditorEvent
        {
            ProtoId = proto.ID,
            ItemName = proto.Name,
            IsEdit = isEdit,
            EntryKey = entryKey ?? string.Empty,
            Spawnlist = current?.Spawnlist ?? DefaultSpawnlist,
            Category = current?.Category ?? DefaultCategory,
            Steps = current?.Steps ?? DefaultSteps(),
            DeconstructSteps = current?.DeconstructSteps ?? new List<CustomConstructionStepData>(),
            Health = current?.Health ?? 0,
            AvailableSpawnlists = EnumerateSpawnlists(),
            AvailableCategoriesBySpawnlist = EnumerateCategoriesBySpawnlist(),
        };

        RaiseNetworkEvent(ev, session);
    }

    /// <summary>In-menu "Construction Items Editor": open the editor for the picked entity (admin-gated).</summary>
    private void OnRequestOpen(RequestOpenCustomConstructionEditorEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanEditConstructionMenu(session))
            return;

        if (!_prototype.TryIndex<EntityPrototype>(msg.ProtoId, out var proto))
            return;

        // The chooser displays the generated child that is actually built. Recipe files, however, are keyed to
        // the original prototype recorded in their header. Resolve that child before looking up or editing its
        // recipe; otherwise submitting "Change Recipe" is rejected by the generated-entity nesting safeguard.
        proto = ResolveOriginalProto(proto);

        // Editing a specific existing entry (Change Recipe from the chooser): straight into the editor.
        if (!string.IsNullOrEmpty(msg.EntryKey))
        {
            OpenEditorFor(session, proto, msg.EntryKey);
            return;
        }

        // If the entity already has recipes and we weren't told to force a new one, show the chooser so the
        // admin can change/remove an existing recipe or pick "Add new recipe".
        var entries = GetEntriesFor(proto.ID);
        if (!msg.ForceAddNew && entries.Count > 0)
        {
            var ev = new OpenCustomConstructionChooserEvent { ProtoId = proto.ID, ItemName = proto.Name };
            foreach (var (key, info) in entries)
                ev.Entries.Add(new CustomConstructionEntryInfo { EntryKey = key, Spawnlist = info.Spawnlist, Category = info.Category });
            RaiseNetworkEvent(ev, session);
            return;
        }

        OpenEditorFor(session, proto, entryKey: null);
    }

    private void OnRemoveEntryNetwork(RemoveCustomConstructionEntryEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanEditConstructionMenu(session) || session.AttachedEntity is not { } user)
            return;

        if (string.IsNullOrEmpty(msg.EntryKey) || !_prototype.TryIndex<EntityPrototype>(msg.ProtoId, out var proto))
            return;

        proto = ResolveOriginalProto(proto);
        RemoveEntry(session, user, proto, msg.EntryKey);
    }

    private void OnRemoveGroup(RemoveCustomConstructionGroupEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanEditConstructionMenu(session) || _generatedDir == null || !Directory.Exists(_generatedDir))
            return;

        if (string.IsNullOrWhiteSpace(msg.Spawnlist))
            return;

        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(_generatedDir, "*.yml"))
        {
            var info = ReadHeaders(file);
            if (info == null || !string.Equals(info.Spawnlist, msg.Spawnlist, StringComparison.Ordinal))
                continue;
            if (!string.IsNullOrEmpty(msg.Category) && !string.Equals(info.Category, msg.Category, StringComparison.Ordinal))
                continue;

            try
            {
                RetireEntryFile(file);
                removed++;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to remove generated entry {file}: {e}");
            }
        }

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{session.Name} bulk-removed {removed} construction menu recipes (spawnlist: {msg.Spawnlist}, category: {(string.IsNullOrEmpty(msg.Category) ? "ALL" : msg.Category)})");

        PopupTo(session, Loc.GetString("construction-menu-group-removed",
            ("count", removed), ("spawnlist", msg.Spawnlist), ("category", string.IsNullOrEmpty(msg.Category) ? "all" : msg.Category)),
            PopupType.LargeCaution);
    }

    // -------------------------------------------------------------------------
    // Hide recipe (menu "Remove Item" - works for vanilla recipes too)
    // -------------------------------------------------------------------------

    private const string OverridesSubDir = "Overrides";
    private const string OverridesPrototypeId = "AU14MenuOverrides";
    private const string OverridesFileName = "AU14MenuOverrides.yml";

    /// <summary>
    /// Hides a construction recipe from the menu by id, recording it in the generated overrides prototype.
    /// Unlike the entry-based remove (which deletes a file WE generated), this can hide ANY recipe - including
    /// hand-authored vanilla ones - because the client filters them out by id at menu-population time.
    /// </summary>
    private void OnHideRecipe(HideConstructionRecipeEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!CanEditConstructionMenu(session) || _generatedDir == null)
            return;

        var recipeId = msg.RecipeId?.Trim();
        if (string.IsNullOrEmpty(recipeId))
            return;

        // Must be a REAL construction recipe id: kills YAML injection through the overrides file (the id is
        // written into it verbatim) and stops garbage ids accumulating forever.
        if (!_prototype.HasIndex<Content.Shared.Construction.Prototypes.ConstructionPrototype>(recipeId))
            return;

        var result = HideRecipeId(recipeId);
        if (result == HideResult.AlreadyHidden)
        {
            PopupTo(session, Loc.GetString("construction-menu-recipe-already-hidden", ("recipe", recipeId)), PopupType.Medium);
            return;
        }

        if (result == HideResult.Failed)
        {
            PopupTo(session, Loc.GetString("construction-menu-recipe-hide-failed"), PopupType.MediumCaution);
            return;
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{session.Name} removed (hid) construction menu recipe {recipeId}");
        PopupTo(session, Loc.GetString("construction-menu-recipe-hidden", ("recipe", recipeId)), PopupType.Medium);
    }

    private enum HideResult { Hidden, AlreadyHidden, Failed }

    /// <summary>
    /// Adds a recipe id to the hidden-overrides prototype, persists it (file + DB) and publishes the updated
    /// overrides to the server and every client, so the hide applies THIS round. Also used when a generated
    /// entry is deleted: clients can't unload prototypes at runtime, so hiding the id is what actually makes
    /// the deleted recipe vanish from open menus.
    /// </summary>
    private HideResult HideRecipeId(string recipeId)
    {
        if (_generatedDir == null)
            return HideResult.Failed;

        var dir = Path.Combine(_generatedDir, OverridesSubDir);
        var path = Path.Combine(dir, OverridesFileName);

        var hidden = ReadHiddenRecipes(path);
        if (!hidden.Add(recipeId))
            return HideResult.AlreadyHidden;

        try
        {
            Directory.CreateDirectory(dir);
            var overridesYaml = BuildOverridesYaml(hidden);
            File.WriteAllText(path, overridesYaml, Encoding.UTF8);
            DbUpsert(DbKindOverrides, Path.GetFileNameWithoutExtension(OverridesFileName), overridesYaml);
            PublishYaml(overridesYaml, "menu overrides");
        }
        catch (Exception e)
        {
            Log.Error($"Failed to write construction menu overrides for hidden recipe {recipeId}: {e}");
            return HideResult.Failed;
        }

        return HideResult.Hidden;
    }

    /// <summary>
    /// Removes a recipe id from the hidden-overrides list (file + DB + live publish). Called when an entry
    /// is (re)added, so a delete-tombstone from earlier in the round can't keep the new recipe invisible.
    /// </summary>
    private void UnhideRecipeId(string recipeId)
    {
        if (UnhideRecipeIdsPersist(new[] { recipeId }) is { } overridesYaml)
            PublishYaml(overridesYaml, "menu overrides");
    }

    /// <summary>
    /// Batch unhide WITHOUT publishing: removes every id from the hidden-overrides list and persists the
    /// result (file + DB) in one write. Returns the updated overrides YAML so the caller can fold it into
    /// its own single publish, or null when nothing changed. Publishing is the expensive part (it reloads
    /// every localization on server and clients), so batch save paths must do it exactly once - calling
    /// the per-id publish in a loop is what made big mass-editor saves hang the server mid-round.
    /// </summary>
    private string? UnhideRecipeIdsPersist(IEnumerable<string> recipeIds)
    {
        if (_generatedDir == null)
            return null;

        var dir = Path.Combine(_generatedDir, OverridesSubDir);
        var path = Path.Combine(dir, OverridesFileName);

        var hidden = ReadHiddenRecipes(path);
        var changed = false;
        foreach (var recipeId in recipeIds)
            changed |= hidden.Remove(recipeId);

        if (!changed)
            return null;

        try
        {
            Directory.CreateDirectory(dir);
            var overridesYaml = BuildOverridesYaml(hidden);
            File.WriteAllText(path, overridesYaml, Encoding.UTF8);
            DbUpsert(DbKindOverrides, Path.GetFileNameWithoutExtension(OverridesFileName), overridesYaml);
            return overridesYaml;
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to unhide recipe ids: {e}");
            return null;
        }
    }

    /// <summary>
    /// Boot-time cleanup for the overrides list: ids of OUR generated entries (AU14Custom_/AU14Tile_) are
    /// hidden as a client-side tombstone when the entry is deleted mid-round; once a real restart has
    /// happened the prototype is gone for good and the tombstone is dead weight, so drop it. Hand-hidden
    /// vanilla recipe ids are always kept.
    /// </summary>
    private void PruneStaleHiddenRecipes()
    {
        if (_generatedDir == null)
            return;

        var dir = Path.Combine(_generatedDir, OverridesSubDir);
        var path = Path.Combine(dir, OverridesFileName);
        var hidden = ReadHiddenRecipes(path);
        if (hidden.Count == 0)
            return;

        var removed = hidden.RemoveWhere(id =>
            (id.StartsWith(FilePrefix, StringComparison.Ordinal) || id.StartsWith(TileFilePrefix, StringComparison.Ordinal))
            && !_prototype.HasIndex<Content.Shared.Construction.Prototypes.ConstructionPrototype>(id));

        if (removed == 0)
            return;

        try
        {
            var overridesYaml = BuildOverridesYaml(hidden);
            File.WriteAllText(path, overridesYaml, Encoding.UTF8);
            DbUpsert(DbKindOverrides, Path.GetFileNameWithoutExtension(OverridesFileName), overridesYaml);
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to prune stale hidden recipe ids: {e}");
        }
    }

    /// <summary>Reads the hidden-recipe id set from the overrides file (the <c>hiddenRecipes:</c> sequence).</summary>
    private HashSet<string> ReadHiddenRecipes(string path)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(path))
            return set;

        try
        {
            var inBlock = false;
            foreach (var raw in File.ReadLines(path))
            {
                var trimmed = raw.Trim();
                if (trimmed.StartsWith("hiddenRecipes:", StringComparison.Ordinal))
                {
                    inBlock = true;
                    continue;
                }

                if (!inBlock || trimmed.StartsWith('#') || trimmed.Length == 0)
                    continue;

                if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                    set.Add(trimmed[2..].Trim());
                else
                    inBlock = false; // left the sequence
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to read construction menu overrides {path}: {e}");
        }

        return set;
    }

    private static string BuildOverridesYaml(HashSet<string> hidden)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Auto-generated by the AU14 construction menu editor (\"Remove Item\").");
        sb.AppendLine("# Construction recipe ids hidden from the menu. Safe to edit, commit, or delete.");
        sb.AppendLine();
        sb.AppendLine("- type: au14MenuOverrides");
        sb.AppendLine($"  id: {OverridesPrototypeId}");
        sb.AppendLine("  hiddenRecipes:");
        foreach (var id in hidden.OrderBy(s => s, StringComparer.Ordinal))
            sb.AppendLine($"  - {id}");
        return sb.ToString();
    }

    private void OnSubmit(SubmitCustomConstructionEditorEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        // Re-validate server-side: never trust the client's view of permission.
        if (!CanEditConstructionMenu(session))
            return;

        if (_generatedDir == null)
            return;

        if (!_prototype.TryIndex<EntityPrototype>(msg.ProtoId, out var proto))
            return;

        if (IsGeneratedCustomEntityId(proto.ID))
        {
            PopupTo(session, Loc.GetString("construction-menu-verb-invalid",
                ("reason", "that is already a generated custom construction entity")), PopupType.MediumCaution);
            return;
        }

        var steps = msg.Steps ?? new();
        if (steps.Count == 0)
        {
            PopupTo(session, Loc.GetString("construction-menu-verb-bad-recipe"), PopupType.MediumCaution);
            return;
        }

        // Validate every referenced prototype exists BEFORE writing — an invalid material/tool produces
        // a recipe that silently fails to build ("not enough materials" / no steps shown). Refuse and
        // tell the admin exactly what's wrong instead of generating a broken entry.
        var isItemRecipe = proto.TryGetComponent<ItemComponent>(out _, _componentFactory);
        if (!ValidateSteps(steps, isItemRecipe, out var invalidReason))
        {
            PopupTo(session, Loc.GetString("construction-menu-verb-invalid", ("reason", invalidReason)), PopupType.MediumCaution);
            return;
        }

        // Deconstruction steps are optional (empty = default crowbar) and only meaningful for structures.
        var deconstructSteps = (isItemRecipe ? null : msg.DeconstructSteps) ?? new();
        if (!ValidateDeconstructSteps(deconstructSteps, out var deconstructReason))
        {
            PopupTo(session, Loc.GetString("construction-menu-verb-invalid", ("reason", deconstructReason)), PopupType.MediumCaution);
            return;
        }

        // Whitelisted: these strings are embedded verbatim in generated YAML, so they must never carry
        // newlines or YAML syntax (injection), and must keep the DB entry key under its column limit.
        var spawnlist = SanitizeName(msg.Spawnlist, DefaultSpawnlist);
        var category = SanitizeName(msg.Category, DefaultCategory);

        // An entry is keyed by entity + spawnlist + category. Editing and changing the spawnlist/category
        // moves the entry; the old version remains authoritative until the replacement publishes successfully.
        var newKey = MakeEntryKey(proto.ID, spawnlist, category);
        var isChange = !string.IsNullOrEmpty(msg.EntryKey);

        try
        {
            var yaml = BuildGeneratedYaml(proto, newKey, spawnlist, category, steps, deconstructSteps, msg.Health);
            if (IsUnsafeGeneratedEntryYaml(yaml, out var reason) || IsOversizedYaml(yaml, out reason))
            {
                Log.Error($"Refusing to write unsafe custom construction entry for {proto.ID} (key {newKey}): {reason}");
                PopupTo(session, Loc.GetString("construction-menu-verb-invalid", ("reason", reason)), PopupType.MediumCaution);
                return;
            }

            // Dry run: report exactly what WOULD be written and stop. The client shows the scrollable
            // confirmation window and re-sends with Preview = false.
            if (msg.Preview)
            {
                var preview = new OpenDbSavePreviewEvent { Kind = "construction-db-preview-kind-entry", Planned = 1 };
                if (isChange && !string.Equals(msg.EntryKey, newKey, StringComparison.Ordinal))
                {
                    preview.Lines.Add($"DELETE file {FilePrefix}{msg.EntryKey}.yml + DB row (entries/{FilePrefix}{msg.EntryKey}) - entry moved");
                }
                preview.Lines.Add($"WRITE file {FilePrefix}{newKey}.yml ({Encoding.UTF8.GetByteCount(yaml)} bytes)");
                preview.Lines.Add($"UPSERT DB row (entries/{FilePrefix}{newKey}) - {proto.ID}, spawnlist '{spawnlist}', category '{category}'");
                RaiseNetworkEvent(preview, session);
                return;
            }

            Directory.CreateDirectory(_generatedDir);

            var newPath = FilePathForKey(newKey);
            var oldPath = isChange ? FilePathForKey(msg.EntryKey) : null;
            var oldYaml = oldPath != null && File.Exists(oldPath) ? File.ReadAllText(oldPath) : null;
            var stagedPath = $"{newPath}.{Guid.NewGuid():N}.pending";
            File.WriteAllText(stagedPath, yaml, Encoding.UTF8);

            // Apply live: load on the server (overwrite) and push to every client, so the new/changed
            // recipe shows up this round instead of "after the next restart". Publishing reloads every
            // localization, so the entry and its overrides unhide share ONE publish.
            var overridesYaml = UnhideRecipeIdsPersist(new[] { $"{FilePrefix}{newKey}" });
            var publishYaml = overridesYaml == null ? yaml : yaml + "\n" + overridesYaml;
            if (!PublishYaml(publishYaml, $"entry {newKey}"))
            {
                File.Delete(stagedPath);
                if (oldYaml != null)
                    PublishYaml(oldYaml, $"rollback entry {msg.EntryKey}");
                PopupTo(session, Loc.GetString("construction-menu-verb-add-failed"), PopupType.MediumCaution);
                return;
            }

            try
            {
                File.Move(stagedPath, newPath, overwrite: true);
                DbUpsert(DbKindEntries, $"{FilePrefix}{newKey}", yaml);
            }
            catch
            {
                if (File.Exists(stagedPath))
                    File.Delete(stagedPath);

                if (oldYaml != null)
                    PublishYaml(oldYaml, $"rollback entry {msg.EntryKey}");
                else
                    UnloadYaml(yaml, $"rollback entry {newKey}");
                throw;
            }

            if (oldPath != null && !string.Equals(msg.EntryKey, newKey, StringComparison.Ordinal))
                RetireEntryFile(oldPath);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to write custom construction entry for {proto.ID} (key {newKey}): {e}");
            PopupTo(session, Loc.GetString("construction-menu-verb-add-failed"), PopupType.MediumCaution);
            return;
        }

        var recipeText = DescribeRecipe(steps);
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{session.Name} {(isChange ? "changed the recipe of" : "added")} construction menu item {proto.ID} (spawnlist: {spawnlist}, category: {category}, recipe: {recipeText})");

        PopupTo(session, isChange
            ? Loc.GetString("construction-menu-verb-recipe-changed", ("item", proto.Name), ("recipe", recipeText))
            : Loc.GetString("construction-menu-verb-added", ("item", proto.Name), ("recipe", recipeText), ("category", category)),
            PopupType.Medium);
    }

    /// <summary>
    /// Deletes a generated entry file everywhere it lives: disk, DB row, the server's loaded prototypes,
    /// and (because clients can't unload prototypes mid-round) hides its construction id as a client-side
    /// tombstone so it vanishes from menus immediately. The tombstone is pruned on the next restart.
    /// </summary>
    private void RetireEntryFile(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        string? yaml = null;
        if (File.Exists(path))
        {
            try
            {
                yaml = File.ReadAllText(path);
                File.Delete(path);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to delete generated entry {path}: {e}");
                throw;
            }
        }

        DbDelete(DbKindEntries, stem);

        if (yaml != null)
            UnloadYaml(yaml, stem);

        // The generated construction recipe id IS the file stem (AU14Custom_<key> / AU14Tile_<key>).
        HideRecipeId(stem);
    }

    private void RemoveEntry(ICommonSession session, EntityUid user, EntityPrototype proto, string entryKey)
    {
        if (_generatedDir == null)
        {
            _popup.PopupEntity(Loc.GetString("construction-menu-verb-no-resources"), user, user, PopupType.MediumCaution);
            return;
        }

        try
        {
            RetireEntryFile(FilePathForKey(entryKey));
        }
        catch (Exception e)
        {
            Log.Error($"Failed to remove custom construction entry {entryKey}: {e}");
            _popup.PopupEntity(Loc.GetString("construction-menu-verb-remove-failed"), user, user, PopupType.MediumCaution);
            return;
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{session.Name} removed construction menu entry {entryKey} (entity {proto.ID})");

        _popup.PopupEntity(Loc.GetString("construction-menu-verb-removed", ("item", proto.Name)), user, user, PopupType.Medium);
    }

    private void PopupTo(ICommonSession session, string message, PopupType type)
    {
        if (session.AttachedEntity is { } ent)
            _popup.PopupEntity(message, ent, ent, type);
    }

    // -------------------------------------------------------------------------
    // Persistence helpers
    // -------------------------------------------------------------------------

    private const string FilePrefix = "AU14Custom_";

    /// <summary>Prefix of the generated buildable child entity id (see <see cref="BuildGeneratedYaml"/>).</summary>
    private const string ChildEntityPrefix = "AU14CustomEntity_";
    private const string MidEntityPrefix = "AU14CustomEntityMid_";

    /// <summary>An entry is uniquely identified by entity + spawnlist + category.</summary>
    private string MakeEntryKey(string entityId, string spawnlist, string category) =>
        $"{Sanitize(entityId)}__{Sanitize(spawnlist)}__{Sanitize(category)}";

    private static bool IsGeneratedCustomEntityId(string id)
    {
        return id.StartsWith(ChildEntityPrefix, StringComparison.Ordinal) ||
               id.StartsWith(MidEntityPrefix, StringComparison.Ordinal);
    }

    private static bool IsUnsafeGeneratedEntryYaml(string yaml, out string reason)
    {
        reason = string.Empty;

        string? currentEntity = null;
        foreach (var raw in yaml.Split('\n'))
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith(HeaderEntity, StringComparison.Ordinal))
            {
                var original = trimmed[HeaderEntity.Length..].Trim();
                if (IsGeneratedCustomEntityId(original))
                {
                    reason = $"generated entries cannot target generated custom entity '{original}'";
                    return true;
                }

                continue;
            }

            if (trimmed.StartsWith("id:", StringComparison.Ordinal))
            {
                currentEntity = trimmed["id:".Length..].Trim();
                continue;
            }

            if (!trimmed.StartsWith("parent:", StringComparison.Ordinal))
                continue;

            var parent = trimmed["parent:".Length..].Trim();
            if (!IsGeneratedCustomEntityId(parent))
                continue;

            reason = $"generated entity '{currentEntity ?? "<unknown>"}' inherits from generated custom entity '{parent}'";
            return true;
        }

        return false;
    }

    private string FilePathForKey(string entryKey) => Path.Combine(_generatedDir!, $"{FilePrefix}{entryKey}.yml");

    private static string KeyFromPath(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        return stem.StartsWith(FilePrefix, StringComparison.Ordinal) ? stem[FilePrefix.Length..] : stem;
    }

    /// <summary>
    /// Maps a right-clicked prototype back to the original entity. A BUILT custom item is the generated
    /// child (<see cref="ChildEntityPrefix"/>&lt;key&gt;); its file header records the real entity id.
    /// Anything else is already the original.
    /// </summary>
    private EntityPrototype ResolveOriginalProto(EntityPrototype clicked)
    {
        if (_generatedDir != null && clicked.ID.StartsWith(ChildEntityPrefix, StringComparison.Ordinal))
        {
            var key = clicked.ID[ChildEntityPrefix.Length..];
            var info = ReadHeaders(FilePathForKey(key));
            if (info != null && !string.IsNullOrEmpty(info.Entity) &&
                _prototype.TryIndex<EntityPrototype>(info.Entity, out var original))
            {
                return original;
            }
        }

        return clicked;
    }

    /// <summary>All generated entries whose target entity is <paramref name="entityId"/>, with their keys.</summary>
    private List<(string Key, EntryInfo Info)> GetEntriesFor(string entityId)
    {
        var result = new List<(string, EntryInfo)>();
        if (_generatedDir == null || !Directory.Exists(_generatedDir))
            return result;

        foreach (var file in Directory.EnumerateFiles(_generatedDir, "*.yml"))
        {
            var info = ReadHeaders(file);
            if (info != null && string.Equals(info.Entity, entityId, StringComparison.Ordinal))
                result.Add((KeyFromPath(file), info));
        }

        return result
            .OrderBy(e => e.Item2.Spawnlist, StringComparer.InvariantCulture)
            .ThenBy(e => e.Item2.Category, StringComparer.InvariantCulture)
            .ToList();
    }

    private List<CustomConstructionStepData> DefaultSteps()
    {
        return new List<CustomConstructionStepData>
        {
            new()
            {
                Kind = CustomConstructionStepKind.Material,
                Value = _cfg.GetCVar(CCVars.ConstructionMenuAddDefaultMaterial),
                Amount = Math.Max(1, _cfg.GetCVar(CCVars.ConstructionMenuAddDefaultAmount)),
                DoAfter = 1f,
            },
        };
    }

    private List<string> EnumerateSpawnlists()
    {
        var lists = new HashSet<string> { DefaultSpawnlist };
        if (_generatedDir != null && Directory.Exists(_generatedDir))
        {
            foreach (var file in Directory.EnumerateFiles(_generatedDir, "*.yml"))
            {
                var info = ReadHeaders(file);
                if (info != null && !string.IsNullOrWhiteSpace(info.Spawnlist))
                    lists.Add(info.Spawnlist);
            }
        }

        return lists.OrderBy(s => s, StringComparer.InvariantCulture).ToList();
    }

    /// <summary>
    /// Reads spawnlist → categories from the generated entry file headers. Includes categories created
    /// in-game whose prototypes haven't loaded yet (they only load on the next restart), so the editor's
    /// category dropdown reflects them for a freshly-created spawnlist.
    /// </summary>
    private Dictionary<string, List<string>> EnumerateCategoriesBySpawnlist()
    {
        var byList = new Dictionary<string, SortedSet<string>>(StringComparer.InvariantCulture);
        if (_generatedDir != null && Directory.Exists(_generatedDir))
        {
            foreach (var file in Directory.EnumerateFiles(_generatedDir, "*.yml"))
            {
                var info = ReadHeaders(file);
                if (info == null || string.IsNullOrWhiteSpace(info.Spawnlist) || string.IsNullOrWhiteSpace(info.Category))
                    continue;

                if (!byList.TryGetValue(info.Spawnlist, out var set))
                {
                    set = new SortedSet<string>(StringComparer.InvariantCulture);
                    byList[info.Spawnlist] = set;
                }
                set.Add(info.Category);
            }
        }

        return byList.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
    }

    /// <summary>
    /// Stack type of an entity prototype, when it is stackable at all (barbed wire, sheets, rods...).
    /// Recipe amounts for these mean UNITS of the stack, not that many separate items.
    /// </summary>
    private bool TryGetStackType(string protoId, out string stackType)
    {
        stackType = string.Empty;

        if (!_prototype.TryIndex<EntityPrototype>(protoId, out var proto) ||
            !proto.TryGetComponent<StackComponent>(out var stack, _componentFactory))
            return false;

        if (string.IsNullOrWhiteSpace(stack.StackTypeId))
            return false;

        stackType = stack.StackTypeId;
        return true;
    }

    private sealed record EntryInfo(string Entity, string Spawnlist, string Category, List<CustomConstructionStepData> Steps, List<CustomConstructionStepData> DeconstructSteps, int Health);

    private EntryInfo? ReadHeaders(string path)
    {
        if (!File.Exists(path))
            return null;

        string entity = string.Empty, spawnlist = DefaultSpawnlist, category = DefaultCategory, stepsRaw = string.Empty, deconstructRaw = string.Empty;
        var health = 0;
        try
        {
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith(HeaderHealth))
                {
                    int.TryParse(line[HeaderHealth.Length..].Trim(), out health);
                    continue;
                }
                // Order matters: "# deconstruct:" must be tested before "# steps:" would never match it, but
                // since both are distinct prefixes the order between them is irrelevant - each is exclusive.
                if (line.StartsWith(HeaderEntity))
                    entity = line[HeaderEntity.Length..].Trim();
                else if (line.StartsWith(HeaderSpawnlist))
                    spawnlist = line[HeaderSpawnlist.Length..].Trim();
                else if (line.StartsWith(HeaderCategory))
                    category = line[HeaderCategory.Length..].Trim();
                else if (line.StartsWith(HeaderDeconstruct))
                    deconstructRaw = line[HeaderDeconstruct.Length..].Trim();
                else if (line.StartsWith(HeaderSteps))
                    stepsRaw = line[HeaderSteps.Length..].Trim();
                else if (!line.StartsWith('#'))
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to read custom construction entry {path}: {e}");
            return null;
        }

        return new EntryInfo(entity, spawnlist, category, DeserializeSteps(stepsRaw), DeserializeSteps(deconstructRaw), health);
    }

    // -------------------------------------------------------------------------
    // Validation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies every step references a prototype that actually exists. An invalid material/tool would
    /// produce a recipe that silently fails to build, so we reject it up front.
    /// </summary>
    private bool ValidateSteps(List<CustomConstructionStepData> steps, bool isItem, out string reason)
    {
        reason = string.Empty;
        if (steps.Count == 0)
        {
            reason = Loc.GetString("construction-menu-invalid-no-steps");
            return false;
        }

        foreach (var step in steps)
        {
            switch (step.Kind)
            {
                // Tool-QUALITY steps (welder/wrench/etc.) drive real multi-step recipes (e.g. wall: steel →
                // wrench → steel → weld). They work for STRUCTURES as long as a consuming step is first
                // (BuildGeneratedYaml guarantees this). The legacy in-hand ITEM build path throws on any
                // tool step, so they're only rejected for items. (Custom tools by entity id — EntityTool —
                // are entity-insert steps handled by Construct and are fine for both.)
                case CustomConstructionStepKind.Tool when isItem:
                    reason = Loc.GetString("construction-menu-invalid-tool-item", ("tool", step.Value));
                    return false;
                case CustomConstructionStepKind.Tool:
                    break;

                // Stack materials must be a real CM material the held sheets provide (CMSteel, CMPlasteel, …).
                case CustomConstructionStepKind.Material:
                    if (!_prototype.HasIndex<StackPrototype>(step.Value))
                    {
                        reason = Loc.GetString("construction-menu-invalid-material", ("material", step.Value));
                        return false;
                    }
                    break;

                // Custom material/tool by prototype id: any entity that actually exists.
                case CustomConstructionStepKind.EntityMaterial:
                case CustomConstructionStepKind.EntityTool:
                    if (!_prototype.HasIndex<EntityPrototype>(step.Value))
                    {
                        reason = Loc.GetString("construction-menu-invalid-entity", ("entity", step.Value));
                        return false;
                    }
                    break;
            }
        }

        // At least one consuming step is required so there's something to build from.
        if (!steps.Any(s => s.Kind is CustomConstructionStepKind.Material or CustomConstructionStepKind.EntityMaterial))
        {
            reason = Loc.GetString("construction-menu-invalid-no-steps");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates the optional deconstruction steps. These drive the world deconstruct edge, so they must be
    /// TOOL-like (a tool quality such as Prying, or a custom tool entity held but not consumed) - you don't
    /// feed materials in to take something apart. An empty list is valid and means "default crowbar".
    /// </summary>
    private bool ValidateDeconstructSteps(List<CustomConstructionStepData> steps, out string reason)
    {
        reason = string.Empty;

        foreach (var step in steps)
        {
            switch (step.Kind)
            {
                case CustomConstructionStepKind.Tool:
                    break;

                case CustomConstructionStepKind.EntityTool:
                    if (!_prototype.HasIndex<EntityPrototype>(step.Value))
                    {
                        reason = Loc.GetString("construction-menu-invalid-entity", ("entity", step.Value));
                        return false;
                    }
                    break;

                // Materials / consumed entities make no sense for taking something apart.
                default:
                    reason = Loc.GetString("construction-menu-invalid-deconstruct-material");
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// On startup, deletes any generated entry whose target entity or step prototypes no longer exist,
    /// so broken recipes don't linger in the menu (the "auto-remove invalid recipe" safety net).
    /// </summary>
    private void ValidateExistingEntries()
    {
        if (_generatedDir == null || !Directory.Exists(_generatedDir))
            return;

        foreach (var file in Directory.EnumerateFiles(_generatedDir, "*.yml"))
        {
            var info = ReadHeaders(file);
            string? reason = null;

            if (info == null)
                reason = "unreadable header";
            else if (string.IsNullOrEmpty(info.Entity) || !_prototype.TryIndex<EntityPrototype>(info.Entity, out var entProto))
                reason = $"missing entity '{info?.Entity}'";
            else if (!ValidateSteps(info.Steps, entProto.TryGetComponent<ItemComponent>(out _, _componentFactory), out var stepReason))
                reason = stepReason;
            else if (!ValidateDeconstructSteps(info.DeconstructSteps, out var deconstructReason))
                reason = deconstructReason;

            if (reason == null)
                continue;

            try
            {
                File.Delete(file);
                DbDelete(DbKindEntries, Path.GetFileNameWithoutExtension(file));
                Log.Warning($"Removed invalid custom construction entry '{Path.GetFileName(file)}': {reason}");
            }
            catch (Exception e)
            {
                Log.Error($"Failed to remove invalid custom construction entry '{file}': {e}");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Step (de)serialization for the file header. Token kinds (joined by '|'):
    //   m:<stack>:<amount>:<doAfter>     material stack (consumed)
    //   t:<quality>:<doAfter>            tool quality (rejected; kept for back-compat reads)
    //   em:<protoId>:<amount>:<doAfter>  custom material by entity id (consumed)
    //   et:<protoId>:<doAfter>           custom tool by entity id (not consumed)
    // -------------------------------------------------------------------------

    private static string SerializeSteps(List<CustomConstructionStepData> steps)
    {
        return string.Join("|", steps.Select(s => s.Kind switch
        {
            CustomConstructionStepKind.Tool => $"t:{s.Value}:{s.DoAfter}",
            CustomConstructionStepKind.EntityMaterial => $"em:{s.Value}:{s.Amount}:{s.DoAfter}",
            CustomConstructionStepKind.EntityTool => $"et:{s.Value}:{s.DoAfter}",
            _ => $"m:{s.Value}:{s.Amount}:{s.DoAfter}",
        }));
    }

    private static List<CustomConstructionStepData> DeserializeSteps(string raw)
    {
        var steps = new List<CustomConstructionStepData>();
        if (string.IsNullOrWhiteSpace(raw))
            return steps;

        foreach (var token in raw.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split(':');
            if (parts.Length < 2)
                continue;

            static float Sec(string s) => float.TryParse(s, out var v) && v > 0 ? v : 1f;
            static int Amt(string s) => int.TryParse(s, out var v) && v > 0 ? v : 1;

            switch (parts[0])
            {
                case "t":
                    steps.Add(new() { Kind = CustomConstructionStepKind.Tool, Value = parts[1], Amount = 0, DoAfter = Sec(parts.Length >= 3 ? parts[2] : "1") });
                    break;
                case "em" when parts.Length >= 3:
                    steps.Add(new() { Kind = CustomConstructionStepKind.EntityMaterial, Value = parts[1], Amount = Amt(parts[2]), DoAfter = Sec(parts.Length >= 4 ? parts[3] : "1") });
                    break;
                case "et":
                    steps.Add(new() { Kind = CustomConstructionStepKind.EntityTool, Value = parts[1], Amount = 0, DoAfter = Sec(parts.Length >= 3 ? parts[2] : "1") });
                    break;
                case "m" when parts.Length >= 3:
                    steps.Add(new() { Kind = CustomConstructionStepKind.Material, Value = parts[1], Amount = Amt(parts[2]), DoAfter = Sec(parts.Length >= 4 ? parts[3] : "1") });
                    break;
            }
        }

        return steps;
    }

    private static string DescribeRecipe(List<CustomConstructionStepData> steps)
    {
        return string.Join(" > ", steps.Select(s => s.Kind switch
        {
            CustomConstructionStepKind.EntityTool => $"{s.Value} (tool)",
            CustomConstructionStepKind.Tool => s.Value,
            _ => $"{s.Amount} {s.Value}",
        }));
    }

    // -------------------------------------------------------------------------
    // YAML generation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds the generated prototypes for the target entity, mirroring EXACTLY how vanilla CM menu items
    /// are authored (e.g. <c>Barbedwire</c> for items, <c>CMBarricadeMetal</c> for structures): a plain
    /// <c>constructionGraph</c> whose build edge consumes one or more held material stacks and whose target
    /// node spawns the entity, plus a <c>construction</c> entry that references it. No <c>rmcPrototype</c> —
    /// the construction-menu build path uses the graph, not the RMC sheet-radial path (which would require
    /// the held sheet's hand-authored <c>Buildable</c> list to include our id, which it never does).
    ///
    /// <para>
    /// CRUCIAL: the construction system requires the SPAWNED entity to carry a <c>Construction</c> component
    /// referencing this graph/node — otherwise it logs "Initial construction does not have a valid target
    /// entity!" and deletes it (this is why building a raw FoodOrange failed: items/food don't declare one,
    /// unlike walls/barricades/barbed-wire which do). So we emit a CHILD entity prototype
    /// (<c>AU14CustomEntity_&lt;id&gt;</c>, <c>parent: &lt;original&gt;</c>) that adds the <c>Construction</c>
    /// component, and point the graph's target node at that child. The child inherits everything from the
    /// original (sprite, food, etc.), so it behaves identically but is now buildable/deconstructable.
    /// </para>
    ///
    /// <para>
    /// <c>objectType</c> is auto-detected: an entity with an <see cref="ItemComponent"/> builds in-hand
    /// (<c>Item</c>); anything else is placed as a ghost (<c>Structure</c>, with snap + tile-clear). Materials
    /// must be CM material stacks (CMSteel, CMPlasteel, …) — that is what the menu's held sheets provide.
    /// Tool steps are rejected by validation, so every step here is a material step.
    /// </para>
    /// </summary>
    private string BuildGeneratedYaml(EntityPrototype proto, string entryKey, string spawnlist, string category, List<CustomConstructionStepData> steps, List<CustomConstructionStepData> deconstructSteps, int health)
    {
        // All generated prototype ids are scoped by the entry key so the same entity can have several
        // independent entries (different recipe/spawnlist/category) without colliding ids.
        var graphId = $"AU14CustomGraph_{entryKey}";
        var constructionId = $"{FilePrefix}{entryKey}";
        var entityId = $"{ChildEntityPrefix}{entryKey}";
        var name = proto.Name.Replace("\"", "'");
        var description = (proto.Description ?? string.Empty).Replace("\"", "'");

        // All step kinds are emitted (stack materials, custom-material entities consumed, custom-tool
        // entities inserted-not-consumed, and tool-quality steps for multi-step recipes like real walls).
        // Tool-quality steps only reach here for structures (ValidateSteps rejects them for in-hand items).
        var buildSteps = steps.ToList();
        if (!buildSteps.Any(s => s.Kind is CustomConstructionStepKind.Material or CustomConstructionStepKind.EntityMaterial))
            buildSteps.Insert(0, new CustomConstructionStepData { Kind = CustomConstructionStepKind.Material, Value = "CMSteel", Amount = 1, DoAfter = 1 });

        // The structure build path requires the FIRST step to be a consuming step (a leading tool step
        // throws). Preserve the admin's ordering otherwise — just hoist the first consuming step to front.
        var firstConsuming = buildSteps.FindIndex(s => s.Kind is CustomConstructionStepKind.Material or CustomConstructionStepKind.EntityMaterial);
        if (firstConsuming > 0)
        {
            var hoisted = buildSteps[firstConsuming];
            buildSteps.RemoveAt(firstConsuming);
            buildSteps.Insert(0, hoisted);
        }

        // An item builds in-hand; everything else is placed as a structure ghost.
        var isItem = proto.TryGetComponent<ItemComponent>(out _, _componentFactory);
        var midEntityId = $"AU14CustomEntityMid_{entryKey}";

        var sb = new StringBuilder();
        sb.AppendLine("# Auto-generated by the AU14 in-game construction menu editor.");
        sb.AppendLine($"{HeaderEntity} {proto.ID}");
        sb.AppendLine($"{HeaderSpawnlist} {spawnlist}");
        sb.AppendLine($"{HeaderCategory} {category}");
        sb.AppendLine($"{HeaderSteps} {SerializeSteps(steps)}");
        sb.AppendLine($"{HeaderDeconstruct} {SerializeSteps(deconstructSteps)}");
        sb.AppendLine($"{HeaderHealth} {health}");
        sb.AppendLine("# Safe to edit, commit, or delete. Deleting removes it from the construction menu.");
        sb.AppendLine();

        void EmitStep(CustomConstructionStepData step)
        {
            // Clamped: a hostile Amount (int.MaxValue) would otherwise emit one YAML block per unit (OOM),
            // and a NaN DoAfter passes a "<= 0" check but casts to int.MinValue.
            var doAfter = float.IsFinite(step.DoAfter) && step.DoAfter > 0
                ? Math.Min((int)Math.Ceiling(step.DoAfter), MaxStepSeconds)
                : 1;
            var amount = Math.Clamp(step.Amount, 1, MaxStepAmount);
            switch (step.Kind)
            {
                case CustomConstructionStepKind.EntityMaterial:
                    // A STACKABLE entity means N units of that stack, not N separate one-unit entities.
                    // Picking BarbedWire1 with an amount of 2 used to emit two entityId steps, so the recipe
                    // demanded two separate single-wire items and a stack of 2 would not satisfy it. Emit a
                    // material step against the stack type instead, which counts units the way players expect.
                    if (TryGetStackType(step.Value, out var stackType))
                    {
                        sb.AppendLine($"      - material: {stackType}");
                        sb.AppendLine($"        amount: {amount}");
                        sb.AppendLine($"        doAfter: {doAfter}");
                        break;
                    }

                    // Non-stackable: require `amount` separate copies of the entity (each insert consumes one).
                    for (var i = 0; i < amount; i++)
                    {
                        sb.AppendLine($"      - entityId: {step.Value}");
                        sb.AppendLine("        consume: true");
                        sb.AppendLine($"        doAfter: {(i == 0 ? doAfter : 0)}");
                    }
                    break;
                case CustomConstructionStepKind.EntityTool:
                    sb.AppendLine($"      - entityId: {step.Value}");
                    sb.AppendLine("        consume: false");
                    sb.AppendLine($"        doAfter: {doAfter}");
                    break;
                case CustomConstructionStepKind.Tool: // tool quality (welder/wrench/etc.)
                    sb.AppendLine($"      - tool: {step.Value}");
                    sb.AppendLine($"        doAfter: {doAfter}");
                    break;
                default: // Material stack
                    sb.AppendLine($"      - material: {step.Value}");
                    sb.AppendLine($"        amount: {amount}");
                    sb.AppendLine($"        doAfter: {doAfter}");
                    break;
            }
        }

        // Emits the SpawnPrototype refund for one consumed build step (material stack -> sheet, custom-material
        // entity -> the entity itself). Tool/entity-tool steps consume nothing, so they refund nothing.
        void EmitRefund(CustomConstructionStepData step)
        {
            var refundAmount = Math.Clamp(step.Amount, 1, MaxStepAmount);
            switch (step.Kind)
            {
                case CustomConstructionStepKind.Material when MaterialSheets.TryGetValue(step.Value, out var sheet):
                    sb.AppendLine("      - !type:SpawnPrototype");
                    sb.AppendLine($"        prototype: {sheet}");
                    sb.AppendLine($"        amount: {refundAmount}");
                    break;
                case CustomConstructionStepKind.EntityMaterial:
                    sb.AppendLine("      - !type:SpawnPrototype");
                    sb.AppendLine($"        prototype: {step.Value}");
                    sb.AppendLine($"        amount: {refundAmount}");
                    break;
            }
        }

        // Emits the `steps:` block of a deconstruct edge from the admin-chosen deconstruct tool steps,
        // defaulting to a single crowbar (Prying) when none were specified. Deconstruct steps are tool /
        // entity-tool only (validated), both of which EmitStep already renders correctly.
        void EmitDeconstructSteps()
        {
            sb.AppendLine("      steps:");
            if (deconstructSteps.Count == 0)
            {
                sb.AppendLine("      - tool: Prying");
                sb.AppendLine("        doAfter: 2");
                return;
            }

            foreach (var step in deconstructSteps)
                EmitStep(step);
        }

        // Multi-step STRUCTURES use the vanilla intermediate-node pattern (girder -> wall): the ghost's
        // FIRST edge carries ONLY the first consuming step — a tool step on the ghost-start edge throws
        // "Invalid first step" and crashes the server — and every remaining step (tools/extra materials)
        // goes on a second edge from an intermediate "in progress" node. Single-step recipes and items use
        // one edge. (Items can't take tool steps at all; ValidateSteps already rejects those.)
        var useChain = !isItem && buildSteps.Count > 1;

        sb.AppendLine("- type: constructionGraph");
        sb.AppendLine($"  id: {graphId}");
        sb.AppendLine("  start: start");
        sb.AppendLine("  graph:");
        sb.AppendLine("  - node: start");
        sb.AppendLine("    edges:");
        if (useChain)
        {
            sb.AppendLine("    - to: mid");
            sb.AppendLine("      completed:");
            sb.AppendLine("      - !type:SnapToGrid");
            sb.AppendLine("      steps:");
            EmitStep(buildSteps[0]);
            sb.AppendLine("  - node: mid");
            sb.AppendLine($"    entity: {midEntityId}");
            sb.AppendLine("    edges:");
            sb.AppendLine("    - to: target");
            sb.AppendLine("      steps:");
            for (var i = 1; i < buildSteps.Count; i++)
                EmitStep(buildSteps[i]);
            // Cancel a half-built frame with a crowbar: refund the first consumed step and remove it, so an
            // in-progress build is never a soft-lock occupying the tile.
            sb.AppendLine("    - to: start");
            sb.AppendLine("      completed:");
            EmitRefund(buildSteps[0]);
            sb.AppendLine("      - !type:DeleteEntity");
            sb.AppendLine("      steps:");
            sb.AppendLine("      - tool: Prying");
            sb.AppendLine("        doAfter: 2");
        }
        else
        {
            sb.AppendLine("    - to: target");
            if (!isItem)
            {
                sb.AppendLine("      completed:");
                sb.AppendLine("      - !type:SnapToGrid");
            }
            sb.AppendLine("      steps:");
            foreach (var step in buildSteps)
                EmitStep(step);
        }
        sb.AppendLine("  - node: target");
        sb.AppendLine($"    entity: {entityId}");

        // Structures get a deconstruct edge that returns the materials used as sheets, then deletes the
        // structure - so anything added to the menu can be taken back down for a refund. The tool steps are
        // the admin-chosen deconstruct steps (default: a single crowbar). Items build/deconstruct in-hand and
        // don't take a world deconstruct edge.
        if (!isItem)
        {
            sb.AppendLine("    edges:");
            sb.AppendLine("    - to: start");
            sb.AppendLine("      completed:");
            foreach (var step in buildSteps)
                EmitRefund(step);
            sb.AppendLine("      - !type:DeleteEntity");
            EmitDeconstructSteps();
        }

        sb.AppendLine();

        // The buildable child entity (final node). Inherits everything from the original and just adds the
        // Construction component so the construction system accepts it as the build target (and deconstructs).
        sb.AppendLine("- type: entity");
        sb.AppendLine($"  id: {entityId}");
        sb.AppendLine($"  parent: {proto.ID}");
        sb.AppendLine("  suffix: AU14 Custom Construction");
        sb.AppendLine("  components:");
        sb.AppendLine("  - type: Construction");
        sb.AppendLine($"    graph: {graphId}");
        sb.AppendLine("    node: target");
        // Every built STRUCTURE participates in the z-level structural-support graph: on the ground it is rooted
        // by the floor it rests on; on an upper z-level it needs a support beam below or it caves in. (Items are
        // not anchored to a grid, so the support graph never tracks them - no point adding it there.)
        if (!isItem)
        {
            sb.AppendLine("  - type: StructuralSupport");
            sb.AppendLine("    cantileverSpan: 2");
        }
        // Optional health override from the editor: replace the parent's destruction threshold so the structure
        // breaks at exactly this much damage. Relies on the parent's Damageable (every CM structure has one).
        if (health > 0)
        {
            sb.AppendLine("  - type: Destructible");
            sb.AppendLine("    thresholds:");
            sb.AppendLine("    - trigger:");
            sb.AppendLine("        !type:DamageTrigger");
            sb.AppendLine($"        damage: {health}");
            sb.AppendLine("      behaviors:");
            sb.AppendLine("      - !type:DoActsBehavior");
            sb.AppendLine("        acts: [ \"Destruction\" ]");
        }
        sb.AppendLine();

        // The intermediate "in progress" entity (mid node) for multi-step structures. It parents the generic
        // construction FRAME, not the original - so the build reads as clearly unfinished and the real entity
        // only appears once the LAST step completes (start -> mid frame -> target). It just adds the per-recipe
        // Construction component pointing at the mid node.
        if (useChain)
        {
            sb.AppendLine("- type: entity");
            sb.AppendLine($"  id: {midEntityId}");
            sb.AppendLine($"  parent: {ConstructionFrameProto}");
            sb.AppendLine("  suffix: AU14 Custom Construction (in progress)");
            sb.AppendLine("  components:");
            sb.AppendLine("  - type: Construction");
            sb.AppendLine($"    graph: {graphId}");
            sb.AppendLine("    node: mid");
            sb.AppendLine();
        }

        sb.AppendLine("- type: construction");
        sb.AppendLine($"  id: {constructionId}");
        sb.AppendLine($"  name: \"{name}\"");
        sb.AppendLine($"  description: \"{description}\"");
        sb.AppendLine($"  graph: {graphId}");
        sb.AppendLine("  startNode: start");
        sb.AppendLine("  targetNode: target");
        sb.AppendLine($"  category: {category}");
        sb.AppendLine($"  spawnlist: {spawnlist}");
        sb.AppendLine("  isCM: true");
        if (isItem)
        {
            sb.AppendLine("  objectType: Item");
        }
        else
        {
            sb.AppendLine("  objectType: Structure");
            sb.AppendLine("  placementMode: SnapgridCenter");
            sb.AppendLine("  canBuildInImpassable: false");
            sb.AppendLine("  conditions:");
            sb.AppendLine("  - !type:TileNotBlocked");
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Misc
    // -------------------------------------------------------------------------

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    // ============================================
    // 🔧 TUNABLE: limits on client-sent editor values
    // ============================================
    /// <summary>Max length of a client-sent spawnlist/category name.</summary>
    private const int MaxNameLength = 64;
    /// <summary>Max per-step material amount (also caps generated YAML size).</summary>
    private const int MaxStepAmount = 30;
    /// <summary>Max per-step build time in seconds.</summary>
    private const int MaxStepSeconds = 300;

    /// <summary>
    /// Max size of ONE generated YAML document (file + DB row), in bytes. Generated entries are normally
    /// a few KB; anything approaching this is corrupt or hostile. Checked on every write AND on every DB
    /// restore, so an oversized row can never be written, restored, or broadcast to clients.
    /// </summary>
    private const int MaxGeneratedYamlBytes = 128 * 1024;

    /// <summary>Single guard every tool's save path runs before persisting generated YAML.</summary>
    private static bool IsOversizedYaml(string yaml, out string reason)
    {
        // UTF-8 length ≈ char count for our generated ASCII yaml; Encoding count is exact and cheap.
        var bytes = Encoding.UTF8.GetByteCount(yaml);
        if (bytes <= MaxGeneratedYamlBytes)
        {
            reason = string.Empty;
            return false;
        }

        reason = $"generated YAML is {bytes / 1024} KiB (max {MaxGeneratedYamlBytes / 1024} KiB)";
        return true;
    }

    /// <summary>
    /// Whitelists a client-sent spawnlist/category name before it is embedded in generated YAML: letters,
    /// digits, space, '_' and '-' only, max <see cref="MaxNameLength"/> chars. Anything else (newlines,
    /// YAML syntax, etc.) is stripped so a hostile string can never inject prototype documents.
    /// </summary>
    private static string SanitizeName(string? value, string fallback)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return fallback;

        var sb = new StringBuilder(Math.Min(trimmed.Length, MaxNameLength));
        foreach (var c in trimmed)
        {
            if (char.IsLetterOrDigit(c) || c is ' ' or '_' or '-')
                sb.Append(c);
            if (sb.Length >= MaxNameLength)
                break;
        }

        var result = sb.ToString().Trim();
        return result.Length == 0 ? fallback : result;
    }

    private static string Sanitize(string id)
    {
        var sb = new StringBuilder(id.Length);
        foreach (var c in id)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    /// <summary>
    /// Locates the repo's <c>Resources</c> directory by walking up from the working/base directory.
    /// Returns null on a packaged server with no writable content tree (the feature then no-ops with a popup).
    /// </summary>
    private string? ResolveGeneratedDir()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                var resources = Path.Combine(dir.FullName, "Resources");
                if (Directory.Exists(Path.Combine(resources, "Prototypes")))
                    return Path.Combine(resources, GeneratedSubDir.Replace('/', Path.DirectorySeparatorChar));

                dir = dir.Parent;
            }
        }

        return null;
    }
}
