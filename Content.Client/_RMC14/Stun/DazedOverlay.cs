using System.Numerics;
using Content.Shared._RMC14.Stun;
using Content.Shared.StatusEffectNew.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Stun;

public sealed partial class DazedOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CircleMaskShader = "GradientCircleMask";

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly IEntityManager _entManager;
    private readonly IPlayerManager _playerManager;

    private readonly ShaderInstance _vignetteShader;

    private const float MinVisionScale = 0.1f;
    private const float MaxVisionScale = 1f;

    public DazedOverlay(IEntityManager entManager, IPlayerManager playerManager, IPrototypeManager prototypeManager)
    {
        _entManager = entManager;
        _playerManager = playerManager;

        _vignetteShader = prototypeManager.Index(CircleMaskShader).InstanceUnique();
    }

    // Picks the strongest active RMCDazed-driven effect (combat daze, opioid haze, etc.) rather than
    // being tied to a single hardcoded status effect, since several sources can apply this vignette.
    private RMCDazedComponent? GetStrongestDazed(EntityUid uid)
    {
        if (!_entManager.TryGetComponent(uid, out StatusEffectContainerComponent? container))
            return null;

        RMCDazedComponent? strongest = null;
        foreach (var effect in container.ActiveStatusEffects)
        {
            if (!_entManager.TryGetComponent(effect, out RMCDazedComponent? candidate))
                continue;

            if (strongest == null || candidate.Alpha > strongest.Alpha)
                strongest = candidate;
        }

        return strongest;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var localEntity = _playerManager.LocalEntity;

        if (localEntity == null)
            return;

        var dazed = GetStrongestDazed(localEntity.Value);

        if (dazed == null)
            return;

        var handle = args.WorldHandle;
        var viewport = args.WorldAABB;
        var visionRadius = args.ViewportBounds.Width * Math.Clamp(1f - dazed.VisionReduction, MinVisionScale, MaxVisionScale);

        _vignetteShader.SetParameter("color", new Vector3(dazed.Color.R, dazed.Color.G, dazed.Color.B));
        _vignetteShader.SetParameter("darknessAlphaOuter", dazed.Alpha);
        _vignetteShader.SetParameter("darknessAlphaInner", dazed.InnerAlpha);

        _vignetteShader.SetParameter("innerCircleRadius", dazed.OuterFadeStart * visionRadius);
        _vignetteShader.SetParameter("innerCircleMaxRadius", dazed.OuterFadeStart * visionRadius);

        _vignetteShader.SetParameter("outerCircleRadius", dazed.OuterFadeEnd * visionRadius);
        _vignetteShader.SetParameter("outerCircleMaxRadius", dazed.OuterFadeEnd * visionRadius);

        handle.UseShader(_vignetteShader);
        handle.DrawRect(viewport, Color.White);
        handle.UseShader(null);
    }
}
