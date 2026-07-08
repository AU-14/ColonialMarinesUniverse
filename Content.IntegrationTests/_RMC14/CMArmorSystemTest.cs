using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._RMC14.Armor;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
[TestOf(typeof(CMArmorSystem))]
public sealed class CMArmorSystemTest
{
    private const string TestArmorEntity = "RMCArmorInvalidOriginDamageable";
    private const string TestOuterBioArmor = "RMCTestOuterBioArmor";
    private const string TestInnerBioArmor = "RMCTestInnerBioArmor";
    private const string TestChestBioArmor = "RMCTestChestBioArmor";
    private static readonly ProtoId<DamageTypePrototype> HeatDamageType = "Heat";
    private static readonly ProtoId<DamageTypePrototype> SlashDamageType = "Slash";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  id: {TestArmorEntity}
  name: {TestArmorEntity}
  components:
  - type: Damageable
    damageContainer: Biological
  - type: CMArmor

- type: entity
  id: {TestOuterBioArmor}
  name: {TestOuterBioArmor}
  components:
  - type: Clothing
    slots:
    - outerClothing
  - type: CMArmor
    bio: 40

- type: entity
  id: {TestInnerBioArmor}
  name: {TestInnerBioArmor}
  components:
  - type: Clothing
    slots:
    - innerClothing
  - type: CMArmor
    bio: 40

- type: entity
  id: {TestChestBioArmor}
  name: {TestChestBioArmor}
  components:
  - type: Clothing
    slots:
    - outerClothing
  - type: CMArmor
    bio: 40
    coveredZones:
    - Chest
";

    [Test]
    public async Task DamageWithInvalidOriginDoesNotResolveOriginTransform()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var sysMan = server.ResolveDependency<IEntitySystemManager>();

        DamageSpecifier result = null;

        await server.WaitPost(() =>
        {
            var target = entMan.SpawnEntity(TestArmorEntity, map.MapCoords);
            var slash = protoMan.Index(SlashDamageType);
            var damageable = sysMan.GetEntitySystem<DamageableSystem>();

            result = damageable.TryChangeDamage(target, new DamageSpecifier(slash, 10), origin: EntityUid.Invalid);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(result, Is.Not.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EquippedArmorOnlyProtectsHitPartSlots()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var protoMan = server.ResolveDependency<IPrototypeManager>();
            var damageable = entMan.System<DamageableSystem>();
            var hitLocation = entMan.System<SharedHitLocationSystem>();
            var inventory = entMan.System<InventorySystem>();
            var heat = protoMan.Index(HeatDamageType);

            var torsoDamage = ApplyForcedHitDamage(
                entMan,
                damageable,
                hitLocation,
                inventory,
                heat,
                BodyPartType.Torso);
            var headDamage = ApplyForcedHitDamage(
                entMan,
                damageable,
                hitLocation,
                inventory,
                heat,
                BodyPartType.Head);

            Assert.Multiple(() =>
            {
                Assert.That(headDamage, Is.GreaterThan(torsoDamage));
                Assert.That(headDamage, Is.GreaterThan(FixedPoint2.Zero));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InnerClothingArmorProtectsLegSlots()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var protoMan = server.ResolveDependency<IPrototypeManager>();
            var damageable = entMan.System<DamageableSystem>();
            var hitLocation = entMan.System<SharedHitLocationSystem>();
            var inventory = entMan.System<InventorySystem>();
            var heat = protoMan.Index(HeatDamageType);

            var legDamage = ApplyForcedHitDamage(
                entMan,
                damageable,
                hitLocation,
                inventory,
                heat,
                BodyPartType.Leg,
                TestInnerBioArmor,
                "jumpsuit");
            var headDamage = ApplyForcedHitDamage(
                entMan,
                damageable,
                hitLocation,
                inventory,
                heat,
                BodyPartType.Head,
                TestInnerBioArmor,
                "jumpsuit");

            Assert.Multiple(() =>
            {
                Assert.That(headDamage, Is.GreaterThan(legDamage));
                Assert.That(headDamage, Is.GreaterThan(FixedPoint2.Zero));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ArmorCoverageOverrideNarrowsMatchingSlot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var armor = entMan.SpawnEntity(TestChestBioArmor, MapCoordinates.Nullspace);

            try
            {
                Assert.That(inventory.TryEquip(human, armor, "outerClothing", silent: true, force: true), Is.True);

                var chest = new CMGetArmorEvent(
                    SlotFlags.OUTERCLOTHING,
                    TargetPart: BodyPartType.Torso,
                    TargetZone: TargetBodyZone.Chest);
                entMan.EventBus.RaiseLocalEvent(human, ref chest);

                var groin = new CMGetArmorEvent(
                    SlotFlags.OUTERCLOTHING,
                    TargetPart: BodyPartType.Torso,
                    TargetZone: TargetBodyZone.GroinPelvis);
                entMan.EventBus.RaiseLocalEvent(human, ref groin);

                Assert.Multiple(() =>
                {
                    Assert.That(chest.Bio, Is.EqualTo(40));
                    Assert.That(groin.Bio, Is.EqualTo(0));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static FixedPoint2 ApplyForcedHitDamage(
        IEntityManager entMan,
        DamageableSystem damageable,
        SharedHitLocationSystem hitLocation,
        InventorySystem inventory,
        DamageTypePrototype damageType,
        BodyPartType part,
        string armorPrototype = TestOuterBioArmor,
        string slot = "outerClothing")
    {
        var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var armor = entMan.SpawnEntity(armorPrototype, MapCoordinates.Nullspace);

        try
        {
            Assert.That(inventory.TryEquip(human, armor, slot, silent: true, force: true), Is.True);

            var before = entMan.GetComponent<DamageableComponent>(human).TotalDamage;
            hitLocation.SetForcedHit(human, part);
            damageable.TryChangeDamage(human, new DamageSpecifier(damageType, 10));
            var after = entMan.GetComponent<DamageableComponent>(human).TotalDamage;

            return after - before;
        }
        finally
        {
            entMan.DeleteEntity(human);
        }
    }
}
