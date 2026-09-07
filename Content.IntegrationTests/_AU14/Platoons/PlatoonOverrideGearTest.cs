using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Roles;
using Content.Server.Jobs;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.CMU14.util;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Platoons;

[TestFixture]
public sealed class PlatoonOverrideGearTest
{
    [Test]
    public async Task PlatoonOverrideJobsHaveStartingGearWithId()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var missing = new List<string>();

            foreach (var platoon in prototypes.EnumeratePrototypes<PlatoonPrototype>())
            {
                foreach (var (jobClass, jobId) in platoon.JobClassOverride)
                {
                    if (!prototypes.TryIndex<JobPrototype>(jobId, out var job))
                    {
                        missing.Add($"{platoon.ID} {jobClass}: {jobId} does not exist");
                        continue;
                    }

                    if (job.StartingGear is not { } startingGearId)
                    {
                        missing.Add($"{platoon.ID} {jobClass}: {job.ID} has no startingGear");
                        continue;
                    }

                    var startingGear = prototypes.Index<StartingGearPrototype>(startingGearId);
                    if (!startingGear.Equipment.ContainsKey("id"))
                        missing.Add($"{platoon.ID} {jobClass}: {job.ID} gear {startingGear.ID} has no id slot");
                }
            }

            Assert.That(missing, Is.Empty, string.Join("\n", missing));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FactionPlatoonJobsHaveSkills()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var profiles = server.System<RoundJobProfileSystem>();
            var jobs = new HashSet<string>();

            foreach (var platoon in prototypes.EnumeratePrototypes<PlatoonPrototype>())
            {
                foreach (var (_, jobId) in platoon.JobClassOverride)
                    jobs.Add(jobId);
            }

            foreach (var job in prototypes.EnumeratePrototypes<JobPrototype>())
            {
                if (job.ID.EndsWith("RMC") ||
                    job.ID.EndsWith("UPP") ||
                    job.ID.EndsWith("WYPMC"))
                {
                    jobs.Add(job.ID);
                }
            }

            var missing = new List<string>();
            foreach (var jobId in jobs)
            {
                if (!prototypes.TryIndex<JobPrototype>(jobId, out var job))
                {
                    missing.Add($"{jobId} does not exist");
                    continue;
                }

                if (!HasResolvedComponent(profiles, job, "Skills"))
                    missing.Add($"{job.ID} resolves no Skills component");
            }

            Assert.That(missing, Is.Empty, string.Join("\n", missing));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GovforFactionPlatoonJobsKeepTacticalMapComponents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var profiles = server.System<RoundJobProfileSystem>();
            var jobs = GetGovforFactionPlatoonJobs(prototypes);

            var missing = new List<string>();
            foreach (var jobId in jobs)
            {
                if (!prototypes.TryIndex<JobPrototype>(jobId, out var job))
                {
                    missing.Add($"{jobId} does not exist");
                    continue;
                }

                if (!HasResolvedComponent(profiles, job, "Marine"))
                    missing.Add($"{job.ID} resolves no Marine component");

                if (!HasResolvedComponent(profiles, job, "UserIFF"))
                    missing.Add($"{job.ID} resolves no UserIFF component");

                if (!HasResolvedComponent(profiles, job, "TacticalMapIcon"))
                    missing.Add($"{job.ID} resolves no TacticalMapIcon component");
            }

            Assert.That(missing, Is.Empty, string.Join("\n", missing));
        });

        await pair.CleanReturnAsync();
    }

    private static bool HasResolvedComponent(
        RoundJobProfileSystem profiles,
        JobPrototype job,
        string componentName)
    {
        foreach (var resolved in profiles.GetProfileComponents(job))
        {
            if (resolved.Components.ContainsKey(componentName))
                return true;
        }

        return false;
    }

    private static HashSet<string> GetGovforFactionPlatoonJobs(IPrototypeManager prototypes)
    {
        var jobs = new HashSet<string>();

        foreach (var platoon in prototypes.EnumeratePrototypes<PlatoonPrototype>())
        {
            foreach (var (_, jobId) in platoon.JobClassOverride)
            {
                if (jobId.StartsWith("AU14JobGOVFOR"))
                    jobs.Add(jobId);
            }
        }

        foreach (var job in prototypes.EnumeratePrototypes<JobPrototype>())
        {
            if (job.ID.StartsWith("AU14JobGOVFOR") &&
                (job.ID.EndsWith("RMC") ||
                 job.ID.EndsWith("UPP") ||
                 job.ID.EndsWith("WYPMC") ||
                 job.ID.EndsWith("CMBCIU")))
            {
                jobs.Add(job.ID);
            }
        }

        return jobs;
    }

}
