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

    // CM-SS13 rolls this only in attack_alien's harm branch; its INTENT_DISARM branch never does, so a
    // xeno tackle must leave the mask alone even with the attacker aiming at the head.

    // CM-SS13 rolls this on the mask, not on the species: a non-Yautja wearing a Yautja gas mask can
    // lose it too, just with the untrained 20% cap and a scream instead of a roar.

}
