using Content.IntegrationTests.Tests.Helpers;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Interaction.Events;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._RMC14.Weapons;

[TestFixture, TestOf(typeof(TriggerSystem))]
public sealed class RMCGrenadeTimerTest
{
    public sealed class GrenadeTriggerListenerSystem : TestListenerSystem<TriggerEvent>;

    [TestCase("CMGrenadeFrag", 4, true, true)]
    [TestCase("CMGrenadeFragOld", 4, true, true)]
    [TestCase("CMGrenadeHighExplosive", 4, true, false)]
    [TestCase("CMGrenadeSmoke", 2, true, false)]
    [TestCase("RMCArmorHelmetHEFA", 4, true, true)]
    [TestCase("RMCGrenadeCustomMetalFoam", 3, true, false)]
    [TestCase("RMCGrenadeFlashBang", 4, true, false)]
    [TestCase("RMCGrenadeIED", 4, true, true)]
    [TestCase("RMCGrenadeIncendiary", 4, true, false)]
    [TestCase("RMCGrenadeWhitePhosphorus", 2, true, false)]
    public async Task GrenadeWaitsForFuseBeforeTriggering(
        string prototype,
        double fuseSeconds,
        bool deleteAfterTrigger,
        bool expectShrapnel)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid grenade = default;

        int CountShrapnel()
        {
            var count = 0;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out _, out var metadata))
            {
                if (!metadata.Deleted && metadata.EntityPrototype?.ID == "CMProjectileShrapnel")
                    count++;
            }

            return count;
        }

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity(null, map.GridCoords);
            entMan.System<SkillsSystem>().SetSkill(user, "RMCSkillPolice", 2);
            grenade = entMan.SpawnEntity(prototype, map.GridCoords);
            entMan.EnsureComponent<TestListenerComponent>(grenade);
            var useEvent = new UseInHandEvent(user);

            entMan.EventBus.RaiseLocalEvent(grenade, useEvent);

            var remaining = entMan.System<TriggerSystem>().GetRemainingTime(grenade);
            Assert.Multiple(() =>
            {
                Assert.That(useEvent.Handled, Is.True);
                Assert.That(entMan.HasComponent<ActiveTimerTriggerComponent>(grenade), Is.True);
                Assert.That(remaining, Is.EqualTo(TimeSpan.FromSeconds(fuseSeconds)).Within(timing.TickPeriod));
            });
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var listener = entMan.System<GrenadeTriggerListenerSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(entMan.EntityExists(grenade), Is.True);
                Assert.That(listener.Count(grenade, ev => ev.Key == TriggerSystem.DefaultTriggerKey), Is.Zero);
                Assert.That(CountShrapnel(), Is.Zero);
            });
        });

        var fuseTicks = (int) Math.Ceiling(fuseSeconds / timing.TickPeriod.TotalSeconds);
        await pair.RunTicksSync(fuseTicks + 5);
        await server.WaitAssertion(() =>
        {
            var listener = entMan.System<GrenadeTriggerListenerSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(entMan.EntityExists(grenade), Is.EqualTo(!deleteAfterTrigger));
                if (!deleteAfterTrigger)
                    Assert.That(listener.Count(grenade, ev => ev.Key == TriggerSystem.DefaultTriggerKey), Is.EqualTo(1));

                if (expectShrapnel)
                    Assert.That(CountShrapnel(), Is.GreaterThan(0));
            });
        });
        await pair.CleanReturnAsync();
    }
}
