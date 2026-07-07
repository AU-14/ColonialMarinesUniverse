// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Client.Administration.Managers;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared._AU14.Construction.CustomConstruction;
using Content.Shared.Popups;

namespace Content.Client._AU14.Construction.CustomConstruction;

/// <summary>
/// Client side of the construction-menu editor. Opens <see cref="ConstructionEditorWindow"/> when the
/// server requests it (after a permitted admin uses a world verb) and relays the confirmed result back
/// to the server. Also drives the in-menu "Construction Items Editor" utility: opens the entity selector,
/// then asks the server to open the editor for the chosen entity (with a client-side admin pre-check).
/// </summary>
public sealed class CustomConstructionEditorClientSystem : EntitySystem
{
    [Dependency] private readonly IClientAdminManager _admin = default!;
    [Dependency] private readonly JobRequirementsManager _jobRequirements = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>
    /// Whitelist-only "role" that unlocks the editor tools for a trusted non-admin (granted with
    /// <c>jobwhitelistadd &lt;player&gt; JModEditor</c>). Must match the server's EditorWhitelistJob.
    /// </summary>
    private const string EditorWhitelistJob = "JModEditor";

    /// <summary>
    /// Client-side pre-check mirroring the server gate: a Host-flagged admin OR a player holding the
    /// <see cref="EditorWhitelistJob"/> whitelist. General admin access is intentionally NOT enough. The
    /// server always re-validates before acting.
    /// </summary>
    private bool CanUseEditor() =>
        _admin.HasFlag(Shared.Administration.AdminFlags.Host) || _jobRequirements.IsWhitelisted(EditorWhitelistJob);

    private ConstructionEditorWindow? _window;
    private EntitySelectorWindow? _selector;
    private TileEditorWindow? _tileWindow;
    private LatheEditorWindow? _latheWindow;
    private RecipeChooserWindow? _chooser;

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
        if (!CanUseEditor())
        {
            _popup.PopupCursor(Loc.GetString("construction-menu-editor-not-admin"), PopupType.MediumCaution);
            return;
        }

        RaiseNetworkEvent(new RequestOpenCustomTileEditorEvent());
    }

    /// <summary>Admin Tools > Lathe Editor: ask the server (which re-checks permission) to open the lathe editor.</summary>
    public void OpenLatheEditor()
    {
        if (!CanUseEditor())
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
        if (!CanUseEditor())
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
        if (!CanUseEditor())
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
        if (!CanUseEditor())
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
        _window.OnSubmit += submit => RaiseNetworkEvent(submit);
        _window.OnRemoveGroup += group => RaiseNetworkEvent(new RemoveCustomConstructionGroupEvent { Spawnlist = group.spawnlist, Category = group.category });
        _window.OnClose += () => _window = null;
        _window.Populate(ev);
        _window.OpenCentered();
    }
}
