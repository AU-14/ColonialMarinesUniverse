using System.Linq;
using Content.Server.Radio.EntitySystems;
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
        agent.NextRadio = now + TimeSpan.FromSeconds(4);
        var keys = Comp<EncryptionKeyHolderComponent>(headset);
        var channel = keys.Channels.FirstOrDefault(c => !keys.ReadOnlyChannels.Contains(c));
        if (channel == default)
            return;
        _reports[uid] = (target, Transform(target).Coordinates, now);
        try
        {
            _radio.SendRadioMessage(uid, Loc.GetString("cmu-expedition-contact-report"), channel, headset, null);
        }
        finally
        {
            _reports.Remove(uid);
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
        ent.Comp.RadioTarget = report.Target;
        ent.Comp.RadioPosition = report.Position;
        ent.Comp.RadioObservedAt = report.Observed;
        ent.Comp.RadioDeliveryAt = report.Observed + TimeSpan.FromSeconds(0.6);
        ent.Comp.ReportsReceived++;
    }

    private void ReceiveContact(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.RadioPosition is not { } report || now < agent.RadioDeliveryAt)
            return;
        agent.RadioPosition = null;
        if (!RadioReady(uid, out _) || agent.RadioObservedAt <= agent.LastContact ||
            now >= agent.RadioObservedAt + agent.MemoryDuration || agent.Home is not { } home ||
            !_transform.InRange(home, report, agent.LeashRange))
            return;
        if (agent.RadioTarget is { } target && IsFriendly(uid, target))
            return;
        if (agent.Target != agent.RadioTarget && agent.Action != null)
            CancelPlan(uid, agent, false);
        agent.Target = agent.RadioTarget;
        agent.LastSeen = report;
        agent.LastContact = agent.RadioObservedAt;
        agent.ForgetAt = agent.RadioObservedAt + agent.MemoryDuration;
        agent.ContactFromRadio = true;
    }
}
