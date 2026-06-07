using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using RMCWoundsSystem = Content.Server._RMC14.Medical.Wounds.WoundsSystem;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class CMURMCWoundBoundaryTest
{
    [Test]
    public async Task CmuHumanNormalDamageCreatesCmuWoundsWithoutRmcWoundedComponent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var hitLocation = entMan.System<SharedHitLocationSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
                var beforeDamage = entMan.GetComponent<DamageableComponent>(human).TotalDamage;

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUHumanMedicalComponent>(human), Is.True);
                    Assert.That(entMan.HasComponent<WoundableComponent>(human), Is.True);
                    Assert.That(entMan.HasComponent<WoundedComponent>(human), Is.False);
                });

                hitLocation.SetForcedHit(human, BodyPartType.Torso);
                damageable.TryChangeDamage(human, Damage("Slash", 20), ignoreResistances: true);

                var damage = entMan.GetComponent<DamageableComponent>(human);
                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);

                Assert.Multiple(() =>
                {
                    Assert.That(damage.TotalDamage, Is.GreaterThan(beforeDamage));
                    Assert.That(wounds.Wounds, Has.Count.EqualTo(1));
                    Assert.That(wounds.Wounds[0].Damage, Is.GreaterThan(FixedPoint2.Zero));
                    Assert.That(entMan.HasComponent<WoundedComponent>(human), Is.False);
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
    public async Task AddWoundDoesNotCreateRmcWoundedComponentForCmuHuman()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var woundsSystem = entMan.System<RMCWoundsSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var woundable = entMan.GetComponent<WoundableComponent>(human);

                woundsSystem.AddWound((human, woundable), FixedPoint2.New(20), WoundType.Brute);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUHumanMedicalComponent>(human), Is.True);
                    Assert.That(entMan.HasComponent<WoundedComponent>(human), Is.False);
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
    public async Task AddWoundKeepsRmcPathForNonCmuWoundable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var woundsSystem = entMan.System<RMCWoundsSystem>();
            var target = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                var woundable = entMan.EnsureComponent<WoundableComponent>(target);

                woundsSystem.AddWound((target, woundable), FixedPoint2.New(20), WoundType.Brute);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUHumanMedicalComponent>(target), Is.False);
                    Assert.That(entMan.TryGetComponent<WoundedComponent>(target, out var wounded), Is.True);
                    Assert.That(wounded!.Wounds, Has.Count.EqualTo(1));
                });
            }
            finally
            {
                entMan.DeleteEntity(target);
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

    private static DamageSpecifier Damage(string type, FixedPoint2 amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        return damage;
    }
}
