#pragma warning disable RA0002 // Drive server-owned fixture state and real gameplay interactions.

using System.Numerics;
using Content.Server.CMU14.Fob;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines;
using Content.Shared.Buckle;
using Content.Shared.CMU14.Fob;
using Content.Shared.Interaction;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Fob;

public sealed partial class CMUFobDefenseTest
{
    private CMUFobEquipmentSystem Equipment => Server.System<CMUFobEquipmentSystem>();

    [Test]
    public async Task LoaderCannotPickUpInstalledGunButCanTransportDetachedModule()
    {
        await SetupGuns(ammo: 7);
        await Server.WaitAssertion(() =>
        {
            var loader = SEntMan.SpawnAtPosition("RMCMechPowerLoader", _gunCoords.Offset(new Vector2(-1, 0)));
            var pilot = SEntMan.SpawnAtPosition("CMMobHuman", SEntMan.GetComponent<TransformComponent>(loader).Coordinates);
            SEntMan.EnsureComponent<MarineComponent>(pilot).Faction = "govfor";
            Server.System<SkillsSystem>().SetSkill(pilot, "RMCSkillConstruction", 3);
            var loaderComp = SEntMan.GetComponent<PowerLoaderComponent>(loader);
            foreach (var (skill, level) in loaderComp.Skills.All)
                Server.System<SkillsSystem>().SetSkill(pilot, skill, level);
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(pilot, pilot, loader), Is.True);
            Server.System<TagSystem>().AddTag(loader, "InstantDoAfters");

            SEntMan.EventBus.RaiseLocalEvent(loader, new UserActivateInWorldEvent(loader, _gun, true));
            Assert.That(HandSys.IsHolding(loader, _gun), Is.False, "Installed guns must not enter loader hands.");
            Assert.That(SEntMan.GetComponent<TransformComponent>(_gun).Anchored, Is.True);
            Assert.That(Gun.Mount, Is.EqualTo(_mount));

            Assert.That(Defense.TryRemoveModule((_gun, Gun), pilot), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(loader, new UserActivateInWorldEvent(loader, _gun, true));
            Assert.That(HandSys.IsHolding(loader, _gun), Is.True);
            Assert.That(Defense.TryInstallModule((_gun, Gun), _mount, pilot, loader), Is.True);
            Assert.That(Gun.Mount, Is.EqualTo(_mount));
            Assert.That(Ammo, Is.EqualTo(7), "Removal and loader transport must retain the real magazine.");
        });
    }

    [Test]
    public async Task RemovedGunReinstallsThroughLoaderAtLoaderReachWithBothSlotsAllocated()
    {
        await SetupGuns(ammo: 7);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Defense.TryRemoveModule((_gun, Gun), SPlayer), Is.True);
            var loaderCoords = _gunCoords.Offset(new Vector2(-2, 0));
            Transform.SetCoordinates(SPlayer, loaderCoords);
            var loader = SEntMan.SpawnAtPosition("RMCMechPowerLoader", loaderCoords);
            Assert.That(HandSys.TryPickupAnyHand(loader, _gun, checkActionBlocker: false), Is.True);
            var install = new PowerLoaderInteractEvent(loader, _mount, _gun, new List<EntityUid> { SPlayer });
            SEntMan.EventBus.RaiseLocalEvent(_gun, ref install);
            Assert.That(Gun.Mount, Is.EqualTo(_mount), "Installation must use the loader's reach, not bare-hand reach.");
            Assert.That(Ammo, Is.EqualTo(7));
            Assert.That(SEntMan.GetComponent<CMUFobDefenseNetworkComponent>(_network).Mounts.Count, Is.EqualTo(2));
        });
    }

    private async Task<Entity<CMUFobEquipmentComponent>> DeployEquipment(string prototype, Vector2 offset)
    {
        NetCoordinates coords = default;
        await Server.WaitPost(() => coords = SEntMan.GetNetCoordinates(_gunCoords.Offset(offset)));
        await SetTile(Plating, coords);
        var wrench = SEntMan.GetEntity(await PlaceInHands("CMWrench"));
        Entity<CMUFobEquipmentComponent> equipment = default;
        await Server.WaitAssertion(() =>
        {
            Transform.SetCoordinates(SPlayer, _gunCoords.Offset(offset + new Vector2(-1, 0)));
            Server.System<TagSystem>().AddTag(SPlayer, "InstantDoAfters");
            var uid = SEntMan.SpawnAtPosition(prototype, SEntMan.GetCoordinates(coords));
            equipment = (uid, SEntMan.GetComponent<CMUFobEquipmentComponent>(uid));
            Assert.That(Equipment.TryStartWork(equipment, SPlayer, wrench), Is.True,
                $"Deploy {prototype} at {offset}, including beside an already deployed utility.");
            Assert.That(equipment.Comp.Deployed, Is.True, "The held wrench must complete actual deployment.");
            Transform.SetCoordinates(SPlayer, SEntMan.GetComponent<TransformComponent>(uid).Coordinates.Offset(new Vector2(-1, 0)));
        });
        return equipment;
    }

    [Test]
    public async Task UtilitiesFollowReserveAndBackupCannotBeReissued()
    {
        await SetupGuns();
        var sensor = await DeployEquipment("CMUFobSensor", new Vector2(0, 1));
        var light = await DeployEquipment("CMUFobIlluminator", new Vector2(0, 2));
        var fence = await DeployEquipment("CMUFobFence", new Vector2(0, 3));
        var generator = await DeployEquipment("CMUFobGenerator", new Vector2(0, 4));
        await Server.WaitAssertion(() =>
        {
            Assert.That(Equipment.IsPowered(light), Is.False);
            Assert.That(Reserve.TryActivate(_network), Is.True);
        });
        await RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Equipment.IsPowered(light), Is.True);
            Assert.That(Equipment.CanSense(_network, _victim), Is.True);
            var before = Reserve.GetRemaining(_network);
            Assert.That(Equipment.TryStartWork(generator, SPlayer, null, true), Is.True);
            Assert.That(Reserve.GetRemaining(_network), Is.GreaterThan(before));
            Assert.That(Equipment.TryStartWork(generator, SPlayer, null, true), Is.False);
            Hurt(fence, 200);
        });
        await RunSeconds(0.3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(fence), Is.True, "Powered mesh must still admit a physical breach.");
            Hurt(sensor, 200);
            Hurt(_control, 150);
            Assert.That(Equipment.IsPowered(light), Is.False, "A broken terminal must immediately disable utilities.");
        });
        await RunSeconds(0.3f);
        await Server.WaitAssertion(() => Assert.That(Equipment.CanSense(_network, _victim), Is.False));
        var replacement = await DeployEquipment("CMUFobGenerator", new Vector2(0, 5));
        await Server.WaitAssertion(() => Assert.That(Equipment.TryStartWork(replacement, SPlayer, null, true), Is.False,
            "Replacing a consumed generator must not create a second backup charge."));
    }

    [Test]
    public async Task ModuleSwapPreservesAmmoAndSensorLossCancelsRocketWarning()
    {
        await SetupGuns();
        EntityUid launcher = default;
        Entity<CMUFobSupportGunComponent> rocket = default;
        var initialAmmo = 0;
        await Server.WaitAssertion(() =>
        {
            initialAmmo = Ammo;
            Assert.That(Defense.TryRemoveModule((_gun, Gun), SPlayer), Is.True);
            var loader = SEntMan.SpawnAtPosition("RMCMechPowerLoader", _gunCoords.Offset(new Vector2(-1, 0)));
            launcher = SEntMan.SpawnAtPosition("CMUFobRocketModule", _gunCoords.Offset(new Vector2(-1, 0)));
            rocket = (launcher, SEntMan.GetComponent<CMUFobSupportGunComponent>(launcher));
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(loader, launcher, checkActionBlocker: false), Is.True);
            Assert.That(Defense.TryInstallModule(rocket, _mount, SPlayer, loader), Is.True);
            Assert.That(Ammo, Is.EqualTo(initialAmmo), "Unbolting must preserve the outgoing gun's physical drum.");
            Assert.That(Reserve.TryActivate(_network), Is.True);
            Assert.That(Defense.TryRemoveModule(rocket, SPlayer), Is.False, "A live platform must reject module removal.");
        });
        var rockets = 0;
        await Server.WaitPost(() => rockets = Guns.GetAmmoCount(launcher));
        await RunSeconds(4);
        await Server.WaitAssertion(() => Assert.That(Guns.GetAmmoCount(launcher), Is.EqualTo(rockets), "No sensor, no launch."));
        await Server.WaitPost(() => Transform.SetCoordinates(_victim, _gunCoords.Offset(new Vector2(-3, 0))));
        var sensor = await DeployEquipment("CMUFobSensor", new Vector2(0, 1));
        await RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(rocket.Comp.Target, Is.EqualTo(_victim), "Sensor targeting also covers targets behind the launcher.");
            Assert.That(Guns.GetAmmoCount(launcher), Is.EqualTo(rockets));
            Transform.SetCoordinates(_victim, _gunCoords.Offset(new Vector2(3, 0)));
        });
        await RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(rocket.Comp.Target, Is.EqualTo(_victim));
            Assert.That(Guns.GetAmmoCount(launcher), Is.EqualTo(rockets), "Acquisition warning must precede ammunition consumption.");
            Hurt(sensor, 200);
        });
        await RunSeconds(3.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(rocket.Comp.Target, Is.Null);
            Assert.That(Guns.GetAmmoCount(launcher), Is.EqualTo(rockets), "Destroying the sensor must cancel the pending launch.");
        });
        await DeployEquipment("CMUFobSensor", new Vector2(0, 1));
        await RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Guns.GetAmmoCount(launcher), Is.LessThan(rockets), "Restored sensor coverage permits a warned, finite launch.");
            Assert.That(Ammo, Is.EqualTo(initialAmmo));
            Assert.That(SEntMan.GetComponent<CMUFobGunMountComponent>(_mount).Gun, Is.EqualTo(launcher));
        });
    }
}
