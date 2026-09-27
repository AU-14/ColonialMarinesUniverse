using System.Numerics;
using Content.Server.Radio;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.CMU14.Radio;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Radio;

/// <summary>
///     The techniques only the faceplate reaches. None of them is needed to get on the air - the guided
///     panel runs the set on the factory AUTO settings and loses nothing it had - but every one of them
///     is something a trained operator can do better with the set than a newcomer can.
/// </summary>
public sealed partial class ANPRCRadioSystem
{
    // the factory AUTO settings the guided panel works the set on
    private const RadioTxPower AutoTxPower = RadioTxPower.Medium;
    private const RadioMode AutoMode = RadioMode.FrequencyHopping;
    private const int AutoSquelch = 3;

    private float _batteryDrainAccumulator;

    // retransmissions are sent on the next tick, never from inside the receive that heard them:
    // RadioReceiveEvent is raised while the radio system is still walking its receivers
    private readonly List<(EntityUid Pack, ProtoId<RadioChannelPrototype> Channel, string Message, ProtoId<LanguagePrototype> Language)> _retransQueue = new();

    private void InitializeExpert()
    {
        Subs.BuiEvents<ANPRCRadioComponent>(ANPRCRadioUI.Key, subs =>
        {
            subs.Event<ANPRCSetBurstMsg>(OnSetBurst);
            subs.Event<ANPRCSetPowerSaveMsg>(OnSetPowerSave);
            subs.Event<ANPRCSetPriorityWatchMsg>(OnSetPriorityWatch);
            subs.Event<ANPRCSetEmconMsg>(OnSetEmcon);
            subs.Event<ANPRCSetRetransMsg>(OnSetRetrans);
            subs.Event<ANPRCPeakAntennaMsg>(OnPeakAntenna);
            subs.Event<ANPRCOtarMsg>(OnOtar);
            subs.Event<ANPRCSetDwellMsg>(OnSetDwell);
            subs.Event<ANPRCJammerBearingMsg>(OnJammerBearing);
            subs.Event<ANPRCReturnToAutoMsg>(OnReturnToAuto);
        });

        SubscribeLocalEvent<ANPRCRadioComponent, ANPRCPeakDoAfterEvent>(OnPeakDoAfter);
    }

    #region Settings

    private void OnSetBurst(Entity<ANPRCRadioComponent> ent, ref ANPRCSetBurstMsg args)
    {
        ent.Comp.Burst = args.Enabled;
        Dirty(ent);
        UpdateBuiState(ent);
    }

    private void OnSetPowerSave(Entity<ANPRCRadioComponent> ent, ref ANPRCSetPowerSaveMsg args)
    {
        ent.Comp.PowerSave = args.Enabled;

        // a duty-cycled receiver cannot sit on every memory or watch a second one
        if (args.Enabled)
        {
            ent.Comp.ScanEnabled = false;
            ent.Comp.PriorityWatchSlot = -1;
        }

        Dirty(ent);
        UpdateEquippedChannels(ent);
        UpdateBuiState(ent);
    }

    private void OnSetPriorityWatch(Entity<ANPRCRadioComponent> ent, ref ANPRCSetPriorityWatchMsg args)
    {
        var slot = args.Slot;

        if (slot >= 0 && (!ent.Comp.Presets.ContainsKey(slot) || slot == ent.Comp.ActiveSlot))
            return;

        ent.Comp.PriorityWatchSlot = slot;

        if (slot >= 0)
            ent.Comp.PowerSave = false;

        Dirty(ent);
        UpdateEquippedChannels(ent);
        UpdateBuiState(ent);
    }

    private void OnSetEmcon(Entity<ANPRCRadioComponent> ent, ref ANPRCSetEmconMsg args)
    {
        ent.Comp.Emcon = args.Enabled;
        Dirty(ent);

        // going silent takes the set off the relay too. whatever it was anchoring goes dark
        UpdateRelayAnchor(ent);
        UpdateBuiState(ent);

        _cmChat.ChatMessageToOne(
            Loc.GetString(args.Enabled ? "anprc-emcon-on" : "anprc-emcon-off"),
            args.Actor);
    }

