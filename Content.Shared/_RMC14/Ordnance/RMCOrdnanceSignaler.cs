using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Ordnance.Signaler;

[Serializable, NetSerializable]
public enum OrdnanceSignalerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class OrdnanceSignalerBoundUIState : BoundUserInterfaceState
{
    public int Frequency;

    public OrdnanceSignalerBoundUIState(int frequency)
    {
        Frequency = frequency;
    }
}

[Serializable, NetSerializable]
public sealed class SelectOrdnanceSignalerFrequencyMessage : BoundUserInterfaceMessage
{
    public uint Frequency;

    public SelectOrdnanceSignalerFrequencyMessage(uint frequency)
    {
        Frequency = frequency;
    }
}
