using Content.Shared.CCVar;
using Content.Shared.CMU14.Ghost;
using Content.Shared.Ghost.Components;
using Robust.Client.Player;
using Robust.Shared.Configuration;

namespace Content.Client.CMU14.Ghost;

public sealed partial class CMUGhostColorSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;

    private CMUGhostColorWindow? _window;

    public override void Initialize()
    {
        _player.LocalPlayerAttached += OnLocalPlayerAttached;
    }

    public override void Shutdown()
    {
        _player.LocalPlayerAttached -= OnLocalPlayerAttached;
        _window?.Close();
    }

    public Color? SavedColor => Color.TryFromHex(_cfg.GetCVar(CCVars.CMUGhostColor), out var color) ? color : null;

    private void OnLocalPlayerAttached(EntityUid uid)
    {
        if (HasComp<GhostComponent>(uid) && SavedColor is { } color)
            RaiseNetworkEvent(new CMUSetGhostColorEvent(color));
    }

    public void SetColor(Color? color)
    {
        _cfg.SetCVar(CCVars.CMUGhostColor, color is { } c ? c.ToHex() : string.Empty);
        _cfg.SaveToFile();

        if (_player.LocalEntity is { } local && HasComp<GhostComponent>(local))
            RaiseNetworkEvent(new CMUSetGhostColorEvent(color));
    }

    public void OpenWindow()
    {
        _window ??= new CMUGhostColorWindow(this);
        _window.SetColor(SavedColor ?? Color.White);
        _window.OpenCentered();
    }
}
