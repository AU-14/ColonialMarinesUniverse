// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether voice chat is enabled. Players on other machines also need an HTTPS reverse proxy
    /// in front of the server before their browsers will allow the microphone.
    /// </summary>
    public static readonly CVarDef<bool> CMUVoiceChatEnabled =
        CVarDef.Create("cmu.voice.enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Whether players can speak over headset channels with voice chat.
    /// </summary>
    public static readonly CVarDef<bool> CMUVoiceChatRadioEnabled =
        CVarDef.Create("cmu.voice.radio_enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Whether voice is allowed on the marine Common channel. Turn it off to keep voice on squad
    /// and department channels instead of the one everyone hears.
    /// </summary>
    public static readonly CVarDef<bool> CMUVoiceChatRadioCommon =
        CVarDef.Create("cmu.voice.radio_common", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Distance in tiles at which voice chat fades out completely.
    /// </summary>
    public static readonly CVarDef<float> CMUVoiceChatRange =
        CVarDef.Create("cmu.voice.range", 10f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// How far from the speaker, in tiles, an opening must be for their voice to reach the adjacent z-level.
    /// </summary>
    public static readonly CVarDef<float> CMUVoiceChatZOpeningRadius =
        CVarDef.Create("cmu.voice.z_opening_radius", 3f, CVar.SERVERONLY);

    /// <summary>
    /// Address and port the voice chat WebSocket listens on.
    /// Expose it through the reverse proxy in front of the status port at /voice/ws.
    /// </summary>
    public static readonly CVarDef<string> CMUVoiceChatWebSocketBind =
        CVarDef.Create("cmu.voice.ws_bind", "127.0.0.1:1213", CVar.SERVERONLY);

    /// <summary>
    /// Public URL of the voice chat page, e.g. https://example.com/voice/. Derived from hub.server_url when empty.
    /// </summary>
    public static readonly CVarDef<string> CMUVoiceChatPublicUrl =
        CVarDef.Create("cmu.voice.public_url", "", CVar.SERVERONLY);

    /// <summary>
    /// WebSocket URL the voice chat page connects to. Auto-detected by the page when empty.
    /// </summary>
    public static readonly CVarDef<string> CMUVoiceChatWebSocketUrl =
        CVarDef.Create("cmu.voice.ws_url", "", CVar.SERVERONLY);

    /// <summary>
    /// Comma-separated proxy IPs whose X-Real-IP and X-Forwarded-For headers are trusted.
    /// Loopback and private addresses are always trusted.
    /// </summary>
    public static readonly CVarDef<string> CMUVoiceChatTrustedProxies =
        CVarDef.Create("cmu.voice.trusted_proxies", "", CVar.SERVERONLY);

    /// <summary>
    /// Maximum simultaneous voice chat connections from one IP address.
    /// </summary>
    public static readonly CVarDef<int> CMUVoiceChatMaxConnectionsPerIp =
        CVarDef.Create("cmu.voice.max_connections_per_ip", 6, CVar.SERVERONLY);

    /// <summary>
    /// Whether shouting carries further and whispering stays close, based on each speaker's own normal loudness.
    /// </summary>
    public static readonly CVarDef<bool> CMUVoiceChatDynamicRange =
        CVarDef.Create("cmu.voice.dynamic_range", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Distance in tiles that shouted voice carries.
    /// </summary>
    public static readonly CVarDef<float> CMUVoiceChatShoutRange =
        CVarDef.Create("cmu.voice.shout_range", 15f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Distance in tiles that whispered voice carries.
    /// </summary>
    public static readonly CVarDef<float> CMUVoiceChatWhisperRange =
        CVarDef.Create("cmu.voice.whisper_range", 3f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// How many dB above a speaker's normal loudness counts as shouting.
    /// </summary>
    public static readonly CVarDef<float> CMUVoiceChatShoutThreshold =
        CVarDef.Create("cmu.voice.shout_threshold", 14f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// How many dB below a speaker's normal loudness counts as whispering.
    /// </summary>
    public static readonly CVarDef<float> CMUVoiceChatWhisperThreshold =
        CVarDef.Create("cmu.voice.whisper_threshold", 5f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// How long one live voice announcement from a console may run.
    /// </summary>
    public static readonly CVarDef<float> CMUVoiceChatAnnounceDuration =
        CVarDef.Create("cmu.voice.announce_duration", 30f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Seconds a console must wait after a voice announcement ends before another can start.
    /// </summary>
    public static readonly CVarDef<float> CMUVoiceChatAnnounceCooldown =
        CVarDef.Create("cmu.voice.announce_cooldown", 30f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Voice chat playback volume, 0 to 2.
    /// </summary>
    public static readonly CVarDef<float> CMUVoiceChatVolume =
        CVarDef.Create("cmu.voice.volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Comma-separated radio channel IDs whose voice you don't want to hear, set from the headset window.
    /// </summary>
    public static readonly CVarDef<string> CMUVoiceChatRadioMuted =
        CVarDef.Create("cmu.voice.radio_muted", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Show the list of players you can currently hear on voice chat.
    /// </summary>
    public static readonly CVarDef<bool> CMUVoiceChatSpeakerList =
        CVarDef.Create("cmu.voice.speaker_list", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Show the voice chat explainer when joining a server that has voice chat enabled.
    /// </summary>
    public static readonly CVarDef<bool> CMUVoiceChatShowIntro =
        CVarDef.Create("cmu.voice.show_intro", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Play your own voice back to you in game.
    /// </summary>
    public static readonly CVarDef<bool> CMUVoiceChatHearSelf =
        CVarDef.Create("cmu.voice.hear_self", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
