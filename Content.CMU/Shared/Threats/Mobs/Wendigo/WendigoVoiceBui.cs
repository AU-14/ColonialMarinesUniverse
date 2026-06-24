using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Wendigo;

[Serializable, NetSerializable]
public enum WendigoVoiceUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class WendigoPlayLineMessage : BoundUserInterfaceMessage
{
    public string EmoteId;

    public WendigoPlayLineMessage(string emoteId)
    {
        EmoteId = emoteId;
    }
}
