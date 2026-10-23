using System.Numerics;
using Content.Shared.CMU14.PepperSpray;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.PepperSpray;

/// <summary>
///     Pepper-sprayed: your vision is washed orange with a burning vignette closing in, worse the more you've taken,
///     clearing up as the burning wears off.
/// </summary>
public sealed class CMUPepperSprayOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CircleMaskShader = "GradientCircleMask";
    private static readonly Color Orange = Color.FromHex("#E8661C");
    private static readonly TimeSpan FadeOut = TimeSpan.FromSeconds(8);

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;

    private float _strength;

    public CMUPepperSprayOverlay(IEntityManager entities, IPlayerManager player, IGameTiming timing, IPrototypeManager prototypes)
    {
        _entities = entities;
        _player = player;
        _timing = timing;
        _shader = prototypes.Index(CircleMaskShader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local ||
            !_entities.TryGetComponent(local, out CMUPepperSprayedComponent? sprayed))
        {
            return false;
        }

        var strength = Math.Min(1f, 0.6f + 0.2f * (sprayed.Sprays - 1));
        var left = sprayed.EndAt - _timing.CurTime;
        if (left < FadeOut)
            strength *= Math.Max(0f, (float) (left / FadeOut));

        _strength = strength;
        return strength > 0.01f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        handle.DrawRect(args.WorldAABB, Orange.WithAlpha(0.35f * _strength));

        var radius = Math.Min(args.ViewportBounds.Width, args.ViewportBounds.Height);
        _shader.SetParameter("color", new Vector3(0.55f, 0.18f, 0.02f));
        _shader.SetParameter("darknessAlphaOuter", 0.9f * _strength);
        _shader.SetParameter("innerCircleRadius", 0.2f * radius);
        _shader.SetParameter("innerCircleMaxRadius", 0.2f * radius);
        _shader.SetParameter("outerCircleRadius", 0.55f * radius);
        _shader.SetParameter("outerCircleMaxRadius", 0.55f * radius);

        handle.UseShader(_shader);
        handle.DrawRect(args.WorldAABB, Color.White);
        handle.UseShader(null);
    }
}
