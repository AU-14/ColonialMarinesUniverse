using System.Numerics;
using Content.Shared.Bed.Sleep;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Sleep;

/// <summary>
///     Asleep, your eyes are closed: the screen is very dark, with a black vignette closing in.
/// </summary>
public sealed class CMUSleepingOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CircleMaskShader = "GradientCircleMask";

    private const float DimAlpha = 0.85f;
    private const float OuterAlpha = 1f;
    private const float FadeStart = 0.08f;
    private const float FadeEnd = 0.36f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly ShaderInstance _shader;

    public CMUSleepingOverlay(IEntityManager entities, IPlayerManager player, IPrototypeManager prototypes)
    {
        _entities = entities;
        _player = player;
        _shader = prototypes.Index(CircleMaskShader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _player.LocalEntity is { } local && _entities.HasComponent<SleepingComponent>(local);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        handle.DrawRect(args.WorldAABB, Color.Black.WithAlpha(DimAlpha));

        var radius = Math.Min(args.ViewportBounds.Width, args.ViewportBounds.Height);
        _shader.SetParameter("color", new Vector3(0f, 0f, 0f));
        _shader.SetParameter("darknessAlphaOuter", OuterAlpha);
        _shader.SetParameter("innerCircleRadius", FadeStart * radius);
        _shader.SetParameter("innerCircleMaxRadius", FadeStart * radius);
        _shader.SetParameter("outerCircleRadius", FadeEnd * radius);
        _shader.SetParameter("outerCircleMaxRadius", FadeEnd * radius);

        handle.UseShader(_shader);
        handle.DrawRect(args.WorldAABB, Color.White);
        handle.UseShader(null);
    }
}
