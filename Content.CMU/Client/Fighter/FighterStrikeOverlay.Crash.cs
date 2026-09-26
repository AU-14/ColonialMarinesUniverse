using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterStrikeOverlay
{
    private void DrawCrashingFighters(in OverlayDrawArgs args, TimeSpan now)
    {
        var query = entities.EntityQueryEnumerator<FighterFlybyComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var flyby, out var transform))
        {
            if (!flyby.Crashing || transform.MapID != args.MapId) continue;
            var point = _transform.GetWorldPosition(uid);
            if (!args.WorldAABB.Enlarged(24).Contains(point)) continue;
            var behind = -_transform.GetWorldRotation(uid).RotateVec(Vector2.UnitY);
            var age = (float) now.TotalSeconds;
            for (var i = 0; i < 20; i++)
            {
                var offset = i * .65f + age * 4 % .65f;
                var smoke = point + behind * offset + new Vector2(MathF.Sin(i + age * 3), MathF.Cos(i * 2 + age)) * .25f;
                _particles.Mote(smoke, new Vector2(.3f + i * .08f),
                    Color.FromHex("#393631").WithAlpha((1 - i / 20f) * .8f), smoke: true, seed: i);
                if (i < 5)
                    _particles.Mote(smoke, new Vector2(.3f + .1f * MathF.Sin(age * 30 + i)),
                        Color.FromHex(i % 2 == 0 ? "#FFB640" : "#FA5124").WithAlpha(1 - i / 5f));
            }
        }
    }
}
