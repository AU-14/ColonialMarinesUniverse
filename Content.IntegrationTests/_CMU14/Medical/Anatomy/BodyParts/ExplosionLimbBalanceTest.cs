using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion.Components;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts;

[TestFixture]
public sealed class ExplosionLimbBalanceTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: BlastCapBiological
          components:
          - type: Damageable
          - type: Injurable
            damageContainer: Biological
          - type: MaxDamage
            max: 100
        - type: entity
          id: BlastCapAllTypes
          parent: BlastCapBiological
          components:
          - type: Injurable
            damageContainer: null
        """;

    [Test]
    public async Task UnsupportedStructuralDamageCannotConsumeHumanDamageCap()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var biological = SEntMan.SpawnEntity("BlastCapBiological", map.GridCoords);
            var allTypes = SEntMan.SpawnEntity("BlastCapAllTypes", map.GridCoords);
            var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = 40, ["Heat"] = 40, ["Structural"] = 1500 } };
            var system = Server.System<DamageableSystem>();
            var applied = system.TryChangeDamage(biological, damage, impact: DamageImpact.Explosion)!;
            Assert.That(applied.GetTotal().Float(), Is.EqualTo(80).Within(0.02));
            Assert.That(applied.DamageDict.ContainsKey("Structural"), Is.False);
            applied = system.TryChangeDamage(allTypes, damage, impact: DamageImpact.Explosion)!;
            Assert.That(applied.GetTotal().Float(), Is.EqualTo(100).Within(0.04),
                "supported structural damage must still be capped");
            Assert.That(applied.DamageDict["Structural"].Float(), Is.GreaterThan(90));
        });
    }

    [Test]
    public async Task PointBlankHedpCanSeverLimbsAndArmorStillProtectsThem()
    {
        var map = await Pair.CreateTestMap();
        EntityUid unarmored = default, armored = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            map.Grid.Comp.CanSplit = false;
            for (var x = -10; x < 75; x++)
            for (var y = -10; y < 11; y++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
            unarmored = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid.Owner, new Vector2(0.5f, 0.5f)));
            armored = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid.Owner, new Vector2(64.5f, 0.5f)));
            var inventory = Server.System<InventorySystem>();
            foreach (var (prototype, slot) in new[] { ("AU14ArmorM3JungleOne", "outerClothing"), ("ArmorHelmetM10", "head") })
                Assert.That(inventory.TryEquip(armored, SEntMan.SpawnEntity(prototype, new EntityCoordinates(armored, Vector2.Zero)),
                    slot, silent: true, force: true), Is.True);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitPost(() =>
        {
            var blast = (ExplosiveComponent) SProtoMan.Index<EntityPrototype>("CMGrenadeHighExplosive").Components["Explosive"].Component;
            foreach (var target in new[] { unarmored, armored })
                Server.System<ExplosionSystem>().QueueExplosion(Server.System<SharedTransformSystem>().GetMapCoordinates(target),
                    blast.ExplosionType, blast.TotalIntensity, blast.IntensitySlope, blast.MaxIntensity,
                    cause: null, maxTileBreak: 0, canCreateVacuum: false);
        });
        await Pair.RunTicksSync(30);
        await Server.WaitAssertion(() =>
        {
            var index = Server.System<CMUMedicalBodyIndexSystem>();
            var nakedLimbs = index.GetBodyParts(unarmored).Count(p => p.Comp.PartType is BodyPartType.Arm or BodyPartType.Leg);
            var armoredLimbs = index.GetBodyParts(armored).Count(p => p.Comp.PartType is BodyPartType.Arm or BodyPartType.Leg);
            Assert.That(nakedLimbs, Is.LessThan(4));
            Assert.That(armoredLimbs, Is.GreaterThan(nakedLimbs));
            Assert.That(index.GetBodyParts(unarmored).Any(p => p.Comp.PartType == BodyPartType.Head), Is.True,
                "the limb adjustment must not bypass the head rules");
        });
    }
}
