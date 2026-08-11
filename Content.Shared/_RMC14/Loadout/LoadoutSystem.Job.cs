using Content.Shared.Roles;

namespace Content.Shared.Clothing;

public sealed partial class LoadoutSystem
{
    /// <summary>
    /// Gets the role loadout prototype ID used by a job, accounting for jobs that share another job's loadout.
    /// </summary>
    public static string GetJobPrototype(JobPrototype job)
    {
        return GetJobPrototype(job.UseLoadoutOfJob?.Id ?? job.ID);
    }
}
