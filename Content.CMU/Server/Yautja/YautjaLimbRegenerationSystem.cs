using System.Linq;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaLimbRegenerationSystem : EntitySystem
{
    [Dependency] private CMUMedicalSchedulerSystem _scheduler = default!;
    [Dependency] private CMUSurgerySystem _surgery = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly CMUMedicalWorkKey RegrowthWork = new("yautja-limb-regrowth");

    public override void Initialize()
    {
        SubscribeLocalEvent<BodyPartSeveredEvent>(OnPartSevered);
        SubscribeLocalEvent<YautjaLimbRegenerationComponent, CMUMedicalWorkDueEvent>(OnRegrowthDue);
    }

    private void OnPartSevered(ref BodyPartSeveredEvent args)
    {
        if (!TryComp(args.Body, out YautjaLimbRegenerationComponent? regeneration)
            || !TryComp(args.Part, out BodyPartComponent? part))
            return;

        regeneration.SeverAttempts.Remove(args.Part);
        var site = new YautjaLimbSite(args.Type, part.Symmetry);
        regeneration.PendingRegrowth[site] = _timing.CurTime + regeneration.RegrowDelay;
        ScheduleNext(args.Body, regeneration);
    }

    private void OnRegrowthDue(
        Entity<YautjaLimbRegenerationComponent> ent,
        ref CMUMedicalWorkDueEvent args)
    {
        if (args.Key != RegrowthWork)
            return;

        var now = _timing.CurTime;
        var due = ent.Comp.PendingRegrowth
            .Where(entry => entry.Value <= now)
            .OrderBy(entry => RegrowthOrder(entry.Key.Type))
            .ToArray();

        foreach (var (site, dueAt) in due)
        {
            if (_surgery.TryGetMissingPartSite(ent.Owner, site.Type, site.Symmetry, out var parent, out var slot))
            {
                _surgery.TryRegenerateLimb(ent.Owner,
                    site.Type,
                    site.Symmetry,
                    parent,
                    slot,
                    () => ent.Comp.PendingRegrowth.TryGetValue(site, out var current) && current == dueAt);
            }

            ent.Comp.PendingRegrowth.Remove(site);
        }

        ScheduleNext(ent.Owner, ent.Comp);
    }

    private void ScheduleNext(EntityUid body, YautjaLimbRegenerationComponent regeneration)
    {
        if (regeneration.PendingRegrowth.Count == 0)
        {
            _scheduler.Cancel(body, RegrowthWork);
            return;
        }

        _scheduler.Schedule(body, RegrowthWork, regeneration.PendingRegrowth.Values.Min());
    }

    private static int RegrowthOrder(BodyPartType type)
        => type is BodyPartType.Arm or BodyPartType.Leg ? 0 : 1;
}
