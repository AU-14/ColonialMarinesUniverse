using Content.IntegrationTests.Fixtures;
using Content.Server.Chemistry.TileReactions;
using Content.Server.CMU14.Fire;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Fire;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.Kitchen;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DamageOverlay;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using System.Reflection;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class SeptemberRegressionTest : GameTest
{
#pragma warning disable RA0002 // Arrange overlapping bonuses and hijack state at the system boundary.
    [TestCase(false)]
    [TestCase(true)]
    public async Task HijackEvolutionSurgeWinsRegardlessOfBoonCreationOrder(bool boonFirst)
    {
        await Server.WaitAssertion(() =>
        {
            var first = SEntMan.Spawn();
            var second = SEntMan.Spawn();
            SEntMan.EnsureComponent<EvolutionOverrideComponent>(first).Amount = boonFirst ? 2 : 10;
            SEntMan.EnsureComponent<EvolutionOverrideComponent>(second).Amount = boonFirst ? 10 : 2;
            var xeno = SEntMan.Spawn();
            var evolution = SEntMan.EnsureComponent<XenoEvolutionComponent>(xeno);
            evolution.RequiresGranter = false;
            evolution.GotPopup = true;
            evolution.LastPointsAt = TimeSpan.MinValue;
            evolution.Max = 100;
            SEntMan.System<XenoEvolutionSystem>().Update(0);
            Assert.That(evolution.Points, Is.EqualTo(FixedPoint2.New(10)));
            SEntMan.DeleteEntity(xeno);
            SEntMan.DeleteEntity(first);
            SEntMan.DeleteEntity(second);
        });
    }

    [Test]
    public async Task HijackedHiveRespawnsBurrowedLarvaeAtQueenWhileOldCoreStillExists()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var component = SEntMan.GetComponent<HiveComponent>(hive);
            var core = SEntMan.SpawnEntity(null, map.GridCoords);
            var queen = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(10, 0)));
            SEntMan.EnsureComponent<HiveCoreComponent>(core);
            SEntMan.EnsureComponent<XenoEvolutionGranterComponent>(queen);
            var system = SEntMan.System<SharedXenoHiveSystem>();
            system.SetHive(core, hive);
            system.SetHive(queen, hive);
            var spawn = typeof(SharedXenoHiveSystem).GetMethod("TryGetBurrowedLarvaSpawnPosition", BindingFlags.Instance | BindingFlags.NonPublic)!;
            object[] args = [new Entity<HiveComponent>(hive, component), default(EntityCoordinates)];
            Assert.That(spawn.Invoke(system, args), Is.True);
            Assert.That(args[1], Is.EqualTo(SEntMan.GetComponent<TransformComponent>(core).Coordinates));
            component.HijackSurged = true;
            Assert.That(spawn.Invoke(system, args), Is.True);
            Assert.That(args[1], Is.EqualTo(SEntMan.GetComponent<TransformComponent>(queen).Coordinates));
            SEntMan.DeleteEntity(core);
            SEntMan.DeleteEntity(queen);
            SEntMan.DeleteEntity(hive);
        });
    }
#pragma warning restore RA0002

    [Test]
    public async Task InjuredXenoReceivesDamageVignetteState()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var xeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = 50;
            SEntMan.System<DamageableSystem>().TryChangeDamage(xeno, damage, ignoreResistances: true);
            Assert.That(SEntMan.GetComponent<DamageOverlayComponent>(xeno).PainLevel, Is.GreaterThan(0));
            SEntMan.DeleteEntity(xeno);
        });
    }

    [Test]
    public async Task SpreadFireStartsSmallAndGrowsWhileWeaponFireIgnitesImmediately()
    {
        var map = await Pair.CreateTestMap();
        EntityUid spread = default;
        var mature = 0;
        await Server.WaitAssertion(() =>
        {
            spread = SEntMan.SpawnEntity("AU14SpreadTileFire", map.GridCoords);
            var direct = SEntMan.SpawnEntity("AU14TileFire", map.GridCoords.Offset(new Vector2(2, 0)));
            mature = SEntMan.GetComponent<RMCIgniteOnCollideComponent>(direct).Intensity;
            Assert.That(SEntMan.GetComponent<RMCIgniteOnCollideComponent>(spread).Intensity, Is.LessThan(mature));
            Assert.That(SEntMan.System<SharedAppearanceSystem>().TryGetData<TileFireVisuals>(spread, TileFireLayers.Base, out var visual), Is.True);
            Assert.That(visual, Is.EqualTo(TileFireVisuals.One));
            SEntMan.DeleteEntity(direct);
        });
        await Pair.RunSeconds(13);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<RMCIgniteOnCollideComponent>(spread).Intensity, Is.EqualTo(mature));
            Assert.That(SEntMan.System<SharedAppearanceSystem>().TryGetData<TileFireVisuals>(spread, TileFireLayers.Base, out var visual), Is.True);
            Assert.That(visual, Is.EqualTo(TileFireVisuals.Three));
            SEntMan.DeleteEntity(spread);
        });
    }

    [Test]
    public async Task ExtinguishingTileAlsoExtinguishesLooseItemsWithoutHotspots()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var item = SEntMan.SpawnEntity("CMPaper", map.GridCoords);
            var flam = SEntMan.EnsureComponent<FlamabilityComponent>(item);
            var fire = SEntMan.System<AU14FireSpreadSystem>();
            Assert.That(fire.Ignite(item, flam), Is.True);
            var tile = SEntMan.System<TurfSystem>().GetTileRef(map.GridCoords)!.Value;
            var water = Server.ResolveDependency<IPrototypeManager>().Index<ReagentPrototype>("Water");
            new ExtinguishTileReaction().TileReact(tile, water, FixedPoint2.New(1), SEntMan, null);
            Assert.That(flam.OnFire, Is.False);
            Assert.That(flam.FireVisualEntity, Is.Null);
            Assert.That(SEntMan.EntityExists(item), Is.True);
            SEntMan.DeleteEntity(item);
        });
    }

    [Test]
    public async Task BloodbursterKeepsPathogenHiveWhenInfectionHasNoSourceHive()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hive = SEntMan.SpawnEntity("CMUPathogenHive", map.GridCoords);
            var victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var larva = SEntMan.SpawnEntity("CMU14XenoBloodburster", map.GridCoords);
            var infection = SEntMan.EnsureComponent<VictimInfectedComponent>(victim);
            Assert.That(SEntMan.GetComponent<HiveMemberComponent>(larva).Hive, Is.EqualTo(hive));
            Assert.That(infection.Hive, Is.Null);
            SEntMan.System<SharedXenoParasiteSystem>().InsertLarva((victim, infection), larva);
            Assert.That(SEntMan.GetComponent<HiveMemberComponent>(larva).Hive, Is.EqualTo(hive));
            SEntMan.DeleteEntity(victim);
            if (SEntMan.EntityExists(larva)) SEntMan.DeleteEntity(larva);
            SEntMan.DeleteEntity(hive);
        });
    }

    [Test]
    public async Task CannabisDryingRecipesAreAvailableToKitchenMachines()
    {
        await Server.WaitAssertion(() =>
        {
            var recipes = SEntMan.System<RecipeManager>().Recipes;
            foreach (var id in new[] { "RecipeDriedCannabis", "RecipeDriedCannabisRainbow" })
                Assert.That(recipes.Any(recipe => recipe.ID == id), Is.True, id);
        });
    }
}
