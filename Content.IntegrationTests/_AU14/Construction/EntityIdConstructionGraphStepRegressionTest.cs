using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;

namespace Content.IntegrationTests._AU14.Construction;

[TestFixture]
[TestOf(typeof(EntityIdConstructionGraphStep))]
public sealed class EntityIdConstructionGraphStepRegressionTest : GameTest
{
    [Test]
    public async Task MissAprilGraphPreservesExactPrototypeMatchingStep()
    {
        await Server.WaitAssertion(() =>
        {
            var graph = SProtoMan.Index<ConstructionGraphPrototype>(
                "AU14CustomGraph_CMPosterMissApril__AU14__Debug");
            var edge = graph.Edge("start", "target");
            Assert.That(edge, Is.Not.Null);

            var step = edge!.Steps.Single();
            Assert.That(step, Is.TypeOf<EntityIdConstructionGraphStep>());
            var entityId = (EntityIdConstructionGraphStep) step;
            Assert.Multiple(() =>
            {
                Assert.That(entityId.EntityId, Is.EqualTo("CMPosterMissApril"));
                Assert.That(entityId.Consume, Is.True);
                Assert.That(entityId.DoAfter, Is.EqualTo(1f));
            });

            var matching = SEntMan.SpawnEntity("CMPosterMissApril", MapCoordinates.Nullspace);
            var unrelated = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(entityId.EntityValid(matching, SEntMan, SEntMan.ComponentFactory), Is.True);
                    Assert.That(entityId.EntityValid(unrelated, SEntMan, SEntMan.ComponentFactory), Is.False);
                });
            }
            finally
            {
                SEntMan.DeleteEntity(matching);
                SEntMan.DeleteEntity(unrelated);
            }
        });
    }
}
