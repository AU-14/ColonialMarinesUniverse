using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Interaction.Events;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._RMC14.Weapons;

[TestFixture, TestOf(typeof(TriggerSystem))]
public sealed class RMCGrenadeTimerTest
{
    [TestCase("CMGrenadeHighExplosive", 4)]
    [TestCase("CMGrenadeSmoke", 2)]
    [TestCase("RMCArmorHelmetHEFA", 4)]
    [TestCase("RMCGrenadeCustomMetalFoam", 3)]
    [TestCase("RMCGrenadeFlashBang", 4)]
    [TestCase("RMCGrenadeIncendiary", 4)]
    [TestCase("RMCGrenadeWhitePhosphorus", 2)]
    public async Task GrenadeWaitsForFuseBeforeTriggering(string prototype, double fuseSeconds)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid grenade = default;

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity(null, map.GridCoords);
            entMan.System<SkillsSystem>().SetSkill(user, "RMCSkillPolice", 2);
            grenade = entMan.SpawnEntity(prototype, map.GridCoords);
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
        await server.WaitAssertion(() => Assert.That(entMan.EntityExists(grenade), Is.True));

        var fuseTicks = (int) Math.Ceiling(fuseSeconds / timing.TickPeriod.TotalSeconds);
        await pair.RunTicksSync(fuseTicks + 5);
        await server.WaitAssertion(() => Assert.That(entMan.EntityExists(grenade), Is.False));
        await pair.CleanReturnAsync();
    }
}
