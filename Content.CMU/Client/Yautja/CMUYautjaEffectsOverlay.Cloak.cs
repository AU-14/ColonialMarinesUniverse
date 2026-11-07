using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Yautja;

public partial class CMUYautjaEffectsOverlay
{
    private void DrawCloakTransitions(in OverlayDrawArgs args)
    {
        var query = _entities.EntityQueryEnumerator<CMUYautjaOpticalCloakComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var cloak, out var sprite))
        {
            var age = (float) (_timing.CurTime - cloak.StartedAt).TotalSeconds;
            if (age <= 0 || age >= CMUYautjaPresentation.CloakDuration || !sprite.Visible)
                continue;
            var coordinates = _transform.GetMapCoordinates(uid);
            if (coordinates.MapId != args.MapId || !args.WorldAABB.Enlarged(1).Contains(coordinates.Position))
                continue;
            var p = age / CMUYautjaPresentation.CloakDuration;
            var covered = CMUYautjaPresentation.CloakCoverage(cloak, _timing.CurTime);
            var retro = cloak.Preset == YautjaInvisibilitySound.Retro;
            var front = retro ? MathF.Floor(covered * 34) - 1 : covered * 34 - 1;
            var rotation = sprite.NoRotation ? -(args.Viewport.Eye?.Rotation ?? Angle.Zero) : _transform.GetWorldRotation(uid);
            var origin = coordinates.Position + rotation.RotateVec(sprite.Offset);
            var bounds = _sprites.GetLocalBounds((uid, sprite));
            var at = origin + (rotation + sprite.Rotation).RotateVec(bounds.Center + new Vector2(0, (16 - front) / 32 * bounds.Height));
            Glow(args.WorldHandle, at, 15, Color.FromHex(retro ? "#9ACFFF" : "#FA503A"), MathF.Sin(p * MathF.PI) * .22f);
        }
    }
}
