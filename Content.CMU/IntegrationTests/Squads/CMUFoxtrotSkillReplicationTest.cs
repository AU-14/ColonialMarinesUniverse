using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Squads.Foxtrot;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Squads;

[TestFixture]
public sealed class CMUFoxtrotSkillReplicationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: CMMobHuman
  id: CMUTestFoxtrotRifleman
  components:
  - type: Skills
    preset: AU14SkillPresetPlatoonRifleman
""";

    [Test]
    public async Task InitialClientStatePreservesFoxtrotBonusesOverTheJobPreset()
    {
        var map = await Pair.CreateTestMap();
        NetEntity member = default;
        await Server.WaitAssertion(() =>
        {
            var mob = SSpawnAtPosition("CMUTestFoxtrotRifleman", map.GridCoords);
            var squad = SSpawnAtPosition("SquadGovforFoxtrot", map.GridCoords);
            SEntMan.System<CMUFoxtrotSquadSystem>().ApplyPerks(mob, squad,
                SProtoMan.Index<JobPrototype>("AU14JobGOVFORSquadRifleman"));
            member = SEntMan.GetNetEntity(mob);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, mob);
        });

        await Pair.RunTicksSync(10);
        await Client.WaitAssertion(() =>
        {
            var mob = CEntMan.GetEntity(member);
            var skills = CEntMan.System<SkillsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(skills.GetSkill(mob, "RMCSkillEngineer"), Is.EqualTo(3),
                    "client startup must preserve the squad bonus instead of reloading the base rifleman preset");
                Assert.That(skills.GetSkill(mob, "RMCSkillConstruction"), Is.EqualTo(3));
            });
        });
    }
}
