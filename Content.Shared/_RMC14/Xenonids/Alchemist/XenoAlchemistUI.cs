using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Alchemist;

[Serializable, NetSerializable]
public enum XenoAlchemistUI
{
    Key
}

[Serializable, NetSerializable]
public sealed class XenoAlchemistChooseBuiMsg(AlchemistChemical chemical) : BoundUserInterfaceMessage
{
    public readonly AlchemistChemical Chemical = chemical;
}
