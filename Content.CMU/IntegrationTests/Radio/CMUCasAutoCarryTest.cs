using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.Dropship.Cas;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Radio;

[TestFixture]
public sealed class CMUCasAutoCarryTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task AutoUsesMountedMasterGauAndWaitsForClearedHot()
    {
        var map = await Pair.CreateTestMap();
        EntityUid ammo = default;
        EntityUid rto = default;
        var id = 0;
        var initialRounds = 0;
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            var ship = SEntMan.EnsureComponent<DropshipComponent>(map.Grid);
            var point = SEntMan.SpawnEntity(null, map.GridCoords);
            var mount = SEntMan.AddComponent<DropshipWeaponPointComponent>(point);
#pragma warning disable RA0002 // Arrange a minimal mounted master weapon without loading a full ship map.
            ship.AttachmentPoints.Add(point);
#pragma warning restore RA0002
            var containers = Server.System<SharedContainerSystem>();
            var weapon = SEntMan.SpawnEntity("RMCDropshipAttachmentGau21Cannon", map.GridCoords);
            ammo = SEntMan.SpawnEntity("RMCDropshipAttachmentAmmoGAU", map.GridCoords);
            Assert.That(containers.Insert(weapon,
                containers.EnsureContainer<ContainerSlot>(point, mount.WeaponContainerSlotId)), Is.True);
            Assert.That(containers.Insert(ammo,
                containers.EnsureContainer<ContainerSlot>(point, mount.AmmoContainerSlotId)), Is.True);
            initialRounds = SEntMan.GetComponent<DropshipAmmoComponent>(ammo).Rounds;
            var console = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<DropshipTerminalWeaponsComponent>(console);
            rto = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<MarineComponent>(rto).Faction = "govfor";
            var mark = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(10, 0)));
            Server.System<SharedDropshipWeaponSystem>().MakeDropshipTarget(mark, "AUTO", "govfor");
            var requests = Server.System<CMUCasRequestSystem>();
            Assert.That(requests.TryCreateRequest(rto, mark, CMUCasMunition.Gau, out id), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(console, new CMUCasRequestActionMsg(id, CMUCasRequestStatus.Accepted)
                { Actor = rto, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(requests.SetCarryMode(id, CMUCasCarryMode.Auto), Is.True);
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<DropshipAmmoComponent>(ammo).Rounds, Is.EqualTo(initialRounds));
            Assert.That(Server.System<CMUCasRequestSystem>().TryClearHot(rto, id), Is.True);
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<DropshipAmmoComponent>(ammo).Rounds, Is.LessThan(initialRounds));
            var requests = Server.System<CMUCasRequestSystem>();
            Assert.That(requests.TryGetRequest(id, out var request), Is.True);
            Assert.That(request.CarryVolleys, Is.GreaterThan(0));
            Assert.That(requests.TryAbort(rto, id), Is.True);
        });
    }
}
