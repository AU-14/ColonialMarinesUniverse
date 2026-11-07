using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Input;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests.Movement;
[TestFixture]
public sealed class RegisteredMoverAndNightVisionMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: RmcMoverMergeFixture
  components:
  - type: RMCActiveInputMover
";

    [Test]
    public async Task DeclarativeRmcMoverSurvivesDetachAndReattachesInputMover()
    {
        var map = await Pair.CreateTestMap();
        EntityUid observer = default;

        await Pair.Server.WaitAssertion(() =>
        {
            observer = Pair.Server.EntMan.SpawnEntity("RmcMoverMergeFixture", map.GridCoords);
        });
        await Pair.RunTicksSync(1);

        await Pair.Server.WaitAssertion(() =>
        {
            var entities = Pair.Server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<RMCActiveInputMoverComponent>(observer), Is.True);
                Assert.That(entities.HasComponent<InputMoverComponent>(observer), Is.False);
                Assert.That(entities.HasComponent<TransientActiveInputMoverComponent>(observer), Is.False);
            });

            entities.EventBus.RaiseLocalEvent(observer, new PlayerAttachedEvent(observer, Pair.Server.PlayerMan.Sessions.Single()));
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<InputMoverComponent>(observer), Is.True);
                Assert.That(entities.HasComponent<RMCActiveInputMoverComponent>(observer), Is.True);
                Assert.That(entities.HasComponent<TransientActiveInputMoverComponent>(observer), Is.True,
                    "InputMover startup owns the separate upstream transient marker");
            });

            entities.EventBus.RaiseLocalEvent(observer, new PlayerDetachedEvent(observer, Pair.Server.PlayerMan.Sessions.Single()));
        });
        await Pair.RunTicksSync(1);

        await Pair.Server.WaitAssertion(() =>
        {
            var entities = Pair.Server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<InputMoverComponent>(observer), Is.False);
                Assert.That(entities.HasComponent<RMCActiveInputMoverComponent>(observer), Is.True,
                    "detach removes only runtime mover state, never the declarative RMC marker");
                Assert.That(entities.HasComponent<TransientActiveInputMoverComponent>(observer), Is.False);
            });

            entities.EventBus.RaiseLocalEvent(observer, new PlayerAttachedEvent(observer, Pair.Server.PlayerMan.Sessions.Single()));
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<InputMoverComponent>(observer), Is.True);
                Assert.That(entities.HasComponent<RMCActiveInputMoverComponent>(observer), Is.True);
                Assert.That(entities.HasComponent<TransientActiveInputMoverComponent>(observer), Is.True);
            });
        });
    }

}