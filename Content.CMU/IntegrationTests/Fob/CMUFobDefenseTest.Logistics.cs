#pragma warning disable RA0002 // Arrange authored exclusions and use real inventory, tools and reserve transitions.

using System.Numerics;
using Content.Server.CMU14.Fob;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared.CMU14.Fob;
using Content.Shared.Interaction;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Fob;

public sealed partial class CMUFobDefenseTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task PlatformRelocationPreservesAllocationAndRejectsOutsideDeploymentArea(bool removeGunFirst)
    {
        await SetupGuns(ammo: 7, portablePlatforms: true);
        NetCoordinates inside = default;
        NetCoordinates outside = default;
        await Server.WaitPost(() =>
        {
            inside = SEntMan.GetNetCoordinates(_gunCoords.Offset(new Vector2(0, 3)));
            outside = SEntMan.GetNetCoordinates(_gunCoords.Offset(new Vector2(_network.Comp.DeploymentRange + 1, 0)));
        });
        await SetTile(Plating, inside);
        await SetTile(Plating, outside);
        var wrench = SEntMan.GetEntity(await PlaceInHands("CMWrench"));
        await Server.WaitAssertion(() =>
        {
            Transform.SetCoordinates(SPlayer, _gunCoords.Offset(new Vector2(-1, 0)));
            var mount = SEntMan.GetComponent<CMUFobGunMountComponent>(_mount);
            if (removeGunFirst)
                Assert.That(Defense.TryRemoveModule((_gun, Gun), SPlayer), Is.True);
            var fold = new InteractUsingEvent(SPlayer, wrench, _mount, _gunCoords);
            SEntMan.EventBus.RaiseLocalEvent(_mount, fold);
            Assert.That(Gun.Mount, Is.Null, "Wrenching the platform must detach its installed module before packing.");
            Assert.That(SEntMan.GetComponent<TransformComponent>(_mount).Anchored, Is.False);
            Transform.SetCoordinates(_mount, SEntMan.GetCoordinates(outside));
            Transform.AttachToGridOrMap(_mount);
            Transform.SetCoordinates(SPlayer, SEntMan.GetCoordinates(outside).Offset(new Vector2(-1, 0)));
            Assert.That(Defense.TryDeployPlatform((_mount, mount), SPlayer), Is.False);
            var loader = SEntMan.SpawnAtPosition("RMCMechPowerLoader", SEntMan.GetCoordinates(outside).Offset(new Vector2(-1, 0)));
            Assert.That(HandSys.TryPickupAnyHand(loader, _mount, checkActionBlocker: false), Is.True);
            Transform.SetCoordinates(loader, SEntMan.GetCoordinates(inside).Offset(new Vector2(-1.25f, 0)));
            Assert.That(HandSys.TryDrop(loader, _mount, SEntMan.GetCoordinates(inside), checkActionBlocker: false), Is.True);
            Transform.SetCoordinates(SPlayer, SEntMan.GetCoordinates(inside).Offset(new Vector2(-1, 0)));
            var deploy = new InteractUsingEvent(SPlayer, wrench, _mount, SEntMan.GetCoordinates(inside));
            SEntMan.EventBus.RaiseLocalEvent(_mount, deploy);
            Assert.That(SEntMan.GetComponent<TransformComponent>(_mount).Anchored, Is.True,
                "A transported, previously allocated platform must wrench down again.");
            Assert.That(mount.Position, Is.EqualTo(Transform.GetWorldPosition(_mount)));
            Assert.That(SEntMan.GetComponent<CMUFobDefenseNetworkComponent>(_network).Mounts.Count, Is.EqualTo(2));
            Assert.That(mount.Gun, Is.Null, "Relocating must not mint a replacement gun.");
            Assert.That(Ammo, Is.EqualTo(7), "The removed module retains its finite magazine.");
            Assert.That(HandSys.TryPickupAnyHand(loader, _gun, checkActionBlocker: false), Is.True);
            var install = new PowerLoaderInteractEvent(loader, _mount, _gun, new List<EntityUid> { SPlayer });
            SEntMan.EventBus.RaiseLocalEvent(_gun, ref install);
            Assert.That(Gun.Mount, Is.EqualTo(_mount));
            Assert.That(Ammo, Is.EqualTo(7));
            var extra = SEntMan.SpawnAtPosition("CMUFobPlatformModule", _gunCoords);
            Transform.SetCoordinates(SPlayer, _gunCoords.Offset(new Vector2(-1, 0)));
            Assert.That(Defense.TryDeployPlatform((extra, SEntMan.GetComponent<CMUFobGunMountComponent>(extra)), SPlayer), Is.True,
                "Relocating the first platform must leave the third allocation available.");
        });
    }

    [Test]
    public async Task FenceBundleConsumesOnlyPlacedSectionsAndRecoveryPreservesDamage()
    {
        await SetupGuns();
        NetCoordinates coords = default;
        await Server.WaitPost(() => coords = SEntMan.GetNetCoordinates(_gunCoords.Offset(new Vector2(0, 1))));
        await SetTile("CMPlanetBeachGrassNorth", coords);
        var wrench = SEntMan.GetEntity(await PlaceInHands("CMWrench"));
        Entity<CMUFobEquipmentComponent> section = default;
        Entity<CMUFobEquipmentComponent> bundle = default;
        var stack = Server.System<SharedStackSystem>();
        var initial = 0;
        await Server.WaitAssertion(() =>
        {
            var at = SEntMan.GetCoordinates(coords);
            Transform.SetCoordinates(SPlayer, at.Offset(new Vector2(-1, 0)));
            Server.System<TagSystem>().AddTag(SPlayer, "InstantDoAfters");
            var uid = SEntMan.SpawnAtPosition("CMUFobFenceStack10", at);
            bundle = (uid, SEntMan.GetComponent<CMUFobEquipmentComponent>(uid));
            initial = stack.GetCount(uid);
            // A mapper exclusion represents a reserved landing footprint, even while no dropship is present.
            var siteUid = SEntMan.SpawnAtPosition(null, at);
            var site = SEntMan.AddComponent<CMUFobSiteComponent>(siteUid);
            site.Network = _network;
            var position = Transform.GetWorldPosition(bundle);
            site.UtilityExclusions.Add(new Box2(position - Vector2.One, position + Vector2.One));
            Assert.That(Equipment.TryStartWork(bundle, SPlayer, wrench), Is.False);
            Assert.That(stack.GetCount(uid), Is.EqualTo(initial));
            site.UtilityExclusions.Clear();
            var bystander = SEntMan.SpawnAtPosition("CMMobHuman", at);
            Assert.That(Equipment.TryStartWork(bundle, SPlayer, wrench), Is.True);
            Assert.That(stack.GetCount(uid), Is.EqualTo(initial - 1));
            Transform.SetCoordinates(bystander, at.Offset(new Vector2(0, 4)));
            var query = SEntMan.AllEntityQueryEnumerator<CMUFobEquipmentComponent>();
            while (query.MoveNext(out var fenceUid, out var equipment))
                if (equipment.Kind == CMUFobEquipmentKind.Fence && equipment.Deployed) section = (fenceUid, equipment);
            Assert.That(Equipment.TryStartWork(bundle, SPlayer, wrench), Is.False, "The placed section blocks deploying another on the same tile.");
            Assert.That(stack.GetCount(uid), Is.EqualTo(initial - 1));
            Hurt(section, 20);
            Transform.SetCoordinates(SPlayer, SEntMan.GetComponent<TransformComponent>(section).Coordinates.Offset(new Vector2(-1, 0)));
            Assert.That(Reserve.TryActivate(_network), Is.True);
        });
        var cutters = SEntMan.GetEntity(await PlaceInHands("CMWirecutter"));
        await RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Equipment.TryStartWork(section, SPlayer, cutters), Is.False, "Powered fences cannot be folded.");
            Assert.That(Reserve.TryDeactivate(_network), Is.True);
            Assert.That(Equipment.TryStartWork(section, SPlayer, cutters), Is.True);
            Assert.That(section.Comp.Deployed, Is.False);
            Assert.That(Damage.GetTotalDamage(section.Owner), Is.EqualTo(FixedPoint2.New(20)), "Folding must retain damage.");
            Assert.That(stack.GetCount(bundle.Owner), Is.EqualTo(initial - 1), "Recovery cannot mint another unused section.");
        });
    }
}
