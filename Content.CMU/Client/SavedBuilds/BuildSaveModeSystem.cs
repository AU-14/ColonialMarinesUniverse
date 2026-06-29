// SPDX-License-Identifier: LicenseRef-AdvancedAtkinsonatorv2-Proprietary
// Copyright (c) 2026 wray-git. All rights reserved.
// Proprietary - reuse only with the Author's prior written authorization. See LICENSE-AdvancedAtkinsonatorv2.md.
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

    public readonly List<BuildSelectionBox> CommittedBoxes = new();
    public readonly HashSet<NetEntity> ManualAdds = new();
    public readonly HashSet<NetEntity> ManualRemoves = new();
    public readonly HashSet<EntityUid> Highlighted = new();

    /// <summary>Raised locally whenever the resolved highlight set changes, so the window can update its count.</summary>
    public event Action? SelectionChanged;

    private BuildSaveOverlay? _overlay;
    private BuildSaveWindow? _window;
    private Vector2i _lastTile;
    private MapId _lastMap = MapId.Nullspace;

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
        });
        _window?.Close();
    }

    public override void Update(float frameTime)
    {
        if (!Active)
            return;

        // Only re-query the server when the player moves to a new tile (the live box follows them),
        // rather than every frame.
        if (_player.LocalEntity is not { } player || !EntityManager.EntityExists(player))
            return;

        var map = _transform.GetMapCoordinates(player);
        var tile = new Vector2i((int) MathF.Floor(map.Position.X), (int) MathF.Floor(map.Position.Y));
        if (tile == _lastTile && map.MapId == _lastMap)
            return;

        _lastTile = tile;
        _lastMap = map.MapId;
        RequestRefresh();
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

    private void RequestRefresh()
    {
        if (!Active)
            return;

        RaiseNetworkEvent(new RequestBuildSelectionEvent { Selection = BuildSelection(includeLive: true) });
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
        foreach (var net in ev.Entities)
        {
            if (TryGetEntity(net, out var uid))
                Highlighted.Add(uid.Value);
        }

        SelectionChanged?.Invoke();
    }
}
