using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Dropship.Cas;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

#pragma warning disable RA0002 // Arrange factions, heat and ammo at the system boundary.

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class CMUCasSupportTest
{
    [Test]
    public async Task RocketFalloutZonesAreBoundedAndThermobaricDamagesResin()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            var grid = maps.CreateGridEntity(mapId);
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            for (var x = -6; x <= 6; x++)
            {
                for (var y = -6; y <= 6; y++)
                    maps.SetTile(grid, new Vector2i(x, y), new Tile(tiles["FloorSteel"].TileId));
            }

            var fallout = entities.System<CMUCasFalloutSystem>();
            var center = new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f));
            var centerMap = new MapCoordinates(new Vector2(0.5f, 0.5f), mapId);
            var far = new MapCoordinates(new Vector2(5.5f, 0.5f), mapId);
            Assert.That(fallout.IsWeedKilled(centerMap), Is.False);
            Assert.That(fallout.IsObscured(centerMap), Is.False);

            entities.SpawnEntity("CMUCasFalloutWeedKiller", center);
            Assert.That(fallout.IsWeedKilled(centerMap), Is.True, "Weedkiller must treat its impact area.");
            Assert.That(fallout.IsWeedKilled(far), Is.False, "Weedkiller treatment must stay within three tiles.");
            Assert.That(fallout.IsObscured(centerMap), Is.False, "Weedkiller must not obscure optics.");

            entities.SpawnEntity("CMUCasFalloutObscurant", center);
            Assert.That(fallout.IsObscured(centerMap), Is.True, "WP smoke must obscure its impact area.");
            Assert.That(fallout.IsObscured(far), Is.False);

            var wall = entities.SpawnEntity("WallXenoResin", new EntityCoordinates(grid.Owner, new Vector2(1.5f, 0.5f)));
            var distant = entities.SpawnEntity("WallXenoResin", new EntityCoordinates(grid.Owner, new Vector2(5.5f, 5.5f)));
            entities.SpawnEntity("CMUCasFalloutResin", center);
            Assert.That(entities.GetComponent<DamageableComponent>(wall).TotalDamage.Float(), Is.GreaterThan(0),
                "Thermobaric overpressure must damage nearby resin.");
            Assert.That(entities.GetComponent<DamageableComponent>(distant).TotalDamage.Float(), Is.EqualTo(0),
                "Thermobaric overpressure must not reach distant resin.");

            var flare = entities.SpawnEntity("CMUCasFalloutFlare", center);
            Assert.That(entities.HasComponent<DropshipTargetComponent>(flare), Is.True,
                "Flare rockets must leave a targetable CAS mark.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpecialRocketsCarryTheirFallout()
    {
        await using var pair = await PoolManager.GetServerClient();
        var prototypes = pair.Server.ProtoMan;
        var factory = pair.Server.ResolveDependency<IComponentFactory>();
        await pair.Server.WaitAssertion(() =>
        {
            void Expect(string rocket, string effect)
            {
                Assert.That(prototypes.Index<EntityPrototype>(rocket)
                        .TryGetComponent(out DropshipAmmoComponent? ammo, factory), Is.True, rocket);
                Assert.That(ammo!.ImpactEffects.Select(id => id.Id), Does.Contain(effect), rocket);
            }

            Expect("RMCDropshipAttachmentAmmoRocketMiniMikeWeedKiller", "CMUCasFalloutWeedKiller");
            Expect("RMCDropshipAttachmentAmmoRocketMiniMikeSmoke", "CMUCasFalloutObscurant");
            Expect("RMCDropshipAttachmentAmmoRocketMiniMikeFlare", "CMUCasFalloutFlare");
            Expect("RMCDropshipAttachmentAmmoRocketMiniMikeThermobaric", "CMUCasFalloutResin");
            Expect("AU14DropshipAttachmentAmmoRocketThermobaricTwo", "CMUCasFalloutResin");
            Expect("RMCDropshipAttachmentAmmoRocketMiniMikeHEAT", "CMUCasFalloutArmor");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeapIsGroundCompatibleAndOverheatsFasterThanSquall()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var prototypes = pair.Server.ProtoMan;
        var factory = pair.Server.ResolveDependency<IComponentFactory>();
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(prototypes.Index<EntityPrototype>("CMUMohawkM90Ammo")
                .TryGetComponent(out DropshipAmmoComponent? heap, factory), Is.True);
            Assert.That(prototypes.Index<EntityPrototype>("CMUMohawkM90AmmoKinetic")
                .TryGetComponent(out DropshipAmmoComponent? squall, factory), Is.True);
            Assert.That(heap!.GroundCompatible, Is.True, "HEAP must fire on the ground.");
            Assert.That(heap.DirectFireHeat, Is.GreaterThan(squall!.DirectFireHeat * 5),
                "HEAP must heat the barrels much faster than Squall.");

            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            var grid = maps.CreateGridEntity(mapId);
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            maps.SetTile(grid, Vector2i.Zero, new Tile(tiles["FloorSteel"].TileId));
            var point = entities.SpawnEntity("CMUMohawkM90Point", new EntityCoordinates(grid.Owner, Vector2.Zero));
            var chin = entities.GetComponent<MohawkChinGunComponent>(point);
            var turret = entities.System<MohawkChinGunSystem>();

            var shots = 0;
            while (!chin.Overheated && shots < 1000)
            {
                turret.AddHeat((point, chin), heap.DirectFireHeat);
                shots++;
            }

            Assert.That(chin.Overheated, Is.True);
            Assert.That(shots, Is.LessThanOrEqualTo(20), "Sustained HEAP fire must overheat quickly.");

            chin.Heat = 0;
            chin.Overheated = false;
            for (var i = 0; i < shots; i++)
                turret.AddHeat((point, chin), squall.DirectFireHeat);
            Assert.That(chin.Overheated, Is.False, "The same number of Squall rounds must not overheat the guns.");

            chin.Heat = chin.MaxHeat;
            chin.Overheated = true;
            turret.Update((chin.MaxHeat - chin.OverheatRecoveredAt) / chin.CoolPerSecond + 1);
            Assert.That(chin.Overheated, Is.False, "Cooling below the recovery threshold must clear the lockout.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task JamsFaultTheHardpointAndAdminToolsRecoverIt()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var config = pair.Server.ResolveDependency<IConfigurationManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            var grid = maps.CreateGridEntity(mapId);
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            maps.SetTile(grid, Vector2i.Zero, new Tile(tiles["FloorSteel"].TileId));
            var coordinates = new EntityCoordinates(grid.Owner, Vector2.Zero);
            var point = entities.SpawnEntity("CMUMohawkM90Point", coordinates);
            var boot = entities.System<DropshipFireControlBootSystem>();
            var maxChance = config.GetCVar(CCVars.DropshipFaultMaxChance);

            Assert.That(boot.TryJam(point), Is.False, "Tests run with jams disabled.");
            config.SetCVar(CCVars.DropshipJamChance, 1f);
            config.SetCVar(CCVars.DropshipFaultMaxChance, 1f);
            try
            {
                Assert.That(boot.TryJam(point), Is.True);
                var fault = entities.GetComponent<DropshipHardpointFaultComponent>(point);
                Assert.That(fault.Faulted, Is.True);
                Assert.That(fault.Reason, Is.Not.Null);
                Assert.That(boot.TryJam(point), Is.False, "A jammed hardpoint must not jam twice.");
                boot.ClearFault(point);
                Assert.That(fault.Faulted, Is.False);
                Assert.That(fault.Reason, Is.Null);
            }
            finally
            {
                config.SetCVar(CCVars.DropshipJamChance, 0f);
                config.SetCVar(CCVars.DropshipFaultMaxChance, maxChance);
            }

            var mount = entities.GetComponent<DropshipWeaponPointComponent>(point);
            var containers = entities.System<SharedContainerSystem>();
            var ammo = entities.SpawnEntity("CMUMohawkM90AmmoKinetic", coordinates);
            Assert.That(containers.Insert(ammo, containers.GetContainer(point, mount.AmmoContainerSlotId)), Is.True);
            var payload = entities.GetComponent<DropshipAmmoComponent>(ammo);
            payload.Rounds = 0;
            Assert.That(entities.System<SharedDropshipWeaponSystem>().AdminRefillAmmo(point), Is.GreaterThan(0));
            Assert.That(payload.Rounds, Is.EqualTo(payload.MaxRounds));

            var chin = entities.GetComponent<MohawkChinGunComponent>(point);
            chin.Calibrated = false;
            chin.Heat = chin.MaxHeat;
            chin.Overheated = true;
            entities.System<MohawkChinGunSystem>().AdminRestore((point, chin));
            Assert.That(chin.Calibrated, Is.True);
            Assert.That(chin.Overheated, Is.False);
            Assert.That(chin.Heat, Is.Zero);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RtoRequestsAndDangerCloseGateStrikes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            var grid = maps.CreateGridEntity(mapId);
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            for (var x = -2; x <= 12; x++)
                maps.SetTile(grid, new Vector2i(x, 0), new Tile(tiles["FloorSteel"].TileId));
            EntityCoordinates At(float x) => new(grid.Owner, new Vector2(x + 0.5f, 0.5f));

            var requests = entities.System<CMUCasRequestSystem>();
            var weapons = entities.System<SharedDropshipWeaponSystem>();
            var rto = entities.SpawnEntity("CMMobHuman", At(10));
            entities.EnsureComponent<MarineComponent>(rto).Faction = "GOVFOR";
            var mark = entities.SpawnEntity(null, At(0));
            weapons.MakeDropshipTarget(mark, "TST", "GOVFOR");

            Assert.That(requests.TryCreateRequest(rto, mark, CMUCasMunition.Rockets, out var id), Is.True);
            Assert.That(requests.TryGetRequest(id, out var request), Is.True);
            Assert.That(request.Status, Is.EqualTo(CMUCasRequestStatus.Pending));
            Assert.That(requests.TryCreateRequest(rto, mark, CMUCasMunition.Rockets, out _), Is.False,
                "A mark can only carry one open request.");
            Assert.That(requests.TryClearHot(rto, id), Is.False, "Pending requests cannot be cleared hot.");

            var enemy = entities.SpawnEntity("CMMobHuman", At(10));
            entities.EnsureComponent<MarineComponent>(enemy).Faction = "OPFOR";
            Assert.That(requests.TryCreateRequest(enemy, mark, CMUCasMunition.Rockets, out _), Is.False,
                "Other factions cannot request strikes on a mark they do not own.");

            Assert.That(requests.TryAbort(rto, id), Is.True);
            requests.TryGetRequest(id, out request);
            Assert.That(request.Status, Is.EqualTo(CMUCasRequestStatus.Denied));

            var console = entities.SpawnEntity("CMUMohawkMidwayGunneryea0f8d621b", At(-2));
            var terminal = entities.GetComponent<DropshipTerminalWeaponsComponent>(console);
            var gunner = entities.SpawnEntity("CMMobHuman", At(-2));
            var weapon = entities.SpawnEntity(null, At(-2));
            Assert.That(requests.CheckDangerClose((console, terminal), At(10), gunner, weapon), Is.False,
                "Strikes on friendlies must warn before firing.");
            Assert.That(requests.CheckDangerClose((console, terminal), At(10), gunner, weapon), Is.True,
                "A repeated strike within the window confirms danger close.");
            Assert.That(requests.CheckDangerClose((console, terminal), new EntityCoordinates(grid.Owner, new Vector2(0.5f, 40)),
                gunner, weapon), Is.True, "Strikes away from friendlies need no confirmation.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UplinkMarkedTargetsDesignationsAndAutoCarryOut()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var entities = pair.Server.EntMan;
        var id = 0;
        EntityUid grid = default;
        await pair.Server.WaitAssertion(() =>
        {
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            var gridEnt = maps.CreateGridEntity(mapId);
            grid = gridEnt.Owner;
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            for (var x = -2; x <= 60; x++)
                maps.SetTile(gridEnt, new Vector2i(x, 0), new Tile(tiles["FloorSteel"].TileId));
            EntityCoordinates At(float x) => new(grid, new Vector2(x + 0.5f, 0.5f));
            EntityUid Mob(float x, string faction)
            {
                var mob = entities.SpawnEntity("CMMobHuman", At(x));
                entities.EnsureComponent<MarineComponent>(mob).Faction = faction;
                return mob;
            }

            var requests = entities.System<CMUCasRequestSystem>();
            var weapons = entities.System<SharedDropshipWeaponSystem>();
            var rto = Mob(0, "GOVFOR");
            Mob(1, "GOVFOR");
            Mob(30, "OPFOR");
            Mob(55, "OPFOR");

            var calledMark = entities.SpawnEntity(null, At(30));
            weapons.MakeDropshipTarget(calledMark, "TST", "GOVFOR");
            Assert.That(requests.TryCreateRequest(rto, calledMark, CMUCasMunition.Gau, out id,
                    CMUCasEffect.Incendiary, "  bunker  "),
                Is.True, "The uplink can request CAS onto an existing marked target.");
            Assert.That(requests.TryGetRequest(id, out var request), Is.True);
            Assert.That(request.RadioMark, Is.False);
            Assert.That(request.Munition, Is.EqualTo(CMUCasMunition.Gau));
            Assert.That(request.Effect, Is.EqualTo(CMUCasEffect.Incendiary));
            Assert.That(request.Remarks, Is.EqualTo("bunker"));
            Assert.That(entities.GetEntity(request.Target), Is.EqualTo(calledMark));

            var farMark = entities.SpawnEntity(null, At(55));
            weapons.MakeDropshipTarget(farMark, "FAR", "GOVFOR");
            Assert.That(requests.TryCreateRequest(rto, farMark, CMUCasMunition.Any, out _), Is.False,
                "Marks beyond 30 tiles are out of call range.");
            var nearMark = entities.SpawnEntity(null, At(25));
            weapons.MakeDropshipTarget(nearMark, "NER", "GOVFOR");
            Assert.That(requests.IsRtoDesignated(nearMark), Is.False);
            var lase = entities.SpawnEntity(null, At(26));
            entities.EnsureComponent<CMUCasRtoDesignationComponent>(lase);
            Assert.That(requests.IsRtoDesignated(nearMark), Is.True, "An RTO lase within two tiles designates the strike.");
            Assert.That(requests.IsRtoDesignated(At(10)), Is.False);
            Assert.That(requests.TryCreateRequest(rto, nearMark, CMUCasMunition.Bofors, out var lasedId), Is.True,
                "Visible marks within 30 tiles can be called.");
            requests.TryGetRequest(lasedId, out var lased);
            Assert.That(lased.Designated, Is.True);

            bool Match(CMUCasMunition munition, string abbreviation) =>
                CMUCasRequestSystem.Matches(munition, new DropshipWeaponComponent { Abbreviation = abbreviation });
            Assert.That(Match(CMUCasMunition.Gau, "GAU"), Is.True);
            Assert.That(Match(CMUCasMunition.Gau, "ATC"), Is.False);
            Assert.That(Match(CMUCasMunition.Bofors, "ATC"), Is.True);
            Assert.That(Match(CMUCasMunition.Howitzer, "HWR"), Is.True);
            Assert.That(Match(CMUCasMunition.Missiles, "MSL"), Is.True);
            Assert.That(Match(CMUCasMunition.Missiles, "RKT"), Is.False);
            Assert.That(Match(CMUCasMunition.Guns, "HWR"), Is.True);
            Assert.That(Match(CMUCasMunition.ChinGun, "M90"), Is.True);
            Assert.That(Match(CMUCasMunition.Any, "FLR"), Is.False, "Flare launchers never carry out a strike.");

            var console = entities.SpawnEntity("CMUMohawkMidwayGunneryea0f8d621b", At(-2));
            var pilot = Mob(-2, "GOVFOR");
            entities.EventBus.RaiseLocalEvent(console, new CMUCasRequestActionMsg(id, CMUCasRequestStatus.Accepted)
                { Actor = pilot, UiKey = DropshipTerminalWeaponsUi.Key });
            requests.TryGetRequest(id, out request);
            Assert.That(request.Status, Is.EqualTo(CMUCasRequestStatus.Accepted));
            Assert.That(request.CarryMode, Is.EqualTo(CMUCasCarryMode.Undecided));
            Assert.That(request.CarryCountdown, Is.EqualTo(12));

            Assert.That(requests.SetCarryMode(lasedId, CMUCasCarryMode.Manual), Is.False,
                "Pending requests have no carry-out decision.");
        });

        await pair.RunSeconds(6);
        await pair.Server.WaitAssertion(() =>
        {
            var requests = entities.System<CMUCasRequestSystem>();
            requests.TryGetRequest(id, out var request);
            Assert.That(request.CarryMode, Is.EqualTo(CMUCasCarryMode.Undecided));
            Assert.That(request.CarryCountdown, Is.InRange(5, 7));
        });

        await pair.RunSeconds(7);
        await pair.Server.WaitAssertion(() =>
        {
            var requests = entities.System<CMUCasRequestSystem>();
            requests.TryGetRequest(id, out var request);
            Assert.That(request.CarryMode, Is.EqualTo(CMUCasCarryMode.Auto),
                "Without a pilot decision the mission defaults to automatic carry-out after 12 seconds.");
            Assert.That(requests.SetCarryMode(id, CMUCasCarryMode.Manual), Is.True);
            requests.TryGetRequest(id, out request);
            Assert.That(request.CarryMode, Is.EqualTo(CMUCasCarryMode.Manual));
        });
        await pair.CleanReturnAsync();
    }
}
