using Content.Server.Chat.Systems;
using Content.Server.Radio.Components;
using Content.Shared._AU14.Radio;
using Content.Shared._RMC14.Chat;
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

    private bool ValidateTransmit(Entity<ANPRCRadioComponent> ent, EntityUid user)
    {
        var radio = ent.Comp;

        if (!radio.Enabled || (!radio.IsEquipped && !radio.Planted))
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-radio-off"), user);
            return false;
        }

        if (radio.MonitorEnabled)
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-monitor-no-transmit"), user);
            return false;
        }

        if (radio.ActiveSlot < 0)
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-no-active-slot"), user);
            return false;
        }

        if (!_powerCell.HasCharge(ent.Owner, GetTransmitCost(radio)))
        {
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
                AppendNetLog(
                    logRadio,
                    _timing.CurTime.TotalSeconds,
                    Name(ent.Owner),
                    $"{args.Channel.LocalizedName} ({TunableFrequencySystem.FormatFreq(args.Channel.Frequency)} MHz)",
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

        var outMessage = string.IsNullOrWhiteSpace(radio.Callsign)
            ? args.Message
            : $"{radio.Callsign} {args.Message}";

        if (radio.FrequencyOverrides.TryGetValue(radio.ActiveSlot, out var frequency))
        {
            args.Channel = null;

            _powerCell.TryUseCharge(wearing.Radio, GetTransmitCost(radio));
            _tunable.BroadcastOnFrequency(ent.Owner, frequency, outMessage);

            AppendNetLog(
                radio,
                _timing.CurTime.TotalSeconds,
                Name(ent.Owner),
                $"{TunableFrequencySystem.FormatFreq(frequency)} MHz",
                outMessage);

            UpdateBuiState(new Entity<ANPRCRadioComponent>(wearing.Radio, radio));
            return;
        }

        if (!HasPreset(radio, args.Channel.ID))
            return;

        var channel = args.Channel;
        args.Channel = null;

        _powerCell.TryUseCharge(wearing.Radio, GetTransmitCost(radio));

        var unsecured = !string.IsNullOrEmpty(channel.Faction) &&
                        radio.Mode != RadioMode.PlainText &&
                        !_crypto.HasMatchingCrypto(wearing.Radio, channel);

        if (unsecured)
        {
            _cmChat.ChatMessageToOne(
                Loc.GetString(
                    "anprc-comsec-unsecured",
                    ("channel", channel.LocalizedName),
                    ("faction", channel.Faction)),
                ent.Owner);
        }

        var sourceWasExempt = HasComp<TelecomExemptComponent>(ent.Owner);
        var radioWasExempt = HasComp<TelecomExemptComponent>(wearing.Radio);

        if (!sourceWasExempt)
            EnsureComp<TelecomExemptComponent>(ent.Owner);

        if (!radioWasExempt)
            EnsureComp<TelecomExemptComponent>(wearing.Radio);

        _radio.SendRadioMessage(ent.Owner, outMessage, channel, wearing.Radio);

        if (!sourceWasExempt)
            RemCompDeferred<TelecomExemptComponent>(ent.Owner);

        if (!radioWasExempt)
            RemCompDeferred<TelecomExemptComponent>(wearing.Radio);

        TryDirectionFind(ent.Owner, radio, channel, unsecured);

        AppendNetLog(
            radio,
            _timing.CurTime.TotalSeconds,
            Name(ent.Owner),
            $"{channel.LocalizedName} ({TunableFrequencySystem.FormatFreq(channel.Frequency)} MHz)",
            outMessage);

        UpdateBuiState(new Entity<ANPRCRadioComponent>(wearing.Radio, radio));
    }

    private void TryDirectionFind(
        EntityUid source,
        ANPRCRadioComponent radio,
        RadioChannelPrototype channel,
        bool unsecured)
    {
        if (string.IsNullOrEmpty(channel.Faction))
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

        var callsignPrefix = string.IsNullOrWhiteSpace(radio.Callsign)
            ? string.Empty
            : $"{radio.Callsign} ";

        _radio.SendRadioMessage(
            ent.Owner,
            $"{callsignPrefix}RADIO CHECK, HOW COPY, OVER",
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

            if (otherXform.MapID != senderMap)
                continue;

            if (!HasPreset(other, channelId.Id))
                continue;

            var distance = (senderPos - _transform.GetWorldPosition(otherXform)).Length();

            var label = string.IsNullOrWhiteSpace(other.Callsign)
                ? Loc.GetString("anprc-radio-check-unknown-station")
                : other.Callsign;

            AddByRange(distance, fullRange, partialRange, label, clear, degraded);
        }

        var headsetQuery = EntityQueryEnumerator<WearingHeadsetComponent, TransformComponent>();

        while (headsetQuery.MoveNext(out var wearerUid, out var wearingHeadset, out var wearerXform))
        {
            if (wearerUid == senderWearer)
                continue;

            if (wearerXform.MapID != senderMap)
                continue;

            if (!TryComp(wearingHeadset.Headset, out EncryptionKeyHolderComponent? keys) ||
                !keys.Channels.Contains(channelId.Id))
            {
                continue;
            }

            var distance = (senderPos - _transform.GetWorldPosition(wearerXform)).Length();
            AddByRange(distance, fullRange, partialRange, Name(wearerUid), clear, degraded);
        }

        _cmChat.ChatMessageToOne(
            Loc.GetString(
                "anprc-radio-check-report",
                ("clear", clear.Count == 0 ? "—" : string.Join(", ", clear)),
                ("degraded", degraded.Count == 0 ? "—" : string.Join(", ", degraded))),
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
