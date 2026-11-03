using System.Linq;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;

namespace Content.Client.UserInterface.Systems.Actions;

/// <summary>
/// CMU14: the Early Warning Joe's Signal wheels. Abilities whose prototype carries
/// <see cref="EWJoeAbilityGroupComponent"/> stay off the hotbar on grant and on attach; RMC's re-sort keeps
/// them off unless the saved order places them (RMCActionsSystem.SignalWheel.cs), which is how a player's
/// pin survives. The wheel fires them through the same path as the Swarm menu.
/// </summary>
public sealed partial class ActionUIController
{
    private bool IsSignalWheelAction(EntityUid? action)
    {
        return action != null && EntityManager.HasComponent<EWJoeAbilityGroupComponent>(action.Value);
    }

    /// <summary>Starts targeting for a targeted ability, or performs an instant one, as a hotbar press would.</summary>
    public void TriggerSignalWheelAction(Entity<ActionComponent> action)
    {
        TriggerSwarmAction(action);
    }

    /// <summary>Whether <paramref name="action"/> currently has a slot on the visible hotbar.</summary>
    public bool CMUIsOnHotbar(EntityUid action)
    {
        return GetActiveHotbarActions().Contains((EntityUid?) action);
    }
}
