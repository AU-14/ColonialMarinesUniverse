using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Serialization;

namespace Content.Shared.Kitchen;

[Serializable, NetSerializable]
public sealed class ReagentGrinderLinkMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ReagentGrinderBottleMessage(ReagentQuantity reagent) : BoundUserInterfaceMessage
{
    public ReagentQuantity Reagent = reagent;
}

[Serializable, NetSerializable]
public sealed class ReagentGrinderDisposeMessage(ReagentQuantity reagent) : BoundUserInterfaceMessage
{
    public ReagentQuantity Reagent = reagent;
}
