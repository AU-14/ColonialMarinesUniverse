using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Hands.Controls;

public sealed partial class HandButton
{
    private TextureRect? _cmuSurrenderIndicator;

    internal void CMUSetSurrendering(bool raised, SpriteSystem sprites)
    {
        if (_cmuSurrenderIndicator == null)
        {
            if (!raised)
                return;

            _cmuSurrenderIndicator = new TextureRect
            {
                Name = "CMUSurrenderIndicator",
                Texture = sprites.Frame0(new SpriteSpecifier.Rsi(
                    new ResPath("_RMC14/Interface/xeno_interface.rsi"), "blocked")),
                TextureScale = new Vector2(2, 2),
                Modulate = Color.White.WithAlpha(0.65f),
                MouseFilter = MouseFilterMode.Ignore,
            };
            AddChild(_cmuSurrenderIndicator);
        }

        if (_cmuSurrenderIndicator.Visible != raised)
            _cmuSurrenderIndicator.Visible = raised;
    }
}
