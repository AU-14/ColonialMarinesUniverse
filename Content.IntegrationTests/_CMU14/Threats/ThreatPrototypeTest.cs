using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Humanoid;
using Content.Server._RMC14.Atmos;
using Content.Shared.ActionBlocker;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.Threats.Mobs.Threat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Prying.Components;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.CMU14.Threats;

[TestFixture]
public sealed class ThreatPrototypeTest : GameTest
{
    private static readonly (string Prototype, string EyeColor)[] Castes =
    [
        ("CMUThreatCommon", "#ff3030"),
        ("CMUThreatCommonPolice", "#ff3030"),
        ("CMUThreatHunter", "#dc143c"),
        ("CMUThreatSmoker", "#a0a0a0"),
        ("CMUThreatBoomer", "#32ff32"),
        ("CMUThreatScreamer", "#ff69b4"),
        ("CMUThreatJockey", "#ff8c00"),
        ("CMUThreatSpitter", "#ffff33"),
        ("CMUThreatWitch", "#ffffff"),
    ];

    private static readonly (string Prototype, int Damage)[] CasteMeleeDamage =
    [
        ("CMUThreatCommon", 30),
        ("CMUThreatCommonPolice", 30),
        ("CMUThreatHunter", 36),
        ("CMUThreatSmoker", 36),
        ("CMUThreatBoomer", 36),
        ("CMUThreatScreamer", 36),
        ("CMUThreatJockey", 36),
        ("CMUThreatSpitter", 36),
        ("CMUThreatWitch", 75),
    ];

    [TestCaseSource(nameof(Castes))]
    public async Task ThreatHaveCasteEyesWithoutEmittingLightAndCanPry((string Prototype, string EyeColor) test)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity(test.Prototype, map.GridCoords);
            Assert.That(SEntMan.HasComponent<CMUThreatComponent>(uid), Is.True);
            Assert.That(SEntMan.HasComponent<PryingComponent>(uid), Is.True);
            Assert.That(SEntMan.HasComponent<PointLightComponent>(uid), Is.False);

