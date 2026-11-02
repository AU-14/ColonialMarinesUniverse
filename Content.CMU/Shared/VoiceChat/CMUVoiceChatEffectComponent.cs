namespace Content.Shared.CMU14.VoiceChat;

public enum CMUVoiceEffect : byte
{
    None,
    Robot,
    Alien,
    Deep,
    High,
    Radio,
    Muffled,
    Abductor,
    Whisper,
}

[RegisterComponent]
public sealed partial class CMUVoiceChatEffectComponent : Component
{
    [DataField]
    public CMUVoiceEffect Effect = CMUVoiceEffect.None;
}
