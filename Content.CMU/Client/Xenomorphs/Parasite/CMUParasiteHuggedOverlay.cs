using System.Numerics;
using Content.Shared._RMC14.Xenonids.Parasite;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Xenomorphs.Parasite;

/// <summary>
///     With a parasite over their face, the host's view is heavily dimmed and closed in by a dark vignette.
/// </summary>
public sealed class CMUParasiteHuggedOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CircleMaskShader = "GradientCircleMask";

    private const float DimAlpha = 0.6f;
    private const float OuterAlpha = 0.97f;
    private const float FadeStart = 0.12f;
    private const float FadeEnd = 0.42f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly ShaderInstance _shader;

    public CMUParasiteHuggedOverlay(IEntityManager entities, IPlayerManager player, IPrototypeManager prototypes)
    {
        _entities = entities;
        _player = player;
        _shader = prototypes.Index(CircleMaskShader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _player.LocalEntity is { } local &&
               _entities.TryGetComponent(local, out InfectableComponent? infectable) &&
               infectable.BeingInfected;
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
