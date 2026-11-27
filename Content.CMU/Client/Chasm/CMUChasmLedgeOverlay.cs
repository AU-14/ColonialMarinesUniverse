using System.Numerics;
using Content.Client.Resources;
using Content.Shared.CMU14.Chasm;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Chasm;

public sealed class CMUChasmLedgeOverlay(IEntityManager entities) : Overlay
{
    private static readonly Color Plate = Color.FromHex("#222529AD");
    private static readonly Color Edge = Color.FromHex("#C7C9C474");
    private static readonly Color Ink = Color.FromHex("#F2F1E9");
    private static readonly Color Unfilled = Color.FromHex("#B5B8B572");
    private static readonly Color Warning = Color.FromHex("#FFBBB1");

    private readonly IPlayerManager _player = IoCManager.Resolve<IPlayerManager>();
    private readonly IInputManager _input = IoCManager.Resolve<IInputManager>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly Font _keyFont = IoCManager.Resolve<IResourceCache>().GetFont("/Fonts/RobotoMono/RobotoMono-Bold.ttf", 12);
    private readonly Font _hintFont = IoCManager.Resolve<IResourceCache>().GetFont("/Fonts/RobotoMono/RobotoMono-Bold.ttf", 7);
    private readonly Texture _keycap = entities.System<SpriteSystem>().Frame0(new SpriteSpecifier.Rsi(
        new ResPath("/Textures/CMU14/Interface/chasm_key.rsi"), "key"));
    private readonly SharedContainerSystem _containers = entities.System<SharedContainerSystem>();
    private readonly SpriteSystem _sprites = entities.System<SpriteSystem>();
    private readonly SharedTransformSystem _transform = entities.System<SharedTransformSystem>();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    protected override void Draw(in OverlayDrawArgs args)
    {
        // Draw text in pixels with its origin on the character, inside the world's FOV pass.
        var handle = args.RenderHandle.DrawingHandleScreen;
        var previous = handle.GetTransform();
        var pixel = 1f / EyeManager.PixelsPerMeter;
        var pixelsToWorld = Matrix3x2.CreateScale(pixel, -pixel);
        var rotation = Matrix3Helpers.CreateRotation(-(args.Viewport.Eye?.Rotation ?? Angle.Zero));
        var visible = args.WorldAABB.Enlarged(2f);
        var grips = entities.EntityQueryEnumerator<CMUChasmLedgeGripComponent, SpriteComponent, TransformComponent>();
        while (grips.MoveNext(out var uid, out var grip, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId || !sprite.Visible || (uid != _player.LocalEntity && sprite.Color.A < 0.5f) ||
                _containers.IsEntityInContainer(uid))
                continue;

            var position = _transform.GetWorldPosition(xform);
            if (!visible.Contains(position))
                continue;

            var head = sprite.Offset + new Vector2(0, _sprites.GetLocalBounds((uid, sprite)).Height / 2 + 0.1f);
            handle.SetTransform(pixelsToWorld * Matrix3x2.CreateTranslation(head) * rotation *
                                Matrix3x2.CreateTranslation(position));
            var key = _input.GetKeyName(CMUChasmLedgeInputSystem.GetKey(grip.Key));
            DrawKeycap(handle, key, grip, _timing.CurTime.TotalSeconds % 0.5 < 0.18);
        }

        handle.SetTransform(previous);
    }

    private void DrawKeycap(DrawingHandleScreen handle, string key, CMUChasmLedgeGripComponent grip, bool down)
    {
        var lastTry = grip.Mistakes == CMUChasmLedgeGripComponent.MaxMistakes - 1;
        var hint = Loc.GetString(lastTry ? "cmu-chasm-ledge-last-try" : "cmu-chasm-ledge-climb");
        var hintSize = handle.GetDimensions(_hintFont, hint, 1f);
        const int count = CMUChasmLedgeGripComponent.RequiredPresses;
        const int pitch = 5;
        var labelWidth = MathF.Ceiling(MathF.Max(count * pitch - 1, hintSize.X));
        const int left = -12;
        const int textX = left + 26;
        var right = textX + labelWidth + 3;

        handle.DrawRect(new UIBox2(left + 22, -21, right, -4), Plate);
        handle.DrawRect(new UIBox2(textX, -21, right - 2, -20), Edge);
        handle.DrawString(_hintFont, new Vector2(textX, -14 - hintSize.Y / 2), hint, lastTry ? Warning : Ink);

        for (var i = 0; i < count; i++)
        {
            var x = textX + i * pitch;
            handle.DrawRect(new UIBox2(x, i < grip.Presses ? -8 : -7, x + pitch - 1, -6),
                i < grip.Presses ? Ink : Unfilled);
        }

        var capTop = down ? -24 : -26;
        handle.DrawTexture(_keycap, new Vector2(left, capTop));
        var keyWidth = handle.GetDimensions(_keyFont, key, 1f).X;
        var scale = MathF.Min(1f, 15f / MathF.Max(1f, keyWidth));
        var keySize = handle.GetDimensions(_keyFont, key, scale);
        handle.DrawString(_keyFont, new Vector2(left + 12, capTop + 11) - keySize / 2, key, scale, Ink);
    }
}
