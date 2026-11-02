using System.Numerics;
using Content.Shared.CMU14.VoiceChat;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.VoiceChat;

public sealed class CMUVoiceSpeakingOverlay : Overlay
{
    private const float Pixel = 1f / EyeManager.PixelsPerMeter;
    private static readonly Vector2 Origin = new Vector2(7f, 12f) * Pixel;
    private static readonly Color Background = Color.Black.WithAlpha(0.6f);

    private readonly IEntityManager _entityManager;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public CMUVoiceSpeakingOverlay(IEntityManager entityManager, IGameTiming timing)
    {
        _entityManager = entityManager;
        _timing = timing;
        _transform = entityManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = Matrix3Helpers.CreateRotation(-(args.Viewport.Eye?.Rotation ?? Angle.Zero));
        var time = (float) _timing.RealTime.TotalSeconds;

        var query = _entityManager.EntityQueryEnumerator<CMUVoiceChatSpeakingComponent, TransformComponent>();
        while (query.MoveNext(out _, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var position = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Contains(position))
                continue;

            handle.SetTransform(Matrix3x2.Multiply(rotation, Matrix3Helpers.CreateTranslation(position)));

            handle.DrawRect(new Box2(Origin, Origin + new Vector2(11f, 9f) * Pixel), Background);
            for (var i = 0; i < 3; i++)
            {
                var height = 2f + 5f * (0.5f + 0.5f * MathF.Sin(time * 9f + i * 1.9f));
                var bottom = Origin + new Vector2(2f + i * 3f, 1f) * Pixel;
                handle.DrawRect(new Box2(bottom, bottom + new Vector2(2f, height) * Pixel), Color.White);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
