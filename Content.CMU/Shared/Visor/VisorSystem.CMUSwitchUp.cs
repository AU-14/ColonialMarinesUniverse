namespace Content.Shared._RMC14.Visor;

// CMU14: lets CMU systems switch a helmet's current visor up. Lives in a partial because CurrentVisor is access-restricted.
public sealed partial class VisorSystem
{
    /// <summary>
    /// Switches the helmet's current visor up, as if the wearer had cycled it off.
    /// </summary>
    public void CMUSwitchVisorUp(Entity<CycleableVisorComponent> cycleable, EntityUid user)
    {
        if (cycleable.Comp.CurrentVisor is not { } current ||
            current < 0 ||
            current >= cycleable.Comp.Containers.Count ||
            !_container.TryGetContainer(cycleable, cycleable.Comp.Containers[current], out var container))
        {
            return;
        }

        foreach (var contained in container.ContainedEntities)
        {
            DeactivateVisor(cycleable, contained, user);
            break;
        }

        if (cycleable.Comp.Action is { } action && Exists(action))
            _actions.SetIcon(action, cycleable.Comp.OffIcon);

        _item.VisualsChanged(cycleable);
    }
}
