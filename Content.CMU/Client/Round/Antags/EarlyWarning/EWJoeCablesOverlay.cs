using System.Numerics;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// THE LIGHT's vignette (phase E6, A-14): while the local player is caught in an Early Warning Joe's
/// light, bright cables creep in from the screen edges over a darkening rim. They converge on the
/// player's own sprite and their reach follows the envelope's progress; at full they touch the sprite
/// (playtest 4), which is when the server lands the paralysis. Same edge technique as the sapper snare
/// vignette, plus procedural lines for the cables.
/// </summary>
public sealed class EWJoeCablesOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _player;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;

    private float _progress;
    private EntityUid _local;

    /// <summary>How far into the sprite's half-tile radius the cable tips sink at full envelope.</summary>
    private const float ContactDepth = 0.75f;

    private const int Cables = 18;
    private const int RimLayers = 16;

    public EWJoeCablesOverlay()
    {
        _entMan = IoCManager.Resolve<IEntityManager>();
        _player = IoCManager.Resolve<IPlayerManager>();
        _timing = IoCManager.Resolve<IGameTiming>();
        _transform = _entMan.System<SharedTransformSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player
            || !_entMan.TryGetComponent(player, out EWJoeEnvelopedComponent? envelope)
            || envelope.Progress <= 0.01f)
        {
            return false;
        }

        _progress = envelope.Progress;
        _local = player;
        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        float left = bounds.Left;
        float top = bounds.Top;
        float right = bounds.Right;
        float bottom = bounds.Bottom;
        var width = right - left;
        var height = bottom - top;
        var center = new Vector2(left + width / 2f, top + height / 2f);
        var time = (float) _timing.CurTime.TotalSeconds;

        // the cables close on the player's sprite, wherever the eye puts it (zoom offset, lookahead);
        // half a tile on screen is the sprite's reach
        var target = center;
        var spriteRadius = 16f;
        if (args.ViewportControl is { } viewport && _entMan.EntityExists(_local))
        {
            var world = _transform.GetWorldPosition(_local);
            target = viewport.WorldToScreen(world);
            spriteRadius = MathF.Max(4f, (viewport.WorldToScreen(world + new Vector2(0f, 0.5f)) - target).Length());
        }

        // darkening rim
        var depth = MathF.Min(width, height) * (0.15f + 0.3f * _progress);
        var band = depth / RimLayers;
        for (var i = 0; i < RimLayers; i++)
        {
            var edge = 1f - i / (float) RimLayers;
            var color = new Color(0.12f, 0.05f, 0f, 0.55f * _progress * edge * edge);
            var off = i * band;
            handle.DrawRect(new UIBox2(left, top + off, right, top + off + band), color);
            handle.DrawRect(new UIBox2(left, bottom - off - band, right, bottom - off), color);
            handle.DrawRect(new UIBox2(left + off, top, left + off + band, bottom), color);
            handle.DrawRect(new UIBox2(right - off - band, top, right - off, bottom), color);
        }

        // cables: from beyond the screen edge toward the sprite, reaching further as the envelope fills;
        // at full their tips sink into the sprite's edge
        var radius = MathF.Max(
            MathF.Max(Vector2.Distance(target, new Vector2(left, top)), Vector2.Distance(target, new Vector2(right, top))),
            MathF.Max(Vector2.Distance(target, new Vector2(left, bottom)), Vector2.Distance(target, new Vector2(right, bottom))));
        var contact = spriteRadius * ContactDepth;
        var tipDistance = EndDistance(_progress, radius, contact);
        var pulse = 0.75f + 0.25f * MathF.Sin(time * 4f);
        var cable = new Color(1f, 0.54f, 0f, 0.85f * pulse * MathF.Min(1f, _progress * 1.5f));
        var glow = cable.WithAlpha(cable.A * 0.35f);
        var touching = tipDistance <= contact + 0.5f;

        for (var i = 0; i < Cables; i++)
        {
            var angle = i / (float) Cables * MathF.Tau + 0.13f * MathF.Sin(time * 0.7f + i);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var start = target + direction * radius;
            var end = target + direction * tipDistance;

            // a slight bend so they read as cables, not rays; it fades out toward the tip so the tip lands
            // where it is aimed
            var normal = new Vector2(-direction.Y, direction.X);
            var bend = normal * (12f * MathF.Sin(time * 1.3f + i * 1.7f));
            var mid = (start + end) / 2f + bend;
            var neck = end + direction * MathF.Min(spriteRadius * 2f, (radius - tipDistance) / 4f);

            DrawThick(handle, start, mid, cable, glow);
            DrawThick(handle, mid, neck, cable, glow);
            DrawThick(handle, neck, end, cable, glow);

            if (touching)
                handle.DrawCircle(end, 2.5f, cable);
        }
    }

    /// <summary>
    /// Distance from the sprite's centre to the cable tips: from the far screen edge at no progress to
    /// <paramref name="contact"/> (inside the sprite's edge) at full. Public for tests.
    /// </summary>
    public static float EndDistance(float progress, float radius, float contact)
    {
        var t = 0.15f + 0.85f * Math.Clamp(progress, 0f, 1f);
        return radius + (contact - radius) * t;
    }

    private static void DrawThick(DrawingHandleScreen handle, Vector2 from, Vector2 to, Color core, Color glow)
    {
        var direction = to - from;
        if (direction.LengthSquared() < 0.01f)
            return;

        var normal = Vector2.Normalize(new Vector2(-direction.Y, direction.X));
        handle.DrawLine(from, to, core);
        handle.DrawLine(from + normal, to + normal, core);
        handle.DrawLine(from + normal * 2.5f, to + normal * 2.5f, glow);
        handle.DrawLine(from - normal * 1.5f, to - normal * 1.5f, glow);
    }
}
