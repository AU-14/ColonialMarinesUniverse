using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     Draws <see cref="Source"/> through the CRT shader - scanlines, crawling grain, and a roll bar
///     that shears the image sideways as it passes.
/// </summary>
/// <remarks>
///     <para>
///     The shear is why this renders the source into a texture first rather than laying an overlay on
///     top. An overlay can only add pixels over what is beneath it; it can never move them. Once the
///     UI is in a texture the shader can sample it at an offset, which is what produces the tear.
///     </para>
///     <para>
///     Sits <em>after</em> <see cref="Source"/> in the tree, so the source has already drawn normally
///     by the time this runs; the shaded copy is opaque and covers it. That avoids having to suppress
///     the source's own drawing, which the UI system offers no hook for - a parent cannot skip its
///     children, and hiding the source would stop <c>RenderControl</c> reaching it too.
///     </para>
///     <para>
///     Once the source is a texture, bloom and barrel curvature become possible in the same pass.
///     Neither is implemented yet.
///     </para>
/// </remarks>
public sealed class CrtScreenControl : Control
{
    private const string ShaderId = "CMUCrtTerminal";


    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private readonly ShaderInstance? _shader;

    private IRenderTexture? _target;
    private Vector2i _targetSize;

    /// <summary>The subtree to draw through the shader. Must precede this control in the tree.</summary>
    public Control? Source { get; set; }

    public Color Phosphor { get; set; } = Color.FromHex("#46FF8E");

    /// <summary>
    ///     Per-instance overrides; null falls through to the cvar. A menu wants a much quieter
    ///     effect than a prop terminal does - no bulge, no tearing, just the surface texture - while
    ///     still sharing the cvars that tune that texture so both move together.
    /// </summary>
    public float? Curvature { get; set; }

    public float? Vignette { get; set; }

    /// <summary>
    ///     Seconds between roll bars; null falls through to the cvar. Overridable for the same reason
    ///     as the two above: the shipped 19s is right for a surface being used and far too slow for
    ///     one being reviewed side by side with the other artifacts.
    /// </summary>
    public float? RollPeriod { get; set; }

    /// <summary>
    ///     Whether the roll bar runs. Off for anything being read continuously: a band that shears
    ///     the text sideways is characterful on a prop and obstructive on a settings page.
    /// </summary>
    public bool Roll { get; set; } = true;

    /// <summary>
    ///     Whether the animated grain runs. Off for anything being read continuously: per-pixel noise
    ///     redrawn every frame under a paragraph is constant motion in the worst place for it.
    ///     Scanlines are static and stay on either way - they are the part that reads as a tube.
    /// </summary>
    public bool Grain { get; set; } = true;


    /// <summary>
    ///     Which scheduled artifact fires: 0 cycles through them by a hash of the cycle index,
    ///     1 tear, 2 chroma split, 3 dropout. Forcing one is for the control kit, where each has to
    ///     be looked at in isolation rather than waited for.
    /// </summary>
    public enum ArtifactKind
    {
        Cycle = 0,
        Tear = 1,
        Chroma = 2,
        Dropout = 3,
    }

    /// <summary>
    ///     Strength of the scheduled artifacts, 0 for none. Left at 0 the surface behaves exactly as
    ///     it did before artifacts existed - the roll bar and grain are unaffected by this.
    /// </summary>
    public float ArtifactAmount { get; set; }

    /// <summary>Seconds between artifact events.</summary>
    public float ArtifactPeriod { get; set; } = 11f;

    /// <summary>How long one event lasts, in seconds.</summary>
    public float ArtifactSweep { get; set; } = 0.5f;

    public ArtifactKind Artifact { get; set; } = ArtifactKind.Cycle;

    /// <summary>False means the shader prototype did not resolve and nothing will ever draw.</summary>
    public bool ShaderLoaded => _shader != null;

    /// <summary>Set when the render-target path threw, so the console command can report why.</summary>
    public string? LastError { get; private set; }

    public CrtScreenControl()
    {
        IoCManager.InjectDependencies(this);

        MouseFilter = MouseFilterMode.Ignore;
        CanKeyboardFocus = false;

        if (_proto.TryIndex<ShaderPrototype>(ShaderId, out var proto))
            _shader = proto.InstanceUnique();
    }

    // Close() only removes the control from the tree, it never calls Dispose() - so the render
    // target has to be released here or it outlives every window that ever hosted this control.
    protected override void ExitedTree()
    {
        base.ExitedTree();
        _target?.Dispose();
        _target = null;
    }

