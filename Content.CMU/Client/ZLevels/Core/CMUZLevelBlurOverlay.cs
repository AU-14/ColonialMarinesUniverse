using System.Numerics;
using Content.Client.Viewport;
using Content.Shared._CMU14.ZLevels;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using SysStopwatch = System.Diagnostics.Stopwatch;

namespace Content.Client._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelBlurOverlay : Overlay
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IGameTiming _timing = default!;
    private ShaderInstance? _blurShader;
    private const float MaxBlurStrength = 2.0f;

    internal static BlurDebugStats LastBlurDebugStats { get; } = new();

    public override bool RequestScreenTexture => IsBlurEnabled();
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ProtoId<ShaderPrototype> _zBlurShader = "CMUZBlur";

    public CMUZLevelBlurOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!IsBlurEnabled())
            return false;

        if (args.Viewport.Eye is not ScalingViewport.ZEye zeye)
            return false;

        if (!ShouldBlurPass(zeye))
            return false;

        if (args.MapId == MapId.Nullspace)
            return false;

        LastBlurDebugStats.NotePass(_timing.CurFrame);
        return true;
    }

    internal static bool ShouldBlurPass(ScalingViewport.ZEye zEye)
    {
        return zEye.Depth < 0 ||
               zEye.Depth == 0 && zEye.BlurCurrentLevel;
    }

    private bool IsBlurEnabled()
    {
        return IsBlurEnabled(_config.GetCVar(CMUZLevelsCVars.BlurStrength));
    }

    internal static bool IsBlurEnabled(float strength)
    {
        return strength > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var drawStart = SysStopwatch.GetTimestamp();
        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        var ambientColor = new Vector3(0, 0, 1); //Default blue

        if (_entity.TryGetComponent<MapLightComponent>(args.MapUid, out var mapLight))
        {
            ambientColor = new Vector3(
                mapLight.AmbientLightColor.R,
                mapLight.AmbientLightColor.G,
                mapLight.AmbientLightColor.B);
        }

        var blurShader = GetBlurShader();
        blurShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        blurShader.SetParameter("BLUR_COLOR", ambientColor);
        blurShader.SetParameter("BLUR_RADIUS", Math.Clamp(_config.GetCVar(CMUZLevelsCVars.BlurStrength), 0f, MaxBlurStrength));

        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(blurShader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
        LastBlurDebugStats.NoteDraw(
            _timing.CurFrame,
            (SysStopwatch.GetTimestamp() - drawStart) * 1000d / SysStopwatch.Frequency);
    }

    private ShaderInstance GetBlurShader()
    {
        return _blurShader ??= _proto.Index(_zBlurShader).InstanceUnique();
    }

    internal sealed class BlurDebugStats
    {
        public uint Frame;
        public int Passes;
        public double DrawMs;

        public void NotePass(uint frame)
        {
            ResetForFrame(frame);
            Passes++;
        }

        public void NoteDraw(uint frame, double elapsedMs)
        {
            ResetForFrame(frame);
            DrawMs += elapsedMs;
        }

        private void ResetForFrame(uint frame)
        {
            if (Frame == frame)
                return;

            Frame = frame;
            Passes = 0;
            DrawMs = 0d;
        }
    }
}
