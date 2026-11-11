using Content.Shared.CMU14.Xenomorphs.Swarm;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Xenomorphs.Swarm;

public sealed class CMUSwarmBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUSwarmWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUSwarmWindow>();
        var actions = EntMan.System<CMUQueenSwarmMenuSystem>();
        _window.Command += SendMessage;
        _window.Target += follow =>
        {
            if (follow)
                actions.Trigger<CMUFollowSwarmActionEvent>(Owner);
            else
                actions.Trigger<CMUPlaceSwarmOrderActionEvent>(Owner);
        };
        _window.Summon.OnPressed += _ => actions.Trigger<CMUSummonSwarmActionEvent>(Owner);
        _window.Allies.OnPressed += _ => actions.Trigger<CMUSwarmRulesActionEvent>(Owner);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CMUSwarmUiState swarm)
            _window?.Update(swarm);
    }
}
