using Content.Server.Chat.Systems;
using Content.Shared._CMU14.Xenomorphs.Pathogen.Mycotoxin;
using Content.Shared._RMC14.BlurredVision;
using Content.Shared._RMC14.Slow;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;

namespace Content.Server._CMU14.Xenomorphs.Pathogen.Mycotoxin;

public sealed class ServerMycotoxinSystem : SharedMycotoxinSystem
{
    [Dependency] private readonly StatusEffectQuerySystem _status = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MycotoxinExposureComponent, MycotoxinEmoteEvent>(OnEmote);
    }

    private void OnEmote(Entity<MycotoxinExposureComponent> ent, ref MycotoxinEmoteEvent args)
    {
        _chat.TryEmoteWithChat(ent, args.Emote);
    }

    protected override void OnFirstExposure(EntityUid victim, bool strongEffects)
    {
        var emoteEv = new MycotoxinEmoteEvent { Emote = "Cough" };
        RaiseLocalEvent(victim, emoteEv);

        if (!strongEffects)
            return;

        _slow.TrySlowdown(victim, TimeSpan.FromSeconds(3));
        _status.TryAddStatusEffect<RMCBlindedComponent>(
            victim, "Blinded", TimeSpan.FromSeconds(4), true);
    }
}