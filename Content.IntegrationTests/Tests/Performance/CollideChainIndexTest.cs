#pragma warning disable RA0002 // Exercise authoritative/admin replacement and chain lifecycle.
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.OnCollide;
using Content.Shared._RMC14.OnCollide;

namespace Content.IntegrationTests.Tests.Performance;

[TestFixture]
public sealed class CollideChainIndexTest : GameTest
{
    [Test]
    public async Task DeletionOnlyVisitsReferencingChainsAndReplacementReindexes()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<OnCollideSystem>();
            var target = SEntMan.SpawnEntity(null, map.GridCoords);
            var other = SEntMan.SpawnEntity(null, map.GridCoords);
            var chains = new List<Entity<CollideChainComponent>>();
            for (var i = 0; i < 100; i++) chains.Add(system.SpawnChain());
            var add = typeof(SharedOnCollideSystem).GetMethod("AddToChain", BindingFlags.NonPublic | BindingFlags.Instance)!;
            bool Hit(Entity<CollideChainComponent> chain, EntityUid uid) =>
                (bool) add.Invoke(system, new object[] { new Entity<CollideChainComponent?>(chain, chain.Comp), uid })!;
            Assert.That(Hit(chains[0], target), Is.True);
            Assert.That(Hit(chains[0], target), Is.False);
            Assert.That(Hit(chains[1], target), Is.True);
            chains[2].Comp.Hit = new() { target };
            chains[2].Comp.Hit = new() { other }; // Old reverse links must be removed.
            var removed = chains[3];
            removed.Comp.Hit = new() { target };
            SEntMan.RemoveComponent<CollideChainComponent>(removed);
            var before = system.ChainCleanupVisits;
            SEntMan.DeleteEntity(target);
            Assert.That(system.ChainCleanupVisits - before, Is.EqualTo(2));
            Assert.That(chains[0].Comp.Hit, Is.Empty);
            Assert.That(chains[1].Comp.Hit, Is.Empty);
            Assert.That(chains[2].Comp.Hit, Does.Contain(other));
            SEntMan.DeleteEntity(other);
            Assert.That(system.ChainCleanupVisits - before, Is.EqualTo(3));
            Assert.That(chains[2].Comp.Hit, Is.Empty);
            foreach (var chain in chains) SEntMan.DeleteEntity(chain);
            TestContext.Progress.WriteLine("PERF chain_cleanup chains=100 deletedTargets=2 visitedChains=3");
        });
    }
    [Test]
    public async Task GeneratedClientStateRefreshesReverseIndex()
    {
        await Client.WaitAssertion(() =>
        {
            var system = Client.System<SharedOnCollideSystem>();
            var chain = system.SpawnChain();
            var oldTarget = CEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var newTarget = CEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                void Apply(EntityUid target)
                {
                    var state = new CollideChainComponent.CollideChainComponent_AutoState
                    {
                        Hit = new() { CEntMan.GetNetEntity(target) },
                    };
                    var ev = new ComponentHandleState(state, null);
                    CEntMan.EventBus.RaiseComponentEvent(chain.Owner, chain.Comp, ref ev);
                }

                Apply(oldTarget);
                Assert.That(chain.Comp.Hit, Does.Contain(oldTarget));
                Apply(newTarget);
                var before = system.ChainCleanupVisits;
                CEntMan.DeleteEntity(oldTarget);
                Assert.That(system.ChainCleanupVisits, Is.EqualTo(before), "Replaced state must drop old reverse links.");
                CEntMan.DeleteEntity(newTarget);
                Assert.That(system.ChainCleanupVisits - before, Is.EqualTo(1));
                Assert.That(chain.Comp.Hit, Is.Empty, "Deletion must clean up a hit populated through generated state.");
            }
            finally
            {
                CEntMan.DeleteEntity(chain.Owner);
                if (CEntMan.EntityExists(oldTarget)) CEntMan.DeleteEntity(oldTarget);
                if (CEntMan.EntityExists(newTarget)) CEntMan.DeleteEntity(newTarget);
            }
        });
    }
}
