using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Yautja;

public partial class CMUYautjaEffectsOverlay
{
    private void DrawXenoButcher(in OverlayDrawArgs args, EntityUid uid, CMUYautjaButcherVisualComponent body,
        SpriteComponent sprite, Vector2 origin, Angle rotation, float phase)
    {
        var bounds = _entities.System<CMUYautjaVisualsSystem>().GetButcherBounds(uid, sprite, true);
        var size = MathF.Sqrt(bounds.Width * bounds.Height);
        Vector2 BodyPoint(float x, float y) => origin + rotation.RotateVec(bounds.Center +
            new Vector2((x / 32 - .5f) * bounds.Width, (.5f - y / 32) * bounds.Height));
        var h = args.WorldHandle;

        // Split shell, peel plates, open thorax, then separate the actual caste's head.
        for (var i = 0; i < 4; i++)
        {
            var wound = i switch { 0 => BodyPoint(20, 14), 1 => BodyPoint(17, 19),
                2 => BodyPoint(15, 22), _ => BodyPoint(22, 13) };
            BloodJet(h, wound, (phase - (.12f + i * .25f)) * 16,
                body.Blood, i + 38, 16 + i * 5, (i == 3 ? .6f : .35f) * size);
        }

        if (phase > .32f)
        {
            var pull = CMUYautjaPresentation.Segment(phase, .32f, .46f);
            for (var side = 0; side < 2; side++)
            {
                var sign = side == 0 ? -1 : 1;
                var wound = BodyPoint(side == 0 ? 14 : 22, 19);
                var fragment = wound + new Vector2(sign * 9, -10 + MathF.Sin(pull * MathF.PI) * 8) / 32 * pull * size;
                DrawVictimFragment(args, uid, sprite, fragment, rotation + sign * pull * .55f, bounds,
                    new UIBox2(side == 0 ? 11 : 20, 14, side == 0 ? 17 : 26, 24));
                PixelLine(h, wound, fragment, body.Blood.WithAlpha((1 - pull) * .65f));
            }
        }

        if (phase > .56f)
        {
            var pull = CMUYautjaPresentation.Segment(phase, .56f, .73f);
            var wound = BodyPoint(16, 21);
            for (var i = 0; i < 3; i++)
            {
                var piece = wound + new Vector2((i - 1) * 7 * pull, -14 * pull +
                    MathF.Sin(pull * MathF.PI) * (7 + i * 2)) / 32 * size;
                DrawTissue(h, i == 1 ? _gib2 : _gib, piece, (.20f + i * .06f) * size,
                    (i - 1) * pull, pull, false, body.Blood);
                PixelLine(h, wound, piece, body.Blood.WithAlpha((1 - pull) * .5f));
            }
        }

        if (phase > .86f)
        {
            var pull = CMUYautjaPresentation.Segment(phase, .86f, .98f);
            var wound = BodyPoint(22, 11);
            var head = wound + new Vector2(5, 15) / 32 * pull * size;
            PixelLine(h, wound, head, body.Blood.WithAlpha((1 - pull) * .75f));
            DrawVictimFragment(args, uid, sprite, head, rotation + pull * .3f, bounds,
                new UIBox2(17, 6, 27, 16));
        }
    }
}
