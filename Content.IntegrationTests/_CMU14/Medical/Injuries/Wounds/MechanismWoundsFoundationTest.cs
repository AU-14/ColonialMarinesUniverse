#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Diagnostics.Examine;
using Content.Shared.CMU14.Medical.Injuries.Trauma;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Injuries.Wounds.Events;
using Content.Server.CMU14.Medical.Diagnostics.Examine;
using Content.Server.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Content.Server.Verbs;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Collections.Generic;
using System.Linq;

namespace Content.IntegrationTests.CMU14.Medical.Injuries.Wounds;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class MechanismWoundsFoundationTest
{

    [Test]
    public async Task ProjectilePiercingCreatesBulletWound()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    torso,
                    Damage("Piercing", 30),
                    impact: DamageImpact.Projectile), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);

                Assert.Multiple(() =>
                {
                    Assert.That(entries, Has.Count.EqualTo(1));
                    Assert.That(entries[0].Mechanism, Is.EqualTo(WoundMechanism.Bullet));
                    Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Untreated));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplosionCreatesSeparateBruteAndBurnBlastWounds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    torso,
                    Damage(("Blunt", 30), ("Heat", 30)),
                    mechanism: CMUTraumaMechanism.Explosive,
                    impact: DamageImpact.Explosion), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);

                Assert.Multiple(() =>
                {
                    Assert.That(entries, Has.Count.EqualTo(2));
                    Assert.That(entries[0].Mechanism, Is.EqualTo(WoundMechanism.Blast));
                    Assert.That(entries[0].Wound.Type, Is.EqualTo(WoundType.Brute));
                    Assert.That(entries[0].Wound.Damage, Is.EqualTo(FixedPoint2.New(27)), "Torso brute resistance remains part of structural wound burden.");
                    Assert.That(entries[1].Wound.Type, Is.EqualTo(WoundType.Burn));
                    Assert.That(entries[1].Wound.Damage, Is.EqualTo(FixedPoint2.New(30)));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RepeatedSameMechanismMergesIntoLargerWound()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 20), impact: DamageImpact.MeleeSlash), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 20), impact: DamageImpact.MeleeSlash), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);

                Assert.Multiple(() =>
                {
                    Assert.That(entries, Has.Count.EqualTo(1));
                    Assert.That(entries[0].Mechanism, Is.EqualTo(WoundMechanism.Slash));
                    Assert.That(entries[0].Wound.Damage, Is.EqualTo(FixedPoint2.New(36)));
                    Assert.That(entries[0].Size, Is.EqualTo(WoundSize.CutFlesh));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DistinctMechanismsRespectSixRecordCap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Piercing", 10), impact: DamageImpact.Projectile), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Piercing", 10), impact: new DamageImpact(DamageImpactDelivery.Melee, DamageImpactContact.Stab, DamageImpactPenetration.Medium, DamageImpactEnergy.Medium)), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Blunt", 10), impact: new DamageImpact(DamageImpactDelivery.Melee, DamageImpactContact.Crush, DamageImpactPenetration.None, DamageImpactEnergy.Medium)), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Heat", 10), impact: new DamageImpact(DamageImpactDelivery.Contact, DamageImpactContact.Burn, DamageImpactPenetration.None, DamageImpactEnergy.Medium)), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Blunt", 10), mechanism: CMUTraumaMechanism.Explosive, impact: DamageImpact.Explosion), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: new DamageImpact(DamageImpactDelivery.Thrown, DamageImpactContact.Fragment, DamageImpactPenetration.Low, DamageImpactEnergy.Medium)), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                Assert.That(ledger.GetEntries(wounds), Has.Count.EqualTo(6));
                Assert.That(ledger.GetEntries(wounds).Where(entry => entry.Wound.Type == WoundType.Burn)
                    .Sum(entry => entry.Wound.Damage.Float()), Is.EqualTo(10));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TreatingAnyWoundOnLimbClearsExternalBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 20), impact: DamageImpact.MeleeSlash), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                Assert.That(wounds.ExternalBleeding, Is.Not.EqualTo(ExternalBleedTier.None));

                Assert.That(woundsSystem.TryTreatWound(torso, out var completed), Is.True);
                Assert.That(completed, Is.False);

                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.None));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NewDamageCanRestartExternalBleedingAfterTreatment()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 20), impact: DamageImpact.MeleeSlash), Is.True);
                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                Assert.That(woundsSystem.TryTreatWound(torso, out _), Is.True);
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.None));

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Piercing", 20), impact: DamageImpact.Projectile), Is.True);

                Assert.That(wounds.ExternalBleeding, Is.Not.EqualTo(ExternalBleedTier.None));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WoundTreatmentClearsCleanupAndUsesTreatedState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);
                Assert.That(entries[0].Cleanup, Is.Not.EqualTo(WoundCleanupFlags.None));

                Assert.That(woundsSystem.TryTreatWound(torso, out var completed), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(completed, Is.True);
                    Assert.That(entries[0].Wound.Treated, Is.True);
                    Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Adequate));
                    Assert.That(entries[0].Cleanup, Is.EqualTo(WoundCleanupFlags.None));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CleanWoundRecordsAreRemovedAfterRecovery()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        EntityUid human = default;
        EntityUid torso = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            torso = GetBodyPart(entMan, human, BodyPartType.Torso);

            Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
            var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
            Assert.That(woundsSystem.TryTreatWound(torso, WoundTreatmentQuality.Optimal, out var completed), Is.True);
            Assert.That(completed, Is.True);
            Assert.That(entMan.System<CMUWoundLedgerSystem>().GetEntries(wounds)[0].Cleanup, Is.EqualTo(WoundCleanupFlags.None));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(27f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.HasComponent<BodyPartWoundComponent>(torso), Is.False);

            var damageable = entMan.GetComponent<DamageableComponent>(human);
            var brute = DamageInGroup(prototypes, damageable.Damage, "Brute");
            var health = entMan.GetComponent<BodyPartHealthComponent>(torso);
            Assert.Multiple(() =>
            {
                Assert.That(brute, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(health.Current, Is.EqualTo(health.Max));
            });

            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BulletWoundsDoNotDefaultToRetainedFragments()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    torso,
                    Damage("Piercing", 20),
                    impact: DamageImpact.Projectile), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entry = ledger.GetEntries(wounds)[0];
                Assert.Multiple(() =>
                {
                    Assert.That(entry.Mechanism, Is.EqualTo(WoundMechanism.Bullet));
                    Assert.That(entry.Cleanup & WoundCleanupFlags.RetainedFragment, Is.EqualTo(WoundCleanupFlags.None));
                    Assert.That(entMan.HasComponent<CMUShrapnelComponent>(torso), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("XenoHedgehogSpikeProjectileSpread")]
    [TestCase("XenoHedgehogSpikeProjectileSpreadShort")]
    public async Task HedgehogSpikeProjectilesAddShrapnel(string projectilePrototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var projectile = entMan.SpawnEntity(projectilePrototype, MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
                var damage = entMan.GetComponent<ProjectileComponent>(projectile).Damage;

                Assert.That(partHealth.TryApplyPartDamage(human, torso, damage, tool: projectile), Is.True);
                Assert.That(entMan.TryGetComponent<CMUShrapnelComponent>(torso, out var shrapnel), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entry = ledger.GetEntries(wounds)[0];
                Assert.Multiple(() =>
                {
                    Assert.That(shrapnel!.Fragments, Is.EqualTo(1));
                    Assert.That(entry.Cleanup & WoundCleanupFlags.RetainedFragment, Is.Not.EqualTo(WoundCleanupFlags.None));
                });
            }
            finally
            {
                entMan.DeleteEntity(projectile);
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid GetBodyPart(IEntityManager entMan, EntityUid bodyUid, BodyPartType type)
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (partUid, part) in body.GetBodyChildren(bodyUid))
        {
            if (part.PartType == type)
                return partUid;
        }

        Assert.Fail($"Expected CMU human to have {type}.");
        return EntityUid.Invalid;
    }

    private static EntityUid GetBodyPart(
        IEntityManager entMan,
        EntityUid bodyUid,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (partUid, part) in body.GetBodyChildren(bodyUid))
        {
            if (part.PartType == type && part.Symmetry == symmetry)
                return partUid;
        }

        Assert.Fail($"Expected CMU human to have {symmetry} {type}.");
        return EntityUid.Invalid;
    }

    private static DamageSpecifier Damage(string type, FixedPoint2 amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        return damage;
    }

    private static DamageSpecifier Damage(params (string Type, FixedPoint2 Amount)[] entries)
    {
        var damage = new DamageSpecifier();
        foreach (var (type, amount) in entries)
        {
            damage.DamageDict[type] = amount;
        }

        return damage;
    }

    private static FixedPoint2 DamageInGroup(IPrototypeManager prototypes, DamageSpecifier damage, string groupId)
    {
        var group = prototypes.Index<DamageGroupPrototype>(groupId);
        return damage.TryGetDamageInGroup(group, out var total) ? total : FixedPoint2.Zero;
    }

}

#pragma warning restore RA0002
