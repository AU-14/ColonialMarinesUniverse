using System.Numerics;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.IntegrationTests.CMU14.Weapons;

// BUG-1022: a gun with the B8 smart-scope on refused to fire at all
[TestFixture]
public sealed class B8SmartScopeIffTest
{
    [Test]
    public async Task B8DoesNotStopAClearShot()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        EntityUid user = default, gun = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var iff = entities.System<GunIFFSystem>();
            user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            iff.SetUserFaction(user, "GOVFOR");

            gun = entities.SpawnEntity("WeaponRifleM4SPR", map.GridCoords);
            var scope = entities.SpawnEntity("RMCAttachmentB8SmartScope", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, gun), Is.True);
            Assert.That(entities.System<AttachableHolderSystem>().Attach(
                (gun, entities.GetComponent<AttachableHolderComponent>(gun)), scope, user, "rmc-aslot-rail"), Is.True);
            Assert.That(entities.HasComponent<GunAttachableIFFComponent>(gun), Is.True);
        });

        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            var ev = new AttemptShootEvent(user, null, map.GridCoords, map.GridCoords.Offset(new Vector2(6, 0)));
            pair.Server.EntMan.EventBus.RaiseLocalEvent(gun, ref ev);
            Assert.That(ev.Cancelled, Is.False, ev.Message);
        });
        await pair.CleanReturnAsync();
    }
}
