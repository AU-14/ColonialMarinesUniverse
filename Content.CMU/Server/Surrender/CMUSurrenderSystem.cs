using Content.Server.Chat.Managers;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.CMU14.Surrender;
using Content.Shared.Coordinates;
using Content.Shared.Cuffs.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs;
using Content.Shared.Standing;
using Robust.Shared.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Surrender;

public sealed class CMUSurrenderSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public static readonly ProtoId<EmotePrototype> SurrenderEmote = "CMUSurrender";
    private static readonly SoundSpecifier RaiseSound = new SoundCollectionSpecifier("Salutes");
    private static readonly EntProtoId RaiseEffect = "CMUEffectSurrenderHands";
    private readonly Dictionary<EntityUid, EntityUid> _effects = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<HandsComponent, BeforeEmoteEvent>(OnBeforeEmote);
        SubscribeLocalEvent<HandsComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<CMUSurrenderComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<CMUSurrenderComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<CMUSurrenderComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CMUSurrenderComponent, ComponentShutdown>(OnShutdown);
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
        RemoveEffect(ent);
        _effects[ent] = SpawnAttachedTo(RaiseEffect, ent.Owner.ToCoordinates());
        _audio.PlayPvs(RaiseSound, ent.Owner);
        AnnounceSurrender(ent);
    }

    private void AnnounceSurrender(EntityUid uid)
    {
        var message = Loc.GetString("cmu-surrender-chat-raise", ("entity", uid));
        var identity = Identity.Entity(uid, EntityManager);
        var wrapped = Loc.GetString("chat-manager-entity-me-wrap-message",
            ("entityName", FormattedMessage.EscapeText(Name(identity))),
            ("entity", identity),
            ("message", FormattedMessage.EscapeText(message)));
        var filter = Filter.Empty().AddInRange(_transform.GetMapCoordinates(uid), SharedChatSystem.VoiceRange);
        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Emotes, message, wrapped, uid,
            hideChat: false, recordReplay: true, colorOverride: null, hidePopup: true);
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

    private void OnShutdown(Entity<CMUSurrenderComponent> ent, ref ComponentShutdown args)
    {
        RemoveEffect(ent);
    }

    private void RemoveEffect(EntityUid uid)
    {
        if (_effects.Remove(uid, out var effect) && !TerminatingOrDeleted(effect))
            QueueDel(effect);
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
