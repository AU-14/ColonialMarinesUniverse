using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Targeting;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMUServerLogLifetimeTest : GameTest
{
    [Test]
    public async Task DeletedUserCancelsTargetingAndReleasesTargets()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var targeting = SEntMan.System<SharedRMCTargetingSystem>();
            var equipment = SEntMan.SpawnEntity(null, map.GridCoords);
            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            var target = SEntMan.SpawnEntity(null, map.GridCoords);
            targeting.Target(equipment, user, target, 30);

            SEntMan.DeleteEntity(user);

            Assert.That(SEntMan.HasComponent<TargetingComponent>(equipment), Is.False,
                "A targeting operation cannot retain its deleted user for network serialization or completion.");
            Assert.That(SEntMan.HasComponent<RMCTargetedComponent>(target), Is.False,
                "Cancelling the operation must also remove the target's laser marker.");
        });
    }

    [Test]
    public async Task DeletingOneLaserPreservesAnotherWithoutRetainingItsSource()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var targeting = SEntMan.System<SharedRMCTargetingSystem>();
            var removed = SEntMan.SpawnEntity(null, map.GridCoords);
            var remaining = SEntMan.SpawnEntity(null, map.GridCoords);
            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            var target = SEntMan.SpawnEntity(null, map.GridCoords);
            targeting.Target(removed, user, target, 30);
            targeting.Target(remaining, user, target, 30);
            targeting.Update(0.1f);

            SEntMan.DeleteEntity(removed);

            var marker = SEntMan.GetComponent<RMCTargetedComponent>(target);
            Assert.That(marker.TargetedBy, Is.EqualTo(new[] { remaining }));
            Assert.That(marker.AlphaMultipliers.Keys, Is.EqualTo(new[] { remaining }));
            SEntMan.GetComponentState(SEntMan.EventBus, marker, null, GameTick.Zero);

            SEntMan.DeleteEntity(target);
            Assert.That(SEntMan.HasComponent<TargetingComponent>(remaining), Is.False,
                "Deleting the last target must release the surviving targeting operation too.");
        });
    }

}
