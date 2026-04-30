using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;
using Content.Shared._RMC14.CrashLand;
using Content.Shared.ParaDrop;

namespace Content.Client.StatusIcon;

public sealed class StatusIconOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly StatusIconSystem _statusIcon;
    private readonly ShaderInstance _unshadedShader;
    private readonly List<StatusIconData> _icons = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    internal StatusIconOverlay()
    {
        IoCManager.InjectDependencies(this);

        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();
        _statusIcon = _entity.System<StatusIconSystem>();
        _unshadedShader = _prototype.Index(UnshadedShader).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        var eyeRot = args.Viewport.Eye?.Rotation ?? default;

        var xformQuery = _entity.GetEntityQuery<TransformComponent>();
        var scaleMatrix = Matrix3Helpers.CreateScale(new Vector2(1, 1));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);

        var query = _entity.AllEntityQueryEnumerator<StatusIconComponent, SpriteComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite, out var xform, out var meta))
        {
            if (xform.MapID != args.MapId || !sprite.Visible)
                continue;

            var spriteBounds = _sprite.GetLocalBounds((uid, sprite));
            var bounds = comp.Bounds ?? spriteBounds;

            var worldPos = _transform.GetWorldPosition(xform, xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            _statusIcon.GetStatusIcons(uid, _icons, meta);
            if (_icons.Count == 0)
                continue;

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matty);

            var countL = 0;
            var countR = 0;
            var accOffsetL = 0;
            var accOffsetR = 0;
            var curTime = _timing.RealTime;
            var fitHeightPx = spriteBounds.Height * EyeManager.PixelsPerMeter;
            var crashOrParaDrop = _entity.HasComponent<CrashLandingComponent>(uid)
                || _entity.HasComponent<ParaDroppingComponent>(uid);
            _icons.Sort();

            foreach (var proto in _icons)
            {
                if (!_statusIcon.IsVisible((uid, meta), proto))
                    continue;

                var texture = _sprite.GetFrame(proto.Icon, curTime);

                float yOffset;
                float xOffset;

                // the icons are ordered left to right, top to bottom.
                // extra icons that don't fit are just cut off.
                if (proto.LocationPreference == StatusIconLocationPreference.Left ||
                    proto.LocationPreference == StatusIconLocationPreference.None && countL <= countR)
                {
                    if (accOffsetL + texture.Height > fitHeightPx)
                        break;
                    if (proto.Layer == StatusIconLayer.Base)
                    {
                        accOffsetL += texture.Height;
                        countL++;
                    }
                    yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float)(accOffsetL - proto.Offset) / EyeManager.PixelsPerMeter;
                    xOffset = -(bounds.Width + sprite.Offset.X) / 2f;

                    if (crashOrParaDrop)
                        yOffset = 0.25f + sprite.Offset.Y;
                }
                else
                {
                    if (accOffsetR + texture.Height > fitHeightPx)
                        break;
                    if (proto.Layer == StatusIconLayer.Base)
                    {
                        accOffsetR += texture.Height;
                        countR++;
                    }

                    yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float)(accOffsetR - proto.Offset) / EyeManager.PixelsPerMeter;
                    xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float)texture.Width / EyeManager.PixelsPerMeter;
                    if (crashOrParaDrop)
                        yOffset = 0.25f + sprite.Offset.Y;
                }

                if (proto.IsShaded)
                    handle.UseShader(null);
                else
                    handle.UseShader(_unshadedShader);

                var position = new Vector2(xOffset, yOffset);
                handle.DrawTexture(texture, position);
            }

            handle.UseShader(null);
            handle.SetTransform(Matrix3x2.Identity);
        }
    }
}
