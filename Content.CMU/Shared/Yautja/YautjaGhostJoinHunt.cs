using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja;

// cmss13 observer "Join the Hunt" action. the server hands it to ghosts of whitelisted players while a hunt is on
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaGhostJoinHuntComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "CMUActionYautjaJoinHunt";

    [DataField, AutoNetworkedField]
    public EntityUid? Action;
}

public sealed partial class YautjaJoinHuntActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed class YautjaJoinHuntConfirmEvent : EntityEventArgs;
