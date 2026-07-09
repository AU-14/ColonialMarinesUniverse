using Content.Server._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Diagnostics.Examine;
using Content.Shared._CMU14.Medical.Injuries.Trauma;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical.Diagnostics;

[TestFixture]
public sealed class CMUMedicalExamineProjectionTest
{
    [Test]
    public async Task BodyLevelProjectionTracksCoherentWoundFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            torso = GetBodyPart(entMan, human, BodyPartType.Torso);

            Assert.That(partHealth.TryApplyPartDamage(
                human,
                torso,
                Damage("Piercing", 30),
                impact: DamageImpact.Projectile), Is.True);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var projectionSystem = entMan.System<CMUMedicalExamineProjectionSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            var projection = entMan.GetComponent<CMUMedicalExamineProjectionComponent>(human);
            Assert.That(projectionSystem.TryGetPart(
                projection,
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                out var torsoProjection), Is.True);
            var sourceEntries = ledger.GetEntries(entMan.GetComponent<BodyPartWoundComponent>(torso));

            Assert.Multiple(() =>
            {
                Assert.That(sourceEntries, Has.Count.EqualTo(1));
                Assert.That(torsoProjection.Wounds, Has.Count.EqualTo(1));
                Assert.That(torsoProjection.Wounds[0].Type, Is.EqualTo(WoundType.Brute));
                Assert.That(torsoProjection.Wounds[0].Size, Is.EqualTo(WoundSize.CutFlesh));
                Assert.That(torsoProjection.Wounds[0].Damage, Is.EqualTo(sourceEntries[0].Wound.Damage));
                Assert.That(torsoProjection.Wounds[0].Mechanism, Is.EqualTo(WoundMechanism.Bullet));
                Assert.That(torsoProjection.Wounds[0].Treated, Is.False);
                Assert.That(torsoProjection.ExternalBleeding, Is.GreaterThan(ExternalBleedTier.None));
                Assert.That(
                    projectionSystem.GetRemainingDamage(projection, WoundType.Brute),
                    Is.EqualTo(sourceEntries[0].Wound.Damage - sourceEntries[0].Wound.Healed));
            });

            Assert.That(woundsSystem.TryTreatWounds(
                torso,
                WoundType.Brute,
                1,
                out var treated), Is.True);
            Assert.That(treated, Is.EqualTo(1));
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var projectionSystem = entMan.System<CMUMedicalExamineProjectionSystem>();
            var projection = entMan.GetComponent<CMUMedicalExamineProjectionComponent>(human);
            Assert.That(projectionSystem.TryGetPart(
                projection,
                BodyPartType.Torso,
                BodyPartSymmetry.None,
                out var torsoProjection), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(torsoProjection.Wounds, Has.Count.EqualTo(1));
                Assert.That(torsoProjection.Wounds[0].Treated, Is.True);
                Assert.That(torsoProjection.ExternalBleeding, Is.EqualTo(ExternalBleedTier.None));
            });
            entMan.DeleteEntity(human);
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

    private static DamageSpecifier Damage(string type, FixedPoint2 amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        return damage;
    }
}
