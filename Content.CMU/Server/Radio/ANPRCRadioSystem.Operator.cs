using System.Linq;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.CMU14.Callsigns;
using Content.Shared.CMU14.Radio;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Radio;

// the operator-facing half of the panel: the quick setup, in-place renames, and the relay
// readings that tell an operator what their set is doing for the people around them
public sealed partial class ANPRCRadioSystem
{
    private const float PanelRefreshInterval = 1f;

    private const string SquadNetLabel = "SQUAD";

    private float _panelRefreshAccumulator;

    // a panel opened on a set nobody has touched since it was last closed would otherwise
    // show whatever the last push said, which can be a whole deployment out of date
    private void OnUiOpened(Entity<ANPRCRadioComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateBuiState(ent);
    }

    private void OnRenameSlot(Entity<ANPRCRadioComponent> ent, ref ANPRCRenameSlotMsg args)
    {
        if (!ent.Comp.SlotLabels.ContainsKey(args.Slot))
            return;

        var label = Sanitize(args.Label, ANPRCRadioComponent.MaxLabelLength);

        if (string.IsNullOrWhiteSpace(label))
            return;

        ent.Comp.SlotLabels[args.Slot] = label;
        Dirty(ent);

        UpdateBuiState(ent);
    }

    private void OnQuickSetup(Entity<ANPRCRadioComponent> ent, ref ANPRCQuickSetupMsg args)
    {
        var radio = ent.Comp;

        if (!radio.Enabled)
        {
            if (!_powerCell.HasCharge(ent.Owner, 1f))
            {
                _cmChat.ChatMessageToOne(Loc.GetString("anprc-battery-depleted"), args.Actor);
                return;
            }

            radio.Enabled = true;
            SetBatteryDrawEnabled(ent.Owner, true);
        }

        var loaded = new List<string>();
        var skipped = new List<string>();
        int? squadSlot = null;

        foreach (var net in GetStandardNets(ent, args.Actor))
        {
            var existing = SlotHolding(radio, net.Channel);

            if (existing != null)
            {
                if (net.Squad)
                    squadSlot = existing;

                continue;
            }

            var slot = FreeSlotFor(radio, net.Label);

            if (slot == null)
            {
                skipped.Add(net.Label);
                continue;
            }

            radio.FrequencyOverrides.Remove(slot.Value);
            radio.Presets[slot.Value] = net.Channel;
            loaded.Add(net.Label);

            if (net.Squad)
                squadSlot = slot;
        }

        // talk on the squad net unless the operator already picked something that works
        var activeWorks = radio.ActiveSlot >= 0 &&
                          (radio.Presets.ContainsKey(radio.ActiveSlot) ||
                           radio.FrequencyOverrides.ContainsKey(radio.ActiveSlot));

        if (!activeWorks)
        {
            radio.ActiveSlot = squadSlot ??
                               radio.Presets.Keys.OrderBy(key => key).Cast<int?>().FirstOrDefault() ??
                               radio.ActiveSlot;
        }

        Dirty(ent);

        UpdateEquippedChannels(ent);
        UpdateRelayAnchor(ent);
        UpdateBuiState(ent);

        var message = loaded.Count > 0
            ? Loc.GetString("anprc-quick-setup-loaded", ("nets", string.Join(", ", loaded)))
            : Loc.GetString("anprc-quick-setup-nothing");

        if (skipped.Count > 0)
            message += " " + Loc.GetString("anprc-quick-setup-full", ("nets", string.Join(", ", skipped)));

        _cmChat.ChatMessageToOne(message, args.Actor);
    }

    private static int? SlotHolding(ANPRCRadioComponent radio, ProtoId<RadioChannelPrototype> channel)
    {
        foreach (var (slot, preset) in radio.Presets)
        {
            if (preset == channel)
                return slot;
        }

        return null;
    }

    // an existing memory with nothing in it first, then a new one. a memory the operator
    // tuned is never overwritten, that is their decision and not routine setup
    private static int? FreeSlotFor(ANPRCRadioComponent radio, string label)
    {
        foreach (var slot in radio.SlotLabels.Keys.OrderBy(key => key))
        {
            if (!radio.Presets.ContainsKey(slot) && !radio.FrequencyOverrides.ContainsKey(slot))
                return slot;
        }

        for (var i = 0; i < ANPRCRadioComponent.MaxSlots; i++)
        {
            if (radio.SlotLabels.ContainsKey(i))
                continue;

            radio.SlotLabels[i] = label;
            return i;
        }

        return null;
    }

