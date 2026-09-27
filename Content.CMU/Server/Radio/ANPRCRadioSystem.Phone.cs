using Content.Server._RMC14.Telephone;
using Content.Shared.CMU14.Radio;
using Content.Shared._RMC14.Hands;
using Content.Shared._RMC14.Telephone;
using Content.Shared.Chat;
using Content.Shared.Popups;
using Content.Server.Power.Components;
using Content.Shared.Radio;
using Content.Shared.Speech;
using Robust.Server.Audio;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Radio;

/// <summary>
///     The AN/PRC-117G as a phone. The RMC exchange runs the calls (dialing, ringing, voicemail, fixed phones);
///     this partial makes the set's single handset carry them and turns a call into a radio link:
///     <list type="bullet">
///         <item>the set answers to its callsign in the phone book and on the line, never the operator's name;</item>
///         <item>its side is whatever fill card is loaded, so a captured set with your card still in it can call your RTOs;</item>
///         <item>both ends must be up, powered and unjammed, and in direct range or both under relay coverage;</item>
///         <item>while a call is up the handset belongs to the call - the set will not key the net until it hangs up;</item>
///         <item>every sentence on the line costs charge and can be direction-found like net traffic.</item>
///     </list>
/// </summary>
public sealed partial class ANPRCRadioSystem
{
    [Dependency] private RMCTelephoneSystem _telephone = default!;
    [Dependency] private RMCHandsSystem _rmcHands = default!;
    [Dependency] private AudioSystem _audio = default!;

    // what an ANPRC with no fill card reports as its side. matches nobody, so an empty set is neither in
    // anyone's phone book nor able to dial out
    private const string NoFillFaction = "au14-anprc-no-fill";

    private const float PhoneTickInterval = 1f;

    private static readonly Color CallChatColor = Color.FromHex("#9956D3");

    private float _phoneAccumulator;

    // calls placed over no link, torn down on the next tick: the exchange rings the far end before it
    // hands the caller the handset, so hanging up inside the ring would leave the handset in their hand
    private readonly Dictionary<EntityUid, EntityUid> _noLinkCalls = new();

    private void InitializePhone()
    {
        SubscribeLocalEvent<ANPRCRadioComponent, AU14GetPhoneNameEvent>(OnPhoneName);
        SubscribeLocalEvent<ANPRCRadioComponent, AU14PhoneReachableEvent>(OnPhoneReachable);
        SubscribeLocalEvent<ANPRCPhoneComponent, ANPRCPhoneActionEvent>(OnPhoneAction);
        SubscribeLocalEvent<ANPRCHandsetComponent, ListenAttemptEvent>(OnHandsetListenAttempt);
        SubscribeLocalEvent<RotaryPhoneComponent, BoundUIOpenedEvent>(OnPhoneBookOpened);
        SubscribeLocalEvent<RMCTelephoneRingEvent>(OnPhoneRing);
    }

    #region Handset binding

    /// <summary>
    ///     The set and the RMC exchange both want to spawn a handset into the same slot at map init, in no
    ///     fixed order. Whichever got there first owns it; this makes sure both sides point at that one.
    /// </summary>
    private void BindPhoneHandset(Entity<ANPRCRadioComponent> pack, EntityUid handset)
    {
        pack.Comp.Handset = handset;
        Comp<ANPRCHandsetComponent>(handset).Radio = pack;
        Dirty(pack);

        if (TryComp(pack, out RotaryPhoneComponent? rotary) && rotary.Phone != handset)
            _telephone.AU14BindPhone((pack, rotary), handset);
    }

    #endregion

    #region Naming and the phone book

    private void OnPhoneName(Entity<ANPRCRadioComponent> ent, ref AU14GetPhoneNameEvent args)
    {
        if (HasComp<ANPRCPhoneComponent>(ent))
            args.Name = GetOnAirName(ent);
    }

    private void OnPhoneReachable(Entity<ANPRCRadioComponent> ent, ref AU14PhoneReachableEvent args)
    {
        if (!HasComp<ANPRCPhoneComponent>(ent))
            return;

        if (!HasLink(ent.Owner, args.Caller))
            args.Cancelled = true;
    }

