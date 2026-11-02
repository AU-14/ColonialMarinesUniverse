using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client.UserInterface.Controls;

public sealed partial class SimpleRadialMenu
{
    private static Control? CreateCMUSurrenderIcon(SpriteSpecifier specifier, Texture texture)
    {
        if (specifier is not SpriteSpecifier.Texture image
            || SpriteSpecifierSerializer.TextureRoot / image.TexturePath != new ResPath("/Textures/CMU14/Interface/Emotes/surrender.png"))
        {
            return null;
        }

        return new TextureRect
        {
            Texture = texture,
            TextureScale = new Vector2(64f / texture.Width, 64f / texture.Height),
        };
    }
}
