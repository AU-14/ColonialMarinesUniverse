using System.Collections.Generic;
using System.Linq;
using Content.Server.Jobs;
using Content.Shared.AU14.util;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14.Platoons;

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

                if (!HasSkillsSpecial(job))
                    missing.Add($"{job.ID} has no Skills job special");
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
            var jobs = GetGovforFactionPlatoonJobs(prototypes);

            var missing = new List<string>();
            foreach (var jobId in jobs)
            {
                if (!prototypes.TryIndex<JobPrototype>(jobId, out var job))
                {
                    missing.Add($"{jobId} does not exist");
                    continue;
                }

                if (!HasSpecialComponent(prototypes, job, "Marine"))
                    missing.Add($"{job.ID} has no Marine job special");

                if (!HasSpecialComponent(prototypes, job, "UserIFF"))
                    missing.Add($"{job.ID} has no UserIFF job special");

                if (!HasSpecialComponent(prototypes, job, "TacticalMapIcon"))
                    missing.Add($"{job.ID} has no TacticalMapIcon job special");
            }

            Assert.That(missing, Is.Empty, string.Join("\n", missing));
        });

        await pair.CleanReturnAsync();
    }

    private static bool HasSkillsSpecial(JobPrototype job)
    {
        foreach (var special in job.Special)
        {
            if (special is AddComponentSpecial { Components: { } components } &&
                components.ContainsKey("Skills"))
            {
                return true;
            }
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

    private static bool HasSpecialComponent(IPrototypeManager prototypes, JobPrototype job, string componentName)
    {
        foreach (var special in GetAppliedAddComponentSpecials(prototypes, job))
        {
            if (special.Components.ContainsKey(componentName))
                return true;
        }

        return false;
    }

    private static List<AddComponentSpecial> GetAppliedAddComponentSpecials(IPrototypeManager prototypes, JobPrototype job)
    {
        if (!job.InheritAddComponentSpecials)
            return job.Special.OfType<AddComponentSpecial>().ToList();

        var results = new List<AddComponentSpecial>();
        AddInheritedAddComponentSpecials(prototypes, job, results);
        return results;
    }

    private static void AddInheritedAddComponentSpecials(
        IPrototypeManager prototypes,
        JobPrototype job,
        List<AddComponentSpecial> results)
    {
        if (job.Parents is { Length: > 0 })
        {
            foreach (var parentId in job.Parents)
            {
                if (prototypes.TryIndex<JobPrototype>(parentId, out var parent))
                    AddInheritedAddComponentSpecials(prototypes, parent, results);
            }
        }

        results.AddRange(job.Special.OfType<AddComponentSpecial>());
    }
}
