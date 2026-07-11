using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical.Anatomy.BodyParts;

[TestFixture]
public sealed class BodyPartSeveranceTest
{
    [Test]
    public async Task HeadSeversOnlyAfterOneHundredSeventyBruteOverkill()
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

            try
            {
                var key = new CMUMedicalBodyPartKey(BodyPartType.Head, BodyPartSymmetry.None);
                Assert.That(medical.TryGetBodyPart(human, key, out head), Is.True);

                var health = entMan.GetComponent<BodyPartHealthComponent>(head);
                Assert.That(health.SeveranceThreshold, Is.EqualTo(FixedPoint2.New(170)));

                // The head's 0.85 brute resistance turns 270 slash into 229.5
                // structural damage: 169.5 past its 60 HP, just short of severance.
                Assert.That(partHealth.TryApplyPartDamage(human, head, Damage("Slash", 270)), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(health.Current, Is.EqualTo(FixedPoint2.New(-169.5f)));
                    Assert.That(medical.TryGetBodyPart(human, key, out var attached), Is.True);
                    Assert.That(attached, Is.EqualTo(head));
                });

                Assert.That(partHealth.TryApplyPartDamage(human, head, Damage("Slash", 1)), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(health.Current, Is.LessThanOrEqualTo(FixedPoint2.New(-170)));
                    Assert.That(medical.TryGetBodyPart(human, key, out _), Is.False);
                    Assert.That(entMan.GetComponent<BodyPartComponent>(head).Body, Is.Null);
                });
            }
            finally
            {
                if (entMan.EntityExists(human))
                    entMan.DeleteEntity(human);
                if (entMan.EntityExists(head))
                    entMan.DeleteEntity(head);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static DamageSpecifier Damage(string type, FixedPoint2 amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        return damage;
    }
}
