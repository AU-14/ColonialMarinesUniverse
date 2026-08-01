// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Client._AU14.Administration;
using Content.Client.Construction;
using Content.Shared._AU14.Administration;
using Content.Shared._AU14.Construction.CustomConstruction;
using Content.Client._AU14.ZLevelBuilding;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared.Popups;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.Construction.CustomConstruction;

/// <summary>
/// Client side of the construction-menu editor. Opens <see cref="ConstructionEditorWindow"/> when the
/// server requests it (after a permitted admin uses a world verb) and relays the confirmed result back
/// to the server. Also drives the in-menu "Construction Items Editor" utility: opens the entity selector,
/// then asks the server to open the editor for the chosen entity (with a client-side admin pre-check).
/// </summary>
public sealed class CustomConstructionEditorClientSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ToolPermissionClientSystem _toolPerms = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    /// <summary>
    /// Client-side pre-check mirroring the server gate: a Host-flagged admin OR a ckey granted this
    /// specific tool through the Tool Permissions system (which replaced the old JModEditor job
    /// whitelist). General admin access is intentionally NOT enough. The server always re-validates.
    /// </summary>
    private bool CanUseEditor(string tool) => _toolPerms.CanUse(tool);

    private ConstructionEditorWindow? _window;
    private EntitySelectorWindow? _selector;
    private TileEditorWindow? _tileWindow;
    private LatheEditorWindow? _latheWindow;
    private RecipeChooserWindow? _chooser;
    private ZLevelTogglesWindow? _zTogglesWindow;
    private MassEntitySelectorWindow? _massSelector;
    private ConstructionEditorWindow? _massEditor;
    private ZBorderSyncWindow? _zSyncWindow;
    private SpawnlistDeleteWindow? _spawnlistDeleteWindow;
    private DbSavePreviewWindow? _dbPreviewWindow;

    /// <summary>Re-sends the stashed submit (with Preview = false) when the admin confirms the DB save
    /// preview window. Set when a preview-gated submit is sent; cleared on confirm, cancel, or replacement.</summary>
    private Action? _pendingDbConfirm;
    private bool _zSyncPickActive;
    private bool _zSyncPickBlacklist;

    /// <summary>
    /// Construction recipe ids the local admin hid via the menu's "Remove Item" button THIS session. The
    /// persisted hide (a generated overrides prototype) only takes effect next restart, so the menu presenter
    /// also consults this set to hide them immediately for the admin who removed them.
    /// </summary>
    public readonly HashSet<string> HiddenRecipeIds = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<OpenCustomConstructionEditorEvent>(OnOpen);
        SubscribeNetworkEvent<OpenCustomConstructionChooserEvent>(OnOpenChooser);
        SubscribeNetworkEvent<OpenCustomTileEditorEvent>(OnOpenTile);
        SubscribeNetworkEvent<OpenCustomLatheEditorEvent>(OnOpenLathe);
        SubscribeNetworkEvent<OpenZLevelTogglesEvent>(OnOpenZLevelToggles);
        SubscribeNetworkEvent<OpenMassConstructionEditorEvent>(OnOpenMassEditor);
        SubscribeNetworkEvent<OpenZBorderSyncEvent>(OnOpenZSync);
        SubscribeNetworkEvent<OpenSpawnlistDeleteEvent>(OnOpenSpawnlistDelete);
        SubscribeNetworkEvent<OpenDbSavePreviewEvent>(OnOpenDbPreview);
        SubscribeLocalEvent<ConstructionMenuFilterEvent>(OnMenuFilter);

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnZSyncPickUse, outsidePrediction: true))
            .Bind(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnZSyncPickCancel, outsidePrediction: true))
            .Register<CustomConstructionEditorClientSystem>();
    }

    private void OnMenuFilter(ref ConstructionMenuFilterEvent args)
    {
        args.HiddenRecipes.UnionWith(HiddenRecipeIds);
        foreach (var overrides in _prototypes.EnumeratePrototypes<AU14MenuOverridesPrototype>())
            args.HiddenRecipes.UnionWith(overrides.HiddenRecipes);
        args.ExcludedSpawnlists.UnionWith(ZLevelPageSpawnlists.Names);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<CustomConstructionEditorClientSystem>();
    }

    /// <summary>Admin Tools > Z-Sync Lists: which walls mirror across z-levels as map borders.</summary>
    public void OpenZSyncLists()
    {
        if (!CanUseEditor(AU14ToolPermissions.ZSync))
        {
            _popup.PopupCursor(Loc.GetString("construction-menu-editor-not-admin"), PopupType.MediumCaution);
            return;
        }

        RaiseNetworkEvent(new RequestOpenZBorderSyncEvent());
    }

    private void OnOpenZSync(OpenZBorderSyncEvent ev)
    {
        // The server re-sends the lists after every change; refresh the open window in place.
        if (_zSyncWindow is { IsOpen: true })
        {
            _zSyncWindow.Populate(ev);
            return;
        }

        _zSyncWindow = new ZBorderSyncWindow();
        _zSyncWindow.OnModify += modify => RaiseNetworkEvent(modify);
        _zSyncWindow.OnPickFromWorld += BeginZSyncPick;
        _zSyncWindow.OnClose += () => _zSyncWindow = null;
        _zSyncWindow.Populate(ev);
        _zSyncWindow.OpenCentered();
    }

    private void BeginZSyncPick(bool blacklist)
    {
        _zSyncPickActive = true;
        _zSyncPickBlacklist = blacklist;
        _popup.PopupCursor(Loc.GetString("au-zsync-pick-instruction"), PopupType.Medium);
    }

    private bool OnZSyncPickUse(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (!_zSyncPickActive || args.State != BoundKeyState.Down)
            return false;

        if (!args.EntityUid.IsValid() || !EntityManager.EntityExists(args.EntityUid))
        {
            _popup.PopupCursor(Loc.GetString("au-zsync-pick-no-entity"), PopupType.MediumCaution);
            return true;
        }

        RaiseNetworkEvent(new PickZBorderSyncEntityEvent
        {
            Entity = GetNetEntity(args.EntityUid),
            Blacklist = _zSyncPickBlacklist,
        });
        _zSyncPickActive = false;
        return true;
    }

    private bool OnZSyncPickCancel(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (!_zSyncPickActive || args.State != BoundKeyState.Down)
            return false;

        _zSyncPickActive = false;
        _popup.PopupCursor(Loc.GetString("au-zsync-pick-cancelled"), PopupType.Medium);
        return true;
    }

    /// <summary>
    /// Admin Tools > Mass Entity Editor: pick MANY entities (with ancestor filtering, e.g. everything under
    /// BaseWall), then fill in ONE recipe that the server applies to each of them as separate entries.
    /// </summary>
    public void OpenMassEditor()
    {
        if (!CanUseEditor(AU14ToolPermissions.Mass))
        {
            _popup.PopupCursor(Loc.GetString("construction-menu-editor-not-admin"), PopupType.MediumCaution);
            return;
        }

        _massSelector?.Close();
        _massSelector = new MassEntitySelectorWindow();
        _massSelector.OnEntitiesSelected += ids =>
        {
            if (ids.Count > 0)
                RaiseNetworkEvent(new RequestOpenMassConstructionEditorEvent { ProtoIds = ids });
        };
        _massSelector.OnTilesSelected += tileIds =>
        {
            if (tileIds.Count == 0)
                return;

            // Tiles mode: one small cost/placement form, then the server fans it out per tile.
            var config = new MassTileConfigWindow(tileIds.Count);
            config.OnSubmit += submit =>
            {
                submit.TileIds = tileIds;
                SendWithDbPreview(submit, preview => submit.Preview = preview);
            };
            config.OpenCentered();
        };
        _massSelector.OnClose += () => _massSelector = null;
        _massSelector.OpenCentered();
    }

    private void OnOpenMassEditor(OpenMassConstructionEditorEvent ev)
    {
        _massEditor?.Close();
        _massEditor = new ConstructionEditorWindow();
        var protoIds = ev.ProtoIds;
        // One editor form; on confirm the single recipe is fanned out server-side to every entity in the batch.
        _massEditor.OnSubmit += submit =>
        {
            var mass = new SubmitMassConstructionEditorEvent
            {
                ProtoIds = protoIds,
                Spawnlist = submit.Spawnlist,
                Category = submit.Category,
                Steps = submit.Steps,
                DeconstructSteps = submit.DeconstructSteps,
                Health = submit.Health,
            };
            SendWithDbPreview(mass, preview => mass.Preview = preview);
        };
        _massEditor.OnClose += () => _massEditor = null;
        _massEditor.Populate(ev.Editor);
        _massEditor.OpenCentered();
    }

    /// <summary>
    /// Preview-first save flow: sends the submit as a dry run (Preview = true) and stashes a re-send
    /// closure. The server answers with <see cref="OpenDbSavePreviewEvent"/> listing every file/DB write
    /// the save would do; confirming in the window re-sends the SAME submit with Preview = false.
    /// </summary>
    private void SendWithDbPreview<T>(T submit, Action<bool> setPreview) where T : EntityEventArgs
    {
        setPreview(true);
        _pendingDbConfirm = () =>
        {
            setPreview(false);
            RaiseNetworkEvent(submit);
        };
        RaiseNetworkEvent(submit);
    }

    private void OnOpenDbPreview(OpenDbSavePreviewEvent ev)
    {
        _dbPreviewWindow?.Close();
        var window = new DbSavePreviewWindow();
        _dbPreviewWindow = window;
        window.OnConfirm += () =>
        {
            _pendingDbConfirm?.Invoke();
            _pendingDbConfirm = null;
        };
        // Closing without confirming (cancel or X) drops the pending save entirely. Guarded so closing a
        // STALE window (replaced by a newer preview above) can't wipe the newer pending confirm.
        window.OnClose += () =>
        {
            if (_dbPreviewWindow != window)
                return;

            _dbPreviewWindow = null;
            _pendingDbConfirm = null;
        };
        window.Populate(ev);
        window.OpenCentered();
    }

    /// <summary>Admin Tools > Delete Spawnlist: ask the server (which re-checks permission) for the
    /// spawnlists that currently hold generated recipes, then confirm the destructive delete.</summary>
    public void OpenSpawnlistDelete()
    {
        if (!CanUseEditor(AU14ToolPermissions.SpawnlistDelete))
        {
            _popup.PopupCursor(Loc.GetString("construction-menu-editor-not-admin"), PopupType.MediumCaution);
            return;
        }

        RaiseNetworkEvent(new RequestOpenSpawnlistDeleteEvent());
    }

    private void OnOpenSpawnlistDelete(OpenSpawnlistDeleteEvent ev)
    {
        _spawnlistDeleteWindow?.Close();
        _spawnlistDeleteWindow = new SpawnlistDeleteWindow();
        _spawnlistDeleteWindow.OnDeleteSpawnlist += (spawnlist, category) =>
            RaiseNetworkEvent(new DeleteSpawnlistEvent { Spawnlist = spawnlist, Category = category });
        _spawnlistDeleteWindow.OnClose += () => _spawnlistDeleteWindow = null;
        _spawnlistDeleteWindow.Populate(ev);
        _spawnlistDeleteWindow.OpenCentered();
    }

    /// <summary>Admin Tools > Z-Level Toggles: ask the server (which re-checks permission) for the map list.</summary>
    public void OpenZLevelToggles()
    {
        if (!CanUseEditor(AU14ToolPermissions.ZLevelToggles))
        {
            _popup.PopupCursor(Loc.GetString("construction-menu-editor-not-admin"), PopupType.MediumCaution);
            return;
        }

        RaiseNetworkEvent(new RequestOpenZLevelTogglesEvent());
    }

    private void OnOpenZLevelToggles(OpenZLevelTogglesEvent ev)
    {
        _zTogglesWindow?.Close();
        _zTogglesWindow = new ZLevelTogglesWindow();
        _zTogglesWindow.OnToggle += (mapProtoId, enabled) =>
            RaiseNetworkEvent(new SetZLevelToggleEvent { MapProtoId = mapProtoId, Enabled = enabled });
        _zTogglesWindow.OnClose += () => _zTogglesWindow = null;
        _zTogglesWindow.Populate(ev);
        _zTogglesWindow.OpenCentered();
    }

    private void OnOpenChooser(OpenCustomConstructionChooserEvent ev)
    {
        _chooser?.Close();
        _chooser = new RecipeChooserWindow();
        var protoId = ev.ProtoId;
        _chooser.OnChange += key => RaiseNetworkEvent(new RequestOpenCustomConstructionEditorEvent(protoId) { EntryKey = key });
        _chooser.OnAddNew += () => RaiseNetworkEvent(new RequestOpenCustomConstructionEditorEvent(protoId) { ForceAddNew = true });
        _chooser.OnRemove += key => RaiseNetworkEvent(new RemoveCustomConstructionEntryEvent { ProtoId = protoId, EntryKey = key });
        _chooser.OnClose += () => _chooser = null;
        _chooser.Populate(ev);
        _chooser.OpenCentered();
    }

    /// <summary>Admin Tools > Tiles Editor: ask the server (which re-checks permission) to open the tile editor.</summary>
    public void OpenTilesEditor()
    {
        if (!CanUseEditor(AU14ToolPermissions.Tiles))
        {
            _popup.PopupCursor(Loc.GetString("construction-menu-editor-not-admin"), PopupType.MediumCaution);
            return;
        }

        RaiseNetworkEvent(new RequestOpenCustomTileEditorEvent());
    }

    /// <summary>Admin Tools > Lathe Editor: ask the server (which re-checks permission) to open the lathe editor.</summary>
    public void OpenLatheEditor()
    {
        if (!CanUseEditor(AU14ToolPermissions.Lathe))
        {
            _popup.PopupCursor(Loc.GetString("construction-menu-editor-not-admin"), PopupType.MediumCaution);
            return;
        }

        RaiseNetworkEvent(new RequestOpenCustomLatheEditorEvent());
    }

    private void OnOpenTile(OpenCustomTileEditorEvent ev)
    {
        _tileWindow?.Close();
        _tileWindow = new TileEditorWindow();
        _tileWindow.OnSubmit += submit => RaiseNetworkEvent(submit);
        _tileWindow.OnClose += () => _tileWindow = null;
        _tileWindow.Populate(ev);
        _tileWindow.OpenCentered();
    }

    private void OnOpenLathe(OpenCustomLatheEditorEvent ev)
    {
        _latheWindow?.Close();
        _latheWindow = new LatheEditorWindow();
        _latheWindow.OnSubmit += submit => RaiseNetworkEvent(submit);
        _latheWindow.OnRemove += recipeId => RaiseNetworkEvent(new RemoveCustomLatheRecipeEvent { RecipeId = recipeId });
        _latheWindow.OnClose += () => _latheWindow = null;
        _latheWindow.Populate(ev);
        _latheWindow.OpenCentered();
    }

    /// <summary>
    /// Entry point for the in-menu "Construction Items Editor" utility. Non-admins get an immediate popup;
    /// admins get the entity selector, and picking an entity asks the server to open the editor for it.
    /// </summary>
    public void OpenItemsEditor()
    {
        if (!CanUseEditor(AU14ToolPermissions.Construction))
        {
            _popup.PopupCursor(Loc.GetString("construction-menu-editor-not-admin"), PopupType.MediumCaution);
            return;
        }

        _selector?.Close();
        _selector = new EntitySelectorWindow();
        _selector.OnEntitySelected += id =>
        {
            if (!string.IsNullOrEmpty(id))
                RaiseNetworkEvent(new RequestOpenCustomConstructionEditorEvent(id));
        };
        _selector.OnClose += () => _selector = null;
        _selector.OpenCentered();
    }

    /// <summary>
    /// Menu detail panel "Change Recipe": open the editor for the recipe's target entity. The server decides
    /// whether to show the chooser (if it already has generated entries) or jump straight into add-new.
    /// </summary>
    public void RequestChangeRecipe(string targetEntityId)
    {
        if (!CanUseEditor(AU14ToolPermissions.Construction))
        {
            _popup.PopupCursor(Loc.GetString("construction-menu-editor-not-admin"), PopupType.MediumCaution);
            return;
        }

        if (!string.IsNullOrEmpty(targetEntityId))
            RaiseNetworkEvent(new RequestOpenCustomConstructionEditorEvent(targetEntityId));
    }

    /// <summary>
    /// Menu detail panel "Remove Item": hide this recipe from the menu by its construction id. Persists on the
    /// server (next restart, all clients) and hides it immediately for this admin this session.
    /// </summary>
    public void HideRecipe(string constructionId)
    {
        if (!CanUseEditor(AU14ToolPermissions.Construction))
        {
            _popup.PopupCursor(Loc.GetString("construction-menu-editor-not-admin"), PopupType.MediumCaution);
            return;
        }

        if (string.IsNullOrEmpty(constructionId))
            return;

        HiddenRecipeIds.Add(constructionId);
        RaiseNetworkEvent(new HideConstructionRecipeEvent { RecipeId = constructionId });
    }

    private void OnOpen(OpenCustomConstructionEditorEvent ev)
    {
        _window?.Close();

        _window = new ConstructionEditorWindow();
        _window.OnSubmit += submit => SendWithDbPreview(submit, preview => submit.Preview = preview);
        _window.OnRemoveGroup += group => RaiseNetworkEvent(new RemoveCustomConstructionGroupEvent { Spawnlist = group.spawnlist, Category = group.category });
        _window.OnClose += () => _window = null;
        _window.Populate(ev);
        _window.OpenCentered();
    }
}
