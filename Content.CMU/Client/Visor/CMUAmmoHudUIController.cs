using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared.CCVar;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client.CMU14.Visor;

/// <summary>
/// The top bar button that switches the squad optic's ammo HUD on and off.
/// </summary>
public sealed class CMUAmmoHudUIController : UIController
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IResourceCache _resource = default!;

    private MenuButton? AmmoHudButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.AmmoHudButton;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += LoadButton;
        gameplayStateLoad.OnScreenUnload += UnloadButton;

        _config.OnValueChanged(CCVars.CMUAmmoHud, OnHudChanged);
    }

    private void LoadButton()
    {
        if (AmmoHudButton is not { } button)
            return;

        var rsi = _resource.GetResource<RSIResource>(CMUAmmoHudOverlay.Rsi).RSI;
        if (rsi.TryGetState("rifle", out var state))
            button.Icon = state.Frame0;

        button.Pressed = _config.GetCVar(CCVars.CMUAmmoHud);
        button.OnToggled += OnToggled;
    }

    private void UnloadButton()
    {
        if (AmmoHudButton is { } button)
            button.OnToggled -= OnToggled;
    }

    private void OnToggled(BaseButton.ButtonToggledEventArgs args)
    {
        _config.SetCVar(CCVars.CMUAmmoHud, args.Pressed);
        _config.SaveToFile();
    }

    private void OnHudChanged(bool enabled)
    {
        if (AmmoHudButton is { } button)
            button.Pressed = enabled;
    }
}
