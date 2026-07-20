using Content.Server.Chat.Systems;
using Content.Server.Radio.Components;
using Content.Shared._AU14.Callsigns;
using Content.Shared._AU14.Radio;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Marines;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Radio;

public sealed partial class ANPRCRadioSystem
{
    private void OnChatGetPrefix(Entity<WearingANPRCComponent> ent, ref ChatGetPrefixEvent args)
    {
        if (args.Channel == null || args.Channel.ID != ANPRCSentinelChannel.Id)
            return;

        if (!TryComp(ent.Comp.Radio, out ANPRCRadioComponent? radio))
            return;

        if (!HasComp<ANPRCRadioUserComponent>(ent.Owner))
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-not-authorized"), ent.Owner);
            args.Channel = null;

            return;
        }

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
            ent.Comp.PendingANPRCTransmit = true;
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

        ent.Comp.PendingANPRCTransmit = true;
        args.Channel = realChannel;
    }

    private void OnWearerSpeakerName(Entity<WearingANPRCComponent> ent, ref TransformSpeakerNameEvent args)
    {
        if (!TryComp(ent.Comp.Radio, out ANPRCRadioComponent? radio) || !radio.NameMaskActive)
            return;

        args.VoiceName = GetOnAirName((ent.Comp.Radio, radio));
    }

    private void OnRadioSpeakerName(Entity<ANPRCRadioComponent> ent, ref TransformSpeakerNameEvent args)
    {
        // the radio entity only speaks for itself (radio checks), always mask
        args.VoiceName = GetOnAirName(ent);
    }

    // a manually set callsign is a station override, otherwise the pack goes on air
    // under the wearer's assigned callsign so renames/squad changes/wearer swaps
    // propagate without touching the pack
    private string GetOnAirName(Entity<ANPRCRadioComponent> radio)
    {
        if (!string.IsNullOrWhiteSpace(radio.Comp.Callsign))
            return radio.Comp.Callsign;

        var wearerCallsign = GetWearerCallsign(radio);

        return string.IsNullOrEmpty(wearerCallsign)
            ? Loc.GetString("anprc-unknown-station")
            : wearerCallsign;
    }

    private string GetWearerCallsign(EntityUid radio)
    {
        var wearer = Transform(radio).ParentUid;

        if (wearer.IsValid() &&
            TryComp(wearer, out AU14CallsignComponent? assigned) &&
            !string.IsNullOrEmpty(assigned.Callsign))
        {
            return assigned.Callsign;
        }

        return string.Empty;
    }

    private bool ValidateTransmit(Entity<ANPRCRadioComponent> ent, EntityUid user, bool quiet = false)
    {
        var radio = ent.Comp;

        if (!radio.Enabled || (!radio.IsEquipped && !radio.Planted))
        {
            if (!quiet)
                _cmChat.ChatMessageToOne(Loc.GetString("anprc-radio-off"), user);
            return false;
        }

        if (radio.MonitorEnabled)
        {
            if (!quiet)
                _cmChat.ChatMessageToOne(Loc.GetString("anprc-monitor-no-transmit"), user);
            return false;
        }

        if (radio.ActiveSlot < 0)
        {
            if (!quiet)
                _cmChat.ChatMessageToOne(Loc.GetString("anprc-no-active-slot"), user);
            return false;
        }

        if (!_powerCell.HasCharge(ent.Owner, GetTransmitCost(radio)))
        {
            if (!quiet)
                _cmChat.ChatMessageToOne(Loc.GetString("anprc-battery-insufficient"), user);
            return false;
        }

        return true;
    }

    private static float GetTransmitCost(ANPRCRadioComponent radio)
    {
        return radio.TransmitChargeCost * radio.TxPower.ChargeMultiplier() * radio.Mode.ChargeMultiplier();
    }

    private void OnSpeak(Entity<WearingANPRCComponent> ent, ref EntitySpokeEvent args)
    {
        var wearing = ent.Comp;

        if (!wearing.PendingANPRCTransmit)
        {
            if (args.Channel != null &&
                args.Channel.Frequency > 0 &&
                TryComp(wearing.Radio, out ANPRCRadioComponent? logRadio) &&
                logRadio.Enabled)
            {
                // log headset traffic under what actually went on air
                var logName = TryComp(ent.Owner, out AU14CallsignComponent? ownCallsign) &&
                              !string.IsNullOrEmpty(ownCallsign.Callsign)
                    ? ownCallsign.Callsign
                    : Name(ent.Owner);

                AppendNetLog(
                    logRadio,
                    _timing.CurTime.TotalSeconds,
                    logName,
                    $"{args.Channel.LocalizedName} ({TunableFrequencySystem.FormatFreq(_freqPlan.GetFrequency(args.Channel))} MHz)",
                    args.Message);

                UpdateBuiState(new Entity<ANPRCRadioComponent>(wearing.Radio, logRadio));
            }

            return;
        }

        wearing.PendingANPRCTransmit = false;

        if (args.Channel == null)
            return;

        if (!TryComp(wearing.Radio, out ANPRCRadioComponent? radio))
            return;

        if (!radio.Enabled || !radio.IsEquipped)
            return;

        var pack = new Entity<ANPRCRadioComponent>(wearing.Radio, radio);

        TransmitThroughPack(ent.Owner, pack, GetOnAirName(pack), ref args);
    }

    // sends one spoken message out through the pack (raw frequency or the active preset
    // net), handles battery cost, COMSEC warning, name masking, DF exposure and logging.
    // speaker is the wearer or a handset user at the pack
    private void TransmitThroughPack(
        EntityUid speaker,
        Entity<ANPRCRadioComponent> pack,
        string senderName,
        ref EntitySpokeEvent args)
    {
        var radio = pack.Comp;

        // callsign goes out as the speaker name, message body carries no prefix
        var outMessage = args.Message;

        if (radio.FrequencyOverrides.TryGetValue(radio.ActiveSlot, out var frequency))
        {
            args.Channel = null;

            _powerCell.TryUseCharge(pack.Owner, GetTransmitCost(radio));
            _tunable.BroadcastOnFrequency(speaker, frequency, outMessage, senderName);

            AppendNetLog(
                radio,
                _timing.CurTime.TotalSeconds,
                senderName,
                $"{TunableFrequencySystem.FormatFreq(frequency)} MHz",
                outMessage);

            UpdateBuiState(pack);
            return;
        }

        if (args.Channel == null || !HasPreset(radio, args.Channel.ID))
            return;

        var channel = args.Channel;
        args.Channel = null;

        _powerCell.TryUseCharge(pack.Owner, GetTransmitCost(radio));

        var unsecured = !string.IsNullOrEmpty(channel.Faction) &&
                        radio.Mode != RadioMode.PlainText &&
                        !_crypto.HasMatchingCrypto(pack.Owner, channel);

        if (unsecured)
        {
            _cmChat.ChatMessageToOne(
                Loc.GetString(
                    "anprc-comsec-unsecured",
                    ("channel", channel.LocalizedName),
                    ("faction", channel.Faction)),
                speaker);
        }

        var sourceWasExempt = HasComp<TelecomExemptComponent>(speaker);
        var radioWasExempt = HasComp<TelecomExemptComponent>(pack.Owner);

        if (!sourceWasExempt)
            EnsureComp<TelecomExemptComponent>(speaker);

        if (!radioWasExempt)
            EnsureComp<TelecomExemptComponent>(pack.Owner);

        // strip the job prefix for the duration of the send so the radio line is just
        // the callsign, no name no role. with the overhaul disabled this goes out unmasked
        JobPrefixComponent? jobPrefix = null;
        var hadJobPrefix = _commsEnabled && TryComp(speaker, out jobPrefix);
        var savedPrefix = jobPrefix?.Prefix ?? default;
        var savedAdditionalPrefix = jobPrefix?.AdditionalPrefix;

        if (hadJobPrefix)
            RemComp<JobPrefixComponent>(speaker);

        radio.NameMaskActive = _commsEnabled;

        try
        {
            _radio.SendRadioMessage(speaker, outMessage, channel, pack.Owner);
        }
        finally
        {
            radio.NameMaskActive = false;

            if (hadJobPrefix)
            {
                var restored = EnsureComp<JobPrefixComponent>(speaker);
                restored.Prefix = savedPrefix;
                restored.AdditionalPrefix = savedAdditionalPrefix;
                Dirty(speaker, restored);
            }
        }

        if (!sourceWasExempt)
            RemCompDeferred<TelecomExemptComponent>(speaker);

        if (!radioWasExempt)
            RemCompDeferred<TelecomExemptComponent>(pack.Owner);

        TryDirectionFind(speaker, radio, channel, unsecured);

        AppendNetLog(
            radio,
            _timing.CurTime.TotalSeconds,
            senderName,
            $"{channel.LocalizedName} ({TunableFrequencySystem.FormatFreq(_freqPlan.GetFrequency(channel))} MHz)",
            outMessage);

        UpdateBuiState(pack);
    }

    private void TryDirectionFind(
        EntityUid source,
        ANPRCRadioComponent radio,
        RadioChannelPrototype channel,
        bool unsecured)
    {
        if (!_commsEnabled || string.IsNullOrEmpty(channel.Faction))
            return;

        var plainText = radio.Mode == RadioMode.PlainText;
        float baseChance;

        if (plainText)
        {
            baseChance = radio.DFChancePlainText;
        }
        else if (unsecured)
        {
            baseChance = radio.DFChanceUnsecured;
        }
        else if (radio.Mode == RadioMode.FrequencyHopping)
        {
            baseChance = radio.DFChanceSecuredFH;
        }
        else
        {
            return;
        }

        if (radio.DFReportFactions.Count == 0)
            return;

        var now = _timing.CurTime;
        var position = _transform.GetWorldPosition(source);

        if (now - radio.DFLastTransmitTime > radio.DFAccumDecay ||
            (position - radio.DFLastTransmitPos).Length() > radio.DFAccumResetDistance)
        {
            radio.DFAccumulation = 0f;
        }

        var chance = (baseChance + radio.DFAccumulation) * radio.TxPower.DFMultiplier();

        if (_garble.GetJamIntensity(source) != RadioJamIntensity.None)
            chance += radio.DFChanceJamBonus;

        radio.DFAccumulation += radio.DFAccumBonus;
        radio.DFLastTransmitTime = now;
        radio.DFLastTransmitPos = position;

        if (!_random.Prob(Math.Clamp(chance, 0f, 0.9f)))
            return;

        foreach (var viewerFaction in radio.DFReportFactions)
        {
            if (_tacticalMap.CreateFactionIntelBlip(source, radio.OperatorFaction, viewerFaction) is not { } location)
                continue;

            var faction = viewerFaction;

            Timer.Spawn(
                radio.DFPingDuration,
                () => _tacticalMap.EraseFactionIntelBlip(location.GridId, location.Key, faction));
        }
    }

    private void OnRadioCheck(Entity<ANPRCRadioComponent> ent, ref ANPRCRadioCheckMsg args)
    {
        var radio = ent.Comp;

        if (!ValidateTransmit(ent, args.Actor))
            return;

        if (!radio.Presets.TryGetValue(radio.ActiveSlot, out var channelId) ||
            string.IsNullOrEmpty(channelId.Id) ||
            !_prototype.TryIndex(channelId, out var channel))
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-no-active-slot"), args.Actor);
            return;
        }

        _powerCell.TryUseCharge(ent.Owner, GetTransmitCost(radio));

        // source is the radio itself, OnRadioSpeakerName swaps in the callsign.
        // phrased per the voice procedure guidebook: addressee, self-id, request
        _radio.SendRadioMessage(
            ent.Owner,
            Loc.GetString("anprc-radio-check-call", ("station", GetOnAirName(ent))),
            channel,
            ent.Owner);

        var (fullRange, partialRange) = _range.GetAnchorRanges(ent.Owner);

        var senderPos = _transform.GetWorldPosition(ent.Owner);
        var senderMap = Transform(ent.Owner).MapID;
        var senderWearer = Transform(ent.Owner).ParentUid;
        var clear = new List<string>();
        var degraded = new List<string>();

        var query = EntityQueryEnumerator<ANPRCRadioComponent, TransformComponent>();

        while (query.MoveNext(out var otherUid, out var other, out var otherXform))
        {
            if (otherUid == ent.Owner || !other.Enabled || !other.IsEquipped)
                continue;

            if (!_range.InVerticalReach(otherXform.MapID, senderMap, 1))
                continue;

            if (!HasPreset(other, channelId.Id))
                continue;

            var distance = (senderPos - _transform.GetWorldPosition(otherXform)).Length();

            AddByRange(distance, fullRange, partialRange, GetOnAirName((otherUid, other)), clear, degraded);
        }

        var headsetQuery = EntityQueryEnumerator<WearingHeadsetComponent, TransformComponent>();

        while (headsetQuery.MoveNext(out var wearerUid, out var wearingHeadset, out var wearerXform))
        {
            if (wearerUid == senderWearer)
                continue;

            if (!_range.InVerticalReach(wearerXform.MapID, senderMap, 1))
                continue;

            if (!TryComp(wearingHeadset.Headset, out EncryptionKeyHolderComponent? keys) ||
                !keys.Channels.Contains(channelId.Id))
            {
                continue;
            }

            var distance = (senderPos - _transform.GetWorldPosition(wearerXform)).Length();

            var label = TryComp(wearerUid, out AU14CallsignComponent? wearerCallsign) &&
                        !string.IsNullOrEmpty(wearerCallsign.Callsign)
                ? wearerCallsign.Callsign
                : Name(wearerUid);

            AddByRange(distance, fullRange, partialRange, label, clear, degraded);
        }

        var nothingHeard = Loc.GetString("anprc-radio-check-nothing-heard");

        _cmChat.ChatMessageToOne(
            Loc.GetString(
                "anprc-radio-check-report",
                ("clear", clear.Count == 0 ? nothingHeard : string.Join(", ", clear)),
                ("degraded", degraded.Count == 0 ? nothingHeard : string.Join(", ", degraded))),
            args.Actor);

        if (_garble.GetJamIntensity(ent.Owner) != RadioJamIntensity.None &&
            _garble.TryGetNearestJammerDirection(ent.Owner, out var jammerDirection))
        {
            _cmChat.ChatMessageToOne(
                Loc.GetString("anprc-radio-check-interference", ("bearing", ShortBearing(jammerDirection))),
                args.Actor);
        }
    }

    private static string ShortBearing(Direction direction)
    {
        return direction switch
        {
            Direction.North => "N",
            Direction.NorthEast => "NE",
            Direction.East => "E",
            Direction.SouthEast => "SE",
            Direction.South => "S",
            Direction.SouthWest => "SW",
            Direction.West => "W",
            Direction.NorthWest => "NW",
            _ => "?"
        };
    }

    private static void AddByRange(
        float distance,
        float fullRange,
        float partialRange,
        string label,
        List<string> clear,
        List<string> degraded)
    {
        if (distance <= fullRange)
            clear.Add(label);
        else if (distance <= partialRange)
            degraded.Add(label);
    }
}
