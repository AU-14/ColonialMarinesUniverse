using System.Linq;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private RadioSystem _radio = default!;
    private readonly Dictionary<EntityUid, (EntityUid Target, EntityCoordinates Position, TimeSpan Observed)> _reports = new();

    private void InitializeRadio()
    {
        SubscribeLocalEvent<CMUExpeditionAgentComponent, HeadsetRadioReceiveRelayEvent>(OnReport);
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnLocalRadioRange);
    }

    private void OnLocalRadioRange(ref RadioReceiveAttemptEvent args)
    {
        if ((HasComp<CMUExpeditionRadioComponent>(args.RadioSource) || HasComp<CMUExpeditionRadioComponent>(args.RadioReceiver)) &&
            !_transform.InRange(Transform(args.RadioSource).Coordinates, Transform(args.RadioReceiver).Coordinates, 40))
            args.Cancelled = true;
    }

    private bool SameSquad(EntityUid a, CMUExpeditionAgentComponent first, EntityUid b, CMUExpeditionAgentComponent second) =>
        first.Squad != 0 && first.Squad == second.Squad && Transform(a).MapID == Transform(b).MapID && IsFriendly(a, b);

    private bool RadioReady(EntityUid uid, out EntityUid headset)
    {
        headset = default;
        if (!_inventory.TryGetSlotEntity(uid, "ears", out var item) || !TryComp<HeadsetComponent>(item, out var radio) ||
            !radio.Enabled || !radio.IsEquipped || !TryComp<EncryptionKeyHolderComponent>(item, out var keys) || keys.Channels.Count == 0)
            return false;
        headset = item.Value;
        return true;
    }

    private void ShareContact(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid target, TimeSpan now)
    {
        if (agent.Squad == 0 || now < agent.NextRadio || !RadioReady(uid, out var headset))
            return;
        agent.NextRadio = now + TimeSpan.FromSeconds(2);
        var keys = Comp<EncryptionKeyHolderComponent>(headset);
        var channel = keys.Channels.FirstOrDefault(c => !keys.ReadOnlyChannels.Contains(c));
        if (channel == default)
            return;
        // Squad knowledge is independent of audible chatter. Do not have every observer
        // transmit the same snapshot, or every recipient acknowledge it in chat.
        var squad = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (squad.MoveNext(out var other, out var buddy))
            if (other != uid && SameSquad(uid, agent, other, buddy) && _mobs.IsAlive(other) &&
                _transform.InRange(Transform(uid).Coordinates, Transform(other).Coordinates, 40) &&
                buddy.LastSharedContact == target && now - buddy.LastSharedAt < TimeSpan.FromSeconds(1.5))
                return;
        var position = Transform(target).Coordinates;
        var announce = AnnounceNewContact(uid, agent, target, now);
        if (!announce)
        {
            ShareSilentContact(uid, agent, target, position, headset, ProtoMan.Index(channel), now);
            return;
        }
        agent.LastSharedContact = target;
        agent.LastSharedAt = now;
        agent.RadioCallouts++;
        // One callout budget for the whole local squad, not a timer for every mouth.
        squad = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (squad.MoveNext(out var other, out var buddy))
        {
            if (other != uid && (!SameSquad(uid, agent, other, buddy) ||
                !_transform.InRange(Transform(uid).Coordinates, Transform(other).Coordinates, 40)))
                continue;
            buddy.NextRadioAnnouncement = now + TimeSpan.FromSeconds(25);
            buddy.LastAnnouncedContact = target;
            buddy.LastAnnouncedPosition = position;
            buddy.LastRadioAnnouncement = now;
        }
        _reports[uid] = (target, Transform(target).Coordinates, now);
        try
        {
            _radio.SendRadioMessage(uid, ContactPhrase(agent), channel, headset, null);
        }
        finally
        {
            _reports.Remove(uid);
        }
    }

    private void ShareSilentContact(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid target,
        EntityCoordinates position, EntityUid headset, RadioChannelPrototype channel, TimeSpan now)
    {
        // These headsets are explicitly telecom-exempt. Still honour native send/receive
        // cancellation hooks, equipment, encryption channels, map and local radio range.
        var message = Loc.GetString("cmu-expedition-contact-report");
        var chat = new MsgChatMessage { Message = new ChatMessage(ChatChannel.Radio, message, message, GetNetEntity(uid), null) };
        var send = new RadioSendAttemptEvent(channel, headset, uid, message, chat);
        RaiseLocalEvent(ref send);
        RaiseLocalEvent(headset, ref send);
        if (send.Cancelled)
            return;
        agent.LastSharedContact = target;
        agent.LastSharedAt = now;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (other == uid || !SameSquad(uid, agent, other, buddy) || HasComp<ActorComponent>(other) ||
                !_mobs.IsAlive(other) || !RadioReady(other, out var receiver) ||
                !TryComp<ActiveRadioComponent>(receiver, out var radio) || !radio.Channels.Contains(channel.ID) ||
                !_transform.InRange(Transform(uid).Coordinates, Transform(other).Coordinates, 40))
                continue;
            var receive = new RadioReceiveAttemptEvent(channel, headset, receiver);
            RaiseLocalEvent(ref receive);
            RaiseLocalEvent(receiver, ref receive);
            if (!receive.Cancelled)
                QueueContactReport(buddy, target, position, now);
        }
    }

    private void OnReport(Entity<CMUExpeditionAgentComponent> ent, ref HeadsetRadioReceiveRelayEvent args)
    {
        var source = args.RelayedEvent.MessageSource;
        if (source == ent.Owner || !_reports.TryGetValue(source, out var report) ||
            HasComp<ActorComponent>(ent) || !_mobs.IsAlive(ent) || !RadioReady(ent, out _) ||
            !TryComp<CMUExpeditionAgentComponent>(source, out var sender) || !SameSquad(ent, ent.Comp, source, sender) ||
            !_transform.InRange(Transform(source).Coordinates, Transform(ent).Coordinates, 40))
            return;
        QueueContactReport(ent.Comp, report.Target, report.Position, report.Observed);
    }

    private static void QueueContactReport(CMUExpeditionAgentComponent agent, EntityUid target,
        EntityCoordinates position, TimeSpan observed)
    {
        // A busy squad may send several reports during the reaction delay. Refresh the
        // snapshot without repeatedly postponing the first report's delivery deadline.
        if (agent.RadioPosition == null)
            agent.RadioDeliveryAt = observed + TimeSpan.FromSeconds(0.6);
        agent.RadioTarget = target;
        agent.RadioPosition = position;
        agent.RadioObservedAt = observed;
        agent.ReportsReceived++;
        agent.RadioDecision = "pending";
    }

    private void ReceiveContact(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.RadioPosition is not { } report || now < agent.RadioDeliveryAt)
            return;
        agent.RadioPosition = null;
        if (!RadioReady(uid, out _))
        {
            agent.RadioDecision = "radio-unavailable";
            return;
        }
        if (agent.RadioObservedAt <= agent.LastContact || now >= agent.RadioObservedAt + agent.RadioMemoryDuration)
        {
            agent.RadioDecision = "stale-report";
            return;
        }
        if (!agent.ContactFromRadio && agent.Target != null && now - agent.LastContact < agent.LostSightDelay ||
            agent.Action != null || agent.State is CMUExpeditionAgentState.Healing or CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.Withdraw)
        {
            agent.RadioDecision = "maintaining-current-action";
            return;
        }
        if (agent.Home is not { } home || !_transform.InRange(home, report, agent.LeashRange))
        {
            agent.RadioDecision = "outside-guard-area";
            return;
        }
        if (agent.RadioTarget is not { } target || !Exists(target) || !AcceptOrderedContact(uid, agent, target))
        {
            agent.RadioDecision = "invalid-target";
            return;
        }
        if (agent.LastSeen == null)
            agent.FirstContact = now;
        var newlyResponding = !agent.ContactFromRadio || agent.Target != target;
        if (newlyResponding && !CommittedMovement(agent))
        {
            ClearCover(agent);
            agent.State = CMUExpeditionAgentState.Watch;
            _steering.Unregister(uid);
        }
        CancelWork(uid, agent);
        agent.Target = agent.RadioTarget;
        agent.LastContactWasMelee = HasComp<Content.Shared._RMC14.Xenonids.XenoComponent>(target);
        agent.LastSeen = report;
        agent.LastContact = agent.RadioObservedAt;
        agent.ForgetAt = agent.RadioObservedAt + agent.RadioMemoryDuration;
        agent.ContactFromRadio = true;
        agent.ReportsAccepted++;
        agent.RadioDecision = "supporting-report";
    }
}
