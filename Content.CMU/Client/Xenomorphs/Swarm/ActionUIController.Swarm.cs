using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Xenomorphs.Swarm;

namespace Content.Client.UserInterface.Systems.Actions;

public sealed partial class ActionUIController
{
    private bool IsSwarmMenuCommand(Entity<ActionComponent> action)
    {
        return _actionsSystem?.GetEvent(action) is CMUSummonSwarmActionEvent or CMUGuardSwarmActionEvent or CMUFollowSwarmActionEvent or CMUSwarmRulesActionEvent or CMUPlaceSwarmOrderActionEvent;
    }

    /// <summary>
    /// Use the normal action targeting controls from the swarm command menu.
    /// </summary>
    public void TriggerSwarmAction(Entity<ActionComponent> action)
    {
        if (EntityManager.TryGetComponent<TargetActionComponent>(action, out var target))
            StartTargeting((action, action.Comp, target));
        else
        {
            StopTargeting();
            _actionsSystem?.TriggerAction(action);
        }
    }
}
