using Content.Server.Station.Systems;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14.Roles;

[TestFixture]
public sealed class FirstReconSkillTest
{
    private static readonly ProtoId<JobPrototype> FirstReconRifleman = "AU14FORECONFirstReconRifleman";

    [Test]
    public async Task FirstReconRiflemanSpawnsWithReconCombatSkills()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var stationSpawning = server.System<StationSpawningSystem>();
            var skills = server.System<SkillsSystem>();
            var rifleman = stationSpawning.SpawnPlayerMob(testMap.GridCoords, FirstReconRifleman, new HumanoidCharacterProfile(), station: null);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(skills.GetSkill(rifleman, "RMCSkillFirearms"), Is.EqualTo(2));
                    Assert.That(skills.GetSkill(rifleman, "RMCSkillCqc"), Is.EqualTo(2));
                    Assert.That(skills.GetSkill(rifleman, "RMCSkillEndurance"), Is.EqualTo(2));
                    Assert.That(skills.GetSkill(rifleman, "RMCSkillMedical"), Is.EqualTo(1));
                    Assert.That(skills.GetSkill(rifleman, "RMCSkillMeleeWeapons"), Is.EqualTo(1));
                });
            }
            finally
            {
                server.EntMan.DeleteEntity(rifleman);
            }
        });

        await pair.CleanReturnAsync();
    }
}
