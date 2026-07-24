using System.Diagnostics.CodeAnalysis;
using Content.Server._RMC14.Marines.Roles.Ranks;
using Content.Server._RMC14.TacticalMap;
using Content.Server.Chat.Managers;
using Content.Shared._AU14.CCVar;
using Content.Server._AU14.Callsigns;
using Content.Shared._AU14.Callsigns;
using Content.Server.Chat.Systems;
using Content.Server.PowerCell;
using Content.Server.Radio;
using Content.Server.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared._AU14.Radio;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.PropCalling;
using Content.Shared.Chat;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Paper;
using Content.Shared.Item;
using Content.Shared.PowerCell;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._AU14.Radio;

public sealed partial class ANPRCRadioSystem : EntitySystem
{
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedCMChatSystem _cmChat = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedAU14CallsignConsoleSystem _consoleAccess = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private TunableFrequencySystem _tunable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private ANPRCCryptoSystem _crypto = default!;
    [Dependency] private ANPRCFrequencyPlanSystem _freqPlan = default!;
    [Dependency] private ANPRCGarbleSystem _garble = default!;
    [Dependency] private ANPRCRangeSystem _range = default!;
    [Dependency] private ANPRCSweepSystem _sweep = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private TacticalMapSystem _tacticalMap = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private INetManager _netManager = default!;

    public static readonly ProtoId<RadioChannelPrototype> ANPRCSentinelChannel = "ANPRCActiveChannel";

    public const string AntennaSlotId = "antenna";

    private bool _commsEnabled;

    public override void Initialize()
    {
        Subs.CVar(_config, AU14CCVars.NewCommsSystem, v => _commsEnabled = v, true);

        SubscribeLocalEvent<ANPRCRadioComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ANPRCRadioComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<WearingANPRCComponent, ChatGetPrefixEvent>(OnChatGetPrefix);
        SubscribeLocalEvent<WearingANPRCComponent, EntitySpokeEvent>(
            OnSpeak,
            before: [typeof(HeadsetSystem)]);

        // has to run after RankSystem so the callsign overrides the rank+name it writes
        // into the event. note the event bus wants all of a system's subscriptions to one
        // event to share the same ordering constraints, so both get the same after:
        SubscribeLocalEvent<WearingANPRCComponent, TransformSpeakerNameEvent>(
            OnWearerSpeakerName,
            after: [typeof(RankSystem)]);
        SubscribeLocalEvent<ANPRCRadioComponent, TransformSpeakerNameEvent>(
            OnRadioSpeakerName,
            after: [typeof(RankSystem)]);
        SubscribeLocalEvent<ANPRCHandsetUserComponent, TransformSpeakerNameEvent>(
            OnHandsetSpeakerName,
            after: [typeof(RankSystem)]);

        SubscribeLocalEvent<ANPRCHandsetUserComponent, ChatGetPrefixEvent>(OnHandsetChatGetPrefix);
        SubscribeLocalEvent<ANPRCHandsetUserComponent, EntitySpokeEvent>(
            OnHandsetSpeak,
            before: [typeof(HeadsetSystem)]);

        SubscribeLocalEvent<ANPRCRadioComponent, MapInitEvent>(OnRadioMapInit);
        SubscribeLocalEvent<ANPRCHandsetComponent, GotEquippedHandEvent>(OnHandsetEquippedHand);
        SubscribeLocalEvent<ANPRCHandsetComponent, GotUnequippedHandEvent>(OnHandsetUnequippedHand);
        SubscribeLocalEvent<ANPRCHandsetComponent, UseInHandEvent>(OnHandsetUseInHand);

        SubscribeLocalEvent<ANPRCRadioComponent, RadioReceiveEvent>(OnRadioReceive);
        SubscribeLocalEvent<ANPRCRadioComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<WearingANPRCComponent, GetVerbsEvent<AlternativeVerb>>(OnWearerGetAltVerbs);

        Subs.BuiEvents<ANPRCRadioComponent>(ANPRCRadioUI.Key, subs =>
        {
            subs.Event<ANPRCSelectSlotMsg>(OnSelectSlot);
            subs.Event<ANPRCTogglePowerMsg>(OnTogglePower);
            subs.Event<ANPRCToggleMonitorMsg>(OnToggleMonitor);
            subs.Event<ANPRCSetModeMsg>(OnSetMode);
            subs.Event<ANPRCSetScanMsg>(OnSetScan);
            subs.Event<ANPRCSetTxPowerMsg>(OnSetTxPower);
            subs.Event<ANPRCSetSquelchMsg>(OnSetSquelch);
            subs.Event<ANPRCSetCallsignMsg>(OnSetCallsign);
            subs.Event<ANPRCAddSlotMsg>(OnAddSlot);
            subs.Event<ANPRCDeleteSlotMsg>(OnDeleteSlot);
            subs.Event<ANPRCSetSlotChannelMsg>(OnSetSlotChannel);
            subs.Event<ANPRCClearSlotMsg>(OnClearSlot);
            subs.Event<ANPRCManualFrequencyMsg>(OnManualFrequency);
            subs.Event<ANPRCRadioCheckMsg>(OnRadioCheck);
            subs.Event<ANPRCOpenDirectoryMsg>(OnOpenDirectory);
            subs.Event<ANPRCSetSweepMsg>(OnSetSweep);
            subs.Event<ANPRCTuneContactMsg>(OnTuneContact);
            subs.Event<ANPRCPrintLogMsg>(OnPrintLog);
        });

        SubscribeLocalEvent<ANPRCRadioComponent, ANPRCPlantDoAfterEvent>(OnPlantDoAfter);
        SubscribeLocalEvent<ANPRCRadioComponent, ANPRCPackUpDoAfterEvent>(OnPackUpDoAfter);
        SubscribeLocalEvent<ANPRCRadioComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);

