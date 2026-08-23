using System.Collections.Generic;
using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaSkillsTest
{
    private static readonly IReadOnlyDictionary<string, int> WarriorSkills = new Dictionary<string, int>
    {
        ["RMCSkillAntagonist"] = 2,
        ["RMCSkillConstruction"] = 2,
        ["RMCSkillCqc"] = 5,
        ["RMCSkillEndurance"] = 3,
        ["RMCSkillEngineer"] = 2,
        ["RMCSkillFirearms"] = 1,
        ["RMCSkillFireman"] = 5,
        ["RMCSkillMedical"] = 2,
        ["RMCSkillMeleeWeapons"] = 2,
        ["RMCSkillPolice"] = 2,
        ["RMCSkillSurgery"] = 3,
    };

    [Test]
    public async Task YautjaUseTheCmss13WarriorSkillProfile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var skills = server.System<SkillsSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var youngblood = entMan.SpawnEntity("CMUMobYautjaYoungblood", MapCoordinates.Nullspace);

            try
            {
                AssertWarriorSkills(skills, hunter);
                AssertWarriorSkills(skills, youngblood);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(youngblood))
                    entMan.DeleteEntity(youngblood);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertWarriorSkills(SkillsSystem skills, EntityUid yautja)
    {
        foreach (var (skill, level) in WarriorSkills)
        {
            Assert.That(skills.GetSkill(yautja, skill), Is.EqualTo(level), skill);
        }
    }
}
