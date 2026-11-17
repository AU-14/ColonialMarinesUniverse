using System.Linq;
using Content.Client._RMC14.Actions;
using Content.Client.Actions;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// The Early Warning Joe's Signal wheels (action-bar report §4-§5): each wheel action opens a radial menu at
/// the mouse listing the earned abilities of its group. Choosing one fires it exactly as its hotbar button
/// would (targeting for targeted abilities). Client-only; the abilities themselves are unchanged.
/// </summary>
public sealed class EWJoeSignalWheelSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private RMCActionsSystem _rmcActions = default!;

    private static readonly Color CoolingDown = Color.FromHex("#2a2a2aCC");

    private static readonly Color Active = Color.FromHex("#5a3a10CC");

    private SimpleRadialMenu? _menu;
    private EWJoeAbilityGroup? _openGroup;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, EWJoeOpenWheelActionEvent>(OnOpenWheel);
        // broadcast: after a detach the old body's owner-only components stay but stop updating
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<EWJoeComponent, ComponentShutdown>(OnJoeShutdown);

        _actions.LinkActions += OnLinkActions;
    }

    /// <summary>A wheel never outlives the antag (stand-down while the player is still in the body).</summary>
    private void OnJoeShutdown(Entity<EWJoeComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner == _player.LocalEntity)
            CloseMenu();
    }

    /// <summary>A wheel never outlives the body it was opened from (ghosting, body swap).</summary>
    private void OnDetached(LocalPlayerDetachedEvent args)
    {
        CloseMenu();
    }

    /// <summary>
    /// A HUD reload rebuilds the bar from its defaults, which leave wheel abilities out; ask RMC's order to
    /// re-sort so a pinned one returns.
    /// </summary>
    private void OnLinkActions(ActionsComponent component)
    {
        // only the Joe's bar: everyone else's HUD reload behaves exactly as before
        if (_player.LocalEntity is { } local && HasComp<EWJoeComponent>(local))
            _rmcActions.CMURequestResort();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _actions.LinkActions -= OnLinkActions;
        CloseMenu();
    }

    private void OnOpenWheel(Entity<EWJoeComponent> ent, ref EWJoeOpenWheelActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Owner != _player.LocalEntity)
            return;

        // the same wheel key again closes it
        var reopened = _menu != null && _openGroup == args.Group;
        CloseMenu();
        if (reopened)
            return;

        var options = new List<RadialMenuOptionBase>();
        foreach (var action in GetWheelActions(ent, args.Group))
        {
            options.Add(BuildOption(action));
        }

        if (options.Count == 0)
            return;

        _menu = new SimpleRadialMenu();
        _menu.SetButtons(options);
        _menu.OnClose += () =>
        {
            _menu = null;
            _openGroup = null;
        };
        _openGroup = args.Group;
        _menu.OpenOverMouseScreenPosition();
    }

    /// <summary>The Joe's earned abilities in <paramref name="group"/>, in the order they were granted.</summary>
    public List<Entity<ActionComponent>> GetWheelActions(EntityUid joe, EWJoeAbilityGroup group)
    {
        return _actions.GetActions(joe)
            .Where(action => TryComp(action, out EWJoeAbilityGroupComponent? comp) && comp.Group == group)
            .OrderBy(action => GetNetEntity(action.Owner).Id)
            .ToList();
    }

    private RadialMenuOptionBase BuildOption(Entity<ActionComponent> action)
    {
        var tooltip = $"{Name(action)}\n{Description(action)}";
        Color? background = null;

        if (action.Comp.Toggled)
        {
            tooltip += $"\n{Loc.GetString("cmu-ew-joe-wheel-active")}";
            background = Active;
        }

        if (!action.Comp.Enabled)
        {
            tooltip += $"\n{Loc.GetString("cmu-ew-joe-wheel-disabled")}";
            background = CoolingDown;
        }
        else if (action.Comp.Cooldown is { } cooldown && cooldown.End > _timing.CurTime)
        {
            var seconds = (int) Math.Ceiling((cooldown.End - _timing.CurTime).TotalSeconds);
            tooltip += $"\n{Loc.GetString("cmu-ew-joe-wheel-cooldown", ("seconds", seconds))}";
            background = CoolingDown;
        }

        return new RadialMenuActionOption<EntityUid>(Choose, action.Owner)
        {
            IconSpecifier = RadialMenuIconSpecifier.With(action.Owner),
            ToolTip = tooltip,
            BackgroundColor = background,
        };
    }

    private void Choose(EntityUid action)
    {
        CloseMenu();
        if (_actions.GetAction(action) is not { } resolved)
            return;

        // only an ability the local body still holds, and only one it can use now (the tooltip said so)
        if (resolved.Comp.AttachedEntity != _player.LocalEntity || !resolved.Comp.Enabled)
            return;

        if (resolved.Comp.Cooldown is { } cooldown && cooldown.End > _timing.CurTime)
            return;

        _ui.GetUIController<ActionUIController>().TriggerSignalWheelAction(resolved);
    }

    private void CloseMenu()
    {
        if (_menu == null)
            return;

        var menu = _menu;
        _menu = null;
        _openGroup = null;
        menu.Close();
    }
}
