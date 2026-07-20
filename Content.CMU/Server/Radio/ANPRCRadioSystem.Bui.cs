using System.Linq;
using Content.Shared._AU14.Radio;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Radio;

public sealed partial class ANPRCRadioSystem
{
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
        else
        {
            ent.Comp.FrequencyOverrides[args.Slot] = frequency;
            ent.Comp.Presets.Remove(args.Slot);
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
                BuildChannelFrequencies()));
    }

    private Dictionary<string, int> BuildChannelFrequencies()
    {
        var frequencies = new Dictionary<string, int>();

        foreach (var proto in _prototype.EnumeratePrototypes<RadioChannelPrototype>())
        {
            if (proto.Frequency <= 0)
                continue;

            frequencies[proto.ID] = _freqPlan.GetFrequency(proto);
        }

        return frequencies;
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
