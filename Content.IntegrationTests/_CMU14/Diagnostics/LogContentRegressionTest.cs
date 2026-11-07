#pragma warning disable RA0002 // Arrange the deployed parent and collapse do-after.
using Content.Shared._RMC14.Deploy;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class LogContentRegressionTest
{

    [Test]
    public async Task CollapsingTentCanPackChildrenWithoutEntityStorage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var user = entities.SpawnEntity(null, map.GridCoords);
            var original = entities.SpawnEntity(null, map.GridCoords);
            var tent = entities.SpawnEntity(null, map.GridCoords);
            var deployable = entities.AddComponent<RMCDeployableComponent>(original);
            deployable.DeploySetups.Add(new RMCDeploySetup { Mode = RMCDeploySetupMode.ReactiveParental });
            deployable.CollapseSound = null;
            var deployed = entities.AddComponent<RMCDeployedEntityComponent>(tent);
            deployed.OriginalEntity = original;
            var containers = entities.System<SharedContainerSystem>();
            containers.Insert(original, containers.EnsureContainer<Container>(tent, "storage"));
            var ev = new RMCParentalCollapseDoAfterEvent();
            ev.DoAfter = new DoAfter(0, new DoAfterArgs(entities, user, TimeSpan.Zero, ev, tent), TimeSpan.Zero);
            entities.EventBus.RaiseLocalEvent(tent, ev);
            Assert.That(containers.GetContainer(original, "storage").Contains(tent), Is.True);
            Assert.That(containers.IsEntityInContainer(original), Is.False);
        });
        await pair.CleanReturnAsync();
    }
}
