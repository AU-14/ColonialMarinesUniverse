using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared._RMC14.Tackle;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

// CMU14 Test: the CM-SS13 mask knockoff (code/modules/mob/living/carbon/xenomorph/attack_alien.dm
// :127-150) as ported into the xeno melee pipeline.
[TestFixture]
public sealed class YautjaMaskKnockoffTest
{
    private const string MaskSlot = "mask";

    // The roll goes through IRobustRandom.Prob, so a given seed resolves to the same outcome every
    // run. Each test walks seeds until the branch it needs comes up, instead of repeating random
    // combat until something happens, which keeps both the passing and the failing branch testable.
    private const int SeedAttempts = 256;

    private static void EnableHitLocation(RobustIntegrationTest.ServerIntegrationInstance server)
    {
        server.CfgMan.SetCVar(CMUMedicalCCVars.Enabled, true);
        server.CfgMan.SetCVar(CMUMedicalCCVars.HitLocationEnabled, true);
    }

    private static MeleeHitEvent Swing(IEntityManager entMan, EntityUid attacker, EntityUid target)
    {
        var damage = new DamageSpecifier { DamageDict = { ["Slash"] = 20 } };
        var args = new MeleeHitEvent([target], attacker, attacker, damage, null);
        entMan.EventBus.RaiseLocalEvent(attacker, args);
        return args;
    }

    private static bool WearsMask(IEntityManager entMan, EntityUid wearer)
    {
        return entMan.System<InventorySystem>().TryGetSlotEntity(wearer, MaskSlot, out _);
    }

    private static EntityUid EquipMask(IEntityManager entMan, EntityUid wearer, EntityCoordinates coords, string prototype)
    {
        var inventory = entMan.System<InventorySystem>();
        if (inventory.TryGetSlotEntity(wearer, MaskSlot, out var existing))
            return existing.Value;

        var mask = entMan.SpawnEntity(prototype, coords);
        Assert.That(inventory.TryEquip(wearer, mask, MaskSlot, silent: true, force: true), Is.True,
            "the test fixture must be able to put a mask on the wearer");
        return mask;
    }

    private static void AimAtHead(IEntityManager entMan, EntityUid attacker, bool head)
    {
        entMan.System<SharedBodyZoneTargetingSystem>()
            .SelectZone(attacker, head ? TargetBodyZone.Head : TargetBodyZone.Chest);
    }

    // Swings until the mask comes off, returning the swing that did it, or null if no seed did.
    private static MeleeHitEvent? SwingUntilMaskDrops(
        IEntityManager entMan,
        IRobustRandom random,
        EntityUid attacker,
        EntityUid wearer)
    {
        for (var seed = 0; seed < SeedAttempts; seed++)
        {
            random.SetSeed(seed);
            var args = Swing(entMan, attacker, wearer);
            if (!WearsMask(entMan, wearer))
                return args;
        }

        return null;
    }

    [Test]
    public async Task HeadAimedXenoAttackKnocksTheMaskOffAndTheDroppedMaskStillWorks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitPost(() => EnableHitLocation(pair.Server));

        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var random = pair.Server.ResolveDependency<IRobustRandom>();

            // The knockoff plays CM-SS13's alien_claw_metal through the CMUAlienClawMetal collection.
            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var yautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var mask = EquipMask(entMan, yautja, map.GridCoords, "CMUYautjaMask");
            AimAtHead(entMan, xeno, head: true);

            var hit = SwingUntilMaskDrops(entMan, random, xeno, yautja);
            Assert.That(hit, Is.Not.Null, "a head-aimed xeno attack can smash the mask off");

            Assert.Multiple(() =>
            {
                Assert.That(WearsMask(entMan, yautja), Is.False, "the mask is genuinely unequipped");
                Assert.That(entMan.EntityExists(mask), Is.True, "the mask stays a real item");
                Assert.That(entMan.GetComponent<TransformComponent>(mask).ParentUid, Is.Not.EqualTo(yautja),
                    "the mask is dropped rather than hidden on the wearer");
                Assert.That(hit!.Handled, Is.True, "CM-SS13 spends the swing on the mask, so no damage is dealt");
            });

