using Content.Shared.Movement.Systems;

using Content.Shared.Actions;
using Content.Shared.Actions.Components;
namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Overclock's speed, on both sides (playtest 3). Directed events still reach a component during its
/// own ComponentShutdown, so a refresh made there would re-apply the bonus it is meant to remove; the
/// handler ignores a component that is stopping, and the shutdown refresh then really drops it. The
/// same refresh runs when the overheat removes the component.
/// </summary>
public sealed class EWJoeOverclockSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeOverclockComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<EWJoeOverclockComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EWJoeOverclockComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnRefreshSpeed(Entity<EWJoeOverclockComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage >= ComponentLifeStage.Stopping)
            return;

        args.ModifySpeed(ent.Comp.SpeedMultiplier);
    }

    private void OnStartup(Entity<EWJoeOverclockComponent> ent, ref ComponentStartup args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnShutdown(Entity<EWJoeOverclockComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        // however it ends (toggle, overheat, going down, stand-down), the action stops showing active
        if (ent.Comp.Action is { } action)
            _actions.SetToggled(new Entity<ActionComponent?>(action, null), false);

        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }
}
