using System.Numerics;
using Content.Client.Clickable;
using Content.Shared.CMU14.Automation;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.CMU14.Automation;

/// <summary>
/// CMU14 (ECS-04): the articulated factory_roboticarm2 rig shared by the inserter and the tray arms. It cuts the West
/// frame into fixed base parts, an upper arm pivoting at the shoulder and a forearm and claw pivoting at the elbow,
/// registers their click-map regions, and draws the cables as curves of short scaled segments. All positions are
/// relative to the shoulder, which the rig draws at the sprite origin.
/// </summary>
public sealed class CMUArmRigSystem : EntitySystem
{
    [Dependency] private IClickMapManager _clickMaps = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public const int CableSegments = 12;

    private ArmPart[]? _parts;
    private Texture? _cableTexture;
    private int _forearmPart = -1;

    /// <summary>The index in the part layers of the forearm and claw, the part that pivots at the elbow.</summary>
    public int ForearmPart
    {
        get
        {
            _parts ??= LoadParts();
            return _forearmPart;
        }
    }

    /// <summary>
    /// Builds the rig's layers on first use: the sprite's layer 0 and new blank layers become the two cables'
    /// segments, then one layer per part. Does nothing once <paramref name="layers"/> has been filled.
    /// </summary>
    public void EnsureLayers(Entity<SpriteComponent> sprite, List<SpriteComponent.Layer> layers, List<SpriteComponent.Layer> cables)
    {
        _parts ??= LoadParts();
        if (layers.Count != 0)
            return;

        // The regions preserve the original pixels and proportions; only the two joint angles change.
        _sprite.TryGetLayer((sprite.Owner, sprite.Comp), 0, out var body, false);
        cables.Add(body!);
        for (var i = 1; i < CableSegments * 2; i++)
            cables.Add(_sprite.AddBlankLayer(sprite));
        foreach (var cable in cables)
        {
            _sprite.LayerSetTexture(cable, _cableTexture!);
            _sprite.LayerSetAutoAnimated(cable, false);
        }
        for (var i = 0; i < _parts.Length; i++)
            layers.Add(_sprite.AddBlankLayer(sprite));
        for (var i = 0; i < _parts.Length; i++)
        {
            _sprite.LayerSetTexture(layers[i], _parts[i].Texture);
            _sprite.LayerSetAutoAnimated(layers[i], false);
        }
    }

    /// <summary>
    /// Places the parts in a frame that is mirrored left-right when <paramref name="mirrored"/> is set: angles and the pivot
    /// are given unmirrored, and every part (base included) is flipped horizontally about the shoulder.
    /// </summary>
    public void PlaceParts(List<SpriteComponent.Layer> layers, Angle shoulder, Angle forearm, Vector2 forearmPivot, bool mirrored)
    {
        PlaceParts(layers, shoulder, forearm, forearmPivot, mirrored, true);
    }

    private void PlaceParts(List<SpriteComponent.Layer> layers, Angle shoulder, Angle forearm, Vector2 forearmPivot, bool mirrored, bool setScale)
    {
        _parts ??= LoadParts();
        for (var i = 0; i < _parts.Length; i++)
        {
            var part = _parts[i];
            var angle = part.Joint switch
            {
                ArmJoint.Shoulder => shoulder,
                ArmJoint.Elbow => forearm,
                _ => Angle.Zero,
            };
            var pivot = part.Joint == ArmJoint.Elbow ? forearmPivot : Vector2.Zero;
            var offset = pivot + angle.RotateVec(part.Offset);
            if (mirrored)
            {
                // Mirror(R(a) p) = R(-a) Mirror(p): flip the offset, negate the angle and draw the texture flipped.
                offset.X = -offset.X;
                angle = -angle;
            }
            _sprite.LayerSetOffset(layers[i], offset);
            _sprite.LayerSetRotation(layers[i], angle);
            if (setScale)
                _sprite.LayerSetScale(layers[i], mirrored ? new Vector2(-1f, 1f) : Vector2.One);
        }
    }

