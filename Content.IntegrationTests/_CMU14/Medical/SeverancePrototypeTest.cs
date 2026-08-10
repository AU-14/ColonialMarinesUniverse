using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.Explosion;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class SeverancePrototypeTest
{
    private static readonly EntProtoId HumanLeftArm = "CMUPartHumanLeftArm";
    private static readonly ProtoId<ExplosionPrototype> RmcExplosion = "RMC";
    private static readonly ProtoId<ExplosionPrototype> RmcMortarExplosion = "RMCMortar";

    [Test]
    public async Task SeveranceTuningLoadsFromPrototypes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ProtoMan;
            var factory = server.EntMan.ComponentFactory;

            var arm = prototypes.Index(HumanLeftArm);
            Assert.That(arm.TryComp<BodyPartHealthComponent>(out var health, factory), Is.True);
            Assert.That(health!.SeveranceDamageCoefficients["Piercing"], Is.EqualTo(0.025f));
            Assert.That(health.SeveranceDamageCoefficients["Slash"], Is.EqualTo(1f));

            AssertMultiplier(prototypes, factory, "CMProjectileShrapnel", 3f);
            AssertMultiplier(prototypes, factory, "CMBulletSniper10x28mm", 10f);
            AssertMultiplier(prototypes, factory, "RMCBulletHMG10x28mmTungsten", 8f);
            AssertMultiplier(prototypes, factory, "RMCBaseProjectileRocket84mm", 8f);

            Assert.That(prototypes.Index(RmcExplosion).SeveranceMultiplier, Is.EqualTo(4f));
            Assert.That(prototypes.Index(RmcMortarExplosion).SeveranceMultiplier, Is.EqualTo(8f));
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertMultiplier(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id,
        float expected)
    {
        var prototype = prototypes.Index(id);
        Assert.That(prototype.TryComp<SeveranceDamageModifierComponent>(out var modifier, factory), Is.True, id.Id);
        Assert.That(modifier!.Multiplier, Is.EqualTo(expected), id.Id);
    }
}
