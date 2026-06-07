using System.Collections.Immutable;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Actions;

public readonly record struct RMCActionOrderData(
    ImmutableArray<EntProtoId> Actions,
    ImmutableArray<EntProtoId> HiddenActions,
    bool HiddenActionsKnown);
