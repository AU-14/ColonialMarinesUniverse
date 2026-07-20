using Content.Server.Chat.Systems;
using Content.Server.Radio.Components;
using Content.Shared._AU14.Callsigns;
using Content.Shared._AU14.Radio;
using Content.Shared._RMC14.Chat;
using Content.Shared.Chat;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Server._AU14.Radio;

public sealed partial class ANPRCRadioSystem
{
    // cord length in tiles
    private const float HandsetRange = 2.5f;

    public override void Update(float frameTime)
    {
        List<(Entity<ANPRCHandsetUserComponent> User, string Reason)>? toRelease = null;

        var query = EntityQueryEnumerator<ANPRCHandsetUserComponent>();

        while (query.MoveNext(out var uid, out var handset))
        {
            string? reason = null;

            if (TerminatingOrDeleted(handset.Radio) ||
                !TryComp(handset.Radio, out ANPRCRadioComponent? radio) ||
                !radio.Enabled ||
                (!radio.IsEquipped && !radio.Planted))
            {
                reason = "anprc-handset-radio-gone";
            }
            else if (!HandsetInReach(uid, handset.Radio))
            {
                reason = "anprc-handset-cord";
            }

            if (reason != null)
            {
                toRelease ??= new List<(Entity<ANPRCHandsetUserComponent>, string)>();
                toRelease.Add(((uid, handset), reason));
                continue;
            }

            // keep holder hearing in sync with the pack (slot switches, scan, crypto)
            SyncHandsetHearing((uid, handset));
        }

        if (toRelease != null)
        {
            foreach (var (user, reason) in toRelease)
            {
                ReleaseHandset(user, reason);
            }
        }

        // dropped/thrown handsets snap back onto the pack next tick unless a hand caught them
        var handsets = EntityQueryEnumerator<ANPRCHandsetComponent>();

        while (handsets.MoveNext(out var uid, out var handset))
        {
            if (handset.Radio is not { } radio ||
                TerminatingOrDeleted(radio) ||
                !TryComp(radio, out ANPRCRadioComponent? radioComp))
            {
                continue;
            }

            if (_container.TryGetContainingContainer((uid, null, null), out var container) &&
                (container.ID == ANPRCRadioComponent.HandsetContainerId ||
                 _hands.IsHolding(container.Owner, uid)))
            {
                continue;
            }

            SnapHandsetHome((uid, handset), (radio, radioComp), null);
        }
    }

    private void OnRadioMapInit(Entity<ANPRCRadioComponent> ent, ref MapInitEvent args)
    {
        _container.EnsureContainer<ContainerSlot>(ent, ANPRCRadioComponent.HandsetContainerId);

        SeedDefaultSlots(ent);

        // whatever antenna state exists at init; the antenna-insert event corrects
        // it if the starting whip lands in the slot after this runs
        UpdatePackVisuals(ent);

        if (!TrySpawnInContainer(ent.Comp.HandsetId, ent, ANPRCRadioComponent.HandsetContainerId, out var handset))
            return;

        ent.Comp.Handset = handset;
        Comp<ANPRCHandsetComponent>(handset.Value).Radio = ent;
        Dirty(ent);
    }

    private void SeedDefaultSlots(Entity<ANPRCRadioComponent> ent)
    {
        // only ever seeds a virgin set, so a mapper-placed or already-tuned station
        // does not get its slots stamped over on init
        if (ent.Comp.DefaultSlots.Count == 0 || ent.Comp.SlotLabels.Count > 0)
            return;

        var slot = 0;

        foreach (var preset in ent.Comp.DefaultSlots)
        {
            if (slot >= ANPRCRadioComponent.MaxSlots)
                break;

            ent.Comp.SlotLabels[slot] = preset.Label;
            ent.Comp.Presets[slot] = preset.Channel;
            slot++;
        }

        if (slot > 0 && ent.Comp.ActiveSlot < 0)
            ent.Comp.ActiveSlot = 0;

        Dirty(ent);
    }

    private void OnHandsetEquippedHand(Entity<ANPRCHandsetComponent> ent, ref GotEquippedHandEvent args)
    {
        if (ent.Comp.Radio is not { } radioUid || !TryComp(radioUid, out ANPRCRadioComponent? radio))
            return;

        // grabbing a second pack's handset hangs up the first, so stripping one out of
        // someone's hand takes over the call
        if (TryComp(args.User, out ANPRCHandsetUserComponent? existing) && existing.Radio != radioUid)
            ReleaseHandset((args.User, existing));

        var user = EnsureComp<ANPRCHandsetUserComponent>(args.User);
        user.Radio = radioUid;
        user.PendingTransmit = false;

        radio.HandsetUser = args.User;

        SyncHandsetHearing((args.User, user));
    }

