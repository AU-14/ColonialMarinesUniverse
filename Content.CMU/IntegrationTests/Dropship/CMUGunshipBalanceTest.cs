using System.Linq;
using Content.IntegrationTests;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Armor;
using Content.Shared.CMU14.Dropship.Integrity;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class CMUGunshipBalanceTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: gamePreset
          id: CMUGunshipRenamedPreset
          gunshipMaxIntegrity: 4500
        """;

    public override PoolSettings PoolSettings => new() { Dirty = true };

    [TestCase("Insurgency", 4500f)]
    [TestCase("ForceOnForce", 4500f)]
    [TestCase("DistressSignal", 2500f)]
    [TestCase("ColonyFall", 2500f)]
    [TestCase("CMUGunshipRenamedPreset", 4500f)]
    public async Task DeployedGunshipUsesPresetHealth(string presetId, float expectedHealth)
    {
        await Server.WaitAssertion(() =>
        {
            var round = Server.System<AuRoundSystem>();
            var previous = round.SelectedPreset;
            round.SetPreset(Server.ResolveDependency<IPrototypeManager>().Index<GamePresetPrototype>(presetId));
            try
            {
                Assert.That(Server.System<MapLoaderSystem>().TryLoadMap(
                    new ResPath("/Maps/_RMC14/Shuttles/dynamic_gunship.yml"), out var map, out var grids,
                    DeserializationOptions.Default with { InitializeMaps = true }), Is.True);
                var grid = grids!.Single();
                var integrity = SEntMan.GetComponent<DropshipIntegrityComponent>(grid.Owner);
                Assert.Multiple(() =>
                {
                    Assert.That(integrity.MaxIntegrity, Is.EqualTo(expectedHealth));
                    Assert.That(integrity.Integrity, Is.EqualTo(expectedHealth),
                        "The deployed gunship must start fully repaired, not at its old capacity.");
                });
                Server.System<SharedMapSystem>().DeleteMap(map!.Value.Comp.MapId);
            }
            finally
            {
                round.SetPreset(previous);
            }
        });
    }

    [TestCase(false, 29, 5f)]
    [TestCase(false, 30, 10f)]
    [TestCase(true, 29, 5f)]
    [TestCase(true, 30, 10f)]
    public async Task BulletResistanceHonorsArmorPenetrationOnBothHullPaths(
        bool damageableHull,
        int armorPenetration,
        float expectedDamage)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var integrity = SEntMan.EnsureComponent<DropshipIntegrityComponent>(map.Grid);
            var hull = SEntMan.SpawnEntity("CMNormandyWall1", map.GridCoords);
            SEntMan.EnsureComponent<DropshipHullComponent>(hull);
            if (damageableHull)
            {
                SEntMan.EnsureComponent<DamageableComponent>(hull);
                SEntMan.EnsureComponent<InjurableComponent>(hull);
            }

            var projectile = SEntMan.SpawnEntity(null, map.GridCoords);
            var physics = SEntMan.AddComponent<PhysicsComponent>(projectile);
            var shot = SEntMan.AddComponent<ProjectileComponent>(projectile);
            shot.Damage = new DamageSpecifier { DamageDict = { ["Piercing"] = FixedPoint2.New(10) } };
            var piercing = SEntMan.AddComponent<CMArmorPiercingComponent>(projectile);
            Server.System<CMArmorSystem>().SetArmorPiercing((projectile, piercing), armorPenetration);
            var before = integrity.Integrity;
            SEntMan.System<SharedProjectileSystem>().ProjectileCollide((projectile, shot, physics), hull);

            Assert.That(integrity.Integrity, Is.EqualTo(before -
                expectedDamage * Server.System<DamageableSystem>().UniversalProjectileDamageModifier).Within(0.01f));
            Assert.That(shot.ProjectileSpent, Is.True);
            Assert.That(shot.Damage.GetTotal(), Is.EqualTo(FixedPoint2.New(10)),
                "Hull resistance must not mutate the projectile's reusable damage specifier.");
        });
    }
}