    private void OnSetRetrans(Entity<ANPRCRadioComponent> ent, ref ANPRCSetRetransMsg args)
    {
        if (args.SlotA < 0 || args.SlotB < 0)
        {
            ent.Comp.RetransSlotA = -1;
            ent.Comp.RetransSlotB = -1;
            Dirty(ent);
            UpdateEquippedChannels(ent);
            UpdateBuiState(ent);
            return;
        }

        if (!ent.Comp.Planted)
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-retrans-needs-staked"), args.Actor);
            return;
        }

        // two different memories, each holding a named net. a raw frequency has no relay to ride
        if (args.SlotA == args.SlotB ||
            !ent.Comp.Presets.ContainsKey(args.SlotA) ||
            !ent.Comp.Presets.ContainsKey(args.SlotB))
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-retrans-needs-nets"), args.Actor);
            return;
        }

        ent.Comp.RetransSlotA = args.SlotA;
        ent.Comp.RetransSlotB = args.SlotB;
        Dirty(ent);

        UpdateEquippedChannels(ent);
        UpdateBuiState(ent);

        _cmChat.ChatMessageToOne(
            Loc.GetString("anprc-retrans-bridged",
                ("a", ent.Comp.SlotLabels.GetValueOrDefault(args.SlotA, $"P{args.SlotA + 1}")),
                ("b", ent.Comp.SlotLabels.GetValueOrDefault(args.SlotB, $"P{args.SlotB + 1}"))),
            args.Actor);
    }

    private void OnReturnToAuto(Entity<ANPRCRadioComponent> ent, ref ANPRCReturnToAutoMsg args)
    {
        var radio = ent.Comp;

        radio.TxPower = AutoTxPower;
        radio.Mode = AutoMode;
        radio.SquelchLevel = AutoSquelch;
        radio.Callsign = string.Empty;
        radio.Burst = false;
        radio.PowerSave = false;
        radio.PriorityWatchSlot = -1;
        radio.Emcon = false;
        radio.RetransSlotA = -1;
        radio.RetransSlotB = -1;

        if (radio.SweepEnabled)
            _sweep.StopSweep(ent);

        Dirty(ent);

        UpdateEquippedChannels(ent);
        UpdateRelayAnchor(ent);
        UpdateBuiState(ent);

        _cmChat.ChatMessageToOne(Loc.GetString("anprc-return-to-auto-done"), args.Actor);
    }

    private static bool IsOffAuto(ANPRCRadioComponent radio)
    {
        return radio.TxPower != AutoTxPower ||
               radio.Mode != AutoMode ||
               radio.SquelchLevel != AutoSquelch ||
               !string.IsNullOrEmpty(radio.Callsign) ||
               radio.Burst ||
               radio.PowerSave ||
               radio.PriorityWatchSlot >= 0 ||
               radio.Emcon ||
               radio.RetransSlotA >= 0 ||
               radio.SweepEnabled;
    }

    #endregion

    #region Antenna peaking

    private void OnPeakAntenna(Entity<ANPRCRadioComponent> ent, ref ANPRCPeakAntennaMsg args)
    {
        if (!ent.Comp.Planted || !ent.Comp.Enabled)
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-peak-needs-staked"), args.Actor);
            return;
        }

        if (ent.Comp.AntennaPeaked)
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-peak-already"), args.Actor);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.Actor, ent.Comp.PeakDelay, new ANPRCPeakDoAfterEvent(),
            ent.Owner, target: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("anprc-peak-start"), ent.Owner, args.Actor);
    }

    private void OnPeakDoAfter(Entity<ANPRCRadioComponent> ent, ref ANPRCPeakDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Planted)
            return;

        args.Handled = true;

        ent.Comp.AntennaPeaked = true;
        Dirty(ent);

        UpdateRelayAnchor(ent);
        UpdateBuiState(ent);

        _popup.PopupEntity(Loc.GetString("anprc-peak-done"), ent.Owner, args.User, PopupType.Medium);
    }

    /// <summary>Everything a staked-only technique leaves behind, taken down with the set.</summary>
    private void ClearStakedTechniques(Entity<ANPRCRadioComponent> ent)
    {
        ent.Comp.AntennaPeaked = false;
        ent.Comp.RetransSlotA = -1;
        ent.Comp.RetransSlotB = -1;
        Dirty(ent);
    }

    #endregion

    #region Retrans

    /// <summary>
    ///     A staked set bridging two memories hears traffic on one and puts it out on the other, under
    ///     its own callsign with the original speaker named in the message. Traffic another set put on
    ///     the air is never repeated, so two bridges on the same pair of nets cannot ping-pong forever.
    /// </summary>
    private void TryQueueRetrans(Entity<ANPRCRadioComponent> ent, ref RadioReceiveEvent args)
    {
        var radio = ent.Comp;

        if (!radio.Planted || radio.Emcon || radio.RetransSlotA < 0 || radio.RetransSlotB < 0)
            return;

        if (args.MessageSource == ent.Owner || HasComp<ANPRCRadioComponent>(args.MessageSource))
            return;

        if (!radio.Presets.TryGetValue(radio.RetransSlotA, out var netA) ||
            !radio.Presets.TryGetValue(radio.RetransSlotB, out var netB))
        {
            return;
        }

        ProtoId<RadioChannelPrototype> target;

        if (args.Channel.ID == netA.Id)
            target = netB;
        else if (args.Channel.ID == netB.Id)
            target = netA;
        else
            return;

        var relayed = Loc.GetString("anprc-retrans-relayed",
            ("speaker", GetSenderDisplayName(args.MessageSource)),
            ("message", args.Message));

        _retransQueue.Add((ent.Owner, target, relayed, args.Language));
    }

    private void FlushRetrans()
    {
        if (_retransQueue.Count == 0)
            return;

        var queued = _retransQueue.ToArray();
        _retransQueue.Clear();

        foreach (var (pack, channelId, message, language) in queued)
        {
            if (TerminatingOrDeleted(pack) ||
                !TryComp(pack, out ANPRCRadioComponent? radio) ||
                !radio.Enabled ||
                !radio.Planted ||
                radio.Emcon ||
                !_prototype.TryIndex(channelId, out var channel))
            {
                continue;
            }

            if (!_powerCell.TryUseCharge(pack, GetTransmitCost(radio)))
                continue;

            // source is the set itself, OnRadioSpeakerName swaps in its callsign
            _radio.SendRadioMessage(pack, message, channel, pack, language);

            TryDirectionFind(pack, radio, channel, false);
            radio.LastTransmit = _timing.CurTime;
        }
    }

    #endregion

    #region Over-the-air rekey

    private void OnOtar(Entity<ANPRCRadioComponent> ent, ref ANPRCOtarMsg args)
    {
        var radio = ent.Comp;

        if (!radio.Enabled || (!radio.IsEquipped && !radio.Planted))
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-radio-off"), args.Actor);
            return;
        }

        if (radio.Emcon)
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-emcon-no-transmit"), args.Actor);
            return;
        }

        var faction = _crypto.GetFillFaction(ent);

        if (string.IsNullOrEmpty(faction) || _crypto.IsFillStale(ent))
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-otar-needs-current-fill"), args.Actor);
            return;
        }

        // the cooldown only starts once a key has actually been pushed
        if (radio.OtarLast != TimeSpan.Zero && _timing.CurTime < radio.OtarLast + radio.OtarCooldown)
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-otar-cooldown"), args.Actor);
            return;
        }

        var targets = FindOtarTargets(ent, faction);

        if (targets.Count == 0)
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-otar-none"), args.Actor);
            return;
        }

        if (!_powerCell.TryUseCharge(ent.Owner, radio.OtarChargeCost))
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-battery-insufficient"), args.Actor);
            return;
        }

        radio.OtarLast = _timing.CurTime;
        radio.LastTransmit = _timing.CurTime;

        var station = GetOnAirName(ent);

        foreach (var target in targets)
        {
            if (!_crypto.TryRekeyOverAir(target))
                continue;

            if (TryComp(target, out ANPRCRadioComponent? targetRadio) && targetRadio.IsEquipped)
            {
                var wearer = Transform(target).ParentUid;

                if (wearer.IsValid())
                {
                    _popup.PopupEntity(Loc.GetString("anprc-otar-received", ("station", station)),
                        wearer, wearer, PopupType.Medium);
                }
            }
        }

        // a key push is a long transmission. it rides the side's own net for the DF chance
        if (TryComp(ent, out ANPRCPhoneComponent? phone) &&
            phone.CoverageChannels.TryGetValue(faction, out var net) &&
            _prototype.TryIndex(net, out RadioChannelPrototype? netProto))
        {
            TryDirectionFind(ent.Owner, radio, netProto, false);
        }

        _cmChat.ChatMessageToOne(Loc.GetString("anprc-otar-sent", ("count", targets.Count)), args.Actor);
        UpdateBuiState(ent);
    }

    /// <summary>Sets on the same side still holding a superseded key that this set could reach over the air.</summary>
    private List<EntityUid> FindOtarTargets(Entity<ANPRCRadioComponent> ent, string faction)
    {
        var targets = new List<EntityUid>();
        var direct = CompOrNull<ANPRCPhoneComponent>(ent)?.DirectRange ?? ANPRCRangeSystem.PartialSignalRange;
        direct *= ent.Comp.TxPower.RangeMultiplier();

        var coveredHere = HasCoverage(ent);
        var query = EntityQueryEnumerator<ANPRCRadioComponent, ANPRCCryptoSlotComponent>();

        while (query.MoveNext(out var uid, out var other, out _))
        {
            if (uid == ent.Owner || !other.Enabled || (!other.IsEquipped && !other.Planted))
                continue;

            if (!string.Equals(_crypto.GetFillFaction(uid), faction, StringComparison.OrdinalIgnoreCase) ||
                !_crypto.IsFillStale(uid))
            {
                continue;
            }

            if (InDirectRange(ent.Owner, uid, direct) || coveredHere && HasCoverage((uid, other)))
                targets.Add(uid);
        }

        return targets;
    }

    #endregion

    #region Search dwell

    private void OnSetDwell(Entity<ANPRCRadioComponent> ent, ref ANPRCSetDwellMsg args)
    {
        if (args.Kilohertz < 0)
        {
            ent.Comp.SweepDwellKilohertz = -1;
            Dirty(ent);
            UpdateBuiState(ent);
            return;
        }

        if (!ent.Comp.SweepEnabled)
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-dwell-needs-search"), args.Actor);
            return;
        }

        // the panel only ever holds the masked number. find the contact it stands for
        foreach (var (frequency, confidence) in ent.Comp.SweepContacts)
        {
            if (ent.Comp.DiscoveredFrequencies.Contains(frequency))
                continue;

            var tier = ANPRCSweepSystem.TierOf(ent.Comp, confidence);

            if (ANPRCSweepSystem.MaskFrequency(frequency, tier).Kilohertz != args.Kilohertz)
                continue;

            ent.Comp.SweepDwellKilohertz = frequency.Kilohertz;
            ent.Comp.SweepDwellLastEmission = _timing.CurTime;
            ent.Comp.SweepPosition = frequency;
            Dirty(ent);

            UpdateBuiState(ent);
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-dwell-on"), args.Actor);
            return;
        }

        _cmChat.ChatMessageToOne(Loc.GetString("anprc-dwell-lost"), args.Actor);
    }

    #endregion

    #region Jammer direction finding

    private void OnJammerBearing(Entity<ANPRCRadioComponent> ent, ref ANPRCJammerBearingMsg args)
    {
        var radio = ent.Comp;

        if (!radio.Enabled || (!radio.IsEquipped && !radio.Planted))
        {
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-radio-off"), args.Actor);
            return;
        }

        if (!_garble.TryGetNearestJammer(ent.Owner, out var jammer, out var jammerPosition))
        {
            radio.JammerBearingTarget = null;
            _cmChat.ChatMessageToOne(Loc.GetString("anprc-jammer-none"), args.Actor);
            UpdateBuiState(ent);
            return;
        }

        var here = _transform.GetWorldPosition(ent.Owner);
        var bearing = CompassBearing(here, jammerPosition);
        var now = _timing.CurTime;

        var haveFirst = radio.JammerBearingTarget == jammer && now - radio.JammerBearingTime <= radio.JammerBearingExpiry;

        if (!haveFirst)
        {
            radio.JammerBearingTarget = jammer;
            radio.JammerBearingPosition = here;
            radio.JammerBearingTime = now;

            _cmChat.ChatMessageToOne(
                Loc.GetString("anprc-jammer-bearing-first",
                    ("bearing", FormatBearing(bearing)),
                    ("baseline", (int) radio.JammerFixBaseline)),
                args.Actor);

            UpdateBuiState(ent);
            return;
        }

        var moved = (here - radio.JammerBearingPosition).Length();

        if (moved < radio.JammerFixBaseline)
        {
            _cmChat.ChatMessageToOne(
                Loc.GetString("anprc-jammer-baseline-short",
                    ("bearing", FormatBearing(bearing)),
                    ("moved", (int) moved),
                    ("baseline", (int) radio.JammerFixBaseline)),
                args.Actor);

            UpdateBuiState(ent);
            return;
        }

        // two bearings far enough apart cross on the jammer
        radio.JammerBearingTarget = null;

        var viewer = radio.OperatorFaction.ToUpperInvariant();

        if (_tacticalMap.CreateFactionIntelBlip(jammer, string.Empty, viewer) is { } location)
        {
            Timer.Spawn(radio.JammerFixDuration,
                () => _tacticalMap.EraseFactionIntelBlip(location.GridId, location.Key, viewer));
        }

        var distance = (jammerPosition - here).Length();

        _cmChat.ChatMessageToOne(
            Loc.GetString("anprc-jammer-fixed",
                ("bearing", FormatBearing(bearing)),
                ("distance", (int) distance),
                ("minutes", (int) radio.JammerFixDuration.TotalMinutes)),
            args.Actor);

        UpdateBuiState(ent);
    }

    /// <summary>Compass bearing in degrees from one world position to another: 0 north, 90 east.</summary>
    public static float CompassBearing(Vector2 from, Vector2 to)
    {
        var offset = to - from;
        var degrees = MathF.Atan2(offset.X, offset.Y) * 180f / MathF.PI;
        return (degrees + 360f) % 360f;
    }

    private static string FormatBearing(float degrees)
    {
        return ((int) MathF.Round(degrees) % 360).ToString("000");
    }

    #endregion

    #region Battery

    /// <summary>What the set is drawing per second right now, without transmissions.</summary>
    private float GetDrawPerSecond(Entity<ANPRCRadioComponent> ent)
    {
        var radio = ent.Comp;

        if (!radio.Enabled || (!radio.IsEquipped && !radio.Planted))
            return 0f;

        var idle = radio.IdleChargePerSecond * (radio.Emcon
            ? radio.EmconIdleMultiplier
            : radio.PowerSave
                ? radio.PowerSaveIdleMultiplier
                : 1f);

        var relay = TryComp(ent, out ANPRCRelayAnchorComponent? anchor) && anchor.Channels.Count > 0
            ? radio.RelayChargePerSecond * radio.TxPower.ChargeMultiplier()
            : 0f;

        return idle + relay;
    }

    /// <summary>
    ///     Once a second: every set that is on and worn or staked pays its idle draw, plus the relay
    ///     surcharge while it anchors nets. The set owns this rather than PowerCellDraw, whose rate only
    ///     the power cell system may change, so POWER SAVE and EMCON can turn it down.
    /// </summary>
    private void UpdateBatteryDrain(float frameTime)
    {
        _batteryDrainAccumulator += frameTime;

        if (_batteryDrainAccumulator < 1f)
            return;

        var seconds = _batteryDrainAccumulator;
        _batteryDrainAccumulator = 0f;

        var query = EntityQueryEnumerator<ANPRCRadioComponent>();
        List<Entity<ANPRCRadioComponent>>? flat = null;

        while (query.MoveNext(out var uid, out var radio))
        {
            var ent = new Entity<ANPRCRadioComponent>(uid, radio);
            var draw = GetDrawPerSecond(ent);

            if (draw <= 0f || !_powerCell.TryGetBatteryFromSlot(uid, out var battery))
                continue;

            _battery.UseCharge(battery.Value.AsNullable(), draw * seconds);

            if (_battery.GetCharge(battery.Value.AsNullable()) <= 0f)
            {
                flat ??= new List<Entity<ANPRCRadioComponent>>();
                flat.Add(ent);
            }
        }

        if (flat == null)
            return;

        foreach (var ent in flat)
        {
            HandleBatteryEmpty(ent);
        }
    }

    #endregion

    /// <summary>The faceplate's extra readings and technique state, built alongside the regular panel state.</summary>
    private ANPRCExpertState BuildExpertState(Entity<ANPRCRadioComponent> ent)
    {
        var radio = ent.Comp;
        var now = _timing.CurTime;

        var state = new ANPRCExpertState
        {
            Burst = radio.Burst,
            PowerSave = radio.PowerSave,
            PriorityWatchSlot = radio.PriorityWatchSlot,
            Emcon = radio.Emcon,
            RetransSlotA = radio.RetransSlotA,
            RetransSlotB = radio.RetransSlotB,
            AntennaPeaked = radio.AntennaPeaked,
            OffAuto = IsOffAuto(radio),
            Jammed = radio.Enabled && _garble.GetJamIntensity(ent.Owner) != RadioJamIntensity.None,
            JammerBaselineNeeded = radio.JammerFixBaseline,
            DrawPerSecond = GetDrawPerSecond(ent),
        };

        if (radio.SweepDwellKilohertz >= 0)
        {
            var real = RadioFrequency.FromKilohertz(radio.SweepDwellKilohertz);
            var tier = ANPRCSweepSystem.TierOf(radio, radio.SweepContacts.GetValueOrDefault(real));
            state.SweepDwellKilohertz = ANPRCSweepSystem.MaskFrequency(real, tier).Kilohertz;
        }

        var faction = _crypto.GetFillFaction(ent);

        if (!string.IsNullOrEmpty(faction) && !_crypto.IsFillStale(ent) && radio.Enabled)
        {
            state.OtarTargets = FindOtarTargets(ent, faction).Count;
            state.OtarReady = state.OtarTargets > 0 && !radio.Emcon &&
                              (radio.OtarLast == TimeSpan.Zero || now >= radio.OtarLast + radio.OtarCooldown);
        }

        if (radio.JammerBearingTarget is { } target &&
            !TerminatingOrDeleted(target) &&
            now - radio.JammerBearingTime <= radio.JammerBearingExpiry)
        {
            state.JammerBearingTaken = true;
            state.JammerBaselineMoved = (_transform.GetWorldPosition(ent.Owner) - radio.JammerBearingPosition).Length();

            if (_garble.TryGetNearestJammer(ent.Owner, out var jammer, out var jammerPosition) && jammer == target)
                state.JammerBearingDegrees = CompassBearing(_transform.GetWorldPosition(ent.Owner), jammerPosition);
        }

        if (TryGetCarrier(ent, out var carrier, out var distance, out var bearing))
        {
            state.CarrierName = TryComp(carrier, out ANPRCRadioComponent? carrierRadio)
                ? GetOnAirName((carrier, carrierRadio))
                : Name(carrier);
            state.CarrierDistance = distance;
            state.CarrierBearingDegrees = bearing;
        }

        if (state.DrawPerSecond > 0f &&
            _powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
        {
            state.BatteryMinutes = _battery.GetCharge(battery.Value.AsNullable()) / state.DrawPerSecond / 60f;
        }

        return state;
    }

    /// <summary>
    ///     The nearest other anchor that carries the net the set is working - the relay this set is actually
    ///     talking through.
    /// </summary>
    private bool TryGetCarrier(Entity<ANPRCRadioComponent> ent, out EntityUid carrier, out float distance, out float bearing)
    {
        carrier = default;
        distance = float.MaxValue;
        bearing = float.NaN;

        if (!ent.Comp.Enabled || !ent.Comp.Presets.TryGetValue(ent.Comp.ActiveSlot, out var channel))
            return false;

        var packXform = Transform(ent);
        var packPos = _transform.GetWorldPosition(packXform);
        var query = EntityQueryEnumerator<ANPRCRelayAnchorComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var anchor, out var xform))
        {
            if (uid == ent.Owner || !anchor.Channels.Contains(channel))
                continue;

            if (!_range.InVerticalReach(xform.MapID, packXform.MapID, anchor.LevelReach))
                continue;

            if (TryComp(uid, out Content.Server.Power.Components.ApcPowerReceiverComponent? power) && !power.Powered)
                continue;

            var (_, partial) = _range.GetAnchorRanges(uid);
            var position = _transform.GetWorldPosition(xform);
            var span = (position - packPos).Length();

            if (span > partial || span >= distance)
                continue;

            carrier = uid;
            distance = span;
            bearing = CompassBearing(packPos, position);
        }

        return carrier.IsValid();
    }
}
