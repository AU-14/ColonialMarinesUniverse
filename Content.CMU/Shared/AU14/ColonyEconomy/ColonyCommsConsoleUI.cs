// Content.Shared.AU14.ColonyEconomy.BudgetConsoleUi.cs
using Robust.Shared.Serialization;

namespace Content.Shared.AU14;

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

    public ColonyCommsConsoleBuiState()
    {

    }
}

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

