using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// FLOW-12: THE LIGHT judges Yautja too. A hunter facing the Joe is a gazer and its envelope fills and lands,
/// while the kill-credit rule (IsOrganic) still does not count it as a person.
/// </summary>
[TestFixture]
public sealed class EWJoeLightYautjaTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Yautja = "CMUMobYautja";

    [Test]
    public async Task TheLightEnvelopsAYautjaWhoFacesTheJoe()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, hunter = default;
        await server.WaitAssertion(() =>
        {
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            var light = entities.EnsureComponent<EWJoeLightComponent>(joe);

            hunter = entities.SpawnEntity(Yautja, EWJoeTestHelpers.TileEast(entities, map, 2));
            EWJoeTestHelpers.Face(entities, hunter, joe);

            var levelFive = entities.System<EWJoeLevelFiveSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(EWJoeTargetRules.IsLightTarget(entities, hunter), "a Yautja is judged by THE LIGHT");
                Assert.That(EWJoeTargetRules.IsOrganic(entities, hunter), Is.False, "but still earns no organic kill credit");
                Assert.That(levelFive.IsGazing((joe, light), (hunter, entities.GetComponent<MobStateComponent>(hunter))),
                    "a Yautja facing the Joe gazes into THE LIGHT");
            });
        });

        // RB-22: the envelope fills over 5 s
        await pair.RunTicksSync((int) Math.Ceiling(5.75 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.TryGetComponent(hunter, out EWJoeEnvelopedComponent? envelope), "the Yautja was caught");
            Assert.Multiple(() =>
            {
                Assert.That(envelope!.ImmuneUntil, Is.GreaterThan(timing.CurTime), "the envelope filled and landed");
                Assert.That(envelope.Progress, Is.EqualTo(0f), "and reset");
            });
        });

        await pair.CleanReturnAsync();
    }
}
