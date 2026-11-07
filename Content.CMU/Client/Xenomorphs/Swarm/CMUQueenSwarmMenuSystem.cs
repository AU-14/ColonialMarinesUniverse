using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared.Actions;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Xenomorphs.Swarm;

public sealed partial class CMUQueenSwarmMenuSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _players = default!;

    public override void Initialize()
    {
        _overlays.AddOverlay(new CMUSwarmFlagOverlay(EntityManager, _players));
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<CMUSwarmFlagOverlay>();
        base.Shutdown();
    }

    public void Trigger<T>(EntityUid queen) where T : BaseActionEvent
    {
        if (_players.LocalEntity != queen)
            return;
        foreach (var action in _actions.GetActions(queen))
        {
            if (_actions.GetEvent(action) is not T || !action.Comp.Enabled)
                continue;
            _ui.GetUIController<ActionUIController>().TriggerSwarmAction(action);
            break;
        }
    }
}
