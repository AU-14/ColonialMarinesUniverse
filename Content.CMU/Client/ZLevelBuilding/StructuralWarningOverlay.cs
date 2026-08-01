// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): a subtle pulsing red screen-edge vignette that warns the local player when they
/// are standing on an upper z-level tile that no support beam below holds up - ground that will cave in. Purely
/// client-side and informational; the actual collapse is decided server-side by <c>ZLevelSupportSystem</c>.
/// </summary>
public sealed class StructuralWarningOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _player;
    private readonly IGameTiming _timing;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _transform;

    public StructuralWarningOverlay()
    {
        IoCManager.InjectDependencies(this);
        _entMan = IoCManager.Resolve<IEntityManager>();
        _player = IoCManager.Resolve<IPlayerManager>();
        _timing = IoCManager.Resolve<IGameTiming>();
        _map = _entMan.System<SharedMapSystem>();
        _transform = _entMan.System<SharedTransformSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _player.LocalEntity is { } player && OnUnsupportedGround(player);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;

        // Draw relative to the actual viewport rectangle (not 0,0) so the vignette is centred on the game view.
        float left = bounds.Left;
        float top = bounds.Top;
        float right = bounds.Right;
        float bottom = bounds.Bottom;
        var w = right - left;
        var h = bottom - top;

        // Soft, pulsing red vignette: many thin frames from the edge inward with a quadratic alpha falloff toward
        // the centre, so it reads as a glow at the screen edges rather than flat bars.
        var pulse = 0.5f + 0.5f * MathF.Sin((float) _timing.CurTime.TotalSeconds * 4f);
        var maxAlpha = 0.10f + 0.20f * pulse;

        const int layers = 18;
        var depth = MathF.Min(w, h) * 0.22f;
        var band = depth / layers;

        for (var i = 0; i < layers; i++)
        {
            var edgeT = 1f - i / (float) layers; // 1 at the edge, ~0 toward the centre
            var alpha = maxAlpha * edgeT * edgeT;
            var color = new Color(0.85f, 0.04f, 0.04f, alpha);
            var off = i * band;

            handle.DrawRect(new UIBox2(left, top + off, right, top + off + band), color);
            handle.DrawRect(new UIBox2(left, bottom - off - band, right, bottom - off), color);
            handle.DrawRect(new UIBox2(left + off, top, left + off + band, bottom), color);
            handle.DrawRect(new UIBox2(right - off - band, top, right - off, bottom), color);
        }
    }

    /// <summary>True if the player is on an upper z-level tile that no support beam on the level below covers.</summary>
    private bool OnUnsupportedGround(EntityUid player)
    {
        if (!_entMan.TryGetComponent<TransformComponent>(player, out var xform) ||
            xform.MapUid is not { } mapUid ||
            xform.GridUid is not { } gridUid ||
            !_entMan.TryGetComponent<MapGridComponent>(gridUid, out var grid))
        {
            return false;
        }

        // Only upper z-levels need support from below; ground/underground are stable on their own.
        if (!_entMan.TryGetComponent<CMUZLevelMapComponent>(mapUid, out var zMap) ||
            zMap.Depth <= 0 ||
            zMap.MapBelow is not { } below)
        {
            return false;
        }

        var playerTile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);

        // Only warn on PLAYER-BUILT z-floors, which carry a StructuralSupport marker/structure on the tile.
        // Mapper-authored upper floors (e.g. the second storey of a pre-built building) have no such marker and
        // are permanent, so they must never trigger the cave-in vignette.
        var builtHere = false;
        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, playerTile))
        {
            if (_entMan.HasComponent<StructuralSupportComponent>(anchored))
            {
                builtHere = true;
                break;
            }
        }

        if (!builtHere)
            return false;

        var query = _entMan.EntityQueryEnumerator<StructuralSupportComponent, TransformComponent>();
        while (query.MoveNext(out var beamUid, out var support, out var beamXform))
        {
            if (beamXform.MapUid != below || (!support.IsVerticalSupport && !support.IsAnchor))
                continue;

            var beamTile = _map.WorldToTile(gridUid, grid, _transform.GetWorldPosition(beamUid));
            if (Math.Abs(playerTile.X - beamTile.X) + Math.Abs(playerTile.Y - beamTile.Y) <= support.CantileverSpan)
                return false; // within a beam's coverage - supported
        }

        return true;
    }
}
