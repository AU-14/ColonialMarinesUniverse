using Content.Shared.Timing;

namespace Content.Shared.CMU14.Medical.Injuries.Wounds;

public abstract partial class SharedCMUWoundsSystem
{
    private readonly DeadlineQueue<BodyPartWoundComponent> _externalBleedDeadlines = new();
    private readonly DeadlineQueue<BodyPartWoundComponent> _woundHealDeadlines = new();
    private readonly DeadlineQueue<InternalBleedingComponent> _internalBleedDeadlines = new();
    private readonly List<BodyPartWoundComponent> _dueExternalBleeds = new();
    private readonly List<BodyPartWoundComponent> _orphanWoundOwners = new();
    private readonly List<InternalBleedingComponent> _dueInternalBleeds = new();

    private void InitializeWoundScheduling()
    {
        SubscribeLocalEvent<BodyPartWoundComponent, ComponentInit>(OnWoundScheduleStartup);
        SubscribeLocalEvent<BodyPartWoundComponent, ComponentShutdown>(OnWoundScheduleShutdown);
        SubscribeLocalEvent<InternalBleedingComponent, ComponentInit>(OnBleedScheduleStartup);
        SubscribeLocalEvent<InternalBleedingComponent, ComponentShutdown>(OnBleedScheduleShutdown);
    }

    private void OnWoundScheduleStartup(Entity<BodyPartWoundComponent> ent, ref ComponentInit args)
    {
        ent.Comp.ScheduledOwner = ent.Owner;
        ent.Comp.ScheduleChanged = ScheduleWounds;
        ScheduleWounds(ent.Comp);
    }

    private void OnWoundScheduleShutdown(Entity<BodyPartWoundComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.ScheduleChanged = null;
        _externalBleedDeadlines.Remove(ent.Comp);
        _woundHealDeadlines.Remove(ent.Comp);
    }

    private void OnBleedScheduleStartup(Entity<InternalBleedingComponent> ent, ref ComponentInit args)
    {
        ent.Comp.ScheduledOwner = ent.Owner;
        ent.Comp.ScheduleChanged = ScheduleInternalBleed;
        ScheduleInternalBleed(ent.Comp);
    }

    private void OnBleedScheduleShutdown(Entity<InternalBleedingComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.ScheduleChanged = null;
        _internalBleedDeadlines.Remove(ent.Comp);
    }

    private void ScheduleWounds(BodyPartWoundComponent comp)
    {
        if (comp.ExternalBleeding == ExternalBleedTier.None) _externalBleedDeadlines.Remove(comp);
        else _externalBleedDeadlines.Schedule(comp, comp.NextExternalBleedTick);
        _woundHealDeadlines.Schedule(comp, comp.NextHealTick);
    }

    private void ScheduleInternalBleed(InternalBleedingComponent comp) =>
        _internalBleedDeadlines.Schedule(comp, comp.NextBleedTick);
}