    // fixed phones build their book when opened; ours goes out right after on the same tick and wins,
    // so the callsigns and the link filter apply to every phone on the exchange
    private void OnPhoneBookOpened(Entity<RotaryPhoneComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is RMCTelephoneUiKey.Key)
            _telephone.AU14SendDirectory(ent);
    }

    #endregion

    #region Placing, answering and ending calls

    private void OnPhoneAction(Entity<ANPRCPhoneComponent> ent, ref ANPRCPhoneActionEvent args)
    {
        if (args.Handled || !TryComp(ent, out ANPRCRadioComponent? radio))
            return;

        args.Handled = true;
        UsePhone((ent, radio), args.Performer);
    }

    private void OnOpenPhone(Entity<ANPRCRadioComponent> ent, ref ANPRCOpenPhoneMsg args)
    {
        UsePhone(ent, args.Actor);
    }

    /// <summary>The one phone control: answer if ringing, hang up if on a call, otherwise open the book.</summary>
    private void UsePhone(Entity<ANPRCRadioComponent> pack, EntityUid user)
    {
        if (!HasComp<ANPRCPhoneComponent>(pack))
            return;

        if (_telephone.AU14IsRinging(pack))
        {
            AnswerCall(pack, user);
            return;
        }

        if (_telephone.AU14InCall(pack))
        {
            EndCall(pack, user, "anprc-call-ended");
            return;
        }

        if (!CanDial(pack, user, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), pack, user, PopupType.SmallCaution);
            return;
        }

        _telephone.AU14OpenDialer(pack, user);
    }

    private bool CanDial(Entity<ANPRCRadioComponent> pack, EntityUid user, out string reason)
    {
        reason = string.Empty;

        if (!HasBaseLink(pack))
        {
            reason = string.IsNullOrEmpty(_crypto.GetFillFaction(pack))
                ? "anprc-call-no-fill"
                : "anprc-call-no-link";
            return false;
        }

        if (pack.Comp.HandsetUser is { } holder && HasComp<ANPRCHandsetUserComponent>(holder))
        {
            if (holder != user)
            {
                reason = "anprc-handset-in-use";
                return false;
            }

            return true;
        }

        // dialing puts the handset in your hand
        if (!_hands.TryGetEmptyHand(user, out _))
        {
            reason = "anprc-handset-hands-full";
            return false;
        }

        return true;
    }

    private void AnswerCall(Entity<ANPRCRadioComponent> pack, EntityUid user)
    {
        // somebody already on the handset takes the call where they stand
        if (pack.Comp.HandsetUser is { } holder && holder != user && HasComp<ANPRCHandsetUserComponent>(holder))
        {
            _popup.PopupEntity(Loc.GetString("anprc-handset-in-use"), pack, user, PopupType.SmallCaution);
            return;
        }

        if (pack.Comp.HandsetUser != user && !_hands.TryGetEmptyHand(user, out _))
        {
            _popup.PopupEntity(Loc.GetString("anprc-handset-hands-full"), pack, user, PopupType.SmallCaution);
            return;
        }

        _telephone.AU14Answer(pack, user);
    }

    private void EndCall(Entity<ANPRCRadioComponent> pack, EntityUid? user, string? messageKey)
    {
        var holder = pack.Comp.HandsetUser;

        if (!_telephone.AU14HangUp(pack, holder ?? user))
            return;

        if (messageKey == null)
            return;

        var notify = holder ?? user;

        if (notify != null && !TerminatingOrDeleted(notify.Value))
            _cmChat.ChatMessageToOne(Loc.GetString(messageKey), notify.Value);
    }

    private void OnPhoneRing(ref RMCTelephoneRingEvent args)
    {
        // a set can only place a call it has a link for. the exchange has already rung the other end by now,
        // so a call over no link is torn straight back down before anyone can answer it
        if (!HasLink(args.Receiving, args.Calling))
        {
            _noLinkCalls[args.Calling] = args.Actor;
            return;
        }

        if (!TryComp(args.Receiving, out ANPRCRadioComponent? radio))
            return;

        var pack = new Entity<ANPRCRadioComponent>(args.Receiving, radio);
        var callerName = _telephone.AU14GetPhoneName(args.Calling);

        // one handset: whoever has it at their ear is on the call the moment it comes in
        if (radio.HandsetUser is { } holder && HasComp<ANPRCHandsetUserComponent>(holder))
        {
            _telephone.AU14Answer(pack, holder);
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-call-incoming-connected", ("caller", callerName)), holder);
            return;
        }

        var wearer = Transform(pack).ParentUid;

        if (radio.IsEquipped && wearer.IsValid() && HasComp<ActorComponent>(wearer))
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-call-incoming", ("caller", callerName)), wearer);
    }

    #endregion

    #region Link

    /// <summary>
    ///     Whether this set could carry a call at all right now, independent of who is at the other end: up,
    ///     powered, unjammed, with a handset and a side on its fill card.
    /// </summary>
    private bool HasBaseLink(Entity<ANPRCRadioComponent> pack)
    {
        var radio = pack.Comp;

        if (!radio.Enabled || (!radio.IsEquipped && !radio.Planted) || radio.RelayOnly || radio.SweepEnabled ||
            radio.Emcon)
        {
            return false;
        }

        if (radio.Handset == null || string.IsNullOrEmpty(_crypto.GetFillFaction(pack)))
            return false;

        if (!_powerCell.HasCharge(pack.Owner, GetTransmitCost(radio)))
            return false;

        return _garble.GetJamIntensity(pack) < RadioJamIntensity.Heavy;
    }

    /// <summary>
    ///     Whether a call can run between two phones. Fixed phones are wired into the exchange and always have a
    ///     line; a set needs its base link and either the other end in direct range or relay coverage on both.
    /// </summary>
    private bool HasLink(EntityUid a, EntityUid b)
    {
        var aPack = TryComp(a, out ANPRCRadioComponent? aRadio) && HasComp<ANPRCPhoneComponent>(a);
        var bPack = TryComp(b, out ANPRCRadioComponent? bRadio) && HasComp<ANPRCPhoneComponent>(b);

        if (!aPack && !bPack)
            return true;

        if (aPack && !HasBaseLink((a, aRadio!)))
            return false;

        if (bPack && !HasBaseLink((b, bRadio!)))
            return false;

        var direct = Math.Max(
            aPack ? GetDirectRange((a, aRadio!)) : 0f,
            bPack ? GetDirectRange((b, bRadio!)) : 0f);

        if (InDirectRange(a, b, direct))
            return true;

        return (!aPack || HasCoverage((a, aRadio!))) && (!bPack || HasCoverage((b, bRadio!)));
    }

    private float GetDirectRange(Entity<ANPRCRadioComponent> pack)
    {
        return CompOrNull<ANPRCPhoneComponent>(pack)?.DirectRange * pack.Comp.TxPower.RangeMultiplier() ?? 0f;
    }

    private bool InDirectRange(EntityUid a, EntityUid b, float range)
    {
        var aXform = Transform(a);
        var bXform = Transform(b);

        if (aXform.MapID != bXform.MapID)
            return false;

        var offset = _transform.GetWorldPosition(aXform) - _transform.GetWorldPosition(bXform);
        return offset.LengthSquared() <= range * range;
    }

    /// <summary>
    ///     Relay coverage on the net that carries this set's side to the exchange. Mirrors
    ///     <see cref="ANPRCRangeSystem.GetRangeTier"/> but skips the set's own relay, or every relaying set
    ///     would count as covering itself.
    /// </summary>
    private bool HasCoverage(Entity<ANPRCRadioComponent> pack)
    {
        if (!TryComp(pack, out ANPRCPhoneComponent? phone) ||
            !phone.CoverageChannels.TryGetValue(_crypto.GetFillFaction(pack), out var channel))
        {
            return false;
        }

        var packXform = Transform(pack);
        var packPos = _transform.GetWorldPosition(packXform);
        var query = EntityQueryEnumerator<ANPRCRelayAnchorComponent, TransformComponent>();

        while (query.MoveNext(out var anchorUid, out var anchor, out var anchorXform))
        {
            if (anchorUid == pack.Owner || !anchor.Channels.Contains(channel))
                continue;

            if (!_range.InVerticalReach(anchorXform.MapID, packXform.MapID, anchor.LevelReach))
                continue;

            if (TryComp(anchorUid, out ApcPowerReceiverComponent? power) && !power.Powered)
                continue;

            var (_, partialRange) = _range.GetAnchorRanges(anchorUid);
            var distance = (_transform.GetWorldPosition(anchorXform) - packPos).Length();

            if (distance <= partialRange)
                return true;
        }

        return false;
    }

    #endregion

    #region Speech on the line

    // the exchange would put the holder's real name on the line. the set speaks for itself, see DeliverCallSpeech
    private void OnHandsetListenAttempt(Entity<ANPRCHandsetComponent> ent, ref ListenAttemptEvent args)
    {
        args.Cancel();
    }

    /// <summary>
    ///     Carries one sentence from the handset holder to the far end, under the set's callsign. Returns false
    ///     when the set is not on a call, so the handset falls through to the net.
    /// </summary>
    private bool TryDeliverCallSpeech(EntityUid speaker, Entity<ANPRCRadioComponent> pack, string message)
    {
        if (!_telephone.AU14InCall(pack))
            return false;

        // still ringing out, or the far end has not picked up - nobody is there to hear it
        if (!_telephone.AU14TryGetOtherPhone(pack, out var otherPhone) ||
            !_rmcHands.TryGetHolder(otherPhone, out var listener) ||
            !TryComp(listener, out ActorComponent? actor))
        {
            return true;
        }

        var radio = pack.Comp;
        var cost = GetTransmitCost(radio);

        if (!_powerCell.TryUseCharge(pack.Owner, cost))
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-battery-insufficient"), speaker);
            return true;
        }

        radio.LastTransmit = _timing.CurTime;

        var jam = _garble.GetJamIntensity(pack);

        if (_telephone.AU14TryGetOtherRotary(pack, out var otherRotary) &&
            HasComp<ANPRCRadioComponent>(otherRotary))
        {
            var farJam = _garble.GetJamIntensity(otherRotary);

            if (farJam > jam)
                jam = farJam;
        }

        if (jam != RadioJamIntensity.None)
            message = _garble.GarbleMessage(message, jam);

        var name = GetHandsetOnAirName(speaker, pack);
        var line = Loc.GetString("anprc-call-says",
            ("name", FormattedMessage.EscapeText(name)),
            ("message", FormattedMessage.EscapeText(message)));

        string? sound = null;

        if (TryComp(radio.Handset, out RMCTelephoneComponent? handset))
            sound = _audio.GetAudioPath(_audio.ResolveSound(handset.SpeakSound));

        _chatManager.ChatMessageToOne(ChatChannel.Local, line, line, otherPhone, false,
            actor.PlayerSession.Channel, CallChatColor, true, sound, -12, hidePopup: true);

        // a call is an emission like any other. find the side's net to hang the DF chance on
        if (TryComp(pack, out ANPRCPhoneComponent? phone) &&
            phone.CoverageChannels.TryGetValue(_crypto.GetFillFaction(pack), out var channelId) &&
            _prototype.TryIndex(channelId, out RadioChannelPrototype? channel))
        {
            TryDirectionFind(pack, radio, channel, false);
        }

        return true;
    }

    #endregion

    /// <summary>
    ///     Once a second: keep each set's side and phone-book presence in step with its fill card and link, and
    ///     end calls that lost their link, their far end, or their handset.
    /// </summary>
    private void UpdatePhones(float frameTime)
    {
        if (_noLinkCalls.Count > 0)
        {
            foreach (var (caller, actor) in _noLinkCalls)
            {
                if (TerminatingOrDeleted(caller) || !_telephone.AU14InCall(caller))
                    continue;

                if (TryComp(caller, out ANPRCRadioComponent? callerRadio))
                {
                    EndCall((caller, callerRadio), actor, "anprc-call-no-link-target");
                    continue;
                }

                // a fixed phone rang a set with no link. its handset is in the caller's hand by now
                _telephone.AU14HangUp(caller, actor);

                if (!TerminatingOrDeleted(actor))
                    _cmChat.ChatMessageToOne(Loc.GetString("anprc-call-no-link-target"), actor);
            }

            _noLinkCalls.Clear();
        }

        _phoneAccumulator += frameTime;

        if (_phoneAccumulator < PhoneTickInterval)
            return;

        _phoneAccumulator = 0f;

        var query = EntityQueryEnumerator<ANPRCPhoneComponent, ANPRCRadioComponent, RotaryPhoneComponent>();

        while (query.MoveNext(out var uid, out _, out var radio, out var rotary))
        {
            var pack = new Entity<ANPRCRadioComponent>(uid, radio);

            var fill = _crypto.GetFillFaction(uid);
            var faction = string.IsNullOrEmpty(fill) ? NoFillFaction : fill;

            if (rotary.Faction != faction)
            {
                rotary.Faction = faction;
                Dirty(uid, rotary);
            }

            // a set with no link reads as busy to a caller working from a stale phone book
            if (HasBaseLink(pack))
                RemComp<RotaryPhoneDndComponent>(uid);
            else if (!_telephone.AU14InCall(uid))
                EnsureComp<RotaryPhoneDndComponent>(uid);

            if (_ui.IsUiOpen(uid, RMCTelephoneUiKey.Key))
                _telephone.AU14SendDirectory(uid);

            if (!_telephone.AU14InCall(uid))
                continue;

            if (!_telephone.AU14TryGetOtherRotary(uid, out var other))
            {
                EndCall(pack, null, "anprc-call-far-end-hung-up");
                continue;
            }

            if (!HasLink(uid, other))
            {
                EndCall(pack, null, "anprc-call-link-lost");
                continue;
            }

            // the handset went back on the pack without anyone hanging up (thrown, dropped, snapped home by
            // the cord). a dialing set with its handset home has hung up in all but name
            if (HasComp<RotaryPhoneDialingComponent>(uid) &&
                radio.Handset is { } handset &&
                _container.TryGetContainingContainer((handset, null, null), out var container) &&
                container.ID == ANPRCRadioComponent.HandsetContainerId)
            {
                EndCall(pack, null, null);
            }
        }

        // fixed phones with their book open keep it current as sets come and go off the air
        var fixedQuery = EntityQueryEnumerator<RotaryPhoneComponent>();

        while (fixedQuery.MoveNext(out var uid, out _))
        {
            if (!HasComp<ANPRCRadioComponent>(uid) && _ui.IsUiOpen(uid, RMCTelephoneUiKey.Key))
                _telephone.AU14SendDirectory(uid);
        }
    }
}
