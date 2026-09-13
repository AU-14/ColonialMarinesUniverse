using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Chemistry.Stimmaster;

[Serializable, NetSerializable]
public sealed class CMUStimmasterCreateMsg(EntProtoId prototype, int amount) : BoundUserInterfaceMessage
{
    public readonly EntProtoId Prototype = prototype;
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class CMUStimmasterSelectInjectorMsg(NetEntity injector, bool fill) : BoundUserInterfaceMessage
{
    public readonly NetEntity Injector = injector;
    public readonly bool Fill = fill;
}

[Serializable, NetSerializable]
public sealed class CMUStimmasterSelectAllMsg(bool selectAll) : BoundUserInterfaceMessage
{
    public readonly bool SelectAll = selectAll;
}

[Serializable, NetSerializable]
public sealed class CMUStimmasterFillMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUStimmasterLabelMsg(string label) : BoundUserInterfaceMessage
{
    public readonly string Label = label;
}

[Serializable, NetSerializable]
public sealed class CMUStimmasterTransferMsg(NetEntity injector) : BoundUserInterfaceMessage
{
    public readonly NetEntity Injector = injector;
}

[Serializable, NetSerializable]
public sealed class CMUStimmasterEjectMsg(NetEntity injector) : BoundUserInterfaceMessage
{
    public readonly NetEntity Injector = injector;
}
