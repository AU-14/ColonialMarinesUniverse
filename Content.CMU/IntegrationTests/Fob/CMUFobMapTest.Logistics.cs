#pragma warning disable RA0002 // Drive real cargo/landing events and physical assembly with controlled timing.

using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Fob;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Fob;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Fob;

public sealed partial class CMUFobMapTest
{
    private Entity<RequisitionsElevatorComponent> CreateSupplyLift(string faction = "govfor")
    {
        var map = Maps.CreateMap(out var id);
        var grid = Maps.CreateGridEntity(id);
        SEntMan.AddComponent<ShipFactionComponent>(grid).Faction = faction;
        var tile = Maps.GetTileRef(_map, SEntMan.GetComponent<MapGridComponent>(_map),
            SEntMan.GetComponent<TransformComponent>(Site("Nexus")).Coordinates).Tile;
        for (var x = -4; x <= 4; x++)
        for (var y = -4; y <= 4; y++)
            Maps.SetTile(grid, new Vector2i(x, y), tile);
        var uid = SEntMan.SpawnAtPosition("CMCargoElevatorGovfor", new EntityCoordinates(grid, 0.5f, 0.5f));
        var comp = SEntMan.GetComponent<RequisitionsElevatorComponent>(uid);
        comp.Faction = faction;
        comp.RoundStartFreeCrateGiven = true;
        BeginSupplyDelivery((uid, comp));
        return (uid, comp);
    }

    private void BeginSupplyDelivery(Entity<RequisitionsElevatorComponent> elevator)
    {
        elevator.Comp.Busy = true;
        elevator.Comp.Mode = RequisitionsElevatorMode.Raising;
        elevator.Comp.NextMode = null;
        elevator.Comp.ToggledAt = SGameTiming.CurTime - TimeSpan.FromSeconds(30);
    }

