using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Targeting;
using Content.Shared.CMU14.Ghost;
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
    public async Task StoppingOneTargetRemovesItsDurationsBeforeItIsDeleted()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var targeting = SEntMan.System<SharedRMCTargetingSystem>();
            var equipment = SEntMan.SpawnEntity(null, map.GridCoords);
            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            var stopped = SEntMan.SpawnEntity(null, map.GridCoords);
            var remaining = SEntMan.SpawnEntity(null, map.GridCoords);
            targeting.Target(equipment, user, stopped, 30);
            targeting.Target(equipment, user, remaining, 30);

            targeting.StopTargeting(equipment, stopped);
            SEntMan.DeleteEntity(stopped);

            var active = SEntMan.GetComponent<TargetingComponent>(equipment);
            Assert.Multiple(() =>
            {
                Assert.That(active.Targets, Is.EqualTo(new[] { remaining }));
                Assert.That(active.LaserDurations.Keys, Is.EqualTo(new[] { remaining }));
                Assert.That(active.OriginalLaserDurations.Keys, Is.EqualTo(new[] { remaining }));
            });
            SEntMan.GetComponentState(SEntMan.EventBus, active, null, GameTick.Zero);
            targeting.StopTargeting(equipment, remaining);
            Assert.That(SEntMan.HasComponent<TargetingComponent>(equipment), Is.False);
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

    [Test]
    public async Task DeletingAppearanceSourcesPreservesReplacementThenClearsDeletedSource()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var appearance = SEntMan.System<CMUGhostAppearanceSystem>();
            var ghost = SEntMan.SpawnEntity(null, map.GridCoords);
            var original = SEntMan.SpawnEntity(null, map.GridCoords);
            var replacement = SEntMan.SpawnEntity(null, map.GridCoords);
            appearance.SetSource(ghost, original);
            appearance.SetSource(ghost, replacement);

            SEntMan.DeleteEntity(original);
            var component = SEntMan.GetComponent<CMUGhostAppearanceComponent>(ghost);
            Assert.That(component.Source, Is.EqualTo(replacement));

            SEntMan.DeleteEntity(replacement);
            Assert.That(component.Source, Is.Null,
                "The surviving ghost must not serialize a deleted body after gibbing or cleanup.");
            SEntMan.GetComponentState(SEntMan.EventBus, component, null, GameTick.Zero);
        });
    }
}
