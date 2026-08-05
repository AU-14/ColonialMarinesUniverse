using Content.Server.StationEvents.Components;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
[TestOf(typeof(BureaucraticErrorRuleComponent))]
public sealed class BureaucraticErrorIgnoredJobsTest
{
    private static readonly ProtoId<JobPrototype>[] ExpectedIgnoredJobs =
    {
        "StationAi",
        "ResearchAssistant",
        "MedicalIntern",
        "SecurityCadet",
        "TechnicalAssistant",
    };

    [Test]
    public async Task PreservesDepartmentEntryJobs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var rule = entMan.SpawnEntity("BureaucraticError", MapCoordinates.Nullspace);
            var component = entMan.GetComponent<BureaucraticErrorRuleComponent>(rule);

            Assert.That(component.IgnoredJobs, Is.EquivalentTo(ExpectedIgnoredJobs));
            entMan.DeleteEntity(rule);
        });

        await pair.CleanReturnAsync();
    }
}