    // using the handset in hand hangs it back up, so the pack wearer isn't stuck
    // throwing it or walking the cord out to put it away
    private void OnHandsetUseInHand(Entity<ANPRCHandsetComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(args.User, out ANPRCHandsetUserComponent? user) || user.Radio != ent.Comp.Radio)
            return;

        args.Handled = true;

        var radio = ent.Comp.Radio;

        ReleaseHandset((args.User, user));

        if (radio != null)
        {
            _popup.PopupEntity(
                Loc.GetString("anprc-handset-released", ("radio", radio.Value)),
                args.User,
                args.User);
        }
    }

    private void OnHandsetUnequippedHand(Entity<ANPRCHandsetComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (TryComp(args.User, out ANPRCHandsetUserComponent? user) && user.Radio == ent.Comp.Radio)
        {
            RevokeHandsetHearing((args.User, user));
            RemComp<ANPRCHandsetUserComponent>(args.User);
        }

        if (TryComp(ent.Comp.Radio, out ANPRCRadioComponent? radio) && radio.HandsetUser == args.User)
            radio.HandsetUser = null;
    }

    // mirror the pack's receive channels onto the holder, minus whatever their own
    // headset already delivers so nothing gets heard twice
    private void SyncHandsetHearing(Entity<ANPRCHandsetUserComponent> user)
    {
        if (!TryComp(user.Comp.Radio, out ANPRCRadioComponent? radio))
            return;

        HashSet<string>? headsetChannels = null;

        if (TryComp(user.Owner, out WearingHeadsetComponent? headset) &&
            TryComp(headset.Headset, out EncryptionKeyHolderComponent? keys))
        {
            headsetChannels = keys.Channels;
        }

        var wanted = new HashSet<string>();

        foreach (var channel in radio.GrantedChannels)
        {
            if (headsetChannels == null || !headsetChannels.Contains(channel))
                wanted.Add(channel);
        }

        if (wanted.SetEquals(user.Comp.GrantedChannels))
            return;

        var active = EnsureComp<ActiveRadioComponent>(user.Owner);

        if (!HasComp<IntrinsicRadioReceiverComponent>(user.Owner))
        {
            AddComp<IntrinsicRadioReceiverComponent>(user.Owner);
            user.Comp.AddedIntrinsicReceiver = true;
        }

        foreach (var channel in user.Comp.GrantedChannels)
        {
            if (!wanted.Contains(channel))
                active.Channels.Remove(channel);
        }

        foreach (var channel in wanted)
        {
            active.Channels.Add(channel);
        }

        user.Comp.GrantedChannels.Clear();
        user.Comp.GrantedChannels.UnionWith(wanted);
    }

    private void RevokeHandsetHearing(Entity<ANPRCHandsetUserComponent> user)
    {
        if (user.Comp.GrantedChannels.Count > 0 &&
            TryComp(user.Owner, out ActiveRadioComponent? active))
        {
            foreach (var channel in user.Comp.GrantedChannels)
            {
                active.Channels.Remove(channel);
            }

            if (active.Channels.Count == 0)
                RemComp<ActiveRadioComponent>(user.Owner);
        }

        user.Comp.GrantedChannels.Clear();

        if (user.Comp.AddedIntrinsicReceiver)
        {
            user.Comp.AddedIntrinsicReceiver = false;
            RemComp<IntrinsicRadioReceiverComponent>(user.Owner);
        }
    }

    private void SnapHandsetHome(
        Entity<ANPRCHandsetComponent> handset,
        Entity<ANPRCRadioComponent> pack,
        EntityUid? user)
    {
        var container =
            _container.EnsureContainer<ContainerSlot>(pack, ANPRCRadioComponent.HandsetContainerId);

        if (container.ContainedEntity == handset.Owner)
            return;

        if (user != null && _hands.TryDropIntoContainer(user.Value, handset.Owner, container))
            return;

        _container.Insert(handset.Owner, container);
    }

    private bool HandsetInReach(EntityUid user, EntityUid radio)
    {
        var userXform = Transform(user);
        var radioXform = Transform(radio);

        if (userXform.MapID != radioXform.MapID)
            return false;

        var offset = _transform.GetWorldPosition(userXform) - _transform.GetWorldPosition(radioXform);

        return offset.LengthSquared() <= HandsetRange * HandsetRange;
    }

    private void OnWearerGetAltVerbs(Entity<WearingANPRCComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == ent.Owner)
            return;

        if (!TryComp(ent.Comp.Radio, out ANPRCRadioComponent? radio) ||
            !radio.Enabled ||
            !radio.IsEquipped)
        {
            return;
        }

        AddHandsetVerbs((ent.Comp.Radio, radio), args.User, ref args);
    }

    private void AddHandsetVerbs(
        Entity<ANPRCRadioComponent> pack,
        EntityUid user,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (TryComp(user, out ANPRCHandsetUserComponent? held) && held.Radio == pack.Owner)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("anprc-verb-handset-release"),
                Priority = 4,
                Act = () =>
                {
                    if (!TryComp(user, out ANPRCHandsetUserComponent? current) ||
                        current.Radio != pack.Owner)
                    {
                        return;
                    }

                    ReleaseHandset((user, current));
                    _popup.PopupEntity(
                        Loc.GetString("anprc-handset-released", ("radio", pack.Owner)),
                        user,
                        user);
                }
            });

            return;
        }

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("anprc-verb-handset"),
            Priority = 4,
            Act = () => TakeHandset(user, pack)
        });
    }

    private void TakeHandset(EntityUid user, Entity<ANPRCRadioComponent> pack)
    {
        if (pack.Comp.HandsetUser is { } current &&
            current != user &&
            !TerminatingOrDeleted(current) &&
            HasComp<ANPRCHandsetUserComponent>(current))
        {
            _popup.PopupEntity(Loc.GetString("anprc-handset-in-use"), pack.Owner, user, PopupType.SmallCaution);
            return;
        }

        var container =
            _container.EnsureContainer<ContainerSlot>(pack.Owner, ANPRCRadioComponent.HandsetContainerId);

        // handset got lost or admin-deleted, spawn a replacement
        if (pack.Comp.Handset is not { } item || TerminatingOrDeleted(item))
        {
            if (!TrySpawnInContainer(pack.Comp.HandsetId, pack.Owner, ANPRCRadioComponent.HandsetContainerId,
                    out var spawned))
            {
                return;
            }

            pack.Comp.Handset = spawned;
            Comp<ANPRCHandsetComponent>(spawned.Value).Radio = pack.Owner;
            Dirty(pack);
            item = spawned.Value;
        }

        if (container.ContainedEntity == item)
            _container.Remove(item, container);

        // pickup fires OnHandsetEquippedHand which wires the user up
        if (!_hands.TryPickupAnyHand(user, item))
        {
            _container.Insert(item, container);
            _popup.PopupEntity(Loc.GetString("anprc-handset-hands-full"), pack.Owner, user, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("anprc-handset-taken", ("radio", pack.Owner)), user, user);
        _cmChat.ChatMessageToOne(Loc.GetString("anprc-handset-hint"), user);
    }

    private void ReleaseHandset(Entity<ANPRCHandsetUserComponent> ent, string? messageKey = null)
    {
        RevokeHandsetHearing(ent);

        if (TryComp(ent.Comp.Radio, out ANPRCRadioComponent? radio))
        {
            if (radio.HandsetUser == ent.Owner)
                radio.HandsetUser = null;

            // this pulls the item out of the holder's hand, OnHandsetUnequippedHand does the rest
            if (radio.Handset is { } item &&
                !TerminatingOrDeleted(item) &&
                TryComp(item, out ANPRCHandsetComponent? handset))
            {
                SnapHandsetHome((item, handset), (ent.Comp.Radio, radio), ent.Owner);
            }
        }

        RemComp<ANPRCHandsetUserComponent>(ent.Owner);

        if (messageKey != null && !TerminatingOrDeleted(ent.Owner))
            _cmChat.ChatMessageToOne(Loc.GetString(messageKey), ent.Owner);
    }

    private void OnHandsetChatGetPrefix(Entity<ANPRCHandsetUserComponent> ent, ref ChatGetPrefixEvent args)
    {
        if (args.Channel == null || args.Channel.ID != ANPRCSentinelChannel.Id)
            return;

        // your own worn pack wins over a held handset
        if (HasComp<WearingANPRCComponent>(ent.Owner))
            return;

        if (!TryComp(ent.Comp.Radio, out ANPRCRadioComponent? radio) ||
            (!radio.IsEquipped && !radio.Planted) ||
            !HandsetInReach(ent.Owner, ent.Comp.Radio))
        {
            ReleaseHandset(ent, "anprc-handset-radio-gone");
            args.Channel = null;
            return;
        }

        // no operator training check, that's the whole point of the handset
        if (!ValidateTransmit((ent.Comp.Radio, radio), ent.Owner))
        {
            args.Channel = null;
            return;
        }

        if (radio.Mode == RadioMode.CipherText && string.IsNullOrEmpty(_crypto.GetFillFaction(ent.Comp.Radio)))
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-ct-mode-no-fill"), ent.Owner);
            args.Channel = null;
            return;
        }

        if (radio.FrequencyOverrides.ContainsKey(radio.ActiveSlot))
        {
            ent.Comp.PendingTransmit = true;
            return;
        }

        if (!radio.Presets.TryGetValue(radio.ActiveSlot, out var channelId) ||
            string.IsNullOrEmpty(channelId.Id))
        {
            _cmChat.ChatMessageToOne(
                Loc.GetString("anprc-slot-empty", ("slot", radio.ActiveSlot + 1)),
                ent.Owner);

            args.Channel = null;
            return;
        }

        if (!_prototype.TryIndex(channelId, out var realChannel))
        {
            args.Channel = null;
            return;
        }

        ent.Comp.PendingTransmit = true;
        args.Channel = realChannel;
    }

    private void OnHandsetSpeak(Entity<ANPRCHandsetUserComponent> ent, ref EntitySpokeEvent args)
    {
        if (!TryComp(ent.Comp.Radio, out ANPRCRadioComponent? radio) ||
            !radio.Enabled ||
            (!radio.IsEquipped && !radio.Planted))
        {
            ent.Comp.PendingTransmit = false;
            return;
        }

        var pack = new Entity<ANPRCRadioComponent>(ent.Comp.Radio, radio);

        if (ent.Comp.PendingTransmit)
        {
            ent.Comp.PendingTransmit = false;

            if (args.Channel == null)
                return;

            TransmitThroughPack(ent.Owner, pack, GetHandsetOnAirName(ent.Owner, pack), ref args);
            return;
        }

        // anything said out loud into a held handset goes out on the active net, whisper
        // to stay off the air. failures are silent so a mis-set pack doesn't spam the
        // holder every sentence
        if (!_commsEnabled || args.Channel != null || args.ObfuscatedMessage != null)
            return;

        // wearing your own pack keeps :r discipline (matches the prefix path)
        if (HasComp<WearingANPRCComponent>(ent.Owner))
            return;

        if (!HandsetInReach(ent.Owner, ent.Comp.Radio) ||
            !ValidateTransmit(pack, ent.Owner, quiet: true))
        {
            return;
        }

        if (radio.Mode == RadioMode.CipherText && string.IsNullOrEmpty(_crypto.GetFillFaction(ent.Comp.Radio)))
            return;

        if (!radio.FrequencyOverrides.ContainsKey(radio.ActiveSlot))
        {
            if (!radio.Presets.TryGetValue(radio.ActiveSlot, out var channelId) ||
                string.IsNullOrEmpty(channelId.Id) ||
                !_prototype.TryIndex(channelId, out var realChannel))
            {
                return;
            }

            args.Channel = realChannel;
        }

        TransmitThroughPack(ent.Owner, pack, GetHandsetOnAirName(ent.Owner, pack), ref args);
    }

    // handset users go on air under their own callsign if they have one, otherwise the station's
    private string GetHandsetOnAirName(EntityUid speaker, Entity<ANPRCRadioComponent> pack)
    {
        if (TryComp(speaker, out AU14CallsignComponent? callsign) &&
            !string.IsNullOrEmpty(callsign.Callsign))
        {
            return callsign.Callsign;
        }

        return GetOnAirName(pack);
    }

    private void OnHandsetSpeakerName(Entity<ANPRCHandsetUserComponent> ent, ref TransformSpeakerNameEvent args)
    {
        if (!TryComp(ent.Comp.Radio, out ANPRCRadioComponent? radio) || !radio.NameMaskActive)
            return;

        // always mask here. deferring holders with a callsign to the callsign system
        // raced its tick flag and could put the real name on air
        args.VoiceName = GetHandsetOnAirName(ent.Owner, (ent.Comp.Radio, radio));
    }
}
