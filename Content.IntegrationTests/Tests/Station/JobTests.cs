using System.Linq;
using Content.Server.Jobs;
using Content.Shared._RMC14.Chat;
using Robust.Shared.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;

namespace Content.IntegrationTests.Tests.Station;

[TestFixture]
[TestOf(typeof(SharedJobSystem))]
public sealed class JobTest
{
    private const string CommanderSpeechStyle = "commanderSpeech";
    private const string SpeechBubbleStyleComponent = "RMCSpeechBubbleSpecificStyle";
    private const string InnateCommandSpeechComponent = "InnateCommandSpeech";

    private static readonly string[] CommandBubbleOverrideJobs =
    [
        // Variants all derive from the same base
        "AU14JobGOVFORPlatCo",
        "AU14JobGOVFORPlatOp",
        "AU14JobGOVFORadvisor",
        "AU14JobGOVFORSectionSergeant",
        "AU14JobGOVFORSquadSergeant"
    ];

    private static readonly string[] AU14JuniorOfficerJobs =
    [
        "AU14JobGOVFORPlatOp",
        "AU14JobOPFORPlatOp",
        "AU14JobGOVFORPlatOpRMC",
        "AU14JobGOVFORPlatOpWYPMC",
        "AU14JobGOVFORPlatOpUPP",
    ];

    /// <summary>
    /// Ensures that every job belongs to at most 1 primary department.
    /// Having no primary or multiple EditorHidden departments is ok.
    /// </summary>
    [Test]
    public async Task PrimaryDepartmentsTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var exemptDepartments = new[] { "AU14DepartmentColonyCommand", "AU14DepartmentThreat", "AU14DepartmentThirdParty" };

            // only checking primary departments so don't bother with others
            var departments = prototypeManager.EnumeratePrototypes<DepartmentPrototype>()
                .Where(department => department.Primary && !department.EditorHidden)
                .ToList();
            var jobs = prototypeManager.EnumeratePrototypes<JobPrototype>();
            foreach (var job in jobs)
            {
                // not actually using the jobs system since that will return the first department
                // and we need to test that there is never more than 1, so it not sorting them is correct
                var primaries = 0;
                foreach (var department in departments)
                {
                    if (exemptDepartments.Contains(department.ID))
                        continue;
                    if (!department.Roles.Contains(job.ID))
                        continue;

                    primaries++;
                    Assert.That(primaries, Is.EqualTo(1), $"The job {job.ID} has more than 1 primary department!");
                }
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CommandBubbleOverrideJobsUseCommanderSpeechStyle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var jobId in CommandBubbleOverrideJobs)
            {
                var job = prototypeManager.Index<JobPrototype>(jobId);

                Assert.That(TryGetSpeechBubbleStyle(job, out var style), Is.True,
                    $"{jobId} should explicitly use the command speech bubble style.");
                Assert.That(style, Is.EqualTo(CommanderSpeechStyle),
                    $"{jobId} should use the command speech bubble style.");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AU14JuniorOfficerJobsDoNotGetInnateCommandSpeech()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var jobId in AU14JuniorOfficerJobs)
            {
                var job = prototypeManager.Index<JobPrototype>(jobId);

                Assert.That(HasSpecialComponent(job, InnateCommandSpeechComponent), Is.False,
                    $"{jobId} should get command-sized bubbles without {InnateCommandSpeechComponent}.");
            }
        });

        await pair.CleanReturnAsync();
    }

    private static bool TryGetSpeechBubbleStyle(JobPrototype job, out string style)
    {
        foreach (var addComponentSpecial in job.Special.OfType<AddComponentSpecial>())
        {
            if (!addComponentSpecial.Components.TryGetComponent(SpeechBubbleStyleComponent, out var component) ||
                component is not RMCSpeechBubbleSpecificStyleComponent bubbleStyle)
            {
                continue;
            }

            style = bubbleStyle.SpeechStyleClass;
            return true;
        }

        style = string.Empty;
        return false;
    }

    private static bool HasSpecialComponent(JobPrototype job, string componentName)
    {
        foreach (var addComponentSpecial in job.Special.OfType<AddComponentSpecial>())
        {
            if (addComponentSpecial.Components.ContainsKey(componentName))
                return true;
        }

        return false;
    }
}