    /// <summary>
    ///     The nets a set in this operator's hands is expected to carry: their own squad's net,
    ///     then whatever the pack is issued with. Only nets the set is entitled to work.
    /// </summary>
    private List<ANPRCStandardNet> GetStandardNets(Entity<ANPRCRadioComponent> ent, EntityUid? viewer)
    {
        var nets = new List<ANPRCStandardNet>();

        // the squad is the wearer's, or for a planted station whoever is working it
        var person = ent.Comp.IsEquipped ? Transform(ent.Owner).ParentUid : viewer;

        if (person is { } member &&
            TryComp(member, out SquadMemberComponent? squadMember) &&
            TryComp(squadMember.Squad, out SquadTeamComponent? team) &&
            team.Radio is { } squadRadio &&
            _prototype.TryIndex(squadRadio, out var squadProto) &&
            squadProto.AnchorGated &&
            KnowsFrequency(ent.Comp, squadProto))
        {
            nets.Add(new ANPRCStandardNet(SquadNetLabel, squadRadio, true));
        }

        var issued = ent.Comp.StandardNets.Count > 0 ? ent.Comp.StandardNets : ent.Comp.DefaultSlots;

        foreach (var preset in issued)
        {
            if (nets.Any(net => net.Channel == preset.Channel))
                continue;

            if (!_prototype.TryIndex(preset.Channel, out var proto) || !KnowsFrequency(ent.Comp, proto))
                continue;

            // an inherited list can carry another side's nets. only the operator's own count
            if (!string.IsNullOrEmpty(ent.Comp.OperatorFaction) &&
                !string.Equals(proto.Faction, ent.Comp.OperatorFaction, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            nets.Add(new ANPRCStandardNet(preset.Label, preset.Channel, false));
        }

        return nets;
    }

    private ANPRCPanelInfo BuildPanelInfo(Entity<ANPRCRadioComponent> ent, float linkQuality)
    {
        var viewer = _ui.GetActors(ent.Owner, ANPRCRadioUI.Key).FirstOrDefault();
        var standard = GetStandardNets(ent, viewer.IsValid() ? viewer : null);

        var relaying = TryComp(ent.Owner, out ANPRCRelayAnchorComponent? anchor) && anchor.Channels.Count > 0;
        var relayed = anchor != null ? anchor.Channels.ToList() : new List<ProtoId<RadioChannelPrototype>>();
        var (full, partial) = relaying ? _range.GetAnchorRanges(ent.Owner) : (0f, 0f);

        var trained = !ent.Comp.IsEquipped || ent.Comp.Planted ||
                      HasComp<ANPRCRadioUserComponent>(Transform(ent.Owner).ParentUid);

        return new ANPRCPanelInfo(
            standard,
            relaying,
            relayed,
            full,
            partial,
            linkQuality,
            trained,
            HasComp<AU14CallsignConsoleComponent>(ent.Owner),
            ent.Comp.HandsetUser != null,
            ent.Comp.LastTransmit,
            ent.Comp.LastReceive);
    }

    /// <summary>
    ///     The set's link on the net it is working, as the range system gates traffic. Negative
    ///     when there is no answer: set down, searching, or on a raw frequency.
    /// </summary>
    private float GetLinkQuality(Entity<ANPRCRadioComponent> ent)
    {
        if (!ent.Comp.Enabled || (!ent.Comp.IsEquipped && !ent.Comp.Planted) || ent.Comp.SweepEnabled)
            return -1f;

        if (!ent.Comp.Presets.TryGetValue(ent.Comp.ActiveSlot, out var channel))
            return -1f;

        if (!_prototype.TryIndex(channel, out var proto) || !proto.AnchorGated)
            return -1f;

        _range.GetRangeTier(ent.Owner, channel.Id, out var quality);

        return quality;
    }

    // the link moves as the wearer walks and the battery drains without anybody touching the
    // set. both would otherwise sit stale until a setting changed, so an open panel is
    // re-pushed when either has drifted
    private void RefreshOpenPanels(float frameTime)
    {
        _panelRefreshAccumulator += frameTime;

        if (_panelRefreshAccumulator < PanelRefreshInterval)
            return;

        _panelRefreshAccumulator = 0f;

        var query = EntityQueryEnumerator<ANPRCRadioComponent>();

        while (query.MoveNext(out var uid, out var radio))
        {
            if (!_ui.IsUiOpen(uid, ANPRCRadioUI.Key))
                continue;

            var ent = new Entity<ANPRCRadioComponent>(uid, radio);
            var battery = _powerCell.TryGetBatteryFromSlot(uid, out var cell)
                ? _battery.GetChargeLevel(cell!.Value.AsNullable())
                : 0f;

            if (!Drifted(radio.PanelLinkQuality, GetLinkQuality(ent)) &&
                !Drifted(radio.PanelBatteryFraction, battery))
            {
                continue;
            }

            UpdateBuiState(ent);
        }
    }

    // NaN is the never-sent case and compares false either way, so a fresh panel always updates
    private static bool Drifted(float sent, float current) => !(MathF.Abs(sent - current) < 0.02f);
}
