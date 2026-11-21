using Content.Server.Station.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Systems;

public sealed partial class StationJobsSystem
{
    /// <summary>Synchronizes event changes with round-start minimums and the job picker's overflow list.</summary>
    public void SetEventRoundStartSlots(EntityUid station, ProtoId<JobPrototype> job, int available, bool set, int delta)
    {
        if (!TryComp<StationJobsComponent>(station, out var comp))
            return;

        var minimum = GetRoundStartJobs(station, comp).GetValueOrDefault(job) ?? 0;
        var updated = set ? available : (int) Math.Clamp((long) minimum + delta, 0, Math.Max(available, 0));
        SetRoundStartJobSlot(station, job, updated, comp);
        var overflow = new HashSet<ProtoId<JobPrototype>>(comp.OverflowJobs);
        if (available == -1)
            overflow.Add(job);
        else
            overflow.Remove(job);
        comp.OverflowJobs = overflow;
    }
}
