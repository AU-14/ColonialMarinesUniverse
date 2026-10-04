using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using GraphicsEye = Robust.Shared.Graphics.Eye;

namespace Content.Client.UserInterface.Systems.Viewport;

public sealed partial class ViewportUIController : UIController
{
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IPlayerManager _playerMan = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;
    public static readonly Vector2i ViewportSize = (EyeManager.PixelsPerMeter * 21, EyeManager.PixelsPerMeter * 15);
    public const int ViewportHeight = 15;
    private MainViewport? Viewport => UIManager.ActiveScreen?.GetWidget<MainViewport>();
    private SharedTransformSystem? _transform;
    private readonly GraphicsEye _fallbackEye = new();
    private bool _warnedNullspaceEye;

    public override void Initialize()
    {
        _configurationManager.OnValueChanged(CCVars.ViewportMinimumWidth, _ => UpdateViewportRatio());
        _configurationManager.OnValueChanged(CCVars.ViewportMaximumWidth, _ => UpdateViewportRatio());
        _configurationManager.OnValueChanged(CCVars.ViewportWidth, _ => UpdateViewportRatio());
        _configurationManager.OnValueChanged(CCVars.ViewportVerticalFit, _ => UpdateViewportRatio());

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
    }

    private void OnScreenLoad()
    {
        ReloadViewport();
    }

    private void UpdateViewportRatio()
    {
        if (Viewport == null)
        {
            return;
        }

        var min = _configurationManager.GetCVar(CCVars.ViewportMinimumWidth);
        var max = _configurationManager.GetCVar(CCVars.ViewportMaximumWidth);
        var width = _configurationManager.GetCVar(CCVars.ViewportWidth);
        var verticalfit = _configurationManager.GetCVar(CCVars.ViewportVerticalFit) && _configurationManager.GetCVar(CCVars.ViewportStretch);

        if (verticalfit)
        {
            width = max;
        }
        else if (width < min || width > max)
        {
            width = CCVars.ViewportWidth.DefaultValue;
        }

        Viewport.Viewport.ViewportSize = (EyeManager.PixelsPerMeter * width, EyeManager.PixelsPerMeter * ViewportHeight);
        Viewport.UpdateCfg();
    }

    public void ReloadViewport()
    {
        if (Viewport == null)
        {
            return;
        }

        UpdateViewportRatio();
        Viewport.Viewport.HorizontalExpand = true;
        Viewport.Viewport.VerticalExpand = true;
        _eyeManager.MainViewport = Viewport.Viewport;
    }

    public override void FrameUpdate(FrameEventArgs e)
    {
        if (Viewport == null)
        {
            return;
        }

        base.FrameUpdate(e);

        var viewportEye = GetViewportEye();
        Viewport.Viewport.Eye = viewportEye;

        if (viewportEye != null)
        {
            _warnedNullspaceEye = false;
            return;
        }

        // verify that the current eye is not "null". Fuck IEyeManager.

        var ent = _playerMan.LocalEntity;
        if (ent == null)
            return;

        _entMan.TryGetComponent(ent, out EyeComponent? eye);

        if (eye?.Eye == _eyeManager.CurrentEye
            && _entMan.GetComponent<TransformComponent>(ent.Value).MapID == MapId.Nullspace)
        {
            // nothing to worry about, the player is just in null space... actually that is probably a problem?
            return;
        }

        if (_warnedNullspaceEye)
            return;

        _warnedNullspaceEye = true;
        // Currently, this shouldn't happen. This likely happened because the main eye was set to null. When this
        // does happen it can create hard to troubleshoot bugs, so lets print some helpful warnings:
        Log.Warning($"Main viewport's eye is in nullspace (main eye is null?). Attached entity: {_entMan.ToPrettyString(ent.Value)}. Entity has eye comp: {eye != null}");
    }

    private IEye? GetViewportEye()
    {
        var currentEye = _eyeManager.CurrentEye;
        if (currentEye.Position.MapId != MapId.Nullspace)
            return currentEye;

        if (_playerMan.LocalEntity is not { } ent ||
            !_entMan.TryGetComponent(ent, out EyeComponent? eye))
        {
            return null;
        }

        if (eye.Eye.Position.MapId != MapId.Nullspace)
            return eye.Eye;

        if (!TryGetEyePosition((ent, eye), out var position))
            return null;

        CopyEye(eye.Eye, _fallbackEye);
        _fallbackEye.Position = position;
        return _fallbackEye;
    }

    private bool TryGetEyePosition(Entity<EyeComponent> ent, out MapCoordinates position)
    {
        var transform = _transform ??= _entMan.System<SharedTransformSystem>();

        if (_entMan.TryGetComponent(ent.Comp.Target, out TransformComponent? xform))
        {
            position = transform.GetMapCoordinates(xform);
            if (position.MapId != MapId.Nullspace)
                return true;
        }

        if (!_entMan.TryGetComponent(ent.Owner, out xform))
        {
            position = default;
            return false;
        }

        position = transform.GetMapCoordinates(xform);
        return position.MapId != MapId.Nullspace;
    }

    private static void CopyEye(GraphicsEye source, GraphicsEye target)
    {
        target.DrawFov = source.DrawFov;
        target.DrawLight = source.DrawLight;
        target.Offset = source.Offset;
        target.Rotation = source.Rotation;
        target.Zoom = source.Zoom;
    }
}
