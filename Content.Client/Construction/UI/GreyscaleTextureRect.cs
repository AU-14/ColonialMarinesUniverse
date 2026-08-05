// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Client.Construction.UI;

/// <summary>
/// A minimal texture control that draws its <see cref="Texture"/> through the engine's "Greyscale" shader.
/// Used to show the AU14 emblem desaturated (a flat <c>Modulate</c> can only multiply colors, it cannot
/// desaturate — so a shader is required for a true greyscale).
/// </summary>
public sealed class GreyscaleTextureRect : Control
{
    private readonly ShaderInstance? _shader;

    public Texture? Texture { get; set; }

    public GreyscaleTextureRect()
    {
        if (IoCManager.Resolve<IPrototypeManager>().TryIndex<ShaderPrototype>("Greyscale", out var proto))
            _shader = proto.Instance();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (Texture == null)
            return;

        if (_shader != null)
            handle.UseShader(_shader);
        handle.DrawTextureRect(Texture, PixelSizeBox);
        if (_shader != null)
            handle.UseShader(null);
    }
}
