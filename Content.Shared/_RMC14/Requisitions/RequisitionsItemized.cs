using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Requisitions;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class RequisitionsItemEntry
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public List<string> Categories = new();

    [DataField]
    public int Cost;

    [DataField]
    public int Weight;

    /// <summary>
    /// Number of units contained by one purchased entity, such as sheets in a material stack.
    /// </summary>
    [DataField]
    public int Units = 1;

    [DataField]
    public bool Packable = true;
}

[DataDefinition]
public sealed partial class RequisitionsItemOverride
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public int? Cost;

    [DataField]
    public int? Weight;
}

[Serializable, NetSerializable]
public sealed class RequisitionsItemStockInfo
{
    public EntProtoId Prototype;
    public int Current;
    public int Max;
    public int SecondsUntilNextReplenish;

    public RequisitionsItemStockInfo(EntProtoId prototype, int current, int max, int secondsUntilNextReplenish)
    {
        Prototype = prototype;
        Current = current;
        Max = max;
        SecondsUntilNextReplenish = secondsUntilNextReplenish;
    }
}

[Serializable, NetSerializable]
public sealed class RequisitionsCheckoutLine
{
    public EntProtoId Prototype;
    public int Amount;

    public RequisitionsCheckoutLine(EntProtoId prototype, int amount)
    {
        Prototype = prototype;
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class RequisitionsCheckoutMsg : BoundUserInterfaceMessage
{
    public int RequestId;
    public List<RequisitionsCheckoutLine> Lines;

    public RequisitionsCheckoutMsg(int requestId, List<RequisitionsCheckoutLine> lines)
    {
        RequestId = requestId;
        Lines = lines;
    }
}

[Serializable, NetSerializable]
public enum RequisitionsCheckoutResult
{
    Success,
    InvalidOrder,
    InsufficientFunds,
    InsufficientStock,
    NoPlatform,
    PlatformFull,
}

[Serializable, NetSerializable]
public sealed class RequisitionsCheckoutResultMsg : BoundUserInterfaceMessage
{
    public int RequestId;
    public RequisitionsCheckoutResult Result;

    public RequisitionsCheckoutResultMsg(int requestId, RequisitionsCheckoutResult result)
    {
        RequestId = requestId;
        Result = result;
    }
}
