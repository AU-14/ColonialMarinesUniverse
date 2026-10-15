using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Marines;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Dropship.Cas;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Radio;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

#pragma warning disable RA0002 // Arrange CAS mounts, thermal limits and explosion policies.

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class CasCorrectionsTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task FlareCameraUsesHighestSolidDeckAndReturnsToTargetBelowSpace()
    {
        var lower = await Pair.CreateTestMap();
        var middle = await Pair.CreateTestMap();
        var upper = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var zLevels = Server.System<CMUZLevelsSystem>();
            var network = zLevels.CreateZNetwork();
            Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int>
            {
                [lower.MapUid] = 0, [middle.MapUid] = 1, [upper.MapUid] = 2,
            }), Is.True);
            var maps = Server.System<SharedMapSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var position = new Vector2(0.5f, 0.5f);
            maps.SetTile(middle.Grid, Vector2i.Zero, new Tile(tiles["FloorSteel"].TileId));
            maps.SetTile(upper.Grid, Vector2i.Zero, Tile.Empty);
            var flare = SEntMan.SpawnEntity(null, new MapCoordinates(position, lower.MapId));
            var weapons = Server.System<SharedDropshipWeaponSystem>();
            Assert.That(weapons.TryGetTopZLevelCoordinates(flare, out var view), Is.True);
            Assert.That(view.MapId, Is.EqualTo(middle.MapId), "Skip the empty upper map, not the occupied roof.");
            maps.SetTile(middle.Grid, Vector2i.Zero, Tile.Empty);
            Assert.That(weapons.TryGetTopZLevelCoordinates(flare, out view), Is.True);
            Assert.That(view.MapId, Is.EqualTo(lower.MapId), "Open sky must show the target's map, not space.");
            SEntMan.DeleteEntity(network);
        });
    }

    [Test]
    public async Task AirborneHeatCapacityDoublesWithoutChangingHeatPerRound()
    {
        var map = await Pair.CreateTestMap();
        EntityUid point = default;
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid);
            point = SEntMan.SpawnEntity("CMUMohawkM90Point", map.GridCoords);
            var chin = SEntMan.GetComponent<MohawkChinGunComponent>(point);
            var turret = Server.System<MohawkChinGunSystem>();
            Assert.That(turret.GetMaxHeat((point, chin)), Is.EqualTo(100));
            SEntMan.EnsureComponent<FTLComponent>(map.Grid).State = FTLState.Travelling;
            Assert.That(turret.GetMaxHeat((point, chin)), Is.EqualTo(200));
            turret.AddHeat((point, chin), 100);
            Assert.That(chin.Heat, Is.EqualTo(100));
            Assert.That(chin.Overheated, Is.False);
            turret.AddHeat((point, chin), 100);
            Assert.That(chin.Heat, Is.EqualTo(200));
            Assert.That(chin.Overheated, Is.True);
            var config = Server.ResolveDependency<IConfigurationManager>();
            Assert.That(config.GetCVar(CCVars.DropshipBootFaultChance), Is.EqualTo(0.02f));
            Assert.That(config.GetCVar(CCVars.DropshipLandingFaultChance), Is.EqualTo(0.005f));
            Assert.That(config.GetCVar(CCVars.DropshipLandingFaultStep), Is.EqualTo(0.002f));
            Assert.That(config.GetCVar(CCVars.DropshipFaultUseMultiplier), Is.EqualTo(0.01f));
            chin.Heat = 150;
            chin.Overheated = false;
            SEntMan.RemoveComponent<FTLComponent>(map.Grid);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<MohawkChinGunComponent>(point).Overheated, Is.True,
                "Landing above the ground limit immediately locks the barrels."));
    }

    [Test]
    public async Task AutoCarryRequiresClearHotAndOnlyUsesRequestedInternalWeapon()
    {
        var map = await Pair.CreateTestMap();
        EntityUid rto = default, console = default, requestedWeapon = default, otherWeapon = default;
        var id = 0;
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            var ship = SEntMan.EnsureComponent<DropshipComponent>(map.Grid);
            EntityUid Mount(string prototype, string abbreviation, bool interior)
            {
                var point = SEntMan.SpawnEntity(null, map.GridCoords);
                string weaponSlot, ammoSlot;
                if (interior)
                {
                    var mount = SEntMan.AddComponent<DropshipUtilityPointComponent>(point);
                    weaponSlot = mount.UtilitySlotId;
                    ammoSlot = mount.AmmoContainerSlotId;
                }
                else
                {
                    var mount = SEntMan.AddComponent<DropshipWeaponPointComponent>(point);
                    weaponSlot = mount.WeaponContainerSlotId;
                    ammoSlot = mount.AmmoContainerSlotId;
                }
                ship.AttachmentPoints.Add(point);
                var containers = Server.System<SharedContainerSystem>();
                var weapon = SEntMan.SpawnEntity(prototype, map.GridCoords);
                Assert.That(SEntMan.GetComponent<DropshipWeaponComponent>(weapon).Abbreviation, Is.EqualTo(abbreviation));
                Assert.That(containers.Insert(weapon, containers.EnsureContainer<ContainerSlot>(point, weaponSlot)), Is.True);
                var crate = SEntMan.SpawnEntity("CMUMohawkM90AmmoKinetic", map.GridCoords);
                var ammo = SEntMan.GetComponent<DropshipAmmoComponent>(crate);
                ammo.RoundsPerShot = 1;
                Assert.That(containers.Insert(crate, containers.EnsureContainer<ContainerSlot>(point, ammoSlot)), Is.True);
                return weapon;
            }
            requestedWeapon = Mount("CMUDropshipAttachmentBOFORS", "ATC", true);
            otherWeapon = Mount("RMCDropshipAttachmentGau21Cannon", "GAU", false);
            console = SEntMan.SpawnEntity("CMUMohawkMidwayGunneryea0f8d621b", map.GridCoords);
            rto = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            _ = Server.System<CasRadioProbeSystem>();
            SEntMan.AddComponent<CasRadioProbeComponent>(rto);
            SEntMan.AddComponent<CasRadioProbeComponent>(console);
            SEntMan.EnsureComponent<MarineComponent>(rto).Faction = "GOVFOR";
            var mark = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(20, 0)));
            var weapons = Server.System<SharedDropshipWeaponSystem>();
            weapons.MakeDropshipTarget(mark, "TST", "GOVFOR");
            var requests = Server.System<CMUCasRequestSystem>();
            Assert.That(requests.TryCreateRequest(rto, mark, CMUCasMunition.Bofors, out id), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(console, new CMUCasRequestActionMsg(id, CMUCasRequestStatus.Accepted)
            { Actor = rto, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(requests.SetCarryMode(id, CMUCasCarryMode.Auto), Is.True);
            var terminal = SEntMan.GetComponent<DropshipTerminalWeaponsComponent>(console);
            Assert.That(weapons.TryFireWeapon(requestedWeapon, SEntMan.GetComponent<TransformComponent>(mark).Coordinates,
                DropshipWeaponStrikeType.Direct, rto, terminal), Is.False,
                "Manual fire cannot bypass an RTO's outstanding clearance.");
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            var weapons = Server.System<SharedDropshipWeaponSystem>();
            weapons.TryGetWeaponAmmo(requestedWeapon, out var requested);
            Assert.That(requested.Comp.Rounds, Is.EqualTo(1200), "AUTO waits for clear hot even without friendlies.");
            Assert.That(Server.System<CMUCasRequestSystem>().TryClearHot(rto, id), Is.True);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            var weapons = Server.System<SharedDropshipWeaponSystem>();
            weapons.TryGetWeaponAmmo(requestedWeapon, out var requested);
            weapons.TryGetWeaponAmmo(otherWeapon, out var other);
            Assert.That(requested.Comp.Rounds, Is.LessThan(1200), "Internal matching weapons must be discoverable.");
            Assert.That(other.Comp.Rounds, Is.EqualTo(1200), "Other onboard weapons must never substitute.");
            SEntMan.EventBus.RaiseLocalEvent(console, new CMUCasRequestActionMsg(id, CMUCasRequestStatus.Complete)
            { Actor = rto, UiKey = DropshipTerminalWeaponsUi.Key });
            var rtoTraffic = SEntMan.GetComponent<CasRadioProbeComponent>(rto).Messages;
            var aircraftTraffic = SEntMan.GetComponent<CasRadioProbeComponent>(console).Messages;
            Assert.That(rtoTraffic.Any(message => message.Contains("CAS request")), Is.True);
            Assert.That(aircraftTraffic.Any(message => message.Contains("accepted")), Is.True);
            Assert.That(aircraftTraffic.Any(message => message.Contains("rounds complete")), Is.True);

            var requests = Server.System<CMUCasRequestSystem>();
            Assert.That(requests.TryGetRequest(id, out var completed), Is.True);
            Assert.That(requests.TryCreateRequest(rto, SEntMan.GetEntity(completed.Target),
                CMUCasMunition.Bombs, out var missingId), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(console, new CMUCasRequestActionMsg(missingId, CMUCasRequestStatus.Accepted)
            { Actor = rto, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(requests.SetCarryMode(missingId, CMUCasCarryMode.Auto), Is.True);
            Assert.That(requests.TryClearHot(rto, missingId), Is.True);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            var request = Server.System<CMUCasRequestSystem>().GetRequests("GOVFOR")
                .Single(request => request.Munition == CMUCasMunition.Bombs);
            Assert.That(request.Status, Is.EqualTo(CMUCasRequestStatus.Denied));
            Assert.That(request.StatusDetail, Is.Not.Empty);
            Assert.That(Server.System<SharedDropshipWeaponSystem>().TryGetWeaponAmmo(otherWeapon, out var other), Is.True);
            Assert.That(other.Comp.Rounds, Is.EqualTo(1200), "Missing requested ordnance never fires other weapons.");
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, false);
        });
    }

    [Test]
    public async Task RequestsCloseWhenTheCalledHostileLeavesTheMark()
    {
        var map = await Pair.CreateTestMap();
        var id = 0;
        await Server.WaitAssertion(() =>
        {
            var rto = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<MarineComponent>(rto).Faction = "GOVFOR";
            var enemy = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(10, 0)));
            SEntMan.EnsureComponent<MarineComponent>(enemy).Faction = "OPFOR";
            var requests = Server.System<CMUCasRequestSystem>();
            Assert.That(requests.TryCreateRadioMarkRequest(rto, CMUCasMunition.Gau, CMUCasEffect.Any, "", out id), Is.True);
            Server.System<SharedTransformSystem>().SetCoordinates(enemy, map.GridCoords.Offset(new Vector2(30, 0)));
        });
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<CMUCasRequestSystem>().TryGetRequest(id, out var request), Is.True);
            Assert.That(request.Status, Is.EqualTo(CMUCasRequestStatus.Denied));
            Assert.That(request.StatusDetail, Is.Not.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CasExplosionsDoNotGibEvenAfterTheSourceIsDeleted(bool projectileImpact)
    {
        var map = await Pair.CreateTestMap();
        EntityUid victim = default;
        await Server.WaitAssertion(() =>
        {
            victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var gib = SEntMan.EnsureComponent<MobGibbedByExplosionTypeComponent>(victim);
            gib.Explosions = ["RMC", "RMCLTBCannon"];
            gib.Threshold = 0;
            var source = SEntMan.SpawnEntity(projectileImpact ? "CMUMohawkM90HEAP" : null, map.GridCoords);
            SEntMan.AddComponent<NonGibbingDamageComponent>(source);
            if (!projectileImpact)
            {
                Server.System<ExplosionSystem>().QueueExplosion(
                    Server.System<SharedTransformSystem>().GetMapCoordinates(victim), "RMC", 20, 1, 5, source,
                    canCreateVacuum: false);
            }
            SEntMan.DeleteEntity(source);
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(victim), Is.False);
            Assert.That(SEntMan.GetComponent<DamageableComponent>(victim).TotalDamage.Float(), Is.GreaterThan(0));
            Assert.That(Server.System<GibbingSystem>().GibbingSuppressed, Is.False,
                "CAS must not leave permanent gib protection on the body or the system.");
            Server.System<SharedBodySystem>().GibBody(victim);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(victim), Is.True,
            "Unrelated gibbing must retain its normal behaviour."));
    }
}

[RegisterComponent]
public sealed partial class CasRadioProbeComponent : Component
{
    public readonly List<string> Messages = new();
}

public sealed class CasRadioProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CasRadioProbeComponent, RadioSendAttemptEvent>(OnTransmission);
    }

    private static void OnTransmission(Entity<CasRadioProbeComponent> ent, ref RadioSendAttemptEvent args)
    {
        if (args.Channel.ID == "radioGovforJTAC")
            ent.Comp.Messages.Add(args.Message);
    }
}
