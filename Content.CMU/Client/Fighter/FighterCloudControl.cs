using System.Numerics;
using Content.Client.Viewport;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Graphics;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Fighter;

/// <summary>Clouds use the ground projection, keeping opaque cores over the server's visibility mask.</summary>
public sealed class FighterCloudControl : Control
{
    private static readonly ProtoId<ShaderPrototype> CloudShader = "CMUFighterClouds";
    private static readonly ProtoId<ShaderPrototype> AtmosphereShader = "CMUFighterAtmosphere";
    private readonly ScalingViewport _viewport;
    private readonly ShaderInstance _shader;
    private readonly ShaderInstance _atmosphere;
    private readonly DrawVertexUV2D[] _vertices = new DrawVertexUV2D[6];
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly IClyde _clyde = IoCManager.Resolve<IClyde>();
    private IRenderTexture? _cloudTexture;
    private IRenderTexture? _atmosphereTexture;
    private TimeSpan _nextAtmosphereRender;
    private FighterAircraftComponent? _aircraft;
    private Vector2 _point;
    private bool _available;

    public bool Canopy { get; init; }

    public FighterCloudControl(ScalingViewport viewport)
    {
        _viewport = viewport;
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Ignore;
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        _shader = prototypes.Index(CloudShader).InstanceUnique();
        _atmosphere = prototypes.Index(AtmosphereShader).InstanceUnique();
    }

    public void SetFlight(FighterAircraftComponent aircraft, Vector2 point, bool available)
    {
        _aircraft = aircraft;
        _point = point;
        _available = available;
    }

    private Vector2 Project(Vector2 point) => _viewport.WorldToScreen(point) - GlobalPixelPosition;

