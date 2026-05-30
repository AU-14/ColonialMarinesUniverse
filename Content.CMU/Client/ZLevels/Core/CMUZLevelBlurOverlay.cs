using System.Numerics;
using Content.Client.Viewport;
using Content.Shared._CMU14.ZLevels;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelBlurOverlay : Overlay
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IConfigurationManager _config = default!;
    private ShaderInstance? _blurShader;
    private const float MaxBlurStrength = 2.0f;

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

        if (zeye.Depth >= 0)
            return false;

        if (args.MapId == MapId.Nullspace)
            return false;

        return true;
    }

    private bool IsBlurEnabled()
    {
        return _config.GetCVar(CMUZLevelsCVars.BlurEnabled) &&
               _config.GetCVar(CMUZLevelsCVars.BlurStrength) > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
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
    }

    private ShaderInstance GetBlurShader()
    {
        return _blurShader ??= _proto.Index(_zBlurShader).InstanceUnique();
    }
}
