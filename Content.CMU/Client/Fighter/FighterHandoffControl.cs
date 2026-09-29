using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Fighter;

/// <summary>Atmospheric cover at the map handoff, behind all usable cockpit controls.</summary>
public sealed class FighterHandoffControl : Control
{
    private readonly ShaderInstance _shader = IoCManager.Resolve<IPrototypeManager>()
        .Index<ShaderPrototype>("CMUFighterClouds").InstanceUnique();
    private readonly IClyde _clyde = IoCManager.Resolve<IClyde>();
    private IRenderTexture? _texture;
    private FighterGroundState? _previous;
    private TimeSpan _revealStarted;
    private float _opacity;

    public void SetFlight(FighterAircraftComponent aircraft, TimeSpan now)
    {
        if (_previous != aircraft.GroundState)
        {
            _revealStarted = _previous != null ? now : now - TimeSpan.FromSeconds(2);
            _previous = aircraft.GroundState;
        }
        var progress = FighterVtol.Progress(now, aircraft.GroundStateStartedAt, aircraft.GroundStateEndsAt);
        _opacity = aircraft.GroundState switch
        {
            FighterGroundState.TakingOff => FighterVtol.Smooth((progress - .72f) / .28f),
            FighterGroundState.Returning when aircraft.RecoveryHandoff => FighterVtol.Smooth(progress),
            FighterGroundState.Airborne or FighterGroundState.Landing => 1 - FighterVtol.Smooth((float) (now - _revealStarted).TotalSeconds / 1.2f),
            _ => 0,
        };
    }

    protected override void Draw(IRenderHandle render)
    {
        if (_opacity <= 0) return;
        var handle = render.DrawingHandleScreen;
        if (_texture == null)
        {
            _texture = _clyde.CreateRenderTarget(new Vector2i(256, 256),
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb, true),
                new TextureSampleParameters { Filter = true }, "fighter-handoff");
            handle.RenderInRenderTarget(_texture, () =>
            {
                handle.SetTransform(Matrix3x2.Identity);
                render.SetScissor(null);
                handle.UseShader(_shader);
                handle.DrawTextureRect(Texture.White, UIBox2.FromDimensions(Vector2.Zero, new Vector2(256)));
                handle.UseShader(null);
            }, Color.Transparent);
        }
        handle.DrawRect(PixelSizeBox, Color.FromHex("#A0B1B9").WithAlpha(_opacity));
        handle.DrawTextureRect(_texture.Texture, PixelSizeBox, Color.White.WithAlpha(_opacity * .55f));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _texture?.Dispose(); _shader.Dispose(); }
        base.Dispose(disposing);
    }
}
