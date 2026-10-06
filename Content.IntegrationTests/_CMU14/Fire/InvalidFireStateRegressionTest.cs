using System.Reflection;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Fire;

[TestFixture]
public sealed class InvalidFireStateRegressionTest
{
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public async Task InvalidStackInputCannotPoisonAnExistingFire(float stacks)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var mob = entities.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(entities.System<SharedRMCFlammableSystem>().Ignite(mob, 30, 20, null), Is.True);
            var flammable = entities.GetComponent<FlammableComponent>(mob);
            var before = flammable.FireStacks;
            entities.System<FlammableSystem>().SetFireStacks(mob, stacks, flammable);
            Assert.That(flammable.FireStacks, Is.EqualTo(before));
            Assert.That(flammable.OnFire, Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(float.NaN, true)]
    [TestCase(float.PositiveInfinity, true)]
    [TestCase(float.NaN, false)]
    public async Task CorruptFireIsExtinguishedWithoutAbortingOtherBurningEntities(float stacks, bool canExtinguish)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var corrupt = entities.SpawnEntity("MobHuman", map.GridCoords);
            var healthy = entities.SpawnEntity("MobHuman", map.GridCoords);
            var fire = entities.System<SharedRMCFlammableSystem>();
            Assert.That(fire.Ignite(corrupt, 30, 20, null), Is.True);
            Assert.That(fire.Ignite(healthy, 30, 20, null), Is.True);
            var bad = entities.GetComponent<FlammableComponent>(corrupt);
            var good = entities.GetComponent<FlammableComponent>(healthy);
            bad.FireStacks = stacks;
            bad.CanExtinguish = canExtinguish;
            bad.NextUpdate = good.NextUpdate = pair.Server.ResolveDependency<IGameTiming>().CurTime;
            var damage = entities.System<DamageableSystem>();
            var before = damage.GetTotalDamage(healthy);
            Assert.DoesNotThrow(() => entities.System<FlammableSystem>().Update(0));
            Assert.Multiple(() =>
            {
                Assert.That(bad.FireStacks, Is.Zero);
                Assert.That(bad.OnFire, Is.False);
                Assert.That(bad.CanExtinguish, Is.EqualTo(canExtinguish));
                Assert.That(damage.GetTotalDamage(healthy), Is.GreaterThan(before));
            });
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public async Task InvalidProtectionCannotThrowFromDamageArithmetic(float protection)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var mob = entities.SpawnEntity("MobHuman", map.GridCoords);
            var flammable = entities.GetComponent<FlammableComponent>(mob);
            flammable.FireStacks = 2;
            flammable.OnFire = true;
            var damage = entities.System<DamageableSystem>();
            var before = damage.GetTotalDamage(mob);
            var apply = typeof(FlammableSystem).GetMethod("ApplyFireDamage", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.DoesNotThrow(() => apply.Invoke(entities.System<FlammableSystem>(), [mob, flammable, protection]));
            Assert.That(damage.GetTotalDamage(mob), Is.EqualTo(before));
        });
        await pair.CleanReturnAsync();
    }
}
