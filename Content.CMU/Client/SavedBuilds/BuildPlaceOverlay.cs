// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.SavedBuilds;

/// <summary>
/// Placement preview for a saved build: a translucent ghost of each entity's sprite plus the footprint
/// outline (world space, anchored to the cursor / snapped tile), and a small controls hint in the
/// top-left (screen space). Reads its state from <see cref="SavedBuildPlacementSystem"/>.
/// </summary>
public sealed class BuildPlaceOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    private static readonly Color FootprintColor = new(0.3f, 1f, 0.4f, 0.9f);
    private static readonly Color GhostModulate = new(0.6f, 1f, 0.7f, 0.55f);
    private static readonly Color TileGhost = new(0.25f, 1f, 0.35f, 0.38f);
    private static readonly Color HintBg = new(0.055f, 0.06f, 0.047f, 0.78f); // near-black amber-terminal panel
    private static readonly Color HintBorder = Color.FromHex("#FFB000");      // amber accent
    private static readonly Color HintText = Color.FromHex("#E8E0C8");        // terminal text
    private const float LineThickness = 0.08f;

    private readonly SavedBuildPlacementSystem _mode;
    private readonly IEyeManager _eye;
    private readonly IInputManager _input;
    private readonly SpriteSystem _sprite;
    private readonly IPrototypeManager _prototype;
    private readonly Font _font;

    public BuildPlaceOverlay(
        SavedBuildPlacementSystem mode,
        IEyeManager eye,
        IInputManager input,
        SpriteSystem sprite,
        IPrototypeManager prototype,
        IResourceCache cache)
    {
        _mode = mode;
        _eye = eye;
        _input = input;
        _sprite = sprite;
        _prototype = prototype;
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 11);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_mode.Active)
            return;

        if (args.Space == OverlaySpace.ScreenSpace)
        {
            DrawControls(args);
            return;
        }

        var origin = _mode.GetTargetMap();
        if (origin.MapId != args.MapId)
            return;

        var rot = _mode.Rotation;
        var pos0 = origin.Position;
        var handle = args.WorldHandle;

        foreach (var ent in _mode.Preview)
        {
            if (!_mode.TryGetLevelTarget(origin, ent.Z, out var levelOrigin) || levelOrigin.MapId != args.MapId)
                continue;

            if (!_prototype.TryIndex<EntityPrototype>(ent.Proto, out var proto))
                continue;

            Texture texture;
            try
            {
                texture = _sprite.Frame0(proto);
            }
            catch
            {
                continue;
            }

            var p = levelOrigin.Position + rot.RotateVec(new Vector2(ent.X, ent.Y));
            handle.DrawTextureRect(texture, Box2.CenteredAround(p, Vector2.One), GhostModulate);
        }

        foreach (var tile in _mode.Tiles)
        {
            if (!_mode.TryGetLevelTarget(origin, tile.Z, out var levelOrigin) || levelOrigin.MapId != args.MapId)
                continue;

            var p = levelOrigin.Position + rot.RotateVec(new Vector2(tile.X, tile.Y));
            handle.DrawRect(Box2.CenteredAround(p, Vector2.One), TileGhost);
        }

        var min = _mode.RelMin - new Vector2(0.5f, 0.5f);
        var max = _mode.RelMax + new Vector2(0.5f, 0.5f);
        var c0 = pos0 + rot.RotateVec(new Vector2(min.X, min.Y));
        var c1 = pos0 + rot.RotateVec(new Vector2(max.X, min.Y));
        var c2 = pos0 + rot.RotateVec(new Vector2(max.X, max.Y));
        var c3 = pos0 + rot.RotateVec(new Vector2(min.X, max.Y));
        DrawSegment(handle, c0, c1);
        DrawSegment(handle, c1, c2);
        DrawSegment(handle, c2, c3);
        DrawSegment(handle, c3, c0);
    }

    private void DrawControls(in OverlayDrawArgs args)
    {
        var rotateKey = _input.GetKeyFunctionButtonString(EngineKeyFunctions.EditorRotateObject);
        var lines = new List<string>
        {
            Loc.GetString(_mode.IsAdmin ? "saved-build-controls-mode-admin" : "saved-build-controls-mode-player"),
            Loc.GetString("saved-build-controls-gridalign", ("state", _mode.GridAligned ? "ON" : "off")),
            Loc.GetString("saved-build-controls-rotate", ("key", rotateKey)),
            Loc.GetString("saved-build-controls-place"),
            Loc.GetString("saved-build-controls-cancel"),
        };

        var handle = args.ScreenHandle;
        const float pad = 6f;
        const float lineHeight = 15f;
        const float width = 230f;

        // Anchor to the top-left of the GAME viewport (inside the 4:3 letterbox), not the whole screen —
        // otherwise the panel hides under the HUD buttons in the screen corner.
        var vp = args.ViewportBounds;
        var x0 = vp.Left + 8f;
        var y0 = vp.Top + 8f;

        var box = new UIBox2(x0, y0, x0 + width + pad * 2, y0 + lineHeight * lines.Count + pad * 2);
        handle.DrawRect(box, HintBg);
        handle.DrawRect(box, HintBorder, false);

        var y = y0 + pad;
        foreach (var line in lines)
        {
            handle.DrawString(_font, new Vector2(x0 + pad, y), line, HintText);
            y += lineHeight;
        }
    }

    private static void DrawSegment(DrawingHandleWorld handle, Vector2 from, Vector2 to)
    {
        var delta = to - from;
        var length = delta.Length();
        if (length <= 0f)
            return;

        var half = LineThickness * 0.5f;
        var mid = (from + to) * 0.5f;
        var angle = delta.ToWorldAngle();
        var rect = new Box2(-length / 2f, -half, length / 2f, half);
        handle.DrawRect(new Box2Rotated(rect.Translated(mid), angle, mid), FootprintColor);
    }
}
