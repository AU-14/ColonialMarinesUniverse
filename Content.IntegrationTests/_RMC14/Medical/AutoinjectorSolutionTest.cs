using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14.Medical;

[TestFixture]
public sealed class AutoinjectorSolutionTest
{
    [TestCase("CMBicaridineAutoInjector")]
    [TestCase("CMDexalinPlusAutoInjector")]
    [TestCase("RMCDyloveneAutoInjector")]
    [TestCase("CMEpinephrineAutoInjector")]
    [TestCase("CMInaprovalineAutoInjector")]
    [TestCase("CMKelotaneAutoInjector")]
    [TestCase("CMTricordrazineAutoInjector")]
    [TestCase("RMCIronAutoInjector")]
    [TestCase("AU14NaloxoneAutoInjector")]
    [TestCase("AU14FirstAidAutoInjectorNoSkill")]
    [TestCase("AU14OxycodoneAutoInjector")]
    [TestCase("AU14ParacetamolAutoInjector")]
    [TestCase("AU14TramadolAutoInjector")]
    public async Task MedicalVendorAutoinjectorsSpawnFilled(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var solution = entMan.System<SharedSolutionContainerSystem>();
            var injector = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.TryGetComponent<SolutionComponent>(injector, out var injectorSolution), Is.True);
                Assert.That(injectorSolution.Id, Is.EqualTo("pen"));
                Assert.That(injectorSolution.Solution.Volume, Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(entMan.HasComponent<SolutionContainerManagerComponent>(injector), Is.False);
                Assert.That(entMan.GetComponent<ExaminableSolutionComponent>(injector).Solution, Is.EqualTo("pen"));
                Assert.That(entMan.GetComponent<SolutionContainerVisualsComponent>(injector).SolutionName, Is.EqualTo("pen"));
                Assert.That(solution.TryGetSolution(injector, "pen", out _, out var contents), Is.True);
                Assert.That(contents.Volume, Is.GreaterThan(FixedPoint2.Zero));
            }
            finally
            {
                entMan.DeleteEntity(injector);
            }
        });

        await pair.CleanReturnAsync();
    }
}
