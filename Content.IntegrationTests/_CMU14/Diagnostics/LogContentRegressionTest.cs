#pragma warning disable RA0002 // Set up the deployment state represented by the log.
using System.Reflection;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Deploy;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class LogContentRegressionTest
{
    [Test]
    public async Task ColonyPoliceHaveScannersAndIPIEJobsHaveLocalizedNames()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            foreach (var gear in new ProtoId<StartingGearPrototype>[] { "AU14GearCivilianCMBDeputy", "AU14GearCivilianCMBMarshal" })
                Assert.That(prototypes.Index(gear).Storage["back"], Does.Contain((EntProtoId) "ForensicScanner"));
            var loc = pair.Server.ResolveDependency<ILocalizationManager>();
            foreach (var job in new ProtoId<JobPrototype>[] { "AU14JobIPIELawyer", "AU14JobIPIEPPO" })
                Assert.That(loc.TryGetString(prototypes.Index(job).Name, out _), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("RMCPouchMedicalSocMarineRaiderFill", 11)] // Two stims in the current authored loadout.
    [TestCase("RMCPouchToolsSocRCMPVEAssaultEngi", 9)]
    public async Task AuthoredPouchContentsAllFit(string prototype, int count)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var pouch = entities.SpawnEntity(prototype, map.GridCoords);
            Assert.That(entities.GetComponent<StorageComponent>(pouch).Container.ContainedEntities, Has.Count.EqualTo(count));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AlreadyBrokenExplosionBlockerDoesNotSchedulePastIterations()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var explosion = pair.Server.System<ExplosionSystem>();
            var method = typeof(ExplosionSystem).GetMethod("GetExplosionTolerance", BindingFlags.Instance | BindingFlags.NonPublic,
                [typeof(FixedPoint2), typeof(FixedPoint2), typeof(SortedDictionary<FixedPoint2, FixedPoint2>)])!;
            var result = (FixedPoint2) method.Invoke(explosion,
                [(FixedPoint2) (-20), (FixedPoint2) 10, new SortedDictionary<FixedPoint2, FixedPoint2>()])!;
            Assert.That(result, Is.EqualTo(FixedPoint2.Zero), "Destroyed blockers must release now, never into a tile set already being enumerated.");
        });
        await pair.CleanReturnAsync();
    }

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
