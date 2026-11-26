using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.Chat;
using Robust.Shared.Prototypes;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    public void SendRelayedSpeech(EntityUid source, string message, string name, ProtoId<LanguagePrototype> language)
        => SendEntitySpeakWithLanguage(source, message, ChatTransmitRange.GhostRangeLimit, name, true, true, language);
}
