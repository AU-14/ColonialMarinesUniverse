// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using Content.Shared._AU14.SavedBuilds;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client._AU14.SavedBuilds;

/// <summary>
/// Client driver for the saved-build selection flow. Tracks the current range, committed range boxes
/// and manual picks, owns the <see cref="BuildSaveOverlay"/>, periodically asks the server to resolve
/// the selection (so highlights respect the server-only whitelist), and sends the save request.
/// </summary>
public sealed class BuildSaveModeSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public const int MaxRadius = 5; // 11x11

    public bool Active { get; private set; }
    public int Radius { get; private set; } = 2;

    /// <summary>
    /// Selection ruleset, set by the construction menu's build-mode dropdown. Mapper mode (re-validated
    /// server-side) lets you select ANY world entity, not just things you built. Default is Player.
    /// </summary>
    public BuildSaveMode Mode { get; set; } = BuildSaveMode.Player;

    /// <summary>Mapper mode only: also include unanchored loose items in the selection (default anchored-only).</summary>
    public bool IncludeLoose { get; set; }

    /// <summary>Mapper mode only: also include supported floor tiles in the selection.</summary>
    public bool IncludeTiles { get; set; }

    /// <summary>Also reach onto the z-levels above/below the selection box. Off by default so a range
    /// append only captures the level you are standing on.</summary>
    public bool IncludeMultiZ { get; set; }

    public readonly List<BuildSelectionBox> CommittedBoxes = new();
    public readonly HashSet<NetEntity> ManualAdds = new();
    public readonly HashSet<NetEntity> ManualRemoves = new();
    public readonly HashSet<EntityUid> Highlighted = new();
    public readonly List<BuildSelectionTile> HighlightedTiles = new();

    /// <summary>Raised locally whenever the resolved highlight set changes, so the window can update its count.</summary>
    public event Action? SelectionChanged;

    private BuildSaveOverlay? _overlay;
    private BuildSaveWindow? _window;
    private Vector2i _lastTile;
    private MapId _lastMap = MapId.Nullspace;

    // 🔧 TUNABLE: minimum seconds between selection resolves sent to the server.
    //
    // Every request re-resolves EVERY committed box server-side and ships the whole highlight set back.
    // On a large selection (hundreds of entities across dozens of boxes) firing that on each tile step
    // and each append is what made range selection crawl. Requests are coalesced into one per interval:
    // the live box still tracks the player locally via the overlay, so the delay is not visible as input
    // lag - only the authoritative highlight set lands a fraction later.
    private const float RefreshIntervalSeconds = 0.2f;

    // 🔧 TUNABLE: ceiling on that interval, and the selection size at which it is reached.
    //
    // A resolve carries the whole highlight set back (a 2000-tile selection is a large payload to rebuild
    // and ship), so the interval scales with how much is currently selected: small selections stay snappy,
    // huge ones back off instead of hammering the server while you drag the range around.
    private const float MaxRefreshIntervalSeconds = 1f;
    private const int RefreshBackoffAtCount = 2000;

    private bool _refreshPending;
    private float _refreshCooldown;

    private float CurrentRefreshInterval
    {
        get
        {
            var count = Highlighted.Count + HighlightedTiles.Count;
            if (count <= 0)
                return RefreshIntervalSeconds;

            var t = Math.Clamp(count / (float) RefreshBackoffAtCount, 0f, 1f);
            return float.Lerp(RefreshIntervalSeconds, MaxRefreshIntervalSeconds, t);
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<BuildSelectionResultEvent>(OnSelectionResult);

        // While selection mode is active, left-click toggles individual entities in/out of the selection.
        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnUse, outsidePrediction: true))
            .Register<BuildSaveModeSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<BuildSaveModeSystem>();
    }

    public int HighlightCount => Highlighted.Count;

    /// <summary>Opens the selection window (and enters selection mode), or focuses it if already open.</summary>
    public void ToggleWindow()
    {
        if (_window is { IsOpen: true })
        {
            _window.Close();
            return;
        }

        Enter();
        _window = new BuildSaveWindow(this);
        _window.OnClose += Exit;
        _window.OpenCentered();
    }

    private void Enter()
    {
        if (Active)
            return;

        Active = true;
        _overlay ??= new BuildSaveOverlay(this, _player, _mapManager, _mapSystem, _transform, EntityManager);
        _overlays.AddOverlay(_overlay);
        RequestRefresh();
    }

    private void Exit()
    {
        if (!Active)
            return;

        Active = false;
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);

        CommittedBoxes.Clear();
        ManualAdds.Clear();
        ManualRemoves.Clear();
        Highlighted.Clear();
        HighlightedTiles.Clear();
        _window = null;
    }

    public void SetRadius(int radius)
    {
        Radius = Math.Clamp(radius, 0, MaxRadius);
        RequestRefresh();
    }

    /// <summary>Appends the live range box (centred on the player) to the committed selection.</summary>
    public void AppendCurrentBox()
    {
        if (!TryGetLiveBox(out var box))
            return;

        CommittedBoxes.Add(box);
        RequestRefresh();
    }

    public void ClearSelection()
    {
        CommittedBoxes.Clear();
        ManualAdds.Clear();
        ManualRemoves.Clear();
        RequestRefresh();
    }

    public void Save(string name)
    {
        RaiseNetworkEvent(new RequestSaveBuildEvent
        {
            Name = name,
            Selection = BuildSelection(includeLive: true),
            Mode = Mode,
            IncludeLoose = IncludeLoose,
            IncludeTiles = IncludeTiles,
            IncludeMultiZ = IncludeMultiZ,
        });
        _window?.Close();
    }

    public override void Update(float frameTime)
    {
        if (!Active)
            return;

        if (_refreshCooldown > 0f)
            _refreshCooldown -= frameTime;

        // Only re-query the server when the player moves to a new tile (the live box follows them),
        // rather than every frame.
        if (_player.LocalEntity is { } player && EntityManager.EntityExists(player))
        {
            var map = _transform.GetMapCoordinates(player);
            var tile = new Vector2i((int) MathF.Floor(map.Position.X), (int) MathF.Floor(map.Position.Y));
            if (tile != _lastTile || map.MapId != _lastMap)
            {
                _lastTile = tile;
                _lastMap = map.MapId;
                RequestRefresh();
            }
        }

        if (_refreshPending && _refreshCooldown <= 0f)
            SendRefresh();
    }

    /// <summary>Left-click handler (active only in selection mode): toggle the clicked entity.</summary>
    private bool OnUse(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (!Active || args.State != BoundKeyState.Down)
            return false;

        if (!args.EntityUid.IsValid() || !EntityManager.EntityExists(args.EntityUid))
            return false;

        var net = GetNetEntity(args.EntityUid);
        if (Highlighted.Contains(args.EntityUid))
        {
            ManualRemoves.Add(net);
            ManualAdds.Remove(net);
        }
        else
        {
            ManualAdds.Add(net);
            ManualRemoves.Remove(net);
        }

        RequestRefresh();
        return true;
    }

    /// <summary>Re-resolves the current selection against the server (e.g. after toggling Include-loose).</summary>
    public void RefreshSelection() => RequestRefresh();

    /// <summary>Queues a resolve. Coalesced by <see cref="RefreshIntervalSeconds"/> so a burst of appends
    /// or a run across tiles produces one request instead of one per event.</summary>
    private void RequestRefresh()
    {
        if (!Active)
            return;

        _refreshPending = true;
    }

    private void SendRefresh()
    {
        _refreshPending = false;
        _refreshCooldown = CurrentRefreshInterval;

        RaiseNetworkEvent(new RequestBuildSelectionEvent
        {
            Selection = BuildSelection(includeLive: true),
            Mode = Mode,
            IncludeLoose = IncludeLoose,
            IncludeTiles = IncludeTiles,
            IncludeMultiZ = IncludeMultiZ,
        });
    }

    private BuildSelectionData BuildSelection(bool includeLive)
    {
        var boxes = new List<BuildSelectionBox>(CommittedBoxes);
        if (includeLive && TryGetLiveBox(out var live))
            boxes.Add(live);

        return new BuildSelectionData
        {
            Boxes = boxes,
            ManualAdds = new List<NetEntity>(ManualAdds),
            ManualRemoves = new List<NetEntity>(ManualRemoves),
        };
    }

    private bool TryGetLiveBox(out BuildSelectionBox box)
    {
        box = default;
        if (_player.LocalEntity is not { } player || !EntityManager.EntityExists(player))
            return false;

        box = new BuildSelectionBox
        {
            Center = GetNetCoordinates(Transform(player).Coordinates),
            Radius = Radius,
        };
        return true;
    }

    private void OnSelectionResult(BuildSelectionResultEvent ev)
    {
        Highlighted.Clear();
        HighlightedTiles.Clear();
        foreach (var net in ev.Entities)
        {
            if (TryGetEntity(net, out var uid))
                Highlighted.Add(uid.Value);
        }
        HighlightedTiles.AddRange(ev.Tiles);

        SelectionChanged?.Invoke();
    }
}
