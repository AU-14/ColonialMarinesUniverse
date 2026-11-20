using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Actions;

[Serializable, NetSerializable]
public sealed class RMCActionOrderChangeEvent(
    EntProtoId orderId, // CMU14: identify the form when updates cross a transition.
    List<EntProtoId> actions,
    List<EntProtoId> hiddenActions,
    bool hiddenActionsKnown) : EntityEventArgs
{
    public readonly EntProtoId OrderId = orderId; // CMU14
    public readonly List<EntProtoId> Actions = actions;
    public readonly List<EntProtoId> HiddenActions = hiddenActions;
    public readonly bool HiddenActionsKnown = hiddenActionsKnown;
}
