using Content.Shared.Body;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;

namespace Content.Client.Body;

public sealed partial class VisualBodySystem
{
    // CMU14: the yautja body textures are drawn in a tan tone (torso mean ~#726747), and the humanoid skin tint
    // multiplies the layer colour. Tinting those directly darkened and shifted every palette entry - CM-SS13's
    // tan #A69964 came out around #4A3D1B. Every skin-tinted yautja layer is therefore desaturated first, so the
    // applied skin colour is the colour that shows; layers tinted with something else (markings, hair, eyes) keep
    // their own colour.
    private void ApplyYautjaSkinShader(EntityUid body, int index, Color skin, Color color, bool eyes = false)
    {
        if (!TryComp<SpriteComponent>(body, out var sprite))
            return;

        var layer = (SpriteComponent.Layer) sprite[index];
        var enabled = TryComp<HumanoidProfileComponent>(body, out var profile) && profile.Species.Id == "Yautja" &&
                      !eyes && color.WithAlpha(1) == skin.WithAlpha(1);
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
