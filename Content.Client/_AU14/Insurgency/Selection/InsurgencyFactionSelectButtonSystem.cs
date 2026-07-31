using Content.Shared._AU14.Insurgency.Selection;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Player;

namespace Content.Client._AU14.Insurgency.Selection;

/// <summary>
///     Shows a small "Choose faction" button in the top-left of the game view for the CLF cell leader
///     while no faction has been chosen, so a leader who closes the selection popup can reopen it.
///     Driven entirely by the presence of <see cref="InsurgencyPendingFactionSelectionComponent"/> on
///     the local player: server adds it on leader spawn and strips it once a faction is applied.
///
///     Pressing the button sends a reopen request the server re-validates. No polling: the button is
///     created and torn down in response to the component and player-attach events.
/// </summary>
public sealed class InsurgencyFactionSelectButtonSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    // ---------------------------------------------------------------------
    // Placement tunables. The button is anchored to the top-left of the active screen. Nudge these to
    // move it clear of the game's letterbox bars if needed.
    // ---------------------------------------------------------------------
    private const float MarginLeft = 10f;
    private const float MarginTop = 10f;

    private Button? _button;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InsurgencyPendingFactionSelectionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<InsurgencyPendingFactionSelectionComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<InsurgencyPendingFactionSelectionComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<InsurgencyPendingFactionSelectionComponent, LocalPlayerDetachedEvent>(OnDetached);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        HideButton();
    }

    private void OnStartup(Entity<InsurgencyPendingFactionSelectionComponent> ent, ref ComponentStartup args)
    {
        if (IsLocal(ent))
            ShowButton();
    }

    private void OnRemove(Entity<InsurgencyPendingFactionSelectionComponent> ent, ref ComponentRemove args)
    {
        if (IsLocal(ent))
            HideButton();
    }

    private void OnAttached(Entity<InsurgencyPendingFactionSelectionComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        ShowButton();
    }

    private void OnDetached(Entity<InsurgencyPendingFactionSelectionComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        HideButton();
    }

    private bool IsLocal(EntityUid uid) => _player.LocalEntity == uid;

    private void ShowButton()
    {
        var screen = _ui.ActiveScreen;
        if (screen == null || _button != null)
            return;

        _button = new Button
        {
            Text = Loc.GetString("insfor-reopen-faction-select-button"),
            HorizontalAlignment = Control.HAlignment.Left,
            VerticalAlignment = Control.VAlignment.Top,
        };
        _button.OnPressed += _ => RaiseNetworkEvent(new InsurgencyReopenFactionSelectEvent());

        screen.AddChild(_button);
        LayoutContainer.SetAnchorPreset(_button, LayoutContainer.LayoutPreset.TopLeft);
        LayoutContainer.SetMarginLeft(_button, MarginLeft);
        LayoutContainer.SetMarginTop(_button, MarginTop);
    }

    private void HideButton()
    {
        if (_button == null)
            return;

        _button.Orphan();
        _button = null;
    }
}
