using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Overwatch;

/// <summary>
/// Draws <see cref="Source"/> through the VHS shader. Same technique as
/// <see cref="Content.Client.CMU14.Interface.CrtScreenControl"/>: the source is rendered into a
/// texture first so the shader can move pixels (wobble, tracking band, chroma bleed), which an
/// overlay cannot. Unlike that control it does not follow the CRT UI theme: a camera feed is tape
/// whatever the menus look like.
/// </summary>
/// <remarks>
/// Must come after <see cref="Source"/> in the tree; the source draws normally first and the shaded,
/// opaque copy covers it. If the render pass fails the plain feed simply shows through.
/// </remarks>
public sealed class CMUVhsScreenControl : Control
{
    private const string ShaderId = "CMUOverwatchVhs";

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private readonly ShaderInstance? _shader;
    private IRenderTexture? _target;
    private Vector2i _targetSize;
    private bool _failed;

    public Control? Source { get; set; }

    /// <summary>Show the feed in night-vision green.</summary>
    public bool NightVision { get; set; }

    public CMUVhsScreenControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;

        if (_proto.TryIndex<ShaderPrototype>(ShaderId, out var proto))
            _shader = proto.InstanceUnique();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _target?.Dispose();
        _target = null;
    }

    protected override void Draw(IRenderHandle renderHandle)
    {
        if (_shader == null || _failed || Source is not { Visible: true } source)
            return;

        var size = PixelSize;
        if (size.X <= 0 || size.Y <= 0)
            return;

        if (_target == null || _targetSize != size)
        {
            _target?.Dispose();
            _target = _clyde.CreateRenderTarget(size, RenderTargetColorFormat.Rgba8Srgb, name: "cmu-overwatch-vhs");
            _targetSize = size;
        }

        var handle = renderHandle.DrawingHandleScreen;
        try
        {
            handle.RenderInRenderTarget(_target,
                () => _ui.RenderControl(renderHandle, source, Vector2i.Zero),
                Color.Black);
        }
        catch (Exception e)
        {
            // Leave the plain feed showing rather than retrying a broken pass every frame.
            _failed = true;
            Logger.GetSawmill("cmu.overwatch").Error($"VHS feed render failed: {e}");
            return;
        }

        _shader.SetParameter("aspect", size.X / (float) size.Y);
        _shader.SetParameter("nightVision", NightVision ? 1f : 0f);
        handle.UseShader(_shader);
        handle.DrawTextureRect(_target.Texture, PixelSizeBox);
        handle.UseShader(null);
    }
}
