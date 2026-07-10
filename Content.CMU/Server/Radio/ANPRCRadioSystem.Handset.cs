using Content.Server.Chat.Systems;
using Content.Shared._AU14.Callsigns;
using Content.Shared._AU14.Radio;
using Content.Shared._RMC14.Chat;
using Content.Shared.Chat;
using Content.Shared.Popups;
using Content.Shared.Verbs;

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
            }
        }

        if (toRelease == null)
            return;

        foreach (var (user, reason) in toRelease)
        {
            ReleaseHandset(user, reason);
        }
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

        // moving to this pack hangs up a handset held on another one
        if (TryComp(user, out ANPRCHandsetUserComponent? existing) &&
            existing.Radio != pack.Owner &&
            TryComp(existing.Radio, out ANPRCRadioComponent? oldRadio) &&
            oldRadio.HandsetUser == user)
        {
            oldRadio.HandsetUser = null;
        }

        var handset = EnsureComp<ANPRCHandsetUserComponent>(user);
        handset.Radio = pack.Owner;
        handset.PendingTransmit = false;

        pack.Comp.HandsetUser = user;

        _popup.PopupEntity(Loc.GetString("anprc-handset-taken", ("radio", pack.Owner)), user, user);
        _cmChat.ChatMessageToOne(Loc.GetString("anprc-handset-hint"), user);
    }

    private void ReleaseHandset(Entity<ANPRCHandsetUserComponent> ent, string? messageKey = null)
    {
        if (TryComp(ent.Comp.Radio, out ANPRCRadioComponent? radio) && radio.HandsetUser == ent.Owner)
            radio.HandsetUser = null;

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
        if (!ent.Comp.PendingTransmit)
            return;

        ent.Comp.PendingTransmit = false;

        if (args.Channel == null)
            return;

        if (!TryComp(ent.Comp.Radio, out ANPRCRadioComponent? radio) ||
            !radio.Enabled ||
            (!radio.IsEquipped && !radio.Planted))
        {
            return;
        }

        var pack = new Entity<ANPRCRadioComponent>(ent.Comp.Radio, radio);

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

        // holders with their own callsign get masked by the callsign system,
        // this covers speakers without one
        if (TryComp(ent.Owner, out AU14CallsignComponent? callsign) &&
            !string.IsNullOrEmpty(callsign.Callsign))
        {
            return;
        }

        args.VoiceName = GetOnAirName((ent.Comp.Radio, radio));
    }
}
