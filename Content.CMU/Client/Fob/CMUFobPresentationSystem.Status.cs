using System.Numerics;
using Content.Shared.CMU14.Fob;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Fob;

public sealed partial class CMUFobPresentationSystem
{
    private enum GunStatusLayer
    {
        Panel,
    }

    private static readonly Color StatusPanel = Color.FromHex("#10171c");
    private static readonly Color StatusRim = Color.FromHex("#52606b");
    private static readonly Color StatusGreen = Color.FromHex("#70ff9a");
    private static readonly Color StatusAmber = Color.FromHex("#ffbf47");
    private static readonly Color StatusRed = Color.FromHex("#ff685b");

    private void DrawGunStatus(Entity<CMUFobPresentationComponent> ent, SpriteComponent sprite,
        CMUFobAnimationComponent animation, float age)
    {
        // The panel stays on the foundation, upright for this viewer, while the gun traverses.
        var center = new Vector2(0, -24);
        var index = 0;
        DrawStatusRect(ent, sprite, animation, ref index, center, new Vector2(15, 7), StatusPanel);
        DrawStatusRect(ent, sprite, animation, ref index, center, new Vector2(13, 5), StatusRim);
        DrawStatusRect(ent, sprite, animation, ref index, center, new Vector2(11, 3), StatusPanel);

        switch (animation.Status)
        {
            case CMUFobPresentationStatus.Active:
            case CMUFobPresentationStatus.Warning:
            case CMUFobPresentationStatus.Starting:
                for (var segment = 0; segment < 3; segment++)
                {
                    var color = StatusGreen;
                    if (animation.Status == CMUFobPresentationStatus.Starting)
                        color = (int) (Math.Max(0, age) * 4) % 3 == segment ? StatusAmber : Color.FromHex("#634823");
                    else if (animation.Status == CMUFobPresentationStatus.Warning)
                        color = StatusAmber.WithAlpha(0.65f + 0.35f * (0.5f + 0.5f * MathF.Sin(age * 8)));
                    DrawStatusRect(ent, sprite, animation, ref index, center + new Vector2((segment - 1) * 4, 0),
                        new Vector2(3, 3), color);
                }
                break;
            case CMUFobPresentationStatus.Working:
                // Pause bars distinguish a reload/service stop from an armed gun.
                DrawStatusRect(ent, sprite, animation, ref index, center + new Vector2(-3, 0), new Vector2(2, 3), StatusAmber);
                DrawStatusRect(ent, sprite, animation, ref index, center + new Vector2(3, 0), new Vector2(2, 3), StatusAmber);
                break;
            case CMUFobPresentationStatus.Empty:
                DrawStatusRect(ent, sprite, animation, ref index, center, new Vector2(9, 3), StatusRed);
                DrawStatusRect(ent, sprite, animation, ref index, center, new Vector2(7, 1), StatusPanel);
                break;
            case CMUFobPresentationStatus.Broken:
                DrawStatusRect(ent, sprite, animation, ref index, center, new Vector2(7, 1), StatusRed, Angle.FromDegrees(30));
                DrawStatusRect(ent, sprite, animation, ref index, center, new Vector2(7, 1), StatusRed, Angle.FromDegrees(-30));
                break;
            default:
                DrawStatusRect(ent, sprite, animation, ref index, center, new Vector2(9, 1), StatusRed);
                break;
        }
    }

    private void DrawStatusRect(Entity<CMUFobPresentationComponent> ent, SpriteComponent sprite,
        CMUFobAnimationComponent animation, ref int index, Vector2 offset, Vector2 size, Color color, Angle rotation = default)
    {
        // A separate pool keeps the unshaded panel from changing shaders on reused barrel/flash layers.
        while (animation.StatusLayers.Count <= index)
        {
            var addedIndex = SpriteSystem.AddTextureLayer((ent, sprite), _clickMaps.CMUSolidTexture);
            if (!SpriteSystem.TryGetLayer((ent, sprite), addedIndex, out var added, true))
                return;
            if (animation.StatusLayers.Count == 0)
                SpriteSystem.LayerMapSet((ent, sprite), GunStatusLayer.Panel, addedIndex);
            sprite.LayerSetShader(addedIndex, "unshaded");
            animation.StatusLayers.Add(added);
        }
        var layer = animation.StatusLayers[index++];
        SpriteSystem.LayerSetVisible(layer, true);
        SpriteSystem.LayerSetOffset(layer, offset / 32);
        SpriteSystem.LayerSetScale(layer, size / new Vector2(_clickMaps.CMUSolidTexture.Width, _clickMaps.CMUSolidTexture.Height));
        SpriteSystem.LayerSetRotation(layer, rotation);
        SpriteSystem.LayerSetColor(layer, color);
    }
}
