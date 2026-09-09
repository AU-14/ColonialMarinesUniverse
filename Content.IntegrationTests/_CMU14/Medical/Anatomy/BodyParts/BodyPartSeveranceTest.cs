using System.Collections.Generic;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts;

[TestFixture]
public sealed class BodyPartSeveranceTest
{
    [Test]
    public async Task AutomaticHeadSeveranceProtectsProjectilesCriticalAndRevivableDeadTargets()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var medical = entities.System<CMUMedicalBodyIndexSystem>();
            var mobState = entities.System<MobStateSystem>();
            var partHealth = entities.System<SharedBodyPartHealthSystem>();
            var projectileTarget = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var criticalTarget = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var deadTarget = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var headKey = new CMUMedicalBodyPartKey(BodyPartType.Head, BodyPartSymmetry.None);
                Assert.That(medical.TryGetBodyPart(projectileTarget, headKey, out var projectileHead), Is.True);
                Assert.That(medical.TryGetBodyPart(criticalTarget, headKey, out var criticalHead), Is.True);
                Assert.That(medical.TryGetBodyPart(deadTarget, headKey, out var deadHead), Is.True);

                var projectileDamage = Damage("Piercing", 1000);
                Assert.That(partHealth.TryApplyPartDamage(
                    projectileTarget,
                    projectileHead,
                    projectileDamage,
                    impact: DamageImpact.ForProjectile(projectileDamage)), Is.True);

                mobState.ChangeMobState(criticalTarget, MobState.Critical);
                Assert.That(partHealth.TryApplyPartDamage(
                    criticalTarget,
                    criticalHead,
                    Damage("Slash", 1000),
                    impact: DamageImpact.MeleeSlash), Is.True);

