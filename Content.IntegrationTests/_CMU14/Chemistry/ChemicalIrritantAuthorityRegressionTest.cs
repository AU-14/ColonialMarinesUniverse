using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ChemicalIrritants;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Chemistry;

[TestFixture]
public sealed class ChemicalIrritantAuthorityRegressionTest : GameTest
{
    private const string Dylovene = "CMDylovene";

    [Test]
    public async Task DyloveneReductionIsServerAuthoritativeAndReplicated()
    {
        await Pair.CreateTestMap();

        var serverTarget = EntityUid.Invalid;
        await Server.WaitAssertion(() =>
        {
            serverTarget = SSpawnAtPosition(null, Pair.TestMap!.GridCoords);
            var irritant = SEntMan.EnsureComponent<ChemicalIrritantComponent>(serverTarget);
            irritant.IrritantAmount = 10f;
            irritant.DepletionPerTick = 0f;
            irritant.NextIrritantEffectAt = TimeSpan.MaxValue;
            irritant.Profile.DyloveneEfficiency = 3f;
            SEntMan.Dirty(serverTarget, irritant);
        });
        await RunUntilSynced();

        var clientTarget = ToClientUid(serverTarget);
        await Client.WaitAssertion(() =>
        {
            var irritant = CEntMan.GetComponent<ChemicalIrritantComponent>(clientTarget);
            Assert.That(irritant.IrritantAmount, Is.EqualTo(10f));
            Assert.That(ApplyDylovene(CEntMan, CProtoMan, clientTarget), Is.True);
            Assert.That(irritant.IrritantAmount, Is.EqualTo(10f),
                "Client evaluation must not resolve or mutate the server-only irritant system.");
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<ChemicalIrritantComponent>(serverTarget).IrritantAmount,
                Is.EqualTo(10f));
        });

        await Server.WaitAssertion(() =>
        {
            var effect = GetDyloveneEffect(SProtoMan);
            var applied = ApplyDylovene(SEntMan, SProtoMan, serverTarget);
            var irritant = SEntMan.GetComponent<ChemicalIrritantComponent>(serverTarget);
            Assert.Multiple(() =>
            {
                Assert.That(effect.Potency, Is.EqualTo(2f));
                Assert.That(effect.Amount, Is.EqualTo(1f));
                Assert.That(applied, Is.True);
                Assert.That(irritant.IrritantAmount, Is.EqualTo(8.5f));
            });
        });
        await RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.GetComponent<ChemicalIrritantComponent>(clientTarget).IrritantAmount,
                Is.EqualTo(8.5f));
        });

        await Server.WaitAssertion(() =>
        {
            var irritant = SEntMan.GetComponent<ChemicalIrritantComponent>(serverTarget);
            irritant.IrritantAmount = 1f;
            SEntMan.Dirty(serverTarget, irritant);
        });
        await RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.GetComponent<ChemicalIrritantComponent>(clientTarget).IrritantAmount,
                Is.EqualTo(1f));
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(ApplyDylovene(SEntMan, SProtoMan, serverTarget), Is.True);
        });
        await RunUntilSynced();

        await Task.WhenAll(
            Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<ChemicalIrritantComponent>(serverTarget), Is.False);
            }),
            Client.WaitAssertion(() =>
            {
                Assert.That(CEntMan.HasComponent<ChemicalIrritantComponent>(clientTarget), Is.False);
            }));
    }

    private static bool ApplyDylovene(
        IEntityManager entities,
        IPrototypeManager prototypes,
        EntityUid target)
    {
        var reagent = prototypes.Index<ReagentPrototype>(Dylovene);
        var quantity = (FixedPoint2)1;
        var context = new ReagentEffectContext(
            reagent,
            new Solution(Dylovene, quantity),
            null,
            null,
            new ReagentQuantity(Dylovene, quantity),
            "Bloodstream",
            null,
            ReagentEffectOrigin.Metabolism);
        return entities.System<SharedEntityEffectsSystem>()
            .TryApplyEffect(target, GetDyloveneEffect(prototypes), reagentContext: context);
    }

    private static ReduceChemicalIrritant GetDyloveneEffect(IPrototypeManager prototypes)
    {
        var reagent = prototypes.Index<ReagentPrototype>(Dylovene);
        return reagent.Metabolisms!.Metabolisms.Values
            .SelectMany(metabolism => metabolism.Effects)
            .OfType<ReduceChemicalIrritant>()
            .Single();
    }
}
