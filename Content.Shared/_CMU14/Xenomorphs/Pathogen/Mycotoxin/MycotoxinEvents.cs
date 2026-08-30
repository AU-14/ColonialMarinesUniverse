using Robust.Shared.Prototypes;
using Content.Shared.Chat.Prototypes;

namespace Content.Shared.CMU14.Xenomorphs.Pathogen.Mycotoxin;

public sealed class MycotoxinEmoteEvent : EntityEventArgs
{
    public ProtoId<EmotePrototype> Emote;
}