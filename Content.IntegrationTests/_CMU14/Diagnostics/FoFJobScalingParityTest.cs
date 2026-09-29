using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.util;
using Content.Shared.Roles;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class FoFJobScalingParityTest : GameTest
{
    // Force on Force scaling is single-sided by convention: entries are declared once under
    // the GOVFOR id and AddJobsRuleSystem expands them onto the OPFOR mirror at apply time,
    // so the 3-player mid-round balancer always sees symmetric faction capacity. This pins
    // the two ways that convention can break: an OPFOR mirror job prototype going missing
    // (expansion silently no-ops), or someone re-declaring OPFOR entries explicitly and
    // reintroducing value drift between the sides.
    [Test]
    public async Task DefaultGovforOpforScaleIsSingleSided()
    {
        await Server.WaitAssertion(() =>
        {
            var scale = SProtoMan.Index<JobScalePrototype>("DefaultGovforOpforScale");
            var problems = new List<string>();

            foreach (var (jobId, _) in scale.Jobs)
            {
                if (jobId.Contains("GOVFOR"))
                {
                    var mirror = jobId.Replace("GOVFOR", "OPFOR");
                    if (!SProtoMan.HasIndex<JobPrototype>(mirror))
                        problems.Add($"{jobId}: no OPFOR job prototype {mirror} to expand onto");
                }
                else if (jobId.Contains("OPFOR"))
                {
                    problems.Add($"{jobId}: declared explicitly; FoF scaling is single-sided and mirrors expand at apply time");
                }
            }

            Assert.That(problems, Is.Empty,
                "Force on Force job scaling convention violations: " + string.Join("; ", problems));
        });
    }

    // The scale only expands the JOB id; gear is authored per side. Every OPFOR mirror of a
    // scaled GOVFOR job must therefore carry its own wired startingGear/dummyStartingGear,
    // otherwise the OPFOR side deploys unarmed while GOVFOR ships fully kitted.
    [Test]
    public async Task GovforScaleJobsHaveMirroredLoadouts()
    {
        await Server.WaitAssertion(() =>
        {
            var scale = SProtoMan.Index<JobScalePrototype>("DefaultGovforOpforScale");
            var problems = new List<string>();

            foreach (var (jobId, _) in scale.Jobs)
            {
                if (!jobId.Contains("GOVFOR"))
                    continue;

                var mirror = jobId.Replace("GOVFOR", "OPFOR");
                if (!SProtoMan.TryIndex<JobPrototype>(mirror, out var opforJob))
                    continue; // missing mirror is the scaling test's finding

                var govforJob = SProtoMan.Index<JobPrototype>(jobId);

                if (govforJob.StartingGear != null && opforJob.StartingGear == null)
                    problems.Add($"{mirror}: no startingGear while {jobId} has {govforJob.StartingGear}");
                if (govforJob.DummyStartingGear != null && opforJob.DummyStartingGear == null)
                    problems.Add($"{mirror}: no dummyStartingGear while {jobId} has {govforJob.DummyStartingGear}");

                if (opforJob.StartingGear != null && !SProtoMan.HasIndex<StartingGearPrototype>(opforJob.StartingGear))
                    problems.Add($"{mirror}: startingGear {opforJob.StartingGear} does not resolve");
                if (opforJob.DummyStartingGear != null && !SProtoMan.HasIndex<StartingGearPrototype>(opforJob.DummyStartingGear))
                    problems.Add($"{mirror}: dummyStartingGear {opforJob.DummyStartingGear} does not resolve");
            }

            Assert.That(problems, Is.Empty,
                "Force on Force loadout parity violations: " + string.Join("; ", problems));
        });
    }
}
