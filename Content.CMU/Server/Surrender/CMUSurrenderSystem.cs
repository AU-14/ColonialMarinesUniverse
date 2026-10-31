using Robust.Shared.Random;
using Content.Server.Chat.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.CMU14.Surrender;
using Content.Shared.Cuffs.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs;
using Content.Shared.Standing;
using Robust.Shared.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.CMU14.Surrender;

public sealed class CMUSurrenderSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public static readonly ProtoId<EmotePrototype> SurrenderEmote = "CMUSurrender";
    private static readonly SoundSpecifier RaiseSound = new SoundCollectionSpecifier("Salutes");

    public override void Initialize()
    {
        SubscribeLocalEvent<HandsComponent, BeforeEmoteEvent>(OnBeforeEmote);
        SubscribeLocalEvent<HandsComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<CMUSurrenderComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<CMUSurrenderComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<CMUSurrenderComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnBeforeEmote(Entity<HandsComponent> ent, ref BeforeEmoteEvent args)
    {
        if (args.Emote.ID != SurrenderEmote || args.Cancelled || HasComp<CMUSurrenderComponent>(ent))
            return;

        if (CanSurrender(ent))
            return;

        args.Cancel();
    }

    private void OnEmote(Entity<HandsComponent> ent, ref EmoteEvent args)
    {
        if (args.Emote.ID != SurrenderEmote || args.Handled)
            return;

        args.Handled = true;
        if (HasComp<CMUSurrenderComponent>(ent))
        {
            LowerHands(ent);
            Announce(ent, "cmu-surrender-lower");
            return;
        }

        if (!CanSurrender(ent))
            return;

        foreach (var hand in ent.Comp.SortedHands)
        {
            if (_hands.TryGetHeldItem(ent.Owner, hand, out _) && !_hands.CanDropHeld(ent, hand))
                return;
        }
        _hands.DropAll((ent.Owner, ent.Comp));
        if (_hands.CountFreeHands((ent.Owner, ent.Comp)) != ent.Comp.Count)
            return;

        EnsureComp<CMUSurrenderComponent>(ent);
        _audio.PlayPvs(RaiseSound, ent.Owner);
        if (_actionBlocker.CanSpeak(ent))
        {
            var index = _random.Next(1, 5);
            _chat.TrySendInGameICMessage(ent, Loc.GetString($"cmu-surrender-line-{index}"),
                InGameICChatType.Speak, ChatTransmitRange.Normal, checkRadioPrefix: false);
        }
        else
        {
            Announce(ent, "cmu-surrender-raise");
        }
    }

    public bool CanSurrender(EntityUid uid)
    {
        return HasComp<HumanoidProfileComponent>(uid)
            && HasComp<HandsComponent>(uid)
            && !_mobState.IsIncapacitated(uid)
            && !(TryComp<CuffableComponent>(uid, out var cuffs) && cuffs.CuffedHandCount > 0)
            && _actionBlocker.CanInteract(uid, null);
    }

    public void LowerHands(EntityUid uid)
    {
        RemComp<CMUSurrenderComponent>(uid);
    }

    private void OnContainerInserted(Entity<CMUSurrenderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        // Wait for actual insertion, so an interrupted cuff attempt cannot end surrender.
        if (TryComp<CuffableComponent>(ent, out var cuffs) && args.Container == cuffs.Container)
            LowerHands(ent);
    }

    private void Announce(EntityUid uid, string message)
    {
        _chat.TrySendInGameICMessage(uid,
            Loc.GetString(message, ("entity", uid)),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            ignoreActionBlocker: true);
    }

    private void OnDowned(Entity<CMUSurrenderComponent> ent, ref DownedEvent args)
    {
        // Shift+R deliberately lies down and identifies the player as the source.
        if (args.DownedBy == ent.Owner)
            return;

        // Beds also lower the standing state as part of buckling.
        if (args.DownedBy == null && TryComp<BuckleComponent>(ent, out var buckle) && buckle.Buckled)
            return;
        LowerHands(ent);
    }

    private void OnMobStateChanged(Entity<CMUSurrenderComponent> ent, ref MobStateChangedEvent args)
    {
        if (_mobState.IsIncapacitated(ent))
            LowerHands(ent);
    }

}


// Scott waz here 10/31/26
