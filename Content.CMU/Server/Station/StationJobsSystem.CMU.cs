using Content.Server.Station.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Systems;

public sealed partial class StationJobsSystem
{
    /// <summary>
    /// Adjusts both the live and round-start views of a job slot as one domain operation.
    /// </summary>
    public bool TryAdjustJobSlotAndRoundStart(EntityUid station,
        ProtoId<JobPrototype> jobId,
        int amount,
        bool createSlot,
        StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        var jobPrototypeId = jobId.ToString();
        if (amount == 0 && !stationJobs.JobList.ContainsKey(jobPrototypeId))
        {
            if (!createSlot || !TrySetJobSlot(station, jobPrototypeId, 0, true, stationJobs))
                return false;
        }
        else if (!TryAdjustJobSlot(station, jobPrototypeId, amount, createSlot, false, stationJobs))
        {
            return false;
        }

        int roundStartSlots;
        if (stationJobs.SetupAvailableJobs.TryGetValue(jobId, out var slots) && slots.Length > 0)
        {
            roundStartSlots = slots[0] < 0
                ? -1
                : Math.Max(0, slots[0] + amount);
        }
        else if (TryGetJobSlot(station, jobPrototypeId, out var liveSlots, stationJobs) && liveSlots != null)
        {
            roundStartSlots = liveSlots.Value;
        }
        else
        {
            roundStartSlots = Math.Max(0, amount);
        }

        SetRoundStartJobSlot(station, jobId, roundStartSlots, stationJobs);
        UpdateOverflowJob(jobId, roundStartSlots, stationJobs);

        return true;
    }

    /// <summary>
    /// Sets both the live and round-start views of a job slot as one domain operation.
    /// </summary>
    public bool TrySetJobSlotAndRoundStart(EntityUid station,
        ProtoId<JobPrototype> jobId,
        int amount,
        bool createSlot,
        StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        if (!TrySetJobSlot(station, jobId.ToString(), amount, createSlot, stationJobs))
            return false;

        SetRoundStartJobSlot(station, jobId, amount, stationJobs);
        UpdateOverflowJob(jobId, amount, stationJobs);

        return true;
    }

    private static void UpdateOverflowJob(ProtoId<JobPrototype> jobId,
        int roundStartSlots,
        StationJobsComponent stationJobs)
    {
        if (stationJobs.OverflowJobs is not HashSet<ProtoId<JobPrototype>> overflowJobs)
        {
            overflowJobs = stationJobs.OverflowJobs == null
                ? []
                : new HashSet<ProtoId<JobPrototype>>(stationJobs.OverflowJobs);
            stationJobs.OverflowJobs = overflowJobs;
        }

        if (roundStartSlots < 0)
            overflowJobs.Add(jobId);
        else
            overflowJobs.Remove(jobId);
    }
}
