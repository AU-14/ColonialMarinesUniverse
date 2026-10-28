// Content.Shared.CMU14.ColonyEconomy.BudgetConsoleUi.cs
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14;

[Serializable, NetSerializable]
public enum ColonyCommsConsoleUI
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ColonyCommsConsoleBuiMsg : BoundUserInterfaceMessage
{

    public ColonyCommsConsoleBuiMsg(float amount)
    {
    }
}

[Serializable, NetSerializable]
public sealed class ColonyCommsConsoleBuiState : BoundUserInterfaceState
{
    // None outside greenshift, the window hides the transport row then
    public ColonyRotationTransportStatus Transport;

    // when the current phase flips over (dispatch, landing, departure), in server CurTime
    public TimeSpan? TransportAt;
    public bool CanCall;
    public bool CanRecall;

    public ColonyCommsConsoleBuiState()
    {

    }

    public ColonyCommsConsoleBuiState(ColonyRotationTransportStatus transport, TimeSpan? transportAt, bool canCall, bool canRecall)
    {
        Transport = transport;
        TransportAt = transportAt;
        CanCall = canCall;
        CanRecall = canRecall;
    }
}

[Serializable, NetSerializable]
public enum ColonyRotationTransportStatus : byte
{
    None,
    Scheduled,
    Inbound,
    Approaching,
    Docked,
    Departing,
}

[Serializable, NetSerializable]
public sealed class ColonyCommsConsoleTransportCallBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ColonyCommsConsoleTransportRecallBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ColonyCommsConsoleMessage : EntityEventArgs
{
    public string Message { get; }

    public ColonyCommsConsoleMessage(string message)
    {
        Message = message;
    }
}

[Serializable, NetSerializable]
public sealed class ColonyCommsConsoleSiren : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class ColonyCommsConsoleSendMessageBuiMsg : BoundUserInterfaceMessage
{
    public string Message { get; }
    public ColonyCommsConsoleSendMessageBuiMsg(string message)
    {
        Message = message;
    }
}

[Serializable, NetSerializable]
public sealed class ColonyCommsConsoleSirenBuiMsg : BoundUserInterfaceMessage
{
}

