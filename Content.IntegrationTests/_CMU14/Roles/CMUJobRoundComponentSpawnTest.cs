using Content.IntegrationTests.Fixtures;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Roles;

[TestFixture]
[TestOf(typeof(StationSpawningSystem))]
public sealed class CMUJobRoundComponentSpawnTest : GameTest
{
    [Test]
    public async Task NormalJobSpawnAppliesRoundSkillComponents()
    {
        var map = await Pair.CreateTestMap();
        var mob = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            var spawning = Server.System<StationSpawningSystem>();
            var skills = Server.System<SkillsSystem>();
            mob = spawning.SpawnPlayerMob(
                map.GridCoords,
                "AU14JobCivilianPhysician",
                new HumanoidCharacterProfile(),
                station: null);

            Assert.That(SEntMan.HasComponent<SkillsComponent>(mob), Is.True,
                "CMU job roundComponents were not applied to the spawned character.");
            Assert.That(skills.GetSkill(mob, "RMCSkillMedical"), Is.EqualTo(3));
        });

        await Server.WaitPost(() => SDeleteNow(mob));
        await Pair.RunUntilSynced();
    }

    [Test]
    public async Task RmcJobSpawnerAppliesRoundSkillComponents()
    {
        var mob = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            mob = SSpawn("CMUTestRmcJobSpawner");
            AssertMedicalSkill(mob, 3);
        });

        await Server.WaitPost(() => SDeleteNow(mob));
        await Pair.RunUntilSynced();
    }

    [Test]
    public async Task GhostRoleSpecialAppliesRoundSkillComponents()
    {
        var mob = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            mob = SSpawn("CMUTestGhostRoleJob");
            AssertMedicalSkill(mob, 3);
        });

        await Server.WaitPost(() => SDeleteNow(mob));
        await Pair.RunUntilSynced();
    }

    private void AssertMedicalSkill(EntityUid mob, int expected)
    {
        var skills = Server.System<SkillsSystem>();
        Assert.That(SEntMan.HasComponent<SkillsComponent>(mob), Is.True,
            "CMU job roundComponents were not applied to the spawned character.");
        Assert.That(skills.GetSkill(mob, "RMCSkillMedical"), Is.EqualTo(expected));
    }

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: CMMobHuman
  id: CMUTestRmcJobSpawner
  components:
  - type: RMCJobSpawner
    job: AU14JobCivilianPhysician
    loadout: false

- type: entity
  parent: CMMobHuman
  id: CMUTestGhostRoleJob
  components:
  - type: GhostRole
    job: AU14JobCivilianPhysician
  - type: GhostRoleApplySpecial
";
}
