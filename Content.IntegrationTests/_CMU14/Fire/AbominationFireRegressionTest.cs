using Content.Shared._RMC14.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

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
        var entities = pair.Server.EntMan;
        var damage = entities.System<DamageableSystem>();
        EntityUid biomass = default;
        FixedPoint2 before = default;
        await pair.Server.WaitAssertion(() =>
        {
            biomass = entities.SpawnEntity(prototype, map.GridCoords);
            Assert.That(entities.System<SharedRMCFlammableSystem>().Ignite(biomass, 30, 2, null), Is.True);
            before = damage.GetTotalDamage(biomass);
        });

        // fire walks a snapshot rebuilt once a second, so a fresh spawn can wait up to a second for
        // its first burn on a pooled server. run real ticks instead of poking Update
        var burned = false;
        for (var i = 0; i < 12 && !burned; i++)
        {
            await pair.RunSeconds(0.25f);
            await pair.Server.WaitPost(() =>
                burned = !entities.EntityExists(biomass) || damage.GetTotalDamage(biomass) > before);
        }

        Assert.That(burned, Is.True, "Burning biomorphs and tendons must take heat damage.");

        var burntOut = false;
        for (var i = 0; i < 60 && !burntOut; i++)
        {
            await pair.RunSeconds(1);
            await pair.Server.WaitPost(() =>
                burntOut = !entities.EntityExists(biomass) || !entities.GetComponent<FlammableComponent>(biomass).OnFire);
        }

        Assert.That(burntOut, Is.True, "A finite fire application must burn out or destroy the biomass.");
        await pair.CleanReturnAsync();
    }
}
