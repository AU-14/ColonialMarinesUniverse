using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Ghost;

/// <summary>
///     Ported from funky-station PR 906. Gives a ghost an action to hide or show its own sprite.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUSelfGhostToggleComponent : Component
{
    [DataField]
    public EntProtoId Action = "CMUActionToggleSelfGhost";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}

public sealed partial class CMUToggleSelfGhostActionEvent : InstantActionEvent;

public sealed class CMUSelfGhostToggleSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSelfGhostToggleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUSelfGhostToggleComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<CMUSelfGhostToggleComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    private void OnShutdown(Entity<CMUSelfGhostToggleComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }
}
