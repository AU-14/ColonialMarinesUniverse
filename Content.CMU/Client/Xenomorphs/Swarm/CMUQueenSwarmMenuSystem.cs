using System.Linq;
using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared.CMU14.Xenomorphs.Swarm;
using Content.Shared.Actions.Components;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Xenomorphs.Swarm;

public sealed partial class CMUQueenSwarmMenuSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IGameTiming _timing = default!;

    private DefaultWindow? _window;
    private EntityUid? _owner;
    private readonly List<(Entity<ActionComponent> Action, Button Button, string Label)> _buttons = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUQueenSwarmComponent, CMUSwarmMenuActionEvent>(OnOpenMenu);
        SubscribeLocalEvent<CMUQueenSwarmComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        _window?.Close();
        _window = null;
        _buttons.Clear();
        base.Shutdown();
    }

    private void OnShutdown(Entity<CMUQueenSwarmComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner == _players.LocalEntity)
            _window?.Close();
    }

    private void OnOpenMenu(Entity<CMUQueenSwarmComponent> ent, ref CMUSwarmMenuActionEvent args)
    {
        if (args.Handled || ent.Owner != _players.LocalEntity)
            return;

        args.Handled = true;
        if (_window is { IsOpen: true })
        {
            _window.Close();
            return;
        }

        _window?.Close();
        _buttons.Clear();
        _owner = ent.Owner;
        var window = new DefaultWindow { Title = Loc.GetString("cmu-swarm-menu-title") };
        _window = window;
        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        window.Contents.AddChild(buttons);
        foreach (var action in _actions.GetActions(ent.Owner).OrderBy(action => _actions.GetEvent(action) switch
                 {
                     CMUSummonSwarmActionEvent => 0,
                     CMUGuardSwarmActionEvent => 1,
                     CMUFollowSwarmActionEvent => 2,
                     _ => 3,
                 }))
        {
            var label = _actions.GetEvent(action) switch
            {
                CMUSummonSwarmActionEvent => "cmu-swarm-menu-summon",
                CMUGuardSwarmActionEvent => "cmu-swarm-menu-guard",
                CMUFollowSwarmActionEvent => "cmu-swarm-menu-escort",
                CMUSwarmRulesActionEvent => "cmu-swarm-menu-rules",
                _ => null,
            };
            if (label == null)
                continue;

            var button = new Button { Text = Loc.GetString(label), MinWidth = 240 };
            button.OnPressed += _ =>
            {
                if (_players.LocalEntity != ent.Owner || Deleted(action))
                    return;
                _ui.GetUIController<ActionUIController>().TriggerSwarmAction(action);
                window.Close();
            };
            buttons.AddChild(button);
            _buttons.Add((action, button, label));
        }
        window.OpenCentered();
        Update(0);
    }

    public override void Update(float frameTime)
    {
        if (_window is not { IsOpen: true })
            return;
        if (_owner != _players.LocalEntity)
        {
            _window.Close();
            return;
        }

        foreach (var (action, button, label) in _buttons)
        {
            var remaining = action.Comp.Cooldown is { } cooldown ? cooldown.End - _timing.CurTime : TimeSpan.Zero;
            button.Disabled = Deleted(action) || !action.Comp.Enabled || remaining > TimeSpan.Zero;
            button.Text = remaining > TimeSpan.Zero
                ? Loc.GetString("cmu-swarm-menu-cooldown", ("seconds", (int) Math.Ceiling(remaining.TotalSeconds)))
                : Loc.GetString(label);
        }
    }
}
