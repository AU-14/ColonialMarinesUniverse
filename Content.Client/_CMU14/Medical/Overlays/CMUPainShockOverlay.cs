using Content.Shared._CMU14.Medical.StatusEffects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Medical.Overlays;

public sealed class CMUPainShockOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public override bool RequestScreenTexture => true;

    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _player;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance? _shader;

    public float CurrentIntensity;

    public float TargetIntensity;

    public PainTier TargetTier = PainTier.None;

    public CMUPainShockOverlay(IEntityManager entMan, IPlayerManager player, IPrototypeManager proto, IGameTiming timing)
    {
        _entMan = entMan;
        _player = player;
        _timing = timing;
        _shader = proto.HasIndex<ShaderPrototype>("GradientCircleMask")
            ? proto.Index<ShaderPrototype>("GradientCircleMask").InstanceUnique()
            : null;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        TargetTier = ReadTierFromLocalPlayer();
        TargetIntensity = TargetTier switch
        {
            PainTier.None => 0f,
            PainTier.Mild => 0.10f,
            PainTier.Moderate => 0.20f,
            PainTier.Severe => 0.35f,
            PainTier.Shock => 0.50f,
            _ => 0f,
        };
        CurrentIntensity = MathHelper.Lerp(CurrentIntensity, TargetIntensity, args.DeltaSeconds * 4f);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (CurrentIntensity < 0.02f || _shader is null)
            return;

        var color = TargetTier switch
        {
            PainTier.Mild => new Color(0.50f, 0.25f, 0.10f, CurrentIntensity),
            PainTier.Moderate => new Color(0.63f, 0.25f, 0.13f, CurrentIntensity),
            PainTier.Severe => new Color(0.75f, 0.19f, 0.19f, CurrentIntensity),
            PainTier.Shock => MakeShockPulse(CurrentIntensity),
            _ => new Color(1.0f, 0.9f, 0.3f, CurrentIntensity),
        };

        _shader.SetParameter("CircleRadius", 0.3f + (1f - CurrentIntensity) * 0.4f);
        _shader.SetParameter("CircleColor", color);
        _shader.SetParameter("BackgroundColor", new Color(0, 0, 0, 0));

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, color);
        handle.UseShader(null);
    }

    private Color MakeShockPulse(float baseAlpha)
    {
        var t = (float)_timing.RealTime.TotalSeconds * MathF.PI * 1.4f;
        var pulse = 1f + 0.2f * MathF.Sin(t);
        return new Color(0.88f, 0.06f, 0.06f, MathF.Min(1f, baseAlpha * pulse));
    }

    public PainTier ReadTierFromLocalPlayer()
    {
        var player = _player.LocalEntity;
        if (player is null)
            return PainTier.None;
        if (!_entMan.TryGetComponent<PainShockComponent>(player.Value, out var pain))
            return PainTier.None;
        return pain.Tier;
    }
}