    /// <summary>Draws one cable as a quadratic curve of <see cref="CableSegments"/> layers starting at <paramref name="first"/>.</summary>
    public void DrawCable(List<SpriteComponent.Layer> cables, int first, Vector2 start, Vector2 end, float slack)
    {
        var span = end - start;
        var control = (start + end) * 0.5f + new Vector2(-span.Y, span.X) * slack;
        var previous = start;
        for (var i = 0; i < CableSegments; i++)
        {
            var t = (i + 1f) / CableSegments;
            var point = (1f - t) * (1f - t) * start + 2f * (1f - t) * t * control + t * t * end;
            var delta = point - previous;
            var layer = cables[first + i];
            _sprite.LayerSetOffset(layer, (previous + point) * 0.5f);
            _sprite.LayerSetRotation(layer, new Angle(delta));
            // A slight overlap prevents pixel rounding from opening gaps between the short segments.
            _sprite.LayerSetScale(layer, new Vector2((delta.Length() * 32f + 0.5f) / 2f, 1f));
            previous = point;
        }
    }

    private ArmPart[] LoadParts()
    {
        var state = _sprite.GetState(new SpriteSpecifier.Rsi(new ResPath("CMU14/Structures/factory.rsi"), "factory_roboticarm2"));
        var texture = LoadArmTexture();
        ArmPart[] parts =
        [
            Part(state, texture, new UIBox2i(0, 44, 64, 64), ArmJoint.Fixed),
            Part(state, texture, new UIBox2i(0, 38, 18, 44), ArmJoint.Fixed),
            Part(state, texture, new UIBox2i(0, 0, 30, 38), ArmJoint.Shoulder),
            Part(state, texture, new UIBox2i(18, 38, 30, 44), ArmJoint.Shoulder),
            // The shoulder bearing covers the upper arm's seam; the claw passes in front of the base.
            Part(state, texture, new UIBox2i(5, 38, 18, 50), ArmJoint.Fixed),
            Part(state, texture, new UIBox2i(30, 0, 64, 44), ArmJoint.Elbow),
        ];
        _forearmPart = Array.FindIndex(parts, part => part.Joint == ArmJoint.Elbow);
        return parts;
    }

    private Texture LoadArmTexture()
    {
        using var stream = _resources.ContentFileRead(new ResPath("/Textures/CMU14/Structures/factory.rsi/factory_roboticarm2.png"));
        using var sheet = Image.Load<Rgba32>(stream);
        using var arm = new Image<Rgba32>(64, 64);
        var source = sheet.GetPixelSpan();
        var pixels = arm.GetPixelSpan();
        for (var y = 0; y < 64; y++)
        for (var x = 0; x < 64; x++)
        {
            // West's first frame is the first cell of the third row. The source's blue-grey cable
            // pixels are replaced at render time by continuous curves; preserve the motor and neck.
            var pixel = source[(y + 128) * sheet.Width + x];
            var housing = x >= 5 && x < 18 && y >= 38 && y < 54;
            if (pixel.B > pixel.R && !housing)
                pixel.A = 0;
            pixels[y * 64 + x] = pixel;
        }
        using var cable = new Image<Rgba32>(2, 2);
        var wire = cable.GetPixelSpan();
        wire[0] = wire[1] = new Rgba32(55, 57, 66, 255);
        wire[2] = wire[3] = new Rgba32(33, 35, 38, 255);
        _cableTexture = Texture.LoadFromImage(cable, "CMU inserter cable");
        return Texture.LoadFromImage(arm, "CMU inserter arm");
    }

    private ArmPart Part(RSI.State state, Texture texture, UIBox2i bounds, ArmJoint joint)
    {
        var center = ((Vector2) bounds.TopLeft + bounds.BottomRight) * 0.5f;
        var pivot = joint == ArmJoint.Elbow ? CMUInserterAnimation.ElbowPixels : CMUInserterAnimation.ShoulderPixels;
        var part = new AtlasTexture(texture, new UIBox2(bounds.TopLeft, bounds.BottomRight));
        _clickMaps.RegisterRsiRegion(part, state.RSI, state.StateId, RsiDirection.West, bounds);
        return new ArmPart(part, CMUInserterAnimation.PixelOffset(center, pivot), joint);
    }

    private enum ArmJoint : byte { Fixed, Shoulder, Elbow }
    private readonly record struct ArmPart(Texture Texture, Vector2 Offset, ArmJoint Joint);
}
