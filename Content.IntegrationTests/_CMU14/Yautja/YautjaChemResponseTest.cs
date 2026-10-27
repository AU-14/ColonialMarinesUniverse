using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared._RMC14.Body;
using Content.Shared.CMU14.Medical.Anatomy.Metabolism.Events;
using Content.Shared.Metabolism;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaChemResponseTest
{
    [TestCase("CMUParacetamol", 1.5f)]
    [TestCase("CMUMethamphetamine", 1.5f)]
    [TestCase("RMCEthanol", 2f)]
    [TestCase("CMUYautjaAnalgesic", 1f)]
    public async Task RegularYautjaUsesConfiguredClearance(string reagent, float expected)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var proto = server.ProtoMan.Index<ReagentPrototype>(reagent);
            var metabolism = expected > 1.5f || reagent == "CMUMethamphetamine"
                ? proto.Metabolisms!.Metabolisms.First(entry => entry.Value.CMUToxicity.Count > 0)
                : proto.Metabolisms!.Metabolisms.First();
            var ev = new MetabolismRateModifyEvent(
                hunter,
                metabolism.Key,
                new ProtoId<ReagentPrototype>(reagent),
                metabolism.Value.CMUToxicity,
                1f);

            entMan.EventBus.RaiseLocalEvent(hunter, ref ev);
            Assert.That(ev.Multiplier, Is.EqualTo(expected).Within(0.001f));
            entMan.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodKeepsDefaultClearance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautjaBadBloodGrunt", MapCoordinates.Nullspace);
            var ev = new MetabolismRateModifyEvent(
                hunter,
                "Bloodstream",
                "CMUParacetamol",
                new HashSet<CMUMetabolismClass>(),
                1f);

            entMan.EventBus.RaiseLocalEvent(hunter, ref ev);
            Assert.That(ev.Multiplier, Is.EqualTo(1f));
            entMan.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }
    [Test]
    public async Task ThweiKillsYautjaPain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            Assert.That(entMan.System<SharedPainShockSystem>().GetTierSuppression(hunter), Is.Zero);
            Assert.That(entMan.System<SharedRMCBloodstreamSystem>()
                .TryGetChemicalSolution(hunter, out var solution, out _), Is.True);
            Assert.That(entMan.System<SharedSolutionContainerSystem>().TryAddReagent(solution, "thwei", 10), Is.True);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(3));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.System<SharedPainShockSystem>().GetTierSuppression(hunter), Is.GreaterThan(0),
                "Thwei in a yautja's blood should be suppressing pain.");
            entMan.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }
}
