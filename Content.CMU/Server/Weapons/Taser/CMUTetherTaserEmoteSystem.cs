using Content.Server.Chat.Systems;
using Content.Server.CMU14.Atmos;
using Content.Shared.CMU14.Weapons.Taser;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Weapons.Taser;

/// <summary>
/// Makes tased targets scream. The current is what's doing the screaming, so it gets out even when they've collapsed:
/// a knocked-out mob is muted and would only mime the emote, so then the scream's sound is played directly.
/// </summary>
public sealed partial class CMUTetherTaserEmoteSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUTaserTetheredComponent, CMUTaserEmoteEvent>(OnEmote);
    }

    private void OnEmote(Entity<CMUTaserTetheredComponent> ent, ref CMUTaserEmoteEvent args)
    {
        var muted = _statusEffects.HasEffectComp<MutedStatusEffectComponent>(ent);

        // Catching fire makes burn panic emote the same scream on its own. When it's heard, don't double it; when the
        // target is muted it only got the text out, so the sound still needs playing.
        var panicEmoted = TryComp(ent, out CMUBurnPanicComponent? panic) &&
                          args.Emote == panic.Emote &&
                          panic.NextEmoteAt - panic.EmoteCooldown >= _timing.CurTime - TimeSpan.FromSeconds(1);

        if (!panicEmoted)
            _chat.TryEmoteWithChat(ent, args.Emote, ignoreActionBlocker: true, forceEmote: true);

        if (muted && TryComp(ent, out VocalComponent? vocal) && vocal.EmoteSounds is { } sounds)
            _chat.TryPlayEmoteSound(ent, _prototypes.Index(sounds), args.Emote);
    }
}
