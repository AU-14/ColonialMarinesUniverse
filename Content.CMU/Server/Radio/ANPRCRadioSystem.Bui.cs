using System.Linq;
using System.Text;
using Content.Shared._AU14.Radio;
using Content.Shared.Chat;
using Content.Shared.Paper;
using Content.Shared.Radio;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Radio;

public sealed partial class ANPRCRadioSystem
{
    private static readonly EntProtoId LogPaperId = "ANPRCNetLogPrintout";

    private void OnGetAltVerbs(Entity<ANPRCRadioComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;

        if (ent.Comp.Planted)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("anprc-verb-packup"),
                Priority = 3,
                Act = () => StartPackUp(ent, user)
            });
        }

        // a planted pack works like a field phone, anyone at it can take the handset.
        // for a worn pack the verb lives on the wearer instead
        if (ent.Comp.Planted && ent.Comp.Enabled)
            AddHandsetVerbs(ent, user, ref args);

        if (!HasComp<ANPRCRadioUserComponent>(user))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("anprc-verb-open"),
            IconEntity = GetNetEntity(ent.Owner),
            Priority = 2,
            Act = () => _ui.OpenUi(ent.Owner, ANPRCRadioUI.Key, user)
        });

        if (!ent.Comp.Planted && !ent.Comp.IsEquipped && !_container.IsEntityInContainer(ent.Owner))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("anprc-verb-plant"),
                Priority = 1,
                Act = () => StartPlant(ent, user)
            });
        }
    }

    private void OnSelectSlot(Entity<ANPRCRadioComponent> ent, ref ANPRCSelectSlotMsg args)
    {
        if (!ent.Comp.SlotLabels.ContainsKey(args.Slot))
            return;

        ent.Comp.ActiveSlot = args.Slot;
        Dirty(ent);

        UpdateEquippedChannels(ent);
        UpdateRelayAnchor(ent);
        UpdateBuiState(ent);
    }

    private void OnAddSlot(Entity<ANPRCRadioComponent> ent, ref ANPRCAddSlotMsg args)
    {
        if (ent.Comp.SlotLabels.Count >= ANPRCRadioComponent.MaxSlots)
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-slot-max-reached"), args.Actor);
            return;
        }

        var index = -1;

        for (var i = 0; i < ANPRCRadioComponent.MaxSlots; i++)
        {
            if (!ent.Comp.SlotLabels.ContainsKey(i))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            return;

        var label = Sanitize(args.Label, ANPRCRadioComponent.MaxLabelLength);

        if (string.IsNullOrWhiteSpace(label))
            label = $"P{index + 1}";

        ent.Comp.SlotLabels[index] = label;

        if (ent.Comp.ActiveSlot < 0)
            ent.Comp.ActiveSlot = index;

        Dirty(ent);

        UpdateEquippedChannels(ent);
        UpdateRelayAnchor(ent);
        UpdateBuiState(ent);
    }

    private void OnDeleteSlot(Entity<ANPRCRadioComponent> ent, ref ANPRCDeleteSlotMsg args)
    {
        if (!ent.Comp.SlotLabels.ContainsKey(args.Slot))
            return;

        ent.Comp.SlotLabels.Remove(args.Slot);
        ent.Comp.Presets.Remove(args.Slot);
        ent.Comp.FrequencyOverrides.Remove(args.Slot);

        if (ent.Comp.ActiveSlot == args.Slot)
        {
            ent.Comp.ActiveSlot = ent.Comp.SlotLabels.Keys
                .OrderBy(key => key)
                .FirstOrDefault(-1);
        }

        Dirty(ent);

        UpdateEquippedChannels(ent);
        UpdateRelayAnchor(ent);
        UpdateBuiState(ent);
    }

    private void OnClearSlot(Entity<ANPRCRadioComponent> ent, ref ANPRCClearSlotMsg args)
    {
        if (!ent.Comp.SlotLabels.ContainsKey(args.Slot))
            return;

        ent.Comp.Presets.Remove(args.Slot);
        ent.Comp.FrequencyOverrides.Remove(args.Slot);

        Dirty(ent);

        UpdateEquippedChannels(ent);
        UpdateRelayAnchor(ent);
        UpdateBuiState(ent);
    }

    private void OnSetSlotChannel(Entity<ANPRCRadioComponent> ent, ref ANPRCSetSlotChannelMsg args)
    {
        if (!ent.Comp.SlotLabels.ContainsKey(args.Slot))
            return;

        if (!_prototype.HasIndex(args.Channel))
            return;

        ent.Comp.FrequencyOverrides.Remove(args.Slot);
        ent.Comp.Presets[args.Slot] = args.Channel;

        Dirty(ent);

        UpdateEquippedChannels(ent);
        UpdateRelayAnchor(ent);
        UpdateBuiState(ent);
    }

    private void OnManualFrequency(Entity<ANPRCRadioComponent> ent, ref ANPRCManualFrequencyMsg args)
    {
        if (!ent.Comp.SlotLabels.ContainsKey(args.Slot))
            return;

        var text = args.FrequencyText
            .Trim()
            .Replace(".", "")
            .Replace(",", "");

        if (!int.TryParse(text, out var frequency) || frequency <= 0)
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-frequency-invalid"), args.Actor);
            return;
        }

        if (_freqPlan.TryGetChannelByFrequency(frequency, out var channel))
        {
            ent.Comp.FrequencyOverrides.Remove(args.Slot);
            ent.Comp.Presets[args.Slot] = channel;
        }
        else if (InDirectBand(frequency))
        {
            ent.Comp.FrequencyOverrides[args.Slot] = frequency;
            ent.Comp.Presets.Remove(args.Slot);
        }
        else
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-frequency-out-of-band"), args.Actor);
            return;
        }

        Dirty(ent);

        UpdateEquippedChannels(ent);
        UpdateRelayAnchor(ent);
        UpdateBuiState(ent);

        var label = ent.Comp.SlotLabels.TryGetValue(args.Slot, out var slotLabel)
            ? slotLabel
            : $"P{args.Slot + 1}";

        _cmChat.ChatMessageToOne(
            Loc.GetString(
                "anprc-frequency-set",
                ("slot", label),
                ("freq", TunableFrequencySystem.FormatFreq(frequency))),
            args.Actor);
    }

    // a raw frequency has to land in a band some receiver in the game can reach: the
    // military band the search receiver walks, or the softwave band the colony's
    // handhelds and tunable headsets live in. anything else would be a private net
    // nothing on the planet could ever find
    private static bool InDirectBand(int frequency)
    {
        return frequency
            is >= ANPRCRadioComponent.SweepBandMin and <= ANPRCRadioComponent.SweepBandMax
            or >= ANPRCRadioComponent.SoftwaveBandMin and <= ANPRCRadioComponent.SoftwaveBandMax;
    }

    private void OnTogglePower(Entity<ANPRCRadioComponent> ent, ref ANPRCTogglePowerMsg args)
    {
        if (!ent.Comp.Enabled && !_powerCell.HasCharge(ent.Owner, 1f))
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-battery-depleted"), args.Actor);
            return;
        }

        ent.Comp.Enabled = !ent.Comp.Enabled;
        Dirty(ent);

        SetBatteryDrawEnabled(ent.Owner, ent.Comp.Enabled);

        if (ent.Comp.Enabled)
            GrantReceiveChannels(ent);
        else
            RevokeReceiveChannels(ent);

        UpdateRelayAnchor(ent);
        UpdateBuiState(ent);
    }

    private void OnToggleMonitor(Entity<ANPRCRadioComponent> ent, ref ANPRCToggleMonitorMsg args)
    {
        ent.Comp.MonitorEnabled = !ent.Comp.MonitorEnabled;
        Dirty(ent);

        UpdateEquippedChannels(ent);
        UpdateBuiState(ent);
    }

    private void OnSetMode(Entity<ANPRCRadioComponent> ent, ref ANPRCSetModeMsg args)
    {
        if (!Enum.IsDefined(args.Mode))
            return;

        ent.Comp.Mode = args.Mode;
        Dirty(ent);

        UpdateBuiState(ent);
    }

    private void OnSetScan(Entity<ANPRCRadioComponent> ent, ref ANPRCSetScanMsg args)
    {
        ent.Comp.ScanEnabled = args.Enabled;
        Dirty(ent);

        UpdateEquippedChannels(ent);
        UpdateBuiState(ent);
    }

    private void OnSetSweep(Entity<ANPRCRadioComponent> ent, ref ANPRCSetSweepMsg args)
    {
        if (args.Enabled == ent.Comp.SweepEnabled)
            return;

        if (!args.Enabled)
        {
            _sweep.StopSweep(ent);
            UpdateEquippedChannels(ent);
            UpdateBuiState(ent);
            return;
        }

        if (!ent.Comp.Enabled || (!ent.Comp.IsEquipped && !ent.Comp.Planted))
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-sweep-needs-online"), args.Actor);
            return;
        }

        ent.Comp.SweepEnabled = true;
        ent.Comp.SweepLastUpdate = TimeSpan.Zero;
        Dirty(ent);

        // dropping every net the moment the search starts is the whole cost of the mode
        UpdateEquippedChannels(ent);
        UpdateBuiState(ent);

        _cmChat.ChatMessageToOne(Loc.GetString("anprc-sweep-started"), args.Actor);
    }

    // tuning a fixed contact writes the raw frequency, so the operator never has to
    // copy a number off the panel and back into the keypad
    private void OnTuneContact(Entity<ANPRCRadioComponent> ent, ref ANPRCTuneContactMsg args)
    {
        if (!ent.Comp.SlotLabels.ContainsKey(args.Slot))
            return;

        if (!ent.Comp.DiscoveredFrequencies.Contains(args.Frequency))
            return;

        if (_freqPlan.TryGetChannelByFrequency(args.Frequency, out var channel))
        {
            ent.Comp.FrequencyOverrides.Remove(args.Slot);
            ent.Comp.Presets[args.Slot] = channel;
        }
        else
        {
            ent.Comp.FrequencyOverrides[args.Slot] = args.Frequency;
            ent.Comp.Presets.Remove(args.Slot);
        }

        Dirty(ent);

        UpdateEquippedChannels(ent);
        UpdateRelayAnchor(ent);
        UpdateBuiState(ent);

        _cmChat.ChatMessageToOne(
            Loc.GetString(
                "anprc-frequency-set",
                ("slot", ent.Comp.SlotLabels[args.Slot]),
                ("freq", TunableFrequencySystem.FormatFreq(args.Frequency))),
            args.Actor);
    }

    // the log lives in the set and rolls over at 50 entries. printing is how an
    // intercept becomes something the cell can act on after the operator moves on
    private void OnPrintLog(Entity<ANPRCRadioComponent> ent, ref ANPRCPrintLogMsg args)
    {
        if (!ent.Comp.Enabled || (!ent.Comp.IsEquipped && !ent.Comp.Planted))
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-sweep-needs-online"), args.Actor);
            return;
        }

        var interceptsOnly = args.InterceptsOnly;

        var entries = ent.Comp.NetLog
            .Where(entry => !interceptsOnly || entry.Intercepted)
            .ToList();

        if (entries.Count == 0)
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-log-print-empty"), args.Actor);
            return;
        }

        var paper = Spawn(LogPaperId, Transform(ent.Owner).Coordinates);

        if (TryComp(paper, out PaperComponent? paperComp))
            _paper.SetContent((paper, paperComp), BuildLogReport(ent, entries, interceptsOnly));

        _hands.TryPickupAnyHand(args.Actor, paper);

        _cmChat.ChatMessageToOne(
            Loc.GetString("anprc-log-printed", ("count", entries.Count)),
            args.Actor);
    }

    private string BuildLogReport(
        Entity<ANPRCRadioComponent> ent,
        List<ANPRCNetLogEntry> entries,
        bool interceptsOnly)
    {
        var sb = new StringBuilder();

        sb.AppendLine(interceptsOnly
            ? "[head=2]INTERCEPT LOG[/head]"
            : "[head=2]NET LOG[/head]");

        var station = !string.IsNullOrEmpty(ent.Comp.Callsign)
            ? ent.Comp.Callsign
            : GetWearerCallsign(ent.Owner);

        if (string.IsNullOrEmpty(station))
            station = "UNKNOWN STATION";

        sb.AppendLine($"[bold]STATION:[/bold] {station}");
        sb.AppendLine($"[bold]ENTRIES:[/bold] {entries.Count}");
        sb.AppendLine();

        foreach (var entry in entries)
        {
            var ts = TimeSpan.FromSeconds(entry.Timestamp);
            var time = $"{(int) ts.TotalMinutes:D2}:{ts.Seconds:D2}";
            var marker = entry.Intercepted ? " [bold](INTERCEPT)[/bold]" : string.Empty;

            sb.AppendLine($"[{time}] {entry.SenderName} - {entry.ChannelDisplay}{marker}");
            sb.AppendLine($"  {entry.Message}");
        }

        sb.AppendLine();
        sb.Append("[italic]Transcribed from an AN/PRC-117G net log. Times are set clock, not local.[/italic]");

        return sb.ToString();
    }

    private void OnSetTxPower(Entity<ANPRCRadioComponent> ent, ref ANPRCSetTxPowerMsg args)
    {
        if (!Enum.IsDefined(args.Power))
            return;

        ent.Comp.TxPower = args.Power;
        Dirty(ent);

        UpdateRelayAnchor(ent);
        UpdateBuiState(ent);
    }

    private void OnSetSquelch(Entity<ANPRCRadioComponent> ent, ref ANPRCSetSquelchMsg args)
    {
        ent.Comp.SquelchLevel = Math.Clamp(args.Level, 0, ANPRCRadioComponent.MaxSquelchLevel);
        Dirty(ent);

        UpdateBuiState(ent);
    }

    private void OnSetCallsign(Entity<ANPRCRadioComponent> ent, ref ANPRCSetCallsignMsg args)
    {
        ent.Comp.Callsign = Sanitize(args.Callsign, ANPRCRadioComponent.MaxCallsignLength);
        Dirty(ent);

        UpdateBuiState(ent);
    }

    private void UpdateBuiState(Entity<ANPRCRadioComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, ANPRCRadioUI.Key))
            return;

        var hasBattery = _powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery);
        var batteryFraction = hasBattery && battery!.MaxCharge > 0f
            ? battery.CurrentCharge / battery.MaxCharge
            : 0f;

        var antennaLabel = "NONE";

        if (TryGetRadioSlot(ent.Owner, AntennaSlotId, out var antennaSlot) &&
            TryComp(antennaSlot.Item, out ANPRCAntennaComponent? antenna))
        {
            antennaLabel = antenna.Label;
        }

        _ui.SetUiState(
            ent.Owner,
            ANPRCRadioUI.Key,
            new ANPRCRadioState(
                new Dictionary<int, ProtoId<RadioChannelPrototype>>(ent.Comp.Presets),
                new Dictionary<int, int>(ent.Comp.FrequencyOverrides),
                new Dictionary<int, string>(ent.Comp.SlotLabels),
                ent.Comp.ActiveSlot,
                ent.Comp.Enabled,
                ent.Comp.IsEquipped,
                ent.Comp.MonitorEnabled,
                ent.Comp.Mode,
                ent.Comp.ScanEnabled,
                ent.Comp.SquelchLevel,
                ent.Comp.TxPower,
                ent.Comp.Planted,
                ent.Comp.Callsign,
                GetWearerCallsign(ent.Owner),
                new List<string>(ent.Comp.CallsignPresets),
                _crypto.GetFillDesignation(ent.Owner),
                _crypto.GetFillFaction(ent.Owner),
                _crypto.IsFillStale(ent.Owner),
                ent.Comp.OperatorFaction,
                new List<ANPRCNetLogEntry>(ent.Comp.NetLog),
                batteryFraction,
                hasBattery,
                antennaLabel,
                BuildChannelFrequencies(ent.Comp),
                ent.Comp.SweepEnabled,
                ent.Comp.SweepPosition,
                BuildSweepContacts(ent.Comp)));
    }

    // the client only ever learns the operator's own nets, the unfactioned ones, and
    // whatever the search receiver has actually fixed. sending the whole plan would
    // hand every enemy frequency to anyone willing to read the state off the wire
    private Dictionary<string, int> BuildChannelFrequencies(ANPRCRadioComponent radio)
    {
        var frequencies = new Dictionary<string, int>();

        foreach (var proto in _prototype.EnumeratePrototypes<RadioChannelPrototype>())
        {
            if (proto.Frequency <= 0)
                continue;

            var frequency = _freqPlan.GetFrequency(proto);

            var known = string.IsNullOrEmpty(proto.Faction) ||
                        string.IsNullOrEmpty(radio.OperatorFaction) ||
                        string.Equals(proto.Faction, radio.OperatorFaction, StringComparison.OrdinalIgnoreCase) ||
                        radio.DiscoveredFrequencies.Contains(frequency);

            if (known)
                frequencies[proto.ID] = frequency;
        }

        return frequencies;
    }

    // unresolved contacts go out with the unearned digits zeroed, so the exact number
    // is not sitting in the BUI state before the operator has worked for it
    private List<ANPRCSweepContact> BuildSweepContacts(ANPRCRadioComponent radio)
    {
        var contacts = new List<ANPRCSweepContact>();
        var tierMax = radio.SweepTierThresholds.Count;

        foreach (var (frequency, confidence) in radio.SweepContacts)
        {
            var tier = ANPRCSweepSystem.TierOf(radio, confidence);

            if (tier <= 0)
                continue;

            // the operator's own nets are never a fix, they arrive already identified
            var known = _freqPlan.IsKnownTo(frequency, radio.OperatorFaction);
            var resolved = known || radio.DiscoveredFrequencies.Contains(frequency);

            contacts.Add(new ANPRCSweepContact(
                resolved ? frequency : ANPRCSweepSystem.MaskFrequency(frequency, tier),
                confidence,
                resolved,
                resolved ? _sweep.GetChannelName(frequency) : string.Empty,
                resolved ? tierMax : tier,
                tierMax,
                known));
        }

        // unknowns first: they are the ones worth the operator's attention
        return contacts
            .OrderBy(contact => contact.Known)
            .ThenByDescending(contact => contact.Resolved)
            .ThenBy(contact => contact.Frequency)
            .ToList();
    }

    private static string Sanitize(string input, int maxLength)
    {
        var upper = input.ToUpperInvariant().Trim();

        var filtered = new string(upper
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == ' ')
            .ToArray());

        return filtered.Length > maxLength
            ? filtered[..maxLength]
            : filtered;
    }
}