        // PropCaller only exists on RMCAdminObserver, so this hands aghosts radio
        // training without editing the upstream prototype file
        SubscribeLocalEvent<PropCallerComponent, MapInitEvent>(OnAdminObserverMapInit);

        SubscribeLocalEvent<ANPRCRadioComponent, ANPRCDirectScanSwitchedEvent>(OnDirectScanSwitched);
        SubscribeLocalEvent<ANPRCRadioComponent, ANPRCSweepStoppedEvent>(OnSweepStopped);
        SubscribeLocalEvent<ANPRCRadioComponent, ANPRCSweepUpdatedEvent>(OnSweepUpdated);
        SubscribeLocalEvent<ANPRCRadioComponent, ANPRCCryptoChangedEvent>(OnCryptoChanged);
        SubscribeLocalEvent<ANPRCRadioComponent, PowerCellSlotEmptyEvent>(OnBatteryEmpty);
        SubscribeLocalEvent<ANPRCRadioComponent, EntInsertedIntoContainerMessage>(OnAntennaInserted);
        SubscribeLocalEvent<ANPRCRadioComponent, EntRemovedFromContainerMessage>(OnAntennaRemoved);
    }

    private void OnRadioReceive(Entity<ANPRCRadioComponent> ent, ref RadioReceiveEvent args)
    {
        // overhaul off = no interception, net logging or scan switching, plain radio
        if (!_commsEnabled)
            return;

        var radio = ent.Comp;

        if (!radio.Enabled || (!radio.IsEquipped && !radio.Planted))
            return;

        var wearer = Transform(ent.Owner).ParentUid;

        if (!wearer.IsValid())
            return;

        if (args.MessageSource != wearer &&
            TryComp(args.MessageSource, out WearingANPRCComponent? ctWearing) &&
            TryComp(ctWearing.Radio, out ANPRCRadioComponent? ctRadio) &&
            ctRadio.Mode == RadioMode.CipherText &&
            !string.IsNullOrEmpty(args.Channel.Faction) &&
            !_crypto.HasMatchingCrypto(ent.Owner, args.Channel))
        {
            return;
        }

        // the wearer's headset or their own intrinsic receiver (a held handset)
        // already delivers this channel, the pack must not double it up
        var covered = false;

        if (TryComp(wearer, out WearingHeadsetComponent? wearingHeadset) &&
            TryComp(wearingHeadset.Headset, out EncryptionKeyHolderComponent? keys))
        {
            covered = keys.Channels.Contains(args.Channel.ID);
        }

        if (!covered &&
            HasComp<IntrinsicRadioReceiverComponent>(wearer) &&
            TryComp(wearer, out ActiveRadioComponent? wearerRadio) &&
            wearerRadio.Channels.Contains(args.Channel.ID))
        {
            covered = true;
        }

        if (args.MessageSource == wearer)
        {
            // sidetone: the operator hears their own traffic back through the pack on
            // nets their headset can't receive, proof the message actually went out
            if (!covered && TryComp(wearer, out ActorComponent? selfActor))
                _netManager.ServerSendMessage(args.ChatMsg, selfActor.PlayerSession.Channel);
        }
        else
        {
            var heard = _garble.ApplyComsecGarble(args.MessageSource, ent.Owner, args.Channel, args.Message);

            // traffic on somebody else's faction net is an intercept: flagged in the
            // log so it can be picked back out and carried off the radio on paper
            var intercepted = !string.IsNullOrEmpty(args.Channel.Faction) &&
                              !string.Equals(
                                  args.Channel.Faction,
                                  radio.OperatorFaction,
                                  StringComparison.OrdinalIgnoreCase);

            AppendNetLog(
                radio,
                _timing.CurTime.TotalSeconds,
                GetSenderDisplayName(args.MessageSource),
                $"{args.Channel.LocalizedName} ({TunableFrequencySystem.FormatFreq(_freqPlan.GetFrequency(args.Channel))} MHz)",
                heard,
                intercepted);

            UpdateBuiState(ent);

            if (!covered && TryComp(wearer, out ActorComponent? actor))
            {
                var senderName = FormattedMessage.EscapeText(GetSenderDisplayName(args.MessageSource));
                var message = FormattedMessage.EscapeText(heard);
                var wrapped = $"[color=#FF6B6B]{senderName}: {message}[/color]";

                _chatManager.ChatMessageToOne(
                    ChatChannel.Radio,
                    heard,
                    wrapped,
                    args.MessageSource,
                    false,
                    actor.PlayerSession.Channel);
            }
        }

        if (!radio.ScanEnabled)
            return;

        foreach (var (slot, channel) in radio.Presets)
        {
            if (channel.Id != args.Channel.ID || slot == radio.ActiveSlot)
                continue;

            radio.ActiveSlot = slot;
            Dirty(ent);

            UpdateRelayAnchor(ent);
            UpdateBuiState(ent);

            _cmChat.ChatMessageToOne(
                Loc.GetString(
                    "anprc-scan-switched",
                    ("slot", slot + 1),
                    ("label", radio.SlotLabels.TryGetValue(slot, out var label) ? label : $"P{slot + 1}"),
                    ("channel", args.Channel.LocalizedName)),
                wearer);

            return;
        }
    }

    private void OnEquipped(Entity<ANPRCRadioComponent> ent, ref GotEquippedEvent args)
    {
        if (args.Slot != ent.Comp.RequiredSlot)
            return;

        ent.Comp.IsEquipped = true;
        Dirty(ent);

        var wearer = EnsureComp<WearingANPRCComponent>(args.Equipee);
        wearer.Radio = ent.Owner;
        wearer.PendingANPRCTransmit = false;

        SetBatteryDrawEnabled(ent.Owner, ent.Comp.Enabled);
        GrantReceiveChannels(ent);
        UpdateRelayAnchor(ent);
        // a panel opened before donning the pack would otherwise stay greyed out on
        // its stale not-worn state
        UpdateBuiState(ent);

        if (TryComp(ent.Owner, out RTORelayComponent? relay))
        {
            relay.Active = true;
            Dirty(ent.Owner, relay);
        }

        if (!HasComp<ANPRCRadioUserComponent>(args.Equipee))
        {
            _popup.PopupEntity(
                Loc.GetString("anprc-not-rto-warning"),
                args.Equipee,
                args.Equipee,
                PopupType.MediumCaution);
        }
    }

    private void OnUnequipped(Entity<ANPRCRadioComponent> ent, ref GotUnequippedEvent args)
    {
        if (args.Slot != ent.Comp.RequiredSlot)
            return;

        ent.Comp.IsEquipped = false;
        Dirty(ent);

        RemComp<WearingANPRCComponent>(args.Equipee);

        SetBatteryDrawEnabled(ent.Owner, false);
        RevokeReceiveChannels(ent);
        RemComp<ANPRCRelayAnchorComponent>(ent.Owner);
        UpdateBuiState(ent);

        if (TryComp(ent.Owner, out RTORelayComponent? relay))
        {
            relay.Active = false;
            Dirty(ent.Owner, relay);
        }
    }

    private void GrantReceiveChannels(Entity<ANPRCRadioComponent> radio)
    {
        if (!radio.Comp.Enabled || (!radio.Comp.IsEquipped && !radio.Comp.Planted))
            return;

        // the set has one receiver. searching the band means it is not sitting on any
        // of the operator's own nets - going deaf is the price of hunting
        if (radio.Comp.SweepEnabled)
            return;

        var active = EnsureComp<ActiveRadioComponent>(radio.Owner);

        if (radio.Comp.MonitorEnabled || radio.Comp.ScanEnabled)
        {
            foreach (var channel in radio.Comp.Presets.Values)
            {
                GrantChannel(radio.Comp, active, channel);
            }

            return;
        }

        if (radio.Comp.ActiveSlot < 0 ||
            !radio.Comp.Presets.TryGetValue(radio.Comp.ActiveSlot, out var activeChannel))
        {
            return;
        }

        GrantChannel(radio.Comp, active, activeChannel);
    }

    private static void GrantChannel(
        ANPRCRadioComponent radio,
        ActiveRadioComponent active,
        ProtoId<RadioChannelPrototype> channel)
    {
        if (string.IsNullOrEmpty(channel.Id))
            return;

        if (active.Channels.Add(channel))
            radio.GrantedChannels.Add(channel);
    }

    private void RevokeReceiveChannels(Entity<ANPRCRadioComponent> radio)
    {
        if (radio.Comp.GrantedChannels.Count == 0)
            return;

        if (TryComp(radio.Owner, out ActiveRadioComponent? active))
        {
            foreach (var channel in radio.Comp.GrantedChannels)
            {
                active.Channels.Remove(channel);
            }

            if (active.Channels.Count == 0)
                RemComp<ActiveRadioComponent>(radio.Owner);
        }

        radio.Comp.GrantedChannels.Clear();
    }

    // ItemSlotsSystem.TryGetSlot resolves with logMissing on, so calling it directly logs
    // an error for any set that has no ItemSlots component. AllComponentsOneToOneDeleteTest
    // builds a bare entity carrying only ANPRCRadio, which hits exactly that. always come
    // through here rather than calling TryGetSlot on a radio directly
    private bool TryGetRadioSlot(EntityUid uid, string slotId, [NotNullWhen(true)] out ItemSlot? slot)
    {
        slot = null;

        return TryComp(uid, out ItemSlotsComponent? slots) &&
               _itemSlots.TryGetSlot(uid, slotId, out slot, slots);
    }

    private void UpdateRelayAnchor(Entity<ANPRCRadioComponent> ent)
    {
        if ((!ent.Comp.IsEquipped && !ent.Comp.Planted) || !ent.Comp.Enabled)
        {
            RemComp<ANPRCRelayAnchorComponent>(ent.Owner);
            return;
        }

        // a worn pack only anchors nets for a trained operator, on anyone else its dead
        // weight. planted packs anchor unattended
        if (!ent.Comp.Planted)
        {
            var packWearer = Transform(ent.Owner).ParentUid;

            if (!HasComp<ANPRCRadioUserComponent>(packWearer))
            {
                RemComp<ANPRCRelayAnchorComponent>(ent.Owner);
                return;
            }
        }

        var anchor = EnsureComp<ANPRCRelayAnchorComponent>(ent.Owner);
        anchor.Channels.Clear();

        var rangeMultiplier = ent.Comp.TxPower.RangeMultiplier();

        anchor.RangeMultiplier = rangeMultiplier;
        anchor.Planted = ent.Comp.Planted;

        if (TryGetRadioSlot(ent.Owner, AntennaSlotId, out var antennaSlot) &&
            TryComp(antennaSlot.Item, out ANPRCAntennaComponent? antenna))
        {
            anchor.FullRange = antenna.FullRange * rangeMultiplier;
            anchor.PartialRange = antenna.PartialRange * rangeMultiplier;
            anchor.RequiresStationary = antenna.RequiresStationary;
            anchor.MovingRangeMultiplier = antenna.MovingRangeMultiplier;
        }
        else
        {
            anchor.FullRange = ANPRCRangeSystem.FullSignalRange * rangeMultiplier;
            anchor.PartialRange = ANPRCRangeSystem.PartialSignalRange * rangeMultiplier;
            anchor.RequiresStationary = false;
            anchor.MovingRangeMultiplier = 1f;
        }

        foreach (var channel in ent.Comp.Presets.Values)
        {
            if (string.IsNullOrEmpty(channel.Id))
                continue;

            if (!string.IsNullOrEmpty(ent.Comp.OperatorFaction) &&
                (!_prototype.TryIndex(channel, out var proto) || proto.Faction != ent.Comp.OperatorFaction))
            {
                continue;
            }

            anchor.Channels.Add(channel);
        }
    }

    public void StartPlant(Entity<ANPRCRadioComponent> ent, EntityUid user)
    {
        if (ent.Comp.Planted || ent.Comp.IsEquipped || _container.IsEntityInContainer(ent.Owner))
            return;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            TimeSpan.FromSeconds(2),
            new ANPRCPlantDoAfterEvent(),
            ent.Owner,
            target: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = false
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    public void StartPackUp(Entity<ANPRCRadioComponent> ent, EntityUid user)
    {
        if (!ent.Comp.Planted)
            return;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            TimeSpan.FromSeconds(3),
            new ANPRCPackUpDoAfterEvent(),
            ent.Owner,
            target: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = false
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnPlantDoAfter(Entity<ANPRCRadioComponent> ent, ref ANPRCPlantDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Planted)
            return;

        args.Handled = true;

        ent.Comp.Planted = true;
        Dirty(ent);

        SetBatteryDrawEnabled(ent.Owner, ent.Comp.Enabled);
        GrantReceiveChannels(ent);
        UpdateRelayAnchor(ent);
        UpdatePackVisuals(ent);
        UpdateBuiState(ent);

        _popup.PopupEntity(Loc.GetString("anprc-retrans-planted"), ent.Owner, args.User);
    }

    private void OnPackUpDoAfter(Entity<ANPRCRadioComponent> ent, ref ANPRCPackUpDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Planted)
            return;

        args.Handled = true;

        ent.Comp.Planted = false;
        Dirty(ent);

        if (!ent.Comp.IsEquipped)
        {
            SetBatteryDrawEnabled(ent.Owner, false);
            RevokeReceiveChannels(ent);
            RemComp<ANPRCRelayAnchorComponent>(ent.Owner);
        }

        UpdatePackVisuals(ent);
        UpdateBuiState(ent);

        _popup.PopupEntity(Loc.GetString("anprc-retrans-packed"), ent.Owner, args.User);
    }

    private void OnPickupAttempt(Entity<ANPRCRadioComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (!ent.Comp.Planted)
            return;

        args.Cancel();

        _popup.PopupEntity(Loc.GetString("anprc-retrans-pickup-blocked"), ent.Owner, args.User);
    }

    private void SetBatteryDrawEnabled(EntityUid uid, bool enabled)
    {
        if (!TryComp(uid, out PowerCellDrawComponent? draw) || draw.Enabled == enabled)
            return;

        draw.Enabled = enabled;
        Dirty(uid, draw);
    }

    private void OnBatteryEmpty(Entity<ANPRCRadioComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        if (!ent.Comp.Enabled)
            return;

        ent.Comp.Enabled = false;
        Dirty(ent);

        SetBatteryDrawEnabled(ent.Owner, false);
        RevokeReceiveChannels(ent);
        UpdateRelayAnchor(ent);
        UpdateBuiState(ent);

        var wearer = Transform(ent.Owner).ParentUid;

        if (wearer.IsValid())
        {
            _popup.PopupEntity(
                Loc.GetString("anprc-battery-empty"),
                wearer,
                wearer,
                PopupType.MediumCaution);
        }
    }

    private void OnAntennaInserted(EntityUid uid, ANPRCRadioComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != AntennaSlotId)
            return;

        var ent = new Entity<ANPRCRadioComponent>(uid, comp);

        UpdateRelayAnchor(ent);
        UpdatePackVisuals(ent);
        UpdateBuiState(ent);
    }

    private void OnAntennaRemoved(EntityUid uid, ANPRCRadioComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != AntennaSlotId)
            return;

        var ent = new Entity<ANPRCRadioComponent>(uid, comp);

        UpdateRelayAnchor(ent);
        UpdatePackVisuals(ent);
        UpdateBuiState(ent);
    }

    // the ground/item sprite tracks the fitted antenna, planting swaps to the tall
    // deployed station art
    private void UpdatePackVisuals(Entity<ANPRCRadioComponent> ent)
    {
        var mast = false;
        var state = ANPRCPackVisualState.Bare;

        if (TryGetRadioSlot(ent.Owner, AntennaSlotId, out var slot) &&
            TryComp(slot.Item, out ANPRCAntennaComponent? antenna))
        {
            // the mast is the only stationary antenna, anything else draws as a whip
            mast = antenna.RequiresStationary;
            state = mast ? ANPRCPackVisualState.Mast : ANPRCPackVisualState.Whip;
        }

        if (ent.Comp.Planted)
            state = mast ? ANPRCPackVisualState.DeployedMast : ANPRCPackVisualState.DeployedWhip;

        _appearance.SetData(ent.Owner, ANPRCPackVisuals.State, state);
    }

    private void OnAdminObserverMapInit(Entity<PropCallerComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<ANPRCRadioUserComponent>(ent);
    }

    private void OnCryptoChanged(Entity<ANPRCRadioComponent> ent, ref ANPRCCryptoChangedEvent args)
    {
        UpdateBuiState(ent);
    }

    // the pack carries a read-only copy of the faction's comms net directory. a captured
    // enemy pack stays shut: CanView gates on the viewer's own faction
    private void OnOpenDirectory(Entity<ANPRCRadioComponent> ent, ref ANPRCOpenDirectoryMsg args)
    {
        if (!TryComp(ent.Owner, out AU14CallsignConsoleComponent? directory))
            return;

        if (!_consoleAccess.CanView((ent.Owner, directory), args.Actor))
        {
            _popup.PopupEntity(Loc.GetString("au14-callsign-console-wrong-faction"), ent.Owner, args.Actor);
            return;
        }

        _ui.TryOpenUi(ent.Owner, AU14CallsignConsoleUI.Key, args.Actor);
    }

    private void OnDirectScanSwitched(Entity<ANPRCRadioComponent> ent, ref ANPRCDirectScanSwitchedEvent args)
    {
        UpdateBuiState(ent);
    }

    private void OnSweepStopped(Entity<ANPRCRadioComponent> ent, ref ANPRCSweepStoppedEvent args)
    {
        UpdateEquippedChannels(ent);
        UpdateBuiState(ent);
    }

    private void OnSweepUpdated(Entity<ANPRCRadioComponent> ent, ref ANPRCSweepUpdatedEvent args)
    {
        UpdateBuiState(ent);
    }

    private void UpdateEquippedChannels(Entity<ANPRCRadioComponent> ent)
    {
        RevokeReceiveChannels(ent);
        GrantReceiveChannels(ent);
    }

    // what a listener sees as the message source. ANPRC traffic is callsign only,
    // never the operator's name
    private string GetSenderDisplayName(EntityUid source)
    {
        // radio checks come from the radio entity itself
        if (TryComp(source, out ANPRCRadioComponent? sourceRadio))
            return GetOnAirName((source, sourceRadio));

        // ANPRC voice traffic, source is the wearer masked mid-transmit
        if (TryComp(source, out WearingANPRCComponent? wearing) &&
            TryComp(wearing.Radio, out ANPRCRadioComponent? radio) &&
            radio.NameMaskActive)
        {
            return GetOnAirName((wearing.Radio, radio));
        }

        // handset traffic, guest speaker's own callsign or else the station's
        if (TryComp(source, out ANPRCHandsetUserComponent? handset) &&
            TryComp(handset.Radio, out ANPRCRadioComponent? handsetRadio) &&
            handsetRadio.NameMaskActive)
        {
            return GetHandsetOnAirName(source, (handset.Radio, handsetRadio));
        }

        // headset traffic from a faction member with an assigned callsign
        if (TryComp(source, out AU14CallsignComponent? callsign) &&
            !string.IsNullOrEmpty(callsign.Callsign) &&
            callsign.RadioMaskTick == _timing.CurTick)
        {
            return callsign.Callsign;
        }

        return Name(source);
    }

    private static bool HasPreset(ANPRCRadioComponent radio, string channelId)
    {
        foreach (var preset in radio.Presets.Values)
        {
            if (!string.IsNullOrEmpty(preset.Id) && preset.Id == channelId)
                return true;
        }

        return false;
    }

    private static void AppendNetLog(
        ANPRCRadioComponent radio,
        double timestamp,
        string sender,
        string channel,
        string message,
        bool intercepted = false)
    {
        radio.NetLog.Enqueue(new ANPRCNetLogEntry((float) timestamp, sender, channel, message, intercepted));

        while (radio.NetLog.Count > ANPRCRadioComponent.MaxNetLogEntries)
        {
            radio.NetLog.Dequeue();
        }
    }
}
