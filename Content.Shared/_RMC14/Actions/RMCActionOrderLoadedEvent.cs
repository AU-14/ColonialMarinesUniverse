using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Actions;

[Serializable, NetSerializable]
public sealed class RMCActionOrderLoadedEvent(
    List<EntProtoId> actions,
    List<EntProtoId> hiddenActions,
    bool hiddenActionsKnown) : EntityEventArgs
{
    public readonly List<EntProtoId> Actions = actions;
    public readonly List<EntProtoId> HiddenActions = hiddenActions;
    public readonly bool HiddenActionsKnown = hiddenActionsKnown;
}
