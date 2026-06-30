using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Threats.Mobs.WorkingJoe;

[Serializable, NetSerializable]
public enum WorkingJoeVoiceUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class WorkingJoePlayLineMessage : BoundUserInterfaceMessage
{
    public string EmoteId;

    public WorkingJoePlayLineMessage(string emoteId) => EmoteId = emoteId;
}
