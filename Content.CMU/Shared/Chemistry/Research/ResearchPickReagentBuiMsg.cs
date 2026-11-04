using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Chemistry.Research;

[Serializable, NetSerializable]
public sealed class ResearchPickReagentBuiMsg(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}