            // The dropped mask keeps normal inventory behaviour and can be worn again.
            Assert.That(inventory.TryEquip(yautja, mask, MaskSlot, silent: true, force: true), Is.True);
            Assert.That(WearsMask(entMan, yautja), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FailedRollKeepsTheMaskEquipped()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitPost(() => EnableHitLocation(pair.Server));

        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.EntMan;
            var random = pair.Server.ResolveDependency<IRobustRandom>();

            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var yautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            EquipMask(entMan, yautja, map.GridCoords, "CMUYautjaMask");
            AimAtHead(entMan, xeno, head: true);

            for (var seed = 0; seed < SeedAttempts; seed++)
            {
                random.SetSeed(seed);
                var args = Swing(entMan, xeno, yautja);
                if (!WearsMask(entMan, yautja))
                    continue;

                Assert.Multiple(() =>
                {
                    Assert.That(WearsMask(entMan, yautja), Is.True, "a failed roll leaves the mask on");
                    Assert.That(args.Handled, Is.False, "and the swing still deals its damage");
                });
                return;
            }

            Assert.Fail("the mask always came off, the roll can never fail");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaWithoutAMaskIsUnaffected()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitPost(() => EnableHitLocation(pair.Server));

        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var random = pair.Server.ResolveDependency<IRobustRandom>();

            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var yautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            AimAtHead(entMan, xeno, head: true);

            if (WearsMask(entMan, yautja))
                Assert.That(inventory.TryUnequip(yautja, MaskSlot, silent: true, force: true), Is.True);

            for (var seed = 0; seed < SeedAttempts; seed++)
            {
                random.SetSeed(seed);
                var args = Swing(entMan, xeno, yautja);
                Assert.Multiple(() =>
                {
                    Assert.That(args.Handled, Is.False, $"seed {seed}");
                    Assert.That(WearsMask(entMan, yautja), Is.False, $"seed {seed}");
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AttackNotAimedAtTheHeadNeverKnocksTheMaskOff()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitPost(() => EnableHitLocation(pair.Server));

        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.EntMan;
            var random = pair.Server.ResolveDependency<IRobustRandom>();

            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var yautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            EquipMask(entMan, yautja, map.GridCoords, "CMUYautjaMask");
            AimAtHead(entMan, xeno, head: false);

            for (var seed = 0; seed < SeedAttempts; seed++)
            {
                random.SetSeed(seed);
                var args = Swing(entMan, xeno, yautja);
                Assert.Multiple(() =>
                {
                    Assert.That(WearsMask(entMan, yautja), Is.True, $"the mask survives every seed, including {seed}");
                    Assert.That(args.Handled, Is.False, $"seed {seed}");
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OtherMaskSlotItemsAndHelmetsAreNotAffected()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitPost(() => EnableHitLocation(pair.Server));

        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var random = pair.Server.ResolveDependency<IRobustRandom>();

            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            EquipMask(entMan, human, map.GridCoords, "CMMaskGas");

            var helmet = entMan.SpawnEntity("ArmorHelmetM10", map.GridCoords);
            Assert.That(inventory.TryEquip(human, helmet, "head", silent: true, force: true), Is.True);

            AimAtHead(entMan, xeno, head: true);

            for (var seed = 0; seed < SeedAttempts; seed++)
            {
                random.SetSeed(seed);
                var args = Swing(entMan, xeno, human);
                Assert.Multiple(() =>
                {
                    Assert.That(WearsMask(entMan, human), Is.True, $"a non-Yautja mask is never smashed off, seed {seed}");
                    Assert.That(inventory.TryGetSlotEntity(human, "head", out var worn) && worn == helmet, Is.True,
                        $"helmets are never affected, seed {seed}");
                    Assert.That(args.Handled, Is.False, $"seed {seed}");
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonXenoAttackerCannotKnockTheMaskOff()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitPost(() => EnableHitLocation(pair.Server));

        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.EntMan;
            var random = pair.Server.ResolveDependency<IRobustRandom>();

            var attacker = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var yautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            EquipMask(entMan, yautja, map.GridCoords, "CMUYautjaMask");
            AimAtHead(entMan, attacker, head: true);

            for (var seed = 0; seed < SeedAttempts; seed++)
            {
                random.SetSeed(seed);
                var args = Swing(entMan, attacker, yautja);
                Assert.Multiple(() =>
                {
                    Assert.That(WearsMask(entMan, yautja), Is.True, $"only Xenomorphs smash masks off, seed {seed}");
                    Assert.That(args.Handled, Is.False, $"seed {seed}");
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    // CM-SS13 rolls this only in attack_alien's harm branch; its INTENT_DISARM branch never does, so a
    // xeno tackle must leave the mask alone even with the attacker aiming at the head.
    [Test]
    public async Task XenoTackleDoesNotKnockTheMaskOff()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitPost(() => EnableHitLocation(pair.Server));

        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.EntMan;
            var random = pair.Server.ResolveDependency<IRobustRandom>();

            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var yautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            EquipMask(entMan, yautja, map.GridCoords, "CMUYautjaMask");
            AimAtHead(entMan, xeno, head: true);

            for (var seed = 0; seed < SeedAttempts; seed++)
            {
                random.SetSeed(seed);
                var disarm = new CMDisarmEvent(xeno);
                entMan.EventBus.RaiseLocalEvent(yautja, ref disarm);
                Assert.That(WearsMask(entMan, yautja), Is.True, $"a tackle is not a mask knockoff, seed {seed}");
            }
        });

        await pair.CleanReturnAsync();
    }

    // CM-SS13 rolls this on the mask, not on the species: a non-Yautja wearing a Yautja gas mask can
    // lose it too, just with the untrained 20% cap and a scream instead of a roar.
    [Test]
    public async Task NonYautjaWearingAYautjaMaskCanStillLoseIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitPost(() => EnableHitLocation(pair.Server));

        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.EntMan;
            var random = pair.Server.ResolveDependency<IRobustRandom>();

            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            EquipMask(entMan, human, map.GridCoords, "CMUYautjaMask");
            AimAtHead(entMan, xeno, head: true);

            var hit = SwingUntilMaskDrops(entMan, random, xeno, human);
            Assert.That(hit, Is.Not.Null, "CM-SS13 keys the knockoff on the mask, not on the wearer's species");
            Assert.That(hit!.Handled, Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