    protected override void Draw(IRenderHandle render)
    {
        var handle = render.DrawingHandleScreen;
        if (_aircraft is not { } aircraft)
            return;
        if (Canopy && _viewport.Eye is { } exteriorEye)
            _point = exteriorEye.Position.Position;
        if (!_available && !Canopy)
        {
            handle.DrawRect(PixelSizeBox, Color.FromHex("#08120F"));
            return;
        }
        var thermal = !Canopy && FighterOptics.Mode(aircraft, _timing.CurTime) == FighterSensorMode.Thermal;
        if (_available && !thermal && aircraft.Altitude == FighterAltitude.High)
        {
            if (_cloudTexture == null)
            {
                _cloudTexture = _clyde.CreateRenderTarget(new Vector2i(256, 256),
                    new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb, true), new TextureSampleParameters { Filter = true }, "fighter-cloud");
                handle.RenderInRenderTarget(_cloudTexture, () =>
                {
                    handle.SetTransform(Matrix3x2.Identity);
                    render.SetScissor(null);
                    handle.UseShader(_shader);
                    handle.DrawTextureRect(Texture.White, UIBox2.FromDimensions(Vector2.Zero, new Vector2(256)));
                    handle.UseShader(null);
                }, Color.Transparent);
            }
            var cellX = (int) MathF.Round(_point.X / FighterFlight.CloudSpacing);
            var cellY = (int) MathF.Round(_point.Y / FighterFlight.CloudSpacing);
            for (var x = cellX - 2; x <= cellX + 2; x++)
            for (var y = cellY - 2; y <= cellY + 2; y++)
                DrawCloud(handle, FighterFlight.CloudCenter(x, y));
        }
        // Project the atmosphere through the same eye as the terrain. Its border
        // mask must move across the view with the map, not flip at a flight phase.
        var topLeft = _viewport.ScreenToMap(GlobalPixelPosition).Position;
        var bottomRight = _viewport.ScreenToMap(GlobalPixelPosition + PixelSize).Position;
        var topRight = _viewport.ScreenToMap(GlobalPixelPosition + new Vector2(PixelSize.X, 0)).Position;
        var bottomLeft = topLeft + bottomRight - topRight;
        var clearInterior = aircraft.Battlefield.Enlarged(-36);
        var atBoundary = Canopy && (!clearInterior.Contains(topLeft) || !clearInterior.Contains(bottomRight) ||
                                   !clearInterior.Contains(topRight) || !clearInterior.Contains(bottomLeft));
        if (Canopy && (!_available || atBoundary) || (_available && !thermal && aircraft.Altitude == FighterAltitude.Cloud))
        {
            _atmosphereTexture ??= _clyde.CreateRenderTarget(new Vector2i(640, 360),
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb, true), new TextureSampleParameters { Filter = true }, "fighter-atmosphere");
            if (_timing.CurTime >= _nextAtmosphereRender)
            {
                _nextAtmosphereRender = _timing.CurTime + TimeSpan.FromMilliseconds(33);
                _atmosphere.SetParameter("origin", (topLeft + bottomRight) / 40f);
                _atmosphere.SetParameter("worldRight", (topRight - topLeft) / 20f);
                _atmosphere.SetParameter("worldForward", (topRight - bottomRight) / 20f);
                _atmosphere.SetParameter("inCloud", aircraft.Altitude == FighterAltitude.Cloud ? 1f : 0f);
                _atmosphere.SetParameter("terrainVisible", Canopy && _available ? 1f : 0f);
                _atmosphere.SetParameter("terrainMin", new Vector2(aircraft.Battlefield.Left, aircraft.Battlefield.Bottom) / 20f);
                _atmosphere.SetParameter("terrainMax", new Vector2(aircraft.Battlefield.Right, aircraft.Battlefield.Top) / 20f);
                handle.RenderInRenderTarget(_atmosphereTexture, () =>
                {
                    handle.SetTransform(Matrix3x2.Identity);
                    render.SetScissor(null);
                    handle.UseShader(_atmosphere);
                    handle.DrawTextureRect(Texture.White, UIBox2.FromDimensions(Vector2.Zero, new Vector2(640, 360)));
                    handle.UseShader(null);
                }, Color.Transparent);
            }
            handle.DrawTextureRect(_atmosphereTexture.Texture, PixelSizeBox);
        }
        if (!_available)
            return;
        if (!Canopy)
        {
            var color = FighterOptics.CloudsBlock(aircraft, _point, _timing.CurTime) ? Color.FromHex("#DDB775") : Color.FromHex("#BCD999");
            var center = Project(_point);
            handle.DrawLine(center - new Vector2(15, 0), center - new Vector2(5, 0), color);
            handle.DrawLine(center + new Vector2(5, 0), center + new Vector2(15, 0), color);
            handle.DrawLine(center - new Vector2(0, 15), center - new Vector2(0, 5), color);
            handle.DrawLine(center + new Vector2(0, 5), center + new Vector2(0, 15), color);
        }
        if (aircraft.Mark is { } mark)
            handle.DrawRect(UIBox2.FromDimensions(Project(mark) - new Vector2(8), new Vector2(16)), Color.FromHex("#E3C88B"), false);
        if (aircraft.TrainingImpact is { } impact)
            handle.DrawCircle(Project(impact), 15, Color.Orange, false);
    }

    private void DrawCloud(DrawingHandleScreen handle, Vector2 center)
    {
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        var a = Vertex(0, 0);
        var b = Vertex(1, 0);
        var c = Vertex(0, 1);
        var d = Vertex(1, 1);
        _vertices[0] = a;
        _vertices[1] = b;
        _vertices[2] = c;
        _vertices[3] = b;
        _vertices[4] = d;
        _vertices[5] = c;
        if (max.X < 0 || max.Y < 0 || min.X > PixelSize.X || min.Y > PixelSize.Y)
            return;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _cloudTexture!.Texture, _vertices);

        DrawVertexUV2D Vertex(int x, int y)
        {
            var uv = new Vector2(x, y);
            var point = Project(center + (uv - new Vector2(.5f)) * new Vector2(79.2f, -54));
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
            return new DrawVertexUV2D(point, uv);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shader.Dispose();
            _atmosphere.Dispose();
            _cloudTexture?.Dispose();
            _atmosphereTexture?.Dispose();
        }
        base.Dispose(disposing);
    }
}
