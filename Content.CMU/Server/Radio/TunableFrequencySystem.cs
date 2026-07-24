using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._AU14.CCVar;
using Content.Shared._AU14.Radio;
using Content.Shared._RMC14.Chat;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Replays;
using Robust.Shared.Utility;

namespace Content.Server._AU14.Radio;

public sealed partial class TunableFrequencySystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private IReplayRecordingManager _replay = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedCMChatSystem _cmChat = default!;
    [Dependency] private ANPRCGarbleSystem _garble = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private ANPRCRangeSystem _range = default!;
    [Dependency] private ANPRCSweepSystem _sweep = default!;

    // direct frequencies reach one z-level up or down, same as a worn manpack
    private const int DirectFreqLevelReach = 1;

    private static readonly ProtoId<RadioChannelPrototype> TunableSentinel = "TunableFrequencyChannel";

    public const float FullRange = 30f;
    public const float PartialRange = 50f;

    private bool _commsEnabled;

    public override void Initialize()
    {
        Subs.CVar(_config, AU14CCVars.NewCommsSystem, v => _commsEnabled = v, true);

        SubscribeLocalEvent<HeadsetComponent, MapInitEvent>(OnHeadsetMapInit);
        SubscribeLocalEvent<TunableHeadsetComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<TunableHeadsetComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<TunedFrequencyComponent, EntitySpokeEvent>(
            OnSpoke,
            before: [typeof(HeadsetSystem)]);

        SubscribeLocalEvent<TunableHeadsetComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
        SubscribeLocalEvent<TunableHeadsetComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);

        Subs.BuiEvents<TunableHeadsetComponent>(TunableFrequencyUI.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<TunableFrequencySetMsg>(OnSetFrequency);
        });
    }

    private void OnHeadsetMapInit(Entity<HeadsetComponent> ent, ref MapInitEvent args)
    {
        if (!HasComp<TunableHeadsetComponent>(ent))
            AddComp<TunableHeadsetComponent>(ent.Owner);

        _ui.SetUi(
            ent.Owner,
            TunableFrequencyUI.Key,
            new InterfaceData("TunableFrequencyBoundUserInterface"));
    }

    private void OnEquipped(Entity<TunableHeadsetComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.RequiredSlots) == SlotFlags.NONE)
            return;

        var wearer = EnsureComp<TunedFrequencyComponent>(args.Equipee);
        wearer.Source = ent.Owner;
        wearer.Frequency = ent.Comp.TunedFrequency;

        UpdateBuiState(ent);
    }

    private void OnUnequipped(Entity<TunableHeadsetComponent> ent, ref GotUnequippedEvent args)
    {
        if (TryComp(args.Equipee, out TunedFrequencyComponent? wearer) && wearer.Source == ent.Owner)
            RemComp<TunedFrequencyComponent>(args.Equipee);
    }

    private void OnSpoke(Entity<TunedFrequencyComponent> ent, ref EntitySpokeEvent args)
    {
        if (args.Channel?.ID != TunableSentinel.Id)
            return;

        args.Channel = null;

        // with the comms overhaul off the direct frequency layer is dead, :x stays local
        if (!_commsEnabled)
            return;

        if (!TryComp(ent.Comp.Source, out HeadsetComponent? headset) ||
            !headset.IsEquipped ||
            !headset.Enabled)
        {
            return;
        }

        if (ent.Comp.Frequency <= 0)
        {
            _cmChat.ChatMessageToOne(Loc.GetString("tunable-radio-off"), ent.Owner);
            return;
        }

        BroadcastOnFrequency(ent.Owner, ent.Comp.Frequency, args.Message);
    }

    public void BroadcastOnFrequency(EntityUid sender, int frequency, string message, string? senderName = null)
    {
        if (!_commsEnabled)
            return;

        if (frequency <= 0 || string.IsNullOrWhiteSpace(message))
            return;

        var senderPos = _transform.GetWorldPosition(sender);
        var senderMap = Transform(sender).MapID;
        var senderJam = _garble.GetJamIntensity(sender);

        // custom frequencies radiate like any other, so a search receiver can find the
        // squad nets an RTO thought were private for being off the published plan
        _sweep.RecordEmission(sender, frequency);

        var query = EntityQueryEnumerator<TunedFrequencyComponent, TransformComponent>();

        while (query.MoveNext(out var receiver, out var tuned, out var xform))
        {
            if (tuned.Frequency != frequency)
                continue;

            if (receiver == sender)
                continue;

            if (!_range.InVerticalReach(xform.MapID, senderMap, DirectFreqLevelReach))
                continue;

            var receiverPos = _transform.GetWorldPosition(xform);

            if (!TryGetLinkIntensity(
                    sender,
                    senderPos,
                    receiver,
                    receiverPos,
                    frequency,
                    default,
                    out var intensity))
            {
                continue;
            }

            DeliverGarbled(sender, receiver, receiver, message, frequency, senderJam, intensity, senderName);
        }

        var anprcQuery = EntityQueryEnumerator<ANPRCRadioComponent, TransformComponent>();

        while (anprcQuery.MoveNext(out var anprc, out var radio, out var xform))
        {
            if (!radio.IsEquipped || !radio.Enabled)
                continue;

            var tunedIn = radio.MonitorEnabled || radio.ScanEnabled
                ? radio.FrequencyOverrides.ContainsValue(frequency)
                : radio.FrequencyOverrides.TryGetValue(radio.ActiveSlot, out var activeFrequency) &&
                  activeFrequency == frequency;

            if (!tunedIn)
                continue;

            if (!_range.InVerticalReach(xform.MapID, senderMap, DirectFreqLevelReach))
                continue;

            var wearer = Transform(anprc).ParentUid;

            if (!wearer.IsValid())
                continue;

            if (wearer == sender)
                continue;

            if (radio.ScanEnabled &&
                (!radio.FrequencyOverrides.TryGetValue(radio.ActiveSlot, out var activeSlotFrequency) ||
                 activeSlotFrequency != frequency))
            {
                foreach (var (slot, slotFrequency) in radio.FrequencyOverrides)
                {
                    if (slotFrequency != frequency || slot == radio.ActiveSlot)
                        continue;

                    radio.ActiveSlot = slot;
                    Dirty(anprc, radio);

                    RaiseLocalEvent(anprc, new ANPRCDirectScanSwitchedEvent());

                    _cmChat.ChatMessageToOne(
                        Loc.GetString(
                            "anprc-scan-switched",
                            ("slot", slot + 1),
                            ("label", radio.SlotLabels.TryGetValue(slot, out var label) ? label : $"P{slot + 1}"),
                            ("channel", $"{FormatFreq(frequency)} MHz")),
                        wearer);

                    break;
                }
            }

            if (TryComp(wearer, out TunedFrequencyComponent? wearerTuned) &&
                wearerTuned.Frequency == frequency)
            {
                continue;
            }

            var anprcPos = _transform.GetWorldPosition(xform);

            if (!TryGetLinkIntensity(
                    sender,
                    senderPos,
                    anprc,
                    anprcPos,
                    frequency,
                    anprc,
                    out var intensity))
            {
                continue;
            }

            DeliverGarbled(sender, anprc, wearer, message, frequency, senderJam, intensity, senderName);
        }

        SendToEntity(sender, sender, message, frequency, senderName);

        _adminLogger.Add(
            LogType.Chat,
            LogImpact.Low,
            $"Tunable frequency message from {ToPrettyString(sender):user} on {FormatFreq(frequency)} MHz: {message}");

        var chat = BuildChatMessage(sender, message, frequency, senderName);
        _replay.RecordServerMessage(chat);

        foreach (var session in Filter.Empty().AddWhereAttachedEntity(HasComp<GhostHearingComponent>).Recipients)
        {
            var wrapped = _chatManager.AddGhostFollowButton(chat.WrappedMessage, sender, session.Channel);

            var ghostChat = wrapped == chat.WrappedMessage
                ? chat
                : new ChatMessage(chat)
                {
                    WrappedMessage = wrapped,
                    GhostFollowEntity = GetNetEntity(sender)
                };

            _netManager.ServerSendMessage(new MsgChatMessage { Message = ghostChat }, session.Channel);
        }
    }

    private bool TryGetLinkIntensity(
        EntityUid sender,
        Vector2 senderPos,
        EntityUid receiver,
        Vector2 receiverPos,
        int frequency,
        EntityUid excludeRelay,
        out RadioJamIntensity intensity)
    {
        var distance = (receiverPos - senderPos).Length();

        if (distance <= PartialRange)
        {
            intensity = distance > FullRange
                ? RadioJamIntensity.Light
                : RadioJamIntensity.None;

            return true;
        }

        var relay = FindANPRCRelay(sender, receiver, frequency, excludeRelay);

        if (relay == null)
        {
            intensity = RadioJamIntensity.None;
            return false;
        }

        var relayPos = _transform.GetWorldPosition(relay.Value);
        var distanceToRelay = (receiverPos - relayPos).Length();

        intensity = distanceToRelay > FullRange
            ? RadioJamIntensity.Light
            : RadioJamIntensity.None;

        return true;
    }

    private void DeliverGarbled(
        EntityUid sender,
        EntityUid jamProbe,
        EntityUid recipient,
        string message,
        int frequency,
        RadioJamIntensity senderJam,
        RadioJamIntensity rangeIntensity,
        string? senderName = null)
    {
        var jam = MaxIntensity(senderJam, _garble.GetJamIntensity(jamProbe));
        var intensity = MaxIntensity(rangeIntensity, jam);

        var finalMessage = intensity != RadioJamIntensity.None
            ? _garble.GarbleMessage(message, intensity)
            : message;

        SendToEntity(sender, recipient, finalMessage, frequency, senderName);
    }

    private EntityUid? FindANPRCRelay(
        EntityUid sender,
        EntityUid receiver,
        int frequency,
        EntityUid excludeAnprc = default)
    {
        var senderPos = _transform.GetWorldPosition(sender);
        var receiverPos = _transform.GetWorldPosition(receiver);
        var mapId = Transform(sender).MapID;

        var query = EntityQueryEnumerator<ANPRCRadioComponent, TransformComponent>();

        while (query.MoveNext(out var anprcUid, out var radio, out var xform))
        {
            if (anprcUid == excludeAnprc)
                continue;

            if (!radio.IsEquipped || !radio.Enabled)
                continue;

            if (!radio.FrequencyOverrides.ContainsValue(frequency))
                continue;

            if (!_range.InVerticalReach(xform.MapID, mapId, DirectFreqLevelReach))
                continue;

            var anprcPos = _transform.GetWorldPosition(xform);

            if ((senderPos - anprcPos).Length() > PartialRange)
                continue;

            if ((receiverPos - anprcPos).Length() > PartialRange)
                continue;

            return anprcUid;
        }

        return null;
    }

    private void SendToEntity(EntityUid sender, EntityUid receiver, string message, int frequency, string? senderName = null)
    {
        if (!TryComp(receiver, out ActorComponent? actor))
            return;

        var chat = BuildChatMessage(sender, message, frequency, senderName);
        _netManager.ServerSendMessage(new MsgChatMessage { Message = chat }, actor.PlayerSession.Channel);
    }

    private ChatMessage BuildChatMessage(EntityUid sender, string message, int frequency, string? senderNameOverride = null)
    {
        var frequencyText = FormatFreq(frequency);
        var senderName = FormattedMessage.EscapeText(senderNameOverride ?? Name(sender));
        var messageText = FormattedMessage.EscapeText(message);

        var wrapped = $"[color=#5B9BD5][bold]FREQ {frequencyText}[/bold][/color] " +
                      $"[color=#C8D2E8]{senderName}[/color] " +
                      $"says, \"{messageText}\"";

        return new ChatMessage(
            ChatChannel.Radio,
            message,
            wrapped,
            GetNetEntity(sender),
            _chatManager.EnsurePlayer(CompOrNull<ActorComponent>(sender)?.PlayerSession.UserId)?.Key,
            display: new ChatDisplayMetadata(
                ChatDisplayKind.Radio,
                senderName: senderName,
                verb: "says",
                channelLabel: $"FREQ {frequencyText}",
                quoteBody: true,
                accentColor: Color.FromHex("#5B9BD5")));
    }

    private void OnGetAltVerbs(Entity<TunableHeadsetComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!_commsEnabled || !args.CanAccess || !args.CanInteract)
            return;

        if (ent.Comp.TunedFrequency <= 0)
            return;

        var targetFrequency = ent.Comp.DefaultFrequency > 0
            ? ent.Comp.DefaultFrequency
            : 0;

        var alreadyAtTarget = ent.Comp.TunedFrequency == targetFrequency;
        var hasDefault = ent.Comp.DefaultFrequency > 0;
        var user = args.User;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = hasDefault
                ? Loc.GetString("tunable-radio-verb-reset")
                : Loc.GetString("tunable-radio-verb-clear"),
            Priority = 1,
            Disabled = alreadyAtTarget,
            Act = () =>
            {
                ent.Comp.TunedFrequency = targetFrequency;
                Dirty(ent);

                var wearer = Transform(ent.Owner).ParentUid;

                if (wearer.IsValid() &&
                    TryComp(wearer, out TunedFrequencyComponent? tuned) &&
                    tuned.Source == ent.Owner)
                {
                    tuned.Frequency = targetFrequency;
                }

                _cmChat.ChatMessageToOne(
                    hasDefault
                        ? Loc.GetString("tunable-radio-reset", ("freq", FormatFreq(targetFrequency)))
                        : Loc.GetString("tunable-radio-cleared"),
                    user);

                UpdateBuiState(ent);
            }
        });
    }

    private void OnGetVerbs(Entity<TunableHeadsetComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!_commsEnabled || !args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("tunable-radio-verb-tune"),
            Priority = 2,
            Act = () => _ui.OpenUi(ent.Owner, TunableFrequencyUI.Key, user)
        });
    }

    private void OnSetFrequency(Entity<TunableHeadsetComponent> ent, ref TunableFrequencySetMsg args)
    {
        if (!_commsEnabled)
            return;

        var text = args.FrequencyText
            .Trim()
            .Replace(".", "")
            .Replace(",", "");

        if (!int.TryParse(text, out var frequency))
        {
            _cmChat.ChatMessageToOne(Loc.GetString("tunable-radio-invalid-freq"), args.Actor);
            return;
        }

        frequency = Math.Clamp(frequency, ent.Comp.MinFrequency, ent.Comp.MaxFrequency);

        ent.Comp.TunedFrequency = frequency;
        Dirty(ent);

        var wearer = Transform(ent.Owner).ParentUid;

        if (wearer.IsValid() &&
            TryComp(wearer, out TunedFrequencyComponent? tuned) &&
            tuned.Source == ent.Owner)
        {
            tuned.Frequency = frequency;
        }

        _cmChat.ChatMessageToOne(
            Loc.GetString("tunable-radio-freq-set", ("freq", FormatFreq(frequency))),
            args.Actor);

        UpdateBuiState(ent);
    }

    private void OnUiOpened(Entity<TunableHeadsetComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateBuiState(ent);
    }

    private void UpdateBuiState(Entity<TunableHeadsetComponent> ent)
    {
        _ui.SetUiState(
            ent.Owner,
            TunableFrequencyUI.Key,
            new TunableFrequencyState(
                ent.Comp.TunedFrequency,
                ent.Comp.MinFrequency,
                ent.Comp.MaxFrequency));
    }

    private static RadioJamIntensity MaxIntensity(RadioJamIntensity a, RadioJamIntensity b)
    {
        return a > b ? a : b;
    }

    public static string FormatFreq(int raw)
    {
        return TunableFrequencyHelpers.FormatFreq(raw);
    }
}

public record struct ANPRCDirectScanSwitchedEvent;
