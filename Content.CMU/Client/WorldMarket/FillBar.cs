// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Numerics;
using Content.Client.CMU14.Crafting;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.WorldMarket;

// A bar that fills: the favour of a faction in a city, the progress of a work, the points of influence.
internal sealed class FillBar : Control
{
    private float _fraction;
    private Color _color;

    public FillBar(float fraction, Color color, int height = 10)
    {
        _fraction = fraction;
        _color = color;
        MinSize = new Vector2(80, height);
        HorizontalExpand = true;
        VerticalAlignment = VAlignment.Center;
    }

    public void Set(float fraction, Color color)
    {
        _fraction = fraction;
        _color = color;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);
        MiniDraw.Pill(handle, box, CraftPalette.Inset);

        var fill = Math.Clamp(_fraction, 0f, 1f);
        if (fill > 0.001f)
            MiniDraw.Pill(handle, new UIBox2(1, 1, Math.Max(PixelSize.Y - 1, (PixelSize.X - 1) * fill), PixelSize.Y - 1), _color);
    }
}