                mobState.ChangeMobState(deadTarget, MobState.Dead);
                Assert.That(partHealth.TryApplyPartDamage(
                    deadTarget,
                    deadHead,
                    Damage("Blunt", 1000),
                    impact: DamageImpact.Explosion), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<BodyPartHealthComponent>(projectileHead).Current,
                        Is.LessThan(FixedPoint2.Zero), "projectile head damage must still be applied");
                    Assert.That(entities.GetComponent<BodyPartHealthComponent>(projectileHead).SeveranceDamage,
                        Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(entities.GetComponent<BodyPartHealthComponent>(criticalHead).SeveranceDamage,
                        Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(entities.GetComponent<BodyPartHealthComponent>(deadHead).SeveranceDamage,
                        Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(medical.TryGetBodyPart(projectileTarget, headKey, out _), Is.True);
                    Assert.That(medical.TryGetBodyPart(criticalTarget, headKey, out _), Is.True);
                    Assert.That(medical.TryGetBodyPart(deadTarget, headKey, out _), Is.True);
                });
            }
            finally
            {
                entities.DeleteEntity(projectileTarget);
                entities.DeleteEntity(criticalTarget);
                entities.DeleteEntity(deadTarget);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LiveVitalRemovalDamagesButRecursiveBodyDeletionDoesNot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<DetachableOrganSystem>();
            var damageable = entMan.System<DamageableSystem>();
            var medical = entMan.System<CMUMedicalBodyIndexSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            EntityUid? detachedBody = null;

            try
            {
                var key = new CMUMedicalBodyPartKey(BodyPartType.Head, BodyPartSymmetry.None);
                Assert.That(medical.TryGetBodyPart(human, key, out var head), Is.True);
                Assert.That(damageable.GetTotalDamage(human), Is.EqualTo(FixedPoint2.Zero));

                detachedBody = body.Detach(head);
                Assert.Multiple(() =>
                {
                    Assert.That(detachedBody, Is.Not.Null);
                    Assert.That(damageable.GetTotalDamage(human), Is.EqualTo(FixedPoint2.New(300)),
                        "removing the last live vital head must retain the fork's bloodloss penalty");
                });

                var intact = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                entMan.DeleteEntity(intact);
                Assert.That(entMan.EntityExists(intact), Is.False,
                    "recursive deletion must not apply vital-part damage to a terminating body");
            }
            finally
            {
                if (entMan.EntityExists(human))
                    entMan.DeleteEntity(human);
                if (detachedBody is { } carrier && entMan.EntityExists(carrier))
                    entMan.DeleteEntity(carrier);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeadSeversOnlyAfterWeightedMeleeImpactCrossesThreshold()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var medical = entMan.System<CMUMedicalBodyIndexSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            EntityUid head = default;
            EntityUid? detachedBody = null;

            try
            {
                var key = new CMUMedicalBodyPartKey(BodyPartType.Head, BodyPartSymmetry.None);
                Assert.That(medical.TryGetBodyPart(human, key, out head), Is.True);

                var health = entMan.GetComponent<BodyPartHealthComponent>(head);
                Assert.That(health.SeveranceThreshold, Is.EqualTo(FixedPoint2.New(170)));
                var choppingImpact = DamageImpact.MeleeSlash with { Energy = DamageImpactEnergy.High };

                // Resistance and the high-energy slash profile leave the first chop below the
                // combined 230-point health and severance threshold.
                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    head,
                    Damage("Slash", 180),
                    impact: choppingImpact), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(health.SeveranceDamage, Is.LessThan(health.Max + health.SeveranceThreshold));
                    Assert.That(medical.TryGetBodyPart(human, key, out var attached), Is.True);
                    Assert.That(attached, Is.EqualTo(head));
                });

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    head,
                    Damage("Slash", 33),
                    impact: choppingImpact), Is.True);
                detachedBody = FindDetachedCarrier(entMan, head);
                Assert.Multiple(() =>
                {
                    Assert.That(health.SeveranceDamage, Is.GreaterThanOrEqualTo(health.Max + health.SeveranceThreshold));
                    Assert.That(medical.TryGetBodyPart(human, key, out _), Is.False);
                    Assert.That(entMan.System<SharedBodySystem>().GetRootPartOrNull(detachedBody.Value)?.Entity,
                        Is.EqualTo(head));
                    Assert.That(entMan.GetComponent<BodyPartComponent>(head).Body, Is.EqualTo(detachedBody));
                    Assert.That(entMan.GetComponent<OrganComponent>(head).Body, Is.EqualTo(detachedBody));
                });
            }
            finally
            {
                if (entMan.EntityExists(human))
                    entMan.DeleteEntity(human);
                if (detachedBody is { } carrier && entMan.EntityExists(carrier))
                    entMan.DeleteEntity(carrier);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SeveredArmMovesItsHandSubtreeIntoRecoverableCarrier()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bodySystem = entMan.System<SharedBodySystem>();
            var medical = entMan.System<CMUMedicalBodyIndexSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            EntityUid? detachedBody = null;

            try
            {
                var armKey = new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left);
                var handKey = new CMUMedicalBodyPartKey(BodyPartType.Hand, BodyPartSymmetry.Left);
                Assert.That(medical.TryGetBodyPart(human, armKey, out var arm), Is.True);
                Assert.That(medical.TryGetBodyPart(human, handKey, out var hand), Is.True);
                Assert.That(entMan.GetComponent<ChildOrganComponent>(hand).Parent, Is.EqualTo(arm));

                Assert.That(partHealth.TryApplyPartDamage(human, arm, Damage("Slash", 1000)), Is.True);
                detachedBody = FindDetachedCarrier(entMan, arm);

                var carrierBody = entMan.GetComponent<BodyComponent>(detachedBody.Value);
                var carrierOrgans = carrierBody.Organs!.ContainedEntities;
                var remainingHands = entMan.GetComponent<HandsComponent>(human);
                Assert.Multiple(() =>
                {
                    Assert.That(bodySystem.GetRootPartOrNull(detachedBody!.Value)?.Entity, Is.EqualTo(arm));
                    Assert.That(carrierOrgans, Is.EquivalentTo(new[] { arm, hand }));
                    Assert.That(entMan.GetComponent<ChildOrganComponent>(hand).Parent, Is.EqualTo(arm));
                    Assert.That(entMan.GetComponent<OrganComponent>(arm).Body, Is.EqualTo(detachedBody));
                    Assert.That(entMan.GetComponent<OrganComponent>(hand).Body, Is.EqualTo(detachedBody));
                    Assert.That(entMan.GetComponent<BodyPartComponent>(arm).Body, Is.EqualTo(detachedBody));
                    Assert.That(entMan.GetComponent<BodyPartComponent>(hand).Body, Is.EqualTo(detachedBody));
                    Assert.That(medical.TryGetBodyPart(human, armKey, out _), Is.False);
                    Assert.That(medical.TryGetBodyPart(human, handKey, out _), Is.False);
                    Assert.That(remainingHands.Hands.Keys, Is.EquivalentTo(new[] { "right" }));
                    Assert.That(remainingHands.SortedHands, Is.EqualTo(new[] { "right" }));
                    Assert.That(remainingHands.ActiveHandId, Is.EqualTo("right"));
                });
            }
            finally
            {
                if (entMan.EntityExists(human))
                    entMan.DeleteEntity(human);
                if (detachedBody is { } carrier && entMan.EntityExists(carrier))
                    entMan.DeleteEntity(carrier);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CanonicalHandsSynchronizeAcrossArmSubtreeDetachAndReattach()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var player = pair.Player!;
        var originalAttached = player.AttachedEntity;
        EntityUid human = default;
        EntityUid arm = default;
        EntityUid hand = default;
        EntityUid torso = default;
        EntityUid carrier = default;
        NetEntity humanNet = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                human = originalAttached is { } original
                    ? entMan.SpawnEntity("CMMobHuman", entMan.GetComponent<TransformComponent>(original).Coordinates)
                    : entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                humanNet = entMan.GetNetEntity(human);
                var medical = entMan.System<CMUMedicalBodyIndexSystem>();
                Assert.That(medical.TryGetBodyPart(
                    human,
                    new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left),
                    out arm),
                    Is.True);
                Assert.That(medical.TryGetBodyPart(
                    human,
                    new CMUMedicalBodyPartKey(BodyPartType.Hand, BodyPartSymmetry.Left),
                    out hand),
                    Is.True);
                torso = entMan.System<SharedBodySystem>().GetRootPartOrNull(human)!.Value.Entity;
                server.PlayerMan.SetAttachedEntity(player, human);
                AssertCanonicalHands(entMan, human, new[] { "left", "right" }, new[] { "right", "left" });
            });
            await pair.RunTicksSync(5);

            await client.WaitAssertion(() =>
            {
                var clientHuman = client.EntMan.GetEntity(humanNet);
                AssertCanonicalHands(client.EntMan, clientHuman, new[] { "left", "right" }, new[] { "right", "left" });
            });

            await server.WaitPost(() =>
            {
                carrier = server.EntMan.System<DetachableOrganSystem>().Detach(arm) ?? EntityUid.Invalid;
                Assert.That(carrier.Valid, Is.True);
                AssertCanonicalHands(server.EntMan, human, new[] { "right" }, new[] { "right" });
            });
            await pair.RunTicksSync(5);

            await client.WaitAssertion(() =>
            {
                var clientHuman = client.EntMan.GetEntity(humanNet);
                AssertCanonicalHands(client.EntMan, clientHuman, new[] { "right" }, new[] { "right" });
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var body = entMan.System<SharedBodySystem>();
                Assert.That(body.AttachPart(torso, "left_arm", arm), Is.True);
                Assert.That(entMan.GetComponent<ChildOrganComponent>(hand).Parent, Is.EqualTo(arm));
                entMan.DeleteEntity(carrier);
                carrier = default;
                AssertCanonicalHands(entMan, human, new[] { "left", "right" }, new[] { "right", "left" });
            });
            await pair.RunTicksSync(5);

            await client.WaitAssertion(() =>
            {
                var clientHuman = client.EntMan.GetEntity(humanNet);
                AssertCanonicalHands(client.EntMan, clientHuman, new[] { "left", "right" }, new[] { "right", "left" });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.PlayerMan.SetAttachedEntity(player, originalAttached);
            });

            // Pool cleanup owns this hierarchy; manually deleting replicated parents with children exercises a pinned
            // RT bug outside this test.
        }

        await pair.CleanReturnAsync();
    }

    private static EntityUid FindDetachedCarrier(IEntityManager entMan, EntityUid expectedRoot)
    {
        foreach (var metadata in entMan.EntityQuery<MetaDataComponent>())
        {
            if (metadata.EntityPrototype?.ID != "DetachedBody")
                continue;

            var carrier = metadata.Owner;
            if (entMan.System<SharedBodySystem>().GetRootPartOrNull(carrier)?.Entity == expectedRoot)
                return carrier;
        }

        Assert.Fail($"Expected a DetachedBody carrier rooted at {expectedRoot}.");
        return EntityUid.Invalid;
    }

    private static void AssertCanonicalHands(
        IEntityManager entMan,
        EntityUid body,
        IReadOnlyCollection<string> expectedIds,
        IReadOnlyList<string> expectedOrder)
    {
        var hands = entMan.GetComponent<HandsComponent>(body);
        Assert.Multiple(() =>
        {
            Assert.That(hands.Hands.Keys, Is.EquivalentTo(expectedIds));
            Assert.That(hands.SortedHands, Is.EqualTo(expectedOrder));
            Assert.That(hands.ActiveHandId, Is.Not.Null);
            Assert.That(hands.Hands.Keys, Has.None.StartsWith("body_part_slot_"));
        });
    }

    private static DamageSpecifier Damage(string type, FixedPoint2 amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        return damage;
    }
}
