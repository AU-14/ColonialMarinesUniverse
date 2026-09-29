using Content.Client.Viewport;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Fighter;

/// <summary>Applies optics color processing only to the shared ground-camera viewport.</summary>
public sealed class FighterSensorOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> SensorShader = "CMUFighterSensor";
    private readonly ShaderInstance _shader = IoCManager.Resolve<IPrototypeManager>().Index(SensorShader).InstanceUnique();

    public ScalingViewport? Viewport;
    public FighterSensorMode Mode;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    protected override bool BeforeDraw(in OverlayDrawArgs args) =>
        Viewport is { Visible: true } && args.ViewportControl == Viewport && Mode != FighterSensorMode.Normal;

    protected override void Draw(in OverlayDrawArgs args)
    {
        _shader.SetParameter("thermal", Mode == FighterSensorMode.Thermal ? 1f : 0f);
        var handle = args.ScreenHandle;
        handle.UseShader(_shader);
        handle.DrawTextureRect(args.Viewport.RenderTarget.Texture, args.ViewportBounds);
        handle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        _shader.Dispose();
        base.DisposeBehavior();
    }
}
