// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._AU14.SavedBuilds;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._AU14.SavedBuilds;

/// <summary>
/// A tiny composite preview of a saved build: draws each stored entity's icon at its relative position, scaled
/// to fit a fixed square. Gives the "Saved Builds" cards a real picture of the build instead of just a count.
/// </summary>
public sealed class SavedBuildThumbnail : Control
{
    private readonly List<BuildPreviewEntity> _preview;
    private readonly SpriteSystem _sprite;

    // Cap how many icons we draw so a huge build can't make the card expensive to render.
    private const int MaxIcons = 60;

    public SavedBuildThumbnail(IReadOnlyList<BuildPreviewEntity> preview, SpriteSystem sprite, float size)
    {
        _preview = new List<BuildPreviewEntity>(preview);
        _sprite = sprite;
        MinSize = new Vector2(size, size);
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_preview.Count == 0)
            return;

        // Bounding box of the build (in tiles) so we can fit it to the control.
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var p in _preview)
        {
            var v = new Vector2(p.X, p.Y);
            min = Vector2.Min(min, v);
            max = Vector2.Max(max, v);
        }

        var spanX = MathF.Max(max.X - min.X, 1f);
        var spanY = MathF.Max(max.Y - min.Y, 1f);
        var box = PixelSize;
        var tilePx = MathF.Min(box.X / (spanX + 1f), box.Y / (spanY + 1f));
        var center = (min + max) / 2f;
        var ctrlCenter = new Vector2(box.X / 2f, box.Y / 2f);
        var half = tilePx / 2f;

        var drawn = 0;
        foreach (var p in _preview)
        {
            if (drawn >= MaxIcons)
                break;

            Texture tex;
            try
            {
                tex = _sprite.Frame0(new SpriteSpecifier.EntityPrototype(p.Proto));
            }
            catch
            {
                continue; // unknown/odd prototype - skip it rather than break the card
            }

            // World Y is up, screen Y is down, so flip Y when placing.
            var relX = (p.X - center.X) * tilePx;
            var relY = -(p.Y - center.Y) * tilePx;
            var pos = ctrlCenter + new Vector2(relX, relY);
            handle.DrawTextureRect(tex, new UIBox2(pos.X - half, pos.Y - half, pos.X + half, pos.Y + half));
            drawn++;
        }
    }
}
