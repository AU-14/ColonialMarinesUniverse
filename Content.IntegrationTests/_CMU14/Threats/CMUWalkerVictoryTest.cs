using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Threats.Rules;
using Content.Shared.CMU14.Xenomorphs.Pathogen.MycotoxinInject;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;

namespace Content.IntegrationTests.CMU14.Threats;

[TestFixture]
public sealed class CMUWalkerVictoryTest : GameTest
{
    private static readonly Robust.Shared.Prototypes.ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype> GOVFORPrototype = "GOVFOR";

    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task ReanimatedGovforRemainsEliminatedForVictory()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.SpawnEntity("CMUPathogenHive", map.GridCoords);
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var injector = SEntMan.SpawnEntity("CMU14XenoNeomorph", map.GridCoords);
            Server.System<NpcFactionSystem>().AddFaction(human, GOVFORPrototype);
            var state = SEntMan.GetComponent<MobStateComponent>(human);
            var rules = Server.System<ThreatRuleHelper>();
            Assert.That(rules.IsEliminated(human, state), Is.False);

            SEntMan.EventBus.RaiseLocalEvent(human, new CMUMycotoxinInjectDoReanimateEvent(human, injector), broadcast: true);

            Assert.That(rules.IsEliminated(human, state), Is.True,
                "reanimating a GOVFOR casualty must not restore a surviving human in victory checks");
            Assert.That(rules.IsExcludedFromVictory(human, state), Is.False,
                "converted casualties must stay in the denominator as eliminated members");
        });
    }
}
