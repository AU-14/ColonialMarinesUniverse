using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Atmos;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Atmos;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Research;

/// <summary>
/// FLOW-23: tile fires of Firepenetrating level 3+ melt the damageable structures on their tile over time;
/// lower levels and indestructible hull walls are unaffected.
/// </summary>
[TestFixture]
public sealed class CMUFireMeltingTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private const string Fire = "RMCTileFire";
    private const string Barricade = "CMBarricadeMetal";
    private const string Wall = "CMWallMetal";
    private const string HullWall = "CMUWallWoodReinforcedHeavy";

    [Test]
    public async Task LevelThreeFireMeltsStructures()
    {
        var (barricade, wall, hull) = await BurnFor(level: 3, intensity: 30, seconds: 3.5);

        await Server.WaitAssertion(() =>
        {
            var damageable = SEntMan.System<DamageableSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.EntityExists(barricade), Is.True);
                Assert.That(damageable.GetTotalDamage(barricade), Is.GreaterThan(FixedPoint2.Zero),
                    "a level 3 fire should melt the barricade it burns on");
                Assert.That(damageable.GetTotalDamage(wall), Is.GreaterThan(FixedPoint2.Zero),
                    "a level 3 fire should melt the wall it burns on");
                Assert.That(SEntMan.EntityExists(hull), Is.True, "the hull wall must survive");
                Assert.That(SEntMan.HasComponent<DamageableComponent>(hull), Is.False,
                    "the hull wall is indestructible and must not gain damage");
            });
        });
    }

    [Test]
    public async Task LevelTwoFireDoesNotMelt()
    {
        var (barricade, wall, _) = await BurnFor(level: 2, intensity: 30, seconds: 3.5);

        await Server.WaitAssertion(() =>
        {
            var damageable = SEntMan.System<DamageableSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(damageable.GetTotalDamage(barricade), Is.EqualTo(FixedPoint2.Zero),
                    "below level 3 the fire must not melt structures");
                Assert.That(damageable.GetTotalDamage(wall), Is.EqualTo(FixedPoint2.Zero));
            });
        });
    }

    [Test]
    public void MeltDamageFormula()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CMUStructureMeltingFireSystem.MeltDamage(2, 30), Is.EqualTo(0f));
            Assert.That(CMUStructureMeltingFireSystem.MeltDamage(3, 30), Is.EqualTo(30f).Within(0.001f));
            Assert.That(CMUStructureMeltingFireSystem.MeltDamage(5, 30), Is.EqualTo(90f).Within(0.001f));
            Assert.That(CMUStructureMeltingFireSystem.MeltDamage(5, 0), Is.EqualTo(0f));
        });
    }

    [Test]
    public void EffectiveFirePenetratingLevelDefaults()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new ReagentPrototype().FirePenetratingLevel, Is.EqualTo(0));
            Assert.That(new ReagentPrototype().EffectiveFirePenetratingLevel, Is.EqualTo(0));
            Assert.That(new ReagentPrototype { FirePenetrating = true }.EffectiveFirePenetratingLevel, Is.EqualTo(1),
                "the legacy bool flag counts as level 1");
            Assert.That(new ReagentPrototype { FirePenetrating = true, FirePenetratingLevel = 4 }.EffectiveFirePenetratingLevel,
                Is.EqualTo(4));
            Assert.That(new ReagentPrototype { FirePenetratingLevel = 3 }.EffectiveFirePenetratingLevel, Is.EqualTo(3));
        });
    }

    /// <summary>
    /// Spawns a fire, a barricade, a damageable wall and an indestructible hull wall on one tile, marks the fire
    /// as melting at <paramref name="level"/>, and runs the server for <paramref name="seconds"/>.
    /// </summary>
    private async Task<(EntityUid Barricade, EntityUid Wall, EntityUid Hull)> BurnFor(int level, int intensity, double seconds)
    {
        var map = await Pair.CreateTestMap();
        EntityUid barricade = default;
        EntityUid wall = default;
        EntityUid hull = default;

        await Server.WaitAssertion(() =>
        {
            var mapSystem = SEntMan.System<SharedMapSystem>();
            var coords = mapSystem.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, map.Tile.GridIndices);

            barricade = SEntMan.SpawnEntity(Barricade, coords);
            wall = SEntMan.SpawnEntity(Wall, coords);
            hull = SEntMan.SpawnEntity(HullWall, coords);
            var fire = SEntMan.SpawnEntity(Fire, coords);

            Assert.Multiple(() =>
            {
                Assert.That(SComp<TransformComponent>(barricade).Anchored, Is.True, "the barricade should be anchored");
                Assert.That(SComp<TransformComponent>(wall).Anchored, Is.True, "the wall should be anchored");
                Assert.That(SComp<TransformComponent>(hull).Anchored, Is.True, "the hull wall should be anchored");
                Assert.That(SComp<TransformComponent>(fire).Anchored, Is.True, "the fire should be anchored");
            });

            var damageable = SEntMan.System<DamageableSystem>();
            Assert.That(damageable.GetTotalDamage(barricade), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(damageable.GetTotalDamage(wall), Is.EqualTo(FixedPoint2.Zero));

            var melt = SEntMan.EnsureComponent<CMUStructureMeltingFireComponent>(fire);
            melt.Level = level;
            melt.Intensity = intensity;
        });

        var timing = Server.ResolveDependency<IGameTiming>();
        var ticks = (int) Math.Ceiling(seconds / timing.TickPeriod.TotalSeconds);
        await Pair.RunTicksSync(ticks);

        return (barricade, wall, hull);
    }
}
