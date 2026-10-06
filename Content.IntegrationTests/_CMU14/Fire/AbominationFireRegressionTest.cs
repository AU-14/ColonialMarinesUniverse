using Content.Server.Atmos.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Fire;

[TestFixture]
public sealed class AbominationFireRegressionTest
{
    [TestCase("AU14BiomorphGrunt")]
    [TestCase("AU14BiomorphSkitter")]
    [TestCase("AU14BiomorphSpider")]
    [TestCase("AU14BiomorphFleshKudzu")]
    public async Task IncendiaryFireDamagesBiomassAndBurnsOut(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var biomass = entities.SpawnEntity(prototype, map.GridCoords);
            var fire = entities.System<SharedRMCFlammableSystem>();
            Assert.That(fire.Ignite(biomass, 30, 2, null), Is.True);
            var flammable = entities.GetComponent<FlammableComponent>(biomass);
            var damage = entities.System<DamageableSystem>();
            var before = damage.GetTotalDamage(biomass);
            var timing = pair.Server.ResolveDependency<IGameTiming>();
            flammable.NextUpdate = timing.CurTime;
            entities.System<FlammableSystem>().Update(0);
            Assert.That(damage.GetTotalDamage(biomass), Is.GreaterThan(before), "Burning biomorphs and tendons must take heat damage.");
            for (var i = 0; i < 50 && entities.EntityExists(biomass) && flammable.OnFire; i++)
            {
                flammable.NextUpdate = timing.CurTime;
                entities.System<FlammableSystem>().Update(0);
            }
            Assert.That(!entities.EntityExists(biomass) || !flammable.OnFire, Is.True,
                "A finite fire application must burn out or destroy the biomass.");
        });
        await pair.CleanReturnAsync();
    }
}
