#pragma warning disable RA0002 // Inspect accumulated damage in the regression assertions.

using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Synth;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Fire;

[TestFixture]
public sealed class SyntheticFireDamageTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [TestCase(false, 0.9f)]
    [TestCase(true, 0.65f)]
    public async Task IgnitionAndBurnTicksRespectSyntheticHeatResistance(bool joe, float resistance)
    {
        PreFinalizeHook += () => TestContext.Out.WriteLine(TestContext.CurrentContext.Result.Message);
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mob = SEntMan.SpawnEntity(joe ? "AU14MobWorkingJoeColony" : "MobHuman", map.GridCoords);
            SEntMan.EnsureComponent<SynthComponent>(mob);
            var fire = Server.System<SharedRMCFlammableSystem>();
            var damage = SEntMan.GetComponent<DamageableComponent>(mob);
            var heat = new DamageSpecifier { DamageDict = { ["Heat"] = 30 } };
            fire.DamageFromFire(mob, heat);
            Assert.That(damage.TotalDamage.Float(), Is.EqualTo(30 * resistance).Within(0.01), "Initial fire contact uses body resistance.");
            Server.System<DamageableSystem>().TryChangeDamage(mob, -damage.Damage, true);
            Assert.That(fire.Ignite(mob, 30, 20, null), Is.True);
            var flammable = SEntMan.GetComponent<FlammableComponent>(mob);
            flammable.NextUpdate = Server.ResolveDependency<IGameTiming>().CurTime;
            Server.System<FlammableSystem>().Update(0);
            var expectedBurn = flammable.Damage.GetTotal().Float() * 6 * resistance;
            Assert.That(damage.TotalDamage.Float(), Is.EqualTo(expectedBurn).Within(0.02), "Ongoing burns must use the same resistance as contact damage.");
            fire.Extinguish(mob);
            Assert.That(flammable.OnFire, Is.False);
        });
    }
}
