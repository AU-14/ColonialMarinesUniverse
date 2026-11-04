using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Yautja;

public partial class CMUYautjaEffectsOverlay
{
    private void DrawPlasmaBolts(in OverlayDrawArgs args)
    {
        var query = _entities.EntityQueryEnumerator<CMUYautjaPlasmaVisualComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bolt, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;
            var at = _transform.GetWorldPosition(uid);
            if (!args.WorldAABB.Enlarged(2).Contains(at))
                continue;
            var distance = (at - bolt.Origin).Length();
            var profile = CMUYautjaPlasmaAppearance.For(bolt.Mode);
            var direction = bolt.Direction.LengthSquared() > .001f ? Vector2.Normalize(bolt.Direction) : Vector2.UnitX;
            var normal = new Vector2(-direction.Y, direction.X);
            var phase = distance * 9 + (float) _timing.CurTime.TotalSeconds * 20;
            at += new Vector2(9, 10) / 32 * Math.Max(0, 1 - distance);
            var h = args.WorldHandle;
            var trail = Math.Min(profile.Trail, distance * 32);
            for (var i = (int) trail; i > 0; i--)
            {
                var strength = 1 - i / (trail + 1);
                var width = Math.Max(0, profile.Width * strength * .7f - .5f);
                var point = at - direction * (i / 32f);
                PixelLine(h, point - normal * (width / 32), point + normal * (width / 32),
                    profile.Glow.WithAlpha(strength * .6f));
            }
            Glow(h, at, 17 * profile.Scale, profile.Glow, .65f);
            PlasmaLobe(h, at, direction, profile.Length, profile.Width, profile, 1);

            if (bolt.Mode == CMUYautjaPlasmaMode.Immobilizer)
            {
                // Twin electrical filaments distinguish the stronger restraint shot from the small stun pulse.
                for (var side = -1; side <= 1; side += 2)
                {
                    var last = at + direction * (4f / 32);
                    for (var i = 0; i <= (int) trail; i += 2)
                    {
                        var point = at - direction * (i / 32f) + normal *
                            (side * (2 + MathF.Sin(phase - i * .7f) * 2) / 32);
                        PixelLine(h, last, point, profile.Shell.WithAlpha(.75f * (1 - i / (trail + 2))));
                        last = point;
                    }
                }
            }
            else if (bolt.Mode == CMUYautjaPlasmaMode.Eradicator)
            {
                // A broad, unstable plasma mass trails two smaller tongues of red-hot plasma.
                for (var side = -1; side <= 1; side += 2)
                    PlasmaLobe(h, at - direction * (Math.Min(9, trail) / 32) +
                        normal * (side * (3 + MathF.Sin(phase) * .5f) / 32), direction,
                        9, 1.5f, profile, .7f);
            }
            else if (bolt.Mode == CMUYautjaPlasmaMode.Lethal)
            {
                PixelLine(h, at - direction * (trail * .7f / 32), at,
                    profile.Core.WithAlpha(.75f));
            }
        }
    }

    private static void PlasmaLobe(DrawingHandleWorld h, Vector2 at, Vector2 direction,
        float length, float width, CMUYautjaPlasmaAppearance profile, float alpha)
    {
        var normal = new Vector2(-direction.Y, direction.X);
        // Draw in native pixels along the flight vector. The rounded nose tapers into the tail.
        for (var x = (int) MathF.Floor(-length * .65f); x <= length * .35f; x++)
        {
            var u = (x + length * .15f) / (length * .5f);
            var radius = width * MathF.Sqrt(Math.Max(0, 1 - u * u));
            for (var y = -(int) radius; y <= (int) radius; y++)
            {
                var edge = MathF.Abs(y) / Math.Max(.1f, radius);
                var color = edge > .65f ? profile.Glow : edge > .3f || u < -.45f ? profile.Shell : profile.Core;
                Pixel(h, at + (direction * x + normal * y) / 32, new Vector2(1f / 32), color.WithAlpha(alpha));
            }
        }
    }

    private void DrawPlasmaMuzzle(DrawingHandleWorld h, Vector2 at, Vector2 target,
        float p, CMUYautjaPlasmaMode mode)
    {
        var profile = CMUYautjaPlasmaAppearance.For(mode);
        var delta = target - at;
        var direction = delta.LengthSquared() > .001f ? Vector2.Normalize(delta) : Vector2.UnitX;
        var flash = Math.Max(0, 1 - p * 3);
        Glow(h, at, 22 * profile.Scale, profile.Glow, flash * .8f);
        if (flash <= 0)
            return;
        PlasmaLobe(h, at + direction * (3f / 32), direction, profile.Length * (1 + p),
            profile.Width * (1 + flash), profile, flash);
        PlasmaArcs(h, at, p * 3, profile, mode == CMUYautjaPlasmaMode.Eradicator ? 6 : 3, 10 * profile.Scale);
    }

    private void DrawPlasmaImpact(DrawingHandleWorld h, Vector2 at, float p, CMUYautjaPlasmaMode mode)
    {
        var profile = CMUYautjaPlasmaAppearance.For(mode);
        var lethal = mode is CMUYautjaPlasmaMode.Lethal or CMUYautjaPlasmaMode.Eradicator;
        var burst = Math.Max(0, 1 - p * 8);
        Glow(h, at, (lethal ? 46 : 27) * profile.Scale, profile.Glow, burst * .85f);
        Glow(h, at, 9 * profile.Scale, profile.Core, Math.Max(0, 1 - p * 15));
        if (lethal)
        {
            Smoke(h, at, p, 28 * profile.Scale, Color.FromHex("#6E6662"));
            Particles(h, at, p, profile.Shell, (int) (26 * profile.Scale), 48 * profile.Scale);
            if (burst > 0)
            {
                for (var i = 0; i < (mode == CMUYautjaPlasmaMode.Eradicator ? 7 : 4); i++)
                {
                    var angle = Random(i + 131) * MathF.Tau;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    PlasmaLobe(h, at + direction * (p * 100 * profile.Scale / 32), direction,
                        10 * profile.Scale, 2 * profile.Scale * burst, profile, burst);
                }
            }
        }
        else
        {
            var snap = p * (mode == CMUYautjaPlasmaMode.Stun ? 6 : 3.5f);
            PlasmaArcs(h, at, snap, profile, mode == CMUYautjaPlasmaMode.Stun ? 5 : 9, 22 * profile.Scale);
            Particles(h, at, p * 3, profile.Shell, 14, 22 * profile.Scale, 5);
        }
    }

    private static void PlasmaArcs(DrawingHandleWorld h, Vector2 at, float p,
        CMUYautjaPlasmaAppearance profile, int count, float reach)
    {
        if (p >= 1)
            return;
        for (var i = 0; i < count; i++)
        {
            var angle = Random(i + 83) * MathF.Tau;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var normal = new Vector2(-direction.Y, direction.X);
            var last = at;
            for (var step = 1; step <= 4; step++)
            {
                var point = at + (direction * (step * reach * (.25f + p * .75f) / 4) +
                    normal * ((Random(i * 17 + step + (int) (p * 9)) - .5f) * 5)) / 32;
                PixelLine(h, last, point, (step == 1 ? profile.Core : profile.Shell).WithAlpha(1 - p));
                last = point;
            }
        }
    }
}
