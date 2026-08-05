using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class CMULimbPrinterMatrixBeakerTest : GameTest
{
    private static readonly EntProtoId MatrixBeaker = "CMULimbPrinterMatrixBeaker";

    [Test]
    public async Task LegacySolutionDoesNotOverlapMapInitSolution()
    {
        var prototypeManager = Server.ResolveDependency<IPrototypeManager>();
        var componentFactory = Server.ResolveDependency<IComponentFactory>();

        await Server.WaitAssertion(() =>
        {
            var prototype = prototypeManager.Index<EntityPrototype>(MatrixBeaker);

#pragma warning disable CS0612 // The compatibility component is what this regression test guards against.
            Assert.That(
                prototype.TryComp<SolutionContainerManagerComponent>(out _, componentFactory),
                Is.False);
#pragma warning restore CS0612
            Assert.That(prototype.TryComp<SolutionComponent>(out var current, componentFactory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(current!.Id, Is.EqualTo("beaker"));
                Assert.That(current.Solution.MaxVolume.Int(), Is.EqualTo(120));
                Assert.That(
                    current.Solution.GetReagentQuantity(new ReagentId("CMUBiogenicMatrix", null)).Int(),
                    Is.EqualTo(120));
            });
        });
    }
}
