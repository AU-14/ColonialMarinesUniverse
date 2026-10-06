using Content.Shared.CMU14.Round.Roles;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Preferences;

[Serializable, NetSerializable]
public enum ForceOnForceSide : byte
{
    Either,
    Govfor,
    Opfor,
}

[Flags, Serializable, NetSerializable]
public enum ForceOnForceFallback : byte
{
    StayInLobby = 0,
    OtherSide = 1,
    OtherRole = 2,
    Both = OtherSide | OtherRole,
}

public sealed partial class HumanoidCharacterProfile
{
    [DataField]
    public ForceOnForceSide FoFSide { get; private set; }

    [DataField]
    public ForceOnForceFallback FoFFallback { get; private set; }

    /// <summary>
    /// Role priorities apply to either side; side eligibility is controlled separately by FoFSide and FoFFallback.
    /// Existing side-specific preferences remain readable through their equivalent round role.
    /// </summary>
    public JobPriority GetForceOnForceJobPriority(JobPrototype job, IPrototypeManager prototypes)
    {
        var priorities = GetJobPrioritiesForGamemode("ForceOnForce");
        var priority = priorities.GetValueOrDefault(job.ID, JobPriority.Never);
        if (job.RoundRole == null || job.RoundSide is not (RoundJobSide.Govfor or RoundJobSide.Opfor))
            return priority;

        foreach (var (id, preference) in priorities)
        {
            if (preference > priority && prototypes.TryIndex(id, out var source) &&
                source.RoundRole == job.RoundRole && source.RoundSide is RoundJobSide.Govfor or RoundJobSide.Opfor)
            {
                priority = preference;
            }
        }

        return priority;
    }

    public HumanoidCharacterProfile WithForceOnForceJobPriority(JobPrototype job, JobPriority priority, IPrototypeManager prototypes)
    {
        var profile = this;
        if (job.RoundRole != null && job.RoundSide is RoundJobSide.Govfor or RoundJobSide.Opfor)
        {
            // Clear legacy entries for the same role so lowering a priority also lowers the hidden side's preference.
            foreach (var (id, _) in GetJobPrioritiesForGamemode("ForceOnForce"))
            {
                if (id != job.ID && prototypes.TryIndex(id, out var source) && source.RoundRole == job.RoundRole &&
                    source.RoundSide is RoundJobSide.Govfor or RoundJobSide.Opfor)
                {
                    profile = profile.WithGamemodeJobPriority("ForceOnForce", id, JobPriority.Never);
                }
            }
        }

        return profile.WithGamemodeJobPriority("ForceOnForce", job.ID, priority);
    }

    public HumanoidCharacterProfile WithForceOnForcePreferences(ForceOnForceSide side, ForceOnForceFallback fallback)
    {
        return new(this)
        {
            FoFSide = Enum.IsDefined(side) ? side : ForceOnForceSide.Either,
            FoFFallback = Enum.IsDefined(fallback) ? fallback : ForceOnForceFallback.StayInLobby,
        };
    }
}
