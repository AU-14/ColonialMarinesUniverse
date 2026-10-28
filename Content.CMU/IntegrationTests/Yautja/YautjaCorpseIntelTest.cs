using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Intel;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
[TestOf(typeof(IntelSystem))]
public sealed class YautjaCorpseIntelTest : GameTest
{
    [TestCase("CMUMobYautja")]
    [TestCase("CMUMobYautjaYoungblood")]
    public async Task DeadYautjaIsWorthAsMuchIntelAsAQueen(string mob)
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var queen = SProtoMan.Index<EntityPrototype>("CMXenoQueen");
            Assert.That(queen.TryComp<IntelRecoverCorpseObjectiveOnDeathComponent>(out var queenIntel, SEntMan.ComponentFactory), Is.True);

            var pred = SEntMan.SpawnEntity(mob, map.GridCoords);
            Assert.That(SEntMan.HasComponent<IntelRecoverCorpseObjectiveComponent>(pred), Is.False);

            SEntMan.System<MobStateSystem>().ChangeMobState(pred, MobState.Dead);

            Assert.That(SEntMan.TryGetComponent(pred, out IntelRecoverCorpseObjectiveComponent? corpse), Is.True);
            Assert.That(corpse!.Value, Is.EqualTo(queenIntel!.Value));

            SEntMan.DeleteEntity(pred);
        });
    }
}
