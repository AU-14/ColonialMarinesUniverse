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
    private readonly ShaderInstance? _blurShader;

    public override bool RequestScreenTexture => _config.GetCVar(CMUZLevelsCVars.BlurEnabled);
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ProtoId<ShaderPrototype> _zBlurShader = "CMUZBlur";

    public CMUZLevelBlurOverlay()
    {
        IoCManager.InjectDependencies(this);
        _blurShader = _proto.Index(_zBlurShader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_config.GetCVar(CMUZLevelsCVars.BlurEnabled))
            return false;

        if (args.Viewport.Eye is not ScalingViewport.ZEye zeye)
            return false;

        if (zeye.Depth >= 0)
            return false;

        if (args.MapId == MapId.Nullspace)
            return false;

        return true;
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

        _blurShader?.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _blurShader?.SetParameter("BLUR_COLOR", ambientColor);

        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(_blurShader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }
}