            var appearance = Server.System<HumanoidOrganAppearanceSystem>();
            Assert.That(appearance.TryGetColors(uid, out _, out var eyes), Is.True);
            Assert.That(eyes, Is.EqualTo(Color.FromHex(test.EyeColor)));
        });
    }

    [Test]
    public async Task RemovedCastesHaveNoPrototypes()
    {
        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            Assert.That(prototypes.HasIndex<EntityPrototype>("CMUThreatCharger"), Is.False);
            Assert.That(prototypes.HasIndex<EntityPrototype>("CMUThreatTank"), Is.False);
            Assert.That(prototypes.Index<EntityPrototype>("CMUThreatJockey").Parents,
                Does.Contain("CMUThreatDwarfBase"));
            Assert.That(prototypes.HasIndex<EntityPrototype>("MobDwarf"), Is.True);
        });
    }

    [Test]
    public async Task ThreatUseDedicatedVocalCollections()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity("CMUThreatCommon", map.GridCoords);
            var threat = SEntMan.GetComponent<CMUThreatComponent>(uid);
            var prototypes = Server.ResolveDependency<IPrototypeManager>();

            Assert.That(SEntMan.HasComponent<DeathgaspComponent>(uid), Is.False,
                "the generic human deathgasp would overlap the threat death vocal");
            AssertCollection(threat.IdleSound, "CMUThreatDocile", 7, prototypes);
            AssertCollection(threat.AttackSound, "CMUThreatAttack", 5, prototypes);
            AssertCollection(threat.HurtSound, "CMUThreatHurt", 6, prototypes);
            AssertCollection(threat.DeathSound, "CMUThreatDeath", 7, prototypes);
        });
    }

    [Test]
    public async Task ThreatPathfindTowardHumanTarget()
    {
        var map = await Pair.CreateTestMap();
        EntityUid threat = default;
        EntityUid target = default;
        var initialDistance = 0f;

        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            for (var x = -2; x <= 8; x++)
            {
                for (var y = -2; y <= 2; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            threat = SEntMan.SpawnEntity("CMUThreatCommon", new EntityCoordinates(map.Grid, 0, 0));
            target = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 6, 0));
            initialDistance = Vector2.Distance(
                transform.GetMapCoordinates(threat).Position,
                transform.GetMapCoordinates(target).Position);
        });

        await Pair.RunSeconds(4f);

        await Server.WaitAssertion(() =>
        {
            var component = SEntMan.GetComponent<CMUThreatComponent>(threat);
            var transform = SEntMan.System<SharedTransformSystem>();
            var finalDistance = Vector2.Distance(
                transform.GetMapCoordinates(threat).Position,
                transform.GetMapCoordinates(target).Position);

            Assert.That(component.CurrentTarget, Is.EqualTo(target));
            Assert.That(SEntMan.HasComponent<InputMoverComponent>(threat), Is.True);
            Assert.That(SEntMan.HasComponent<MobMoverComponent>(threat), Is.True);
            Assert.That(finalDistance, Is.LessThan(initialDistance - 0.5f));
        });
    }

    private static void AssertCollection(
        SoundSpecifier? sound,
        string expectedId,
        int expectedFiles,
        IPrototypeManager prototypes)
    {
        Assert.That(sound, Is.TypeOf<SoundCollectionSpecifier>());
        var collection = (SoundCollectionSpecifier) sound!;
        Assert.That(collection.Collection?.Id, Is.EqualTo(expectedId));
        Assert.That(prototypes.Index<SoundCollectionPrototype>(expectedId).PickFiles,
            Has.Count.EqualTo(expectedFiles));
    }

    [Test]
    public async Task DeadThreatRemainUntilBurnedForOneMinute()
    {
        var map = await Pair.CreateTestMap();
        EntityUid threat = default;
        var ashBefore = 0;

        await Server.WaitAssertion(() =>
        {
            threat = SEntMan.SpawnEntity("CMUThreatCommon", map.GridCoords);
            ashBefore = SEntMan.EntityQuery<MetaDataComponent>()
                .Count(meta => !meta.Deleted && meta.EntityPrototype?.ID == "Ash");
            Server.System<MobStateSystem>().ChangeMobState(threat, MobState.Dead);
        });

        await Pair.RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(threat), Is.False, "threat corpses must not disappear on death");
            var flammable = SEntMan.GetComponent<FlammableComponent>(threat);
            flammable.Damage = new DamageSpecifier();
            Assert.That(Server.System<RMCFlammableSystem>().Ignite((threat, flammable), 1, 120, 120), Is.True);
        });

        await Pair.RunSeconds(59f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(threat), Is.False, "the corpse burned away before one minute elapsed");
        });

        await Pair.RunSeconds(2f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(threat), Is.True);
            Assert.That(SEntMan.EntityQuery<MetaDataComponent>()
                .Count(meta => !meta.Deleted && meta.EntityPrototype?.ID == "Ash"), Is.EqualTo(ashBefore + 1));
        });
    }

    [TestCaseSource(nameof(CasteMeleeDamage))]
    public async Task ThreatMeleeUsesBladeSoundsAndBypassesArmor((string Prototype, int Damage) test)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity(test.Prototype, map.GridCoords);
            var melee = SEntMan.GetComponent<MeleeWeaponComponent>(uid);
            var impact = SEntMan.GetComponent<DamageImpactProfileComponent>(uid);

            Assert.That(melee.ResistanceBypass, Is.True);
            Assert.That(melee.Damage.DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(test.Damage)));
            Assert.That(melee.SwingSound, Is.TypeOf<SoundPathSpecifier>());
            Assert.That(((SoundPathSpecifier) melee.SwingSound).Path.ToString(),
                Is.EqualTo("/Audio/Weapons/slash.ogg"));
            Assert.That(melee.HitSound, Is.TypeOf<SoundPathSpecifier>());
            Assert.That(((SoundPathSpecifier) melee.HitSound!).Path.ToString(),
                Is.EqualTo("/Audio/Weapons/bladeslice.ogg"));
            Assert.That(impact.Melee.Penetration, Is.EqualTo(DamageImpactPenetration.Forced));
        });
    }

    [Test]
    public async Task ThreatUseUpdatedDamageAndDelimbBalance()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var common = SEntMan.SpawnEntity("CMUThreatCommon", map.GridCoords);
            var hunter = SEntMan.SpawnEntity("CMUThreatHunter", map.GridCoords);
            var smoker = SEntMan.SpawnEntity("CMUThreatSmoker", map.GridCoords);
            var boomer = SEntMan.SpawnEntity("CMUThreatBoomer", map.GridCoords);
            var jockey = SEntMan.SpawnEntity("CMUThreatJockey", map.GridCoords);
            var spitter = SEntMan.SpawnEntity("CMUThreatSpitter", map.GridCoords);
            var witch = SEntMan.SpawnEntity("CMUThreatWitch", map.GridCoords);

            Assert.That(SEntMan.GetComponent<CMUThreatComponent>(common).DelimbChance, Is.EqualTo(0.05f));
            Assert.That(SEntMan.GetComponent<CMUThreatComponent>(hunter).DelimbChance, Is.EqualTo(0.15f));
            Assert.That(SEntMan.GetComponent<CMUThreatComponent>(witch).DelimbChance, Is.EqualTo(0.15f));

            var hunterComp = SEntMan.GetComponent<CMUHunterThreatComponent>(hunter);
            AssertDamage(hunterComp.ImpactDamage, "Slash", 60);
            AssertDamage(hunterComp.PinDamage, "Slash", 21);
            AssertDamage(SEntMan.GetComponent<CMUSmokerThreatComponent>(smoker).ChokeDamage,
                "Asphyxiation", 7.5f);
            AssertDamage(SEntMan.GetComponent<CMUJockeyThreatComponent>(jockey).RideDamage, "Slash", 15);
            AssertDamage(SEntMan.GetComponent<CMUSpitterThreatComponent>(spitter).AcidDamage, "Heat", 12);

            var boomerComp = SEntMan.GetComponent<CMUBoomerThreatComponent>(boomer);
            Assert.That(boomerComp.ExplosionRadius, Is.EqualTo(3f));
            Assert.That(boomerComp.IedTotalIntensity, Is.EqualTo(80f));
        });
    }

    [Test]
    public async Task SpitterUsesXenoProjectileAndCreatesThreeByThreeAcidPools()
    {
        var map = await CreateOpenTestMap();
        EntityUid projectile = default;
        Vector2i center = default;

        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            Assert.That(prototypes.Index<EntityPrototype>("CMUThreatSpitProjectile").Parents,
                Does.Contain("XenoSpitProjectile"));

            projectile = SEntMan.SpawnEntity("CMUThreatSpitProjectile", new EntityCoordinates(map.Grid, 2, 0));
            var transform = SEntMan.System<SharedTransformSystem>();
            var maps = Server.System<SharedMapSystem>();
            var spit = SEntMan.GetComponent<CMUThreatSpitProjectileComponent>(projectile);
            spit.Destination = transform.GetMapCoordinates(projectile);
            spit.EndsAt = Server.Timing.CurTime;
            spit.AcidRadius = 0.45f;
            spit.AcidDuration = TimeSpan.FromSeconds(12);
            spit.AcidDamageInterval = TimeSpan.FromSeconds(1);
            center = maps.TileIndicesFor(map.Grid,
                SEntMan.GetComponent<TransformComponent>(projectile).Coordinates);
        });

        await Pair.RunSeconds(0.5f);

        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var pools = SEntMan.EntityQuery<CMUThreatAcidPoolComponent, TransformComponent>().ToArray();
            Assert.That(pools, Has.Length.EqualTo(9));

            var occupiedTiles = pools
                .Select(pool => maps.TileIndicesFor(map.Grid, pool.Item2.Coordinates))
                .ToHashSet();
            var expectedTiles = new HashSet<Vector2i>();
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                    expectedTiles.Add(center + new Vector2i(x, y));
            }

            Assert.That(occupiedTiles, Is.EquivalentTo(expectedTiles));
            Assert.That(pools.All(pool => pool.Item1.Radius == 0.45f), Is.True);
        });
    }

    [Test]
    public async Task ThreatTargetActiveDestructibleLightsButIgnoreInvincibleLights()
    {
        var map = await CreateOpenTestMap();
        EntityUid threat = default;
        EntityUid destructibleLight = default;
        EntityUid invincibleLight = default;

        await Server.WaitAssertion(() =>
        {
            threat = SEntMan.SpawnEntity("CMUThreatCommon", new EntityCoordinates(map.Grid, 0, 0));
            invincibleLight = SEntMan.SpawnEntity("AlwaysPoweredWallLight", new EntityCoordinates(map.Grid, 2, 0));
            SEntMan.RemoveComponent<DamageableComponent>(invincibleLight);
            destructibleLight = SEntMan.SpawnEntity("AlwaysPoweredWallLight", new EntityCoordinates(map.Grid, 5, 0));

            Assert.That(SEntMan.HasComponent<CMUThreatLightTargetComponent>(destructibleLight), Is.True);
            Assert.That(SEntMan.HasComponent<DamageableComponent>(destructibleLight), Is.True);
            Assert.That(SEntMan.HasComponent<DamageableComponent>(invincibleLight), Is.False);
        });

        await Pair.RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            var component = SEntMan.GetComponent<CMUThreatComponent>(threat);
            Assert.That(component.CurrentTarget, Is.EqualTo(destructibleLight));
            Assert.That(component.CurrentTarget, Is.Not.EqualTo(invincibleLight));
        });
    }

    private static void AssertDamage(DamageSpecifier damage, string type, float expected)
    {
        Assert.That(damage.DamageDict[type], Is.EqualTo(FixedPoint2.New(expected)));
    }

    [Test]
    public async Task JockeyAttachesAndBlocksVictimMovement()
    {
        var map = await CreateOpenTestMap();
        EntityUid jockey = default;
        EntityUid victim = default;

        await Server.WaitAssertion(() =>
        {
            jockey = SEntMan.SpawnEntity("CMUThreatJockey", new EntityCoordinates(map.Grid, 0, 0));
            victim = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 3, 0));
        });

        await Pair.RunSeconds(4f);

        await Server.WaitAssertion(() =>
        {
            var pin = SEntMan.GetComponent<CMUThreatPinComponent>(victim);
            Assert.That(pin.Kind, Is.EqualTo(CMUThreatPinKind.Jockey));
            Assert.That(pin.Attacker, Is.EqualTo(jockey));
            Assert.That(SEntMan.GetComponent<TransformComponent>(jockey).ParentUid, Is.EqualTo(victim));
            Assert.That(SEntMan.GetComponent<TransformComponent>(jockey).LocalPosition,
                Is.EqualTo(new Vector2(0f, 0.45f)));
            Assert.That(SEntMan.GetComponent<PhysicsComponent>(jockey).CanCollide, Is.False);
            Assert.That(Server.System<ActionBlockerSystem>().CanMove(victim), Is.False);
        });

        await Pair.RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.GetComponent<TransformComponent>(jockey);
            Assert.That(transform.ParentUid, Is.EqualTo(victim));
            Assert.That(transform.LocalPosition, Is.EqualTo(new Vector2(0f, 0.45f)));
        });
    }

    [Test]
    public async Task SmokerRemainsStationaryWhileTongueIsActive()
    {
        var map = await CreateOpenTestMap();
        EntityUid smoker = default;
        EntityUid victim = default;

        await Server.WaitAssertion(() =>
        {
            smoker = SEntMan.SpawnEntity("CMUThreatSmoker", new EntityCoordinates(map.Grid, 0, 0));
            victim = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 6, 0));
            SEntMan.GetComponent<CMUSmokerThreatComponent>(smoker).ChokeDamage = new DamageSpecifier();
        });

        await Pair.RunSeconds(1f);

        Vector2 pinnedPosition = default;
        await Server.WaitAssertion(() =>
        {
            var pin = SEntMan.GetComponent<CMUThreatPinComponent>(victim);
            Assert.That(pin.Kind, Is.EqualTo(CMUThreatPinKind.Smoker));
            Assert.That(Server.System<ActionBlockerSystem>().CanMove(smoker), Is.False);
            pinnedPosition = SEntMan.System<SharedTransformSystem>().GetMapCoordinates(smoker).Position;
        });

        await Pair.RunSeconds(2f);

        await Server.WaitAssertion(() =>
        {
            var currentPosition = SEntMan.System<SharedTransformSystem>().GetMapCoordinates(smoker).Position;
            Assert.That(SEntMan.HasComponent<CMUThreatPinComponent>(victim), Is.True);
            Assert.That(Vector2.Distance(currentPosition, pinnedPosition), Is.LessThan(0.1f));
        });
    }

    [Test]
    public async Task RallyPointOverridesCombatAndMovesNearbyThreat()
    {
        var map = await CreateOpenTestMap();
        EntityUid threat = default;
        EntityUid target = default;
        var transform = SEntMan.System<SharedTransformSystem>();

        await Server.WaitAssertion(() =>
        {
            threat = SEntMan.SpawnEntity("CMUThreatCommon", new EntityCoordinates(map.Grid, 0, 0));
            target = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, -2, 0));
        });

        await Pair.RunSeconds(1f);

        EntityUid rally = default;
        Vector2 initialPosition = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUThreatComponent>(threat).CurrentTarget, Is.EqualTo(target));
            initialPosition = transform.GetMapCoordinates(threat).Position;
            rally = SEntMan.SpawnEntity("CMUThreatRallyPoint", new EntityCoordinates(map.Grid, 8, 0));
        });

        await Pair.RunSeconds(8f);

        await Server.WaitAssertion(() =>
        {
            var threatComponent = SEntMan.GetComponent<CMUThreatComponent>(threat);
            var currentPosition = transform.GetMapCoordinates(threat).Position;
            var rallyPosition = transform.GetMapCoordinates(rally).Position;
            Assert.That(Vector2.Distance(currentPosition, rallyPosition),
                Is.LessThan(Vector2.Distance(initialPosition, rallyPosition) - 0.5f));
            Assert.That(threatComponent.RallyPoint, Is.EqualTo(rally).Or.Null);
        });
    }

    [Test]
    public async Task HunterPinsInPlaceForFifteenSeconds()
    {
        var map = await CreateOpenTestMap();
        EntityUid hunter = default;
        EntityUid victim = default;

        await Server.WaitAssertion(() =>
        {
            hunter = SEntMan.SpawnEntity("CMUThreatHunter", new EntityCoordinates(map.Grid, 0, 0));
            victim = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 3, 0));

            var hunterAbility = SEntMan.GetComponent<CMUHunterThreatComponent>(hunter);
            hunterAbility.ImpactDamage = new DamageSpecifier();
            hunterAbility.PinDamage = new DamageSpecifier();
            SEntMan.GetComponent<MeleeWeaponComponent>(hunter).Damage = new DamageSpecifier();
        });

        await Pair.RunSeconds(4f);

        Vector2 pinnedPosition = default;
        await Server.WaitAssertion(() =>
        {
            var pin = SEntMan.GetComponent<CMUThreatPinComponent>(victim);
            Assert.That(pin.Kind, Is.EqualTo(CMUThreatPinKind.Hunter));
            Assert.That(pin.Attacker, Is.EqualTo(hunter));
            Assert.That((pin.EndsAt - Server.Timing.CurTime).TotalSeconds, Is.InRange(10.5, 15.1));
            pinnedPosition = SEntMan.System<SharedTransformSystem>().GetMapCoordinates(hunter).Position;
        });

        await Pair.RunSeconds(2f);

        await Server.WaitAssertion(() =>
        {
            var currentPosition = SEntMan.System<SharedTransformSystem>().GetMapCoordinates(hunter).Position;
            Assert.That(Vector2.Distance(currentPosition, pinnedPosition), Is.LessThan(0.1f));
            Assert.That(SEntMan.HasComponent<CMUThreatPinComponent>(victim), Is.True);
        });
    }

    private async Task<TestMapData> CreateOpenTestMap()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = -3; x <= 9; x++)
            {
                for (var y = -3; y <= 3; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }
        });
        return map;
    }
}
