#pragma warning disable RA0002 // Control tile effects to distinguish no-op refreshes from armor changes.

using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Damage;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Performance;

[TestFixture]
public sealed class FireDirtyRegressionTest : GameTest
{
    [Test]
    public async Task UnchangedTileEffectsDoNotDirtyButArmorChangesDo()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        EntityUid fire = default;
        GameTick before = default;
        await Server.WaitAssertion(() =>
        {
            fire = SEntMan.SpawnEntity("RMCTileFire", map.GridCoords);
            var ignition = SEntMan.GetComponent<RMCIgniteOnCollideComponent>(fire);
            ignition.TileDamage = null;
            target = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<PhysicsComponent>(target);
            var stepping = SEntMan.AddComponent<SteppingOnFireComponent>(target);
            Server.System<SharedRMCFlammableSystem>().Update(0f);
            before = stepping.LastModifiedTick;
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var stepping = SEntMan.GetComponent<SteppingOnFireComponent>(target);
            Assert.That(stepping.LastModifiedTick, Is.EqualTo(before));
            var ignition = SEntMan.GetComponent<RMCIgniteOnCollideComponent>(fire);
            ignition.TileDamage = new DamageSpecifier();
            ignition.ArmorMultiplier = 0.5;
            ignition.ArmorWhitelist = null;
            Server.System<SharedRMCFlammableSystem>().Update(0f);
            Assert.That(stepping.ArmorMultiplier, Is.EqualTo(0.5));
            Assert.That(stepping.LastModifiedTick, Is.GreaterThan(before));
            before = stepping.LastModifiedTick;
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<SteppingOnFireComponent>(target).LastModifiedTick, Is.EqualTo(before)));
    }
}