    private EntityUid[] SupplyCrates()
    {
        var crates = new List<EntityUid>();
        var query = SEntMan.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (meta.EntityPrototype?.ID is "CMUFobWeaponryCrate" or "CMUFobSupportCrate" or "CMUFobMonitoringCrate")
                crates.Add(uid);
        }
        return crates.ToArray();
    }

    [TestCase(false, "Nexus")]
    [TestCase(true, "Nexus")]
    [TestCase(true, "Robotics")]
    public async Task ShipsideDeliveryAndAssemblyCannotDuplicateTheAllocation(bool enabled, string selected)
    {
        Entity<RequisitionsElevatorComponent> lift = default;
        EntityUid[] crates = [];
        await Server.WaitAssertion(() =>
        {
            LoadSurface();
            StartRound(enabled);
            CreateSupplyLift("opfor");
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SupplyCrates(), Is.Empty, "The enemy lift cannot receive the Marine allocation.");
            lift = CreateSupplyLift();
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(lift.Comp.Mode, Is.EqualTo(RequisitionsElevatorMode.Raised));
            crates = SupplyCrates();
            if (!enabled)
            {
                Assert.That(crates, Is.Empty, "Disabled rounds cannot receive pilot hardware.");
                return;
            }
            Assert.That(crates.Length, Is.EqualTo(3), "One physical shipment contains the three distinct supply crates.");
            var carrier = SEntMan.GetComponent<TransformComponent>(lift).MapUid;
            Assert.That(crates.All(crate => SEntMan.GetComponent<TransformComponent>(crate).MapUid == carrier), Is.True);
            Assert.That(OnSurface<CMUFobSupportGunComponent>(), Is.Empty);
            BeginSupplyDelivery(lift);
            Land(Site(selected));
        });
        if (!enabled) return;
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SupplyCrates(), Is.EquivalentTo(crates), "Cycling the elevator cannot grant a second shipment.");
            var site = Site(selected);
            Assert.That(Reserve.TryGetNetwork("govfor", out var bank), Is.True);
            var lz = SEntMan.GetComponent<TransformComponent>(site).Coordinates;
            var staging = lz.Offset(new Vector2(7, 0));
            Assert.That(Server.System<CMUFobInteractionSystem>().HasOperationalControl(bank), Is.False);
            var cargo = new List<EntityUid>();
            var storage = Server.System<SharedEntityStorageSystem>();
            foreach (var crate in crates)
            {
                // Transport the actual crates and unpack their actual contents; no replacement equipment is spawned.
                Transform.SetCoordinates(crate, staging);
                var contents = SEntMan.GetComponent<Content.Shared.Storage.Components.EntityStorageComponent>(crate).Contents.ContainedEntities.ToArray();
                foreach (var item in contents)
                {
                    Assert.That(storage.Remove(item, crate), Is.True);
                    cargo.Add(item);
                }
            }
            var user = SEntMan.SpawnAtPosition("CMMobHuman", staging.Offset(new Vector2(1, 0)));
            SEntMan.EnsureComponent<MarineComponent>(user).Faction = "govfor";
            Server.System<SkillsSystem>().SetSkill(user, "RMCSkillConstruction", 3);
            var wrench = cargo.Single(uid => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "CMWrench");
            var hands = Server.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(user, wrench), Is.True);
            var control = cargo.Single(SEntMan.HasComponent<CMUFobControlComponent>);
            var outside = lz.Offset(new Vector2(bank.Comp.DeploymentRange + 2, 0));
            Transform.SetCoordinates(control, outside);
            Transform.SetCoordinates(user, outside.Offset(new Vector2(-1, 0)));
            SEntMan.EventBus.RaiseLocalEvent(control, new InteractUsingEvent(user, wrench, control, outside));
            Assert.That(SEntMan.GetComponent<TransformComponent>(control).Anchored, Is.False,
                "Moving a terminal beyond the LZ deployment area must not move the area with it.");
            Transform.SetCoordinates(control, staging.Offset(new Vector2(0, 1)));
            Transform.SetCoordinates(user, staging.Offset(new Vector2(1, 1)));
            SEntMan.EventBus.RaiseLocalEvent(control, new InteractUsingEvent(user, wrench, control,
                SEntMan.GetComponent<TransformComponent>(control).Coordinates));
            Assert.That(Server.System<CMUFobInteractionSystem>().TryBindControl((control,
                SEntMan.GetComponent<CMUFobControlComponent>(control)), out _), Is.True);
            var platforms = cargo.Where(SEntMan.HasComponent<CMUFobGunMountComponent>).ToArray();
            var heavy = cargo.First(uid => SEntMan.TryGetComponent<CMUFobSupportGunComponent>(uid, out var g) && !g.RequiresSensor);
            var rocket = cargo.Single(uid => SEntMan.TryGetComponent<CMUFobSupportGunComponent>(uid, out var g) && g.RequiresSensor);
            var defense = Server.System<CMUFobDefenseSystem>();
            var loader = SEntMan.SpawnAtPosition("RMCMechPowerLoader", SEntMan.GetComponent<TransformComponent>(control).Coordinates);
            for (var i = 0; i < 2; i++)
            {
                var coords = lz.Offset(new Vector2(-7, i == 0 ? -1 : 1));
                Transform.SetCoordinates(platforms[i], coords);
                Transform.SetCoordinates(user, coords.Offset(new Vector2(-1, 0)));
                Transform.SetCoordinates(loader, coords.Offset(new Vector2(-1, 0)));
                SEntMan.EventBus.RaiseLocalEvent(platforms[i], new InteractUsingEvent(user, wrench, platforms[i], coords));
                var slots = SEntMan.GetComponent<CMUFobDefenseNetworkComponent>(bank).Mounts;
                Assert.That(slots[i], Is.EqualTo(platforms[i]), "Assembly registers the delivered platform where the operator placed it.");
                var mount = SEntMan.GetComponent<CMUFobGunMountComponent>(platforms[i]);
                var weapon = i == 0 ? rocket : heavy;
                Transform.SetCoordinates(weapon, coords.Offset(new Vector2(-1, 0)));
                Assert.That(hands.TryPickupAnyHand(loader, weapon, checkActionBlocker: false), Is.True);
                var ammo = Server.System<SharedGunSystem>().GetAmmoCount(weapon);
                Assert.That(ammo, Is.GreaterThan(0), "Delivered modules must retain their initial magazine through cargo preparation.");
                Assert.That(defense.TryInstallModule((weapon, SEntMan.GetComponent<CMUFobSupportGunComponent>(weapon)), platforms[i], user, loader), Is.True);
                Assert.That(Server.System<SharedGunSystem>().GetAmmoCount(weapon), Is.EqualTo(ammo));
            }
            Assert.That(OnSurface<CMUFobSupportGunComponent>().Count(uid => SEntMan.GetComponent<CMUFobSupportGunComponent>(uid).Mount != null), Is.EqualTo(2));
            var unusedHeavy = cargo.Single(uid => SEntMan.TryGetComponent<CMUFobSupportGunComponent>(uid, out var g) && !g.RequiresSensor && g.Mount == null);
            Assert.That(Server.System<SharedGunSystem>().GetAmmoCount(unusedHeavy), Is.GreaterThan(0),
                "The unused module remains a real finite-ammo spare, not a third deployed weapon.");
            var extra = SEntMan.SpawnAtPosition("CMUFobPlatformModule", lz.Offset(new Vector2(-7, 3)));
            Transform.SetCoordinates(user, lz.Offset(new Vector2(-8, 3)));
            Assert.That(defense.TryDeployPlatform((extra, SEntMan.GetComponent<CMUFobGunMountComponent>(extra)), user), Is.False,
                "Free placement must still enforce the two-platform budget.");
            var after = OnSurface<CMUFobSupportGunComponent>();
            Land(Site(selected == "Nexus" ? "Robotics" : "Nexus"));
            Assert.That(OnSurface<CMUFobSupportGunComponent>(), Is.EquivalentTo(after));
            CreateSupplyLift(); // Replacing the requisitions hardware must not reset the grant.
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() => Assert.That(SupplyCrates(), Is.EquivalentTo(crates)));
    }
}
