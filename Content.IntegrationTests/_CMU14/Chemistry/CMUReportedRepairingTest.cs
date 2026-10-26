using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Effects.Positive;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.IntegrationTests.CMU14.Chemistry;

[TestFixture]
public sealed class CMUReportedRepairingTest
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RA0039", Justification = "CMU14: This test supplies isolated mutable fixture data; prototype registration and inheritance are not exercised.")]
    [TestCase("CMBarricadeMetal", true)]
    [TestCase("CMUCombatDrone", true)]
    [TestCase("CMUFlamerDrone", true)]
    [TestCase("CMMobHuman", false)]
    public async Task RepairingTouchHealsEligibleTargetsOnly(string prototype, bool repairable)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var target = entities.SpawnEntity(prototype, map.GridCoords);
            var damage = entities.System<DamageableSystem>();
            damage.TryChangeDamage(target, new DamageSpecifier { DamageDict = { ["Blunt"] = 30, ["Heat"] = 30 } }, true);
            var reagent = new ReagentPrototype
            {
                Metabolisms = new ReagentMetabolisms
                {
                    Metabolisms = new()
                    {
                        ["Medicine"] = new ReagentEffectsEntry { Effects = [new Repairing { Potency = 2 }] },
                    },
                },
            };
            var quantity = new ReagentQuantity("Water", 1);
            var before = damage.GetTotalDamage(target);
            var reactive = entities.System<ReactiveSystem>();
            reactive.ReactionEntity(target, ReactionMethod.Ingestion, reagent, quantity);
            Assert.That(damage.GetTotalDamage(target), Is.EqualTo(before), "Contact repair is not an ingestion effect.");
            reactive.ReactionEntity(target, ReactionMethod.Touch, reagent, quantity);
            Assert.That(damage.GetTotalDamage(target), Is.EqualTo(before - (FixedPoint2)(repairable ? 20 : 0)),
                "One unit repairs ten brute and ten burn on the intended inorganic targets.");
        });
        await pair.CleanReturnAsync();
    }
}
