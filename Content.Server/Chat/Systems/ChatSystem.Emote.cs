using System.Collections.Frozen;
using Content.Shared.CMU14.Chat;
using Content.Shared._RMC14.Voicelines;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private const string ScreamEmoteId = "Scream";

    private static readonly string[] RunechatPainMessages =
    [
        "OW!!",
        "AGH!!",
        "ARGH!!",
        "OUCH!!",
        "ACK!!",
        "OUF!",
    ];

    private static readonly string[] RunechatScreamMessages =
    [
        "FUCK!!!",
        "AGH!!!",
        "ARGH!!!",
        "AAAA!!!",
        "HGH!!!",
        "NGHHH!!!",
        "NNHH!!!",
        "SHIT!!!",
    ];

    private static readonly FrozenSet<string> PainEmoteIds = new[]
    {
        "PainGrimace",
        "TroubleEyeOpen",
        "TroubleStanding",
    }.ToFrozenSet();

    [Dependency] private HumanoidVoicelinesSystem _humanoidVoicelines = default!;

    protected override void GetEmotePresentation(
        EmotePrototype emote,
        out string? speechBubbleMessage,
        out string? speechStyleClass)
    {
        if (emote.ID == ScreamEmoteId)
        {
            speechBubbleMessage = _random.Pick(RunechatScreamMessages);
            speechStyleClass = CMURunechatStyles.Scream;
            return;
        }

        if (PainEmoteIds.Contains(emote.ID))
        {
            speechBubbleMessage = _random.Pick(RunechatPainMessages);
            speechStyleClass = CMURunechatStyles.Pain;
            return;
        }

        speechBubbleMessage = null;
        speechStyleClass = null;
    }

    protected override bool CanInvokeChatEmote(EntityUid source, EmotePrototype emote)
    {
        return _rmcEmote.TryEmote(source);
    }

    protected override Filter GetEmoteSoundFilter(EntityUid source)
    {
        return Filter.Pvs(source)
            .RemoveWhere(session => !_humanoidVoicelines.ShouldPlayEmote(source, session));
    }
}
