using Content.Shared.Body;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;

namespace Content.Client.Body;

public sealed partial class VisualBodySystem
{
    // CMU14: achromatic choices also desaturate the colored source textures.
    private void ApplyYautjaSkinShader(EntityUid body, int index, Color skin, Color color, bool eyes = false)
    {
        if (!TryComp<SpriteComponent>(body, out var sprite))
            return;

        var layer = (SpriteComponent.Layer) sprite[index];
        var enabled = TryComp<HumanoidProfileComponent>(body, out var profile) && profile.Species.Id == "Yautja" &&
                      !eyes && color.WithAlpha(1) == skin.WithAlpha(1) &&
                      (skin == YautjaCharacterProfile.GetSkinColorColor(YautjaSkinColor.Gray) ||
                       skin == YautjaCharacterProfile.GetSkinColorColor(YautjaSkinColor.White));
        if (enabled)
            sprite.LayerSetShader(index, "Greyscale");
        else if (layer.ShaderPrototype?.Id == "Greyscale")
            sprite.LayerSetShader(index, null, null);
    }

    private void ApplyYautjaMarkingShader(Entity<SpriteComponent?> body, int index, EntityUid organ, HumanoidVisualLayers layer)
    {
        if (!Resolve(body, ref body.Comp) || layer != HumanoidVisualLayers.Hair || !TryComp<VisualOrganComponent>(organ, out var visual))
            return;

        ApplyYautjaSkinShader(body.Owner, index, visual.Profile.SkinColor, body.Comp[index].Color);
    }
}
