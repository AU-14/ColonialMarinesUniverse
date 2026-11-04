using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.Xenomorphs.Pathogen.MycotoxinInject;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class WalkerFirearmsRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private const string Firearms = "RMCSkillFirearms";

    [Test]
    public async Task SkillessPlanetsideBodyCanShootOnceItTurns()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var skills = SEntMan.System<SkillsSystem>();
            SEntMan.SpawnEntity("CMUPathogenHive", map.GridCoords);
            var injector = SEntMan.SpawnEntity("CMU14XenoNeomorph", map.GridCoords);

            // planetside corpses have no job, so no skills at all
            var corpse = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.RemoveComponent<SkillsComponent>(corpse);
            Assert.That(skills.GetSkill(corpse, Firearms), Is.EqualTo(0));

            // a trained marine keeps what they had
            var marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            skills.SetSkill(marine, Firearms, 3);

            SEntMan.EventBus.RaiseLocalEvent(corpse, new CMUMycotoxinInjectDoReanimateEvent(corpse, injector), broadcast: true);
            SEntMan.EventBus.RaiseLocalEvent(marine, new CMUMycotoxinInjectDoReanimateEvent(marine, injector), broadcast: true);

            Assert.That(skills.GetSkill(corpse, Firearms), Is.EqualTo(1),
                "walkers need firearms 1 or every gun gets the unskilled spread");
            Assert.That(skills.GetSkill(marine, Firearms), Is.EqualTo(3),
                "turning must never lower a skill the body already had");
        });
    }
}