    // The IRenderHandle overload rather than the DrawingHandleScreen one: RenderControl needs an
    // IRenderHandle and DrawingHandleScreen has no way back to it.
    protected override void Draw(IRenderHandle renderHandle)
    {
        var handle = renderHandle.DrawingHandleScreen;

        if (_shader == null || Source == null)
            return;

        // Checked here, every frame, rather than trusted to callers. This is a CRT-theme effect and
        // must never appear on the base UI - but each window gating it for itself means every new
        // caller has to remember, and has to subscribe to every cvar that could change the answer.
        // OptionsMenu did neither correctly: it watched CrtUiEnabled but only re-applied the
        // palette, so turning the theme off left the grain running on the settings window.
        if (!StyleNano.CrtUiEnabled)
            return;

        var intensity = _cfg.GetCVar(CCVars.CMUCrtEffectIntensity);
        if (intensity <= 0f)
            return;

        var size = PixelSize;
        if (size.X <= 0 || size.Y <= 0)
            return;

        if (_target == null || _targetSize != size)
        {
            _target?.Dispose();
            _target = _clyde.CreateRenderTarget(size, RenderTargetColorFormat.Rgba8Srgb, name: "cmu-crt");
            _targetSize = size;
        }

        try
        {
            // Re-entrant render pass: we are inside the UI's own draw when this runs. If the engine
            // objects to that, it will surface here rather than taking the client down.
            handle.RenderInRenderTarget(_target,
                () => _ui.RenderControl(renderHandle, Source, Vector2i.Zero),
                Color.Transparent);
        }
        catch (Exception e)
        {
            LastError = e.Message;
            return;
        }

        LastError = null;

        // Authored values, used as authored. An earlier version of this scaled the amplitudes down by
        // control size, on the theory that defaults tuned on a 1000x800 mock must be too strong for a
        // small panel. That was wrong twice over: it made the effect nearly invisible on the surface
        // it was meant to fix, and capping the roll bar's shear to a few pixels turned one coherent
        // tear into per-glyph speckle, which reads as corrupted text rather than as a CRT. The roll
        // bar is supposed to displace text by several characters - that is what makes it a roll bar.
        //
        // Surfaces that genuinely want a quieter effect say so through the per-instance properties
        // below, which is a caller stating an intent rather than this control guessing one from a
        // rectangle.
        _shader.SetParameter("intensity", intensity);
        _shader.SetParameter("pitch", _cfg.GetCVar(CCVars.CMUCrtEffectPitch));
        _shader.SetParameter("staticAmount",
            Grain ? _cfg.GetCVar(CCVars.CMUCrtEffectStatic) : 0f);
        _shader.SetParameter("rollPeriod",
            RollPeriod ?? _cfg.GetCVar(CCVars.CMUCrtEffectRollPeriod));
        _shader.SetParameter("rollSweep", _cfg.GetCVar(CCVars.CMUCrtEffectRollSweep));
        _shader.SetParameter("rollHeight", _cfg.GetCVar(CCVars.CMUCrtEffectRollHeight));
        _shader.SetParameter("rollDisplace",
            Roll ? _cfg.GetCVar(CCVars.CMUCrtEffectRollDisplace) : 0f);
        _shader.SetParameter("rollLift",
            Roll ? _cfg.GetCVar(CCVars.CMUCrtEffectRollLift) : 0f);
        _shader.SetParameter("artifactAmount", ArtifactAmount);
        _shader.SetParameter("artifactPeriod", ArtifactPeriod);
        _shader.SetParameter("artifactSweep", ArtifactSweep);
        _shader.SetParameter("artifactMode", (float) Artifact);
        _shader.SetParameter("curvature",
            Curvature ?? _cfg.GetCVar(CCVars.CMUCrtEffectCurvature));
        _shader.SetParameter("vignette",
            Vignette ?? _cfg.GetCVar(CCVars.CMUCrtEffectVignette));
        _shader.SetParameter("aspect", size.Y > 0 ? size.X / (float) size.Y : 1f);
        _shader.SetParameter("phosphor", Linear(Phosphor));

        handle.UseShader(_shader);
        handle.DrawTextureRect(_target.Texture, PixelSizeBox);
        handle.UseShader(null);
    }

    /// <summary>
    ///     Converts an sRGB colour to linear for use as a shader uniform.
    /// </summary>
    /// <remarks>
    ///     <see cref="Color"/> components are sRGB. The render target is <c>Rgba8Srgb</c>, so the GPU
    ///     encodes linear to sRGB on write - handing sRGB components straight to the shader means
    ///     they get encoded a second time and publish far brighter than the colour chosen. Near
    ///     blacks suffer worst: 7/255 arriving as roughly 48/255 is the difference between invisible
    ///     and a pale band around the picture.
    /// </remarks>
    private static Vector3 Linear(Color srgb)
    {
        var linear = Color.FromSrgb(srgb);
        return new Vector3(linear.R, linear.G, linear.B);
    }
}
