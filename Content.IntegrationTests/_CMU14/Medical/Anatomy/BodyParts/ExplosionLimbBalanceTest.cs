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

}
