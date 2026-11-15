using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMULoggedReferenceLifetimeTest : GameTest
{
    [Test]
    public async Task DeletingSecretionOwnerClearsSurvivorsWithoutClearingAnotherOwner()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var owner = SEntMan.SpawnEntity(null, map.GridCoords);
            var other = SEntMan.SpawnEntity(null, map.GridCoords);
            var list = SEntMan.AddComponent<XenoSecretionListComponent>(owner);
            var otherList = SEntMan.AddComponent<XenoSecretionListComponent>(other);
            var structure = SEntMan.SpawnEntity(null, map.GridCoords);
            var transferred = SEntMan.SpawnEntity(null, map.GridCoords);
            var limited = SEntMan.AddComponent<XenoSecretionLimitedComponent>(structure);
            var otherLimited = SEntMan.AddComponent<XenoSecretionLimitedComponent>(transferred);
#pragma warning disable RA0002 // Arrange the ownership graph that construction and evolution produce.
            limited.Xeno = owner;
            otherLimited.Xeno = other;
            list.Built["WallXenoResin"] = [SEntMan.GetNetEntity(structure), SEntMan.GetNetEntity(transferred)];
            otherList.Built["WallXenoResin"] = [SEntMan.GetNetEntity(transferred)];
#pragma warning restore RA0002
            SEntMan.DeleteEntity(owner);
            Assert.That(limited.Xeno, Is.Null);
            Assert.That(otherLimited.Xeno, Is.EqualTo(other), "Removing an old owner must preserve transferred ownership.");
            Assert.DoesNotThrow(() => SEntMan.GetComponentState(SEntMan.EventBus, limited, null, GameTick.Zero));
            SEntMan.DeleteEntity(other);
            Assert.That(otherLimited.Xeno, Is.Null);
        });
    }

    [Test]
    public async Task DeletedGunUserReleasesAuthorizationBeforeAnotherInteraction()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var gun = SEntMan.SpawnEntity(null, map.GridCoords);
            var idLock = SEntMan.AddComponent<GunIDLockComponent>(gun);
            var owner = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var nextUser = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var shot = new AttemptShootEvent(owner, null, map.GridCoords, null);
            SEntMan.EventBus.RaiseLocalEvent(gun, ref shot);
            Assert.That(idLock.User, Is.EqualTo(owner));
            shot = new AttemptShootEvent(nextUser, null, map.GridCoords, null);
            SEntMan.EventBus.RaiseLocalEvent(gun, ref shot);
            Assert.That(shot.Cancelled, Is.True);
            SEntMan.DeleteEntity(owner);
            Assert.That(idLock.User, Is.EqualTo(EntityUid.Invalid));
            Assert.DoesNotThrow(() => SEntMan.GetComponentState(SEntMan.EventBus, idLock, null, GameTick.Zero));
            shot = new AttemptShootEvent(nextUser, null, map.GridCoords, null);
            SEntMan.EventBus.RaiseLocalEvent(gun, ref shot);
            Assert.That(shot.Cancelled, Is.False);
            Assert.That(idLock.User, Is.EqualTo(nextUser));
        });
    }

}
