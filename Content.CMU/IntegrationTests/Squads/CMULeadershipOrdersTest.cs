using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Marines.Orders;
using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.CMU14.Squads;

[TestFixture]
public sealed class CMULeadershipOrdersTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestLeadershipZero
          components:
          - type: Skills

        - type: entity
          id: CMUTestLeadershipOne
          components:
          - type: Skills
            skills:
              RMCSkillLeadership: 1

        - type: entity
          id: CMUTestLeadershipTwo
          components:
          - type: Skills
            skills:
              RMCSkillLeadership: 2

        - type: entity
          id: CMUTestLeadershipPreset
          components:
          - type: SkillPreset
            skills:
              RMCSkillLeadership: 1

        - type: entity
          id: CMUTestLeadershipFromPreset
          components:
          - type: Skills
            preset: CMUTestLeadershipPreset
        """;

    [TestCase("CMUTestLeadershipZero", false)]
    [TestCase("CMUTestLeadershipOne", true)]
    [TestCase("CMUTestLeadershipTwo", true)]
    [TestCase("CMUTestLeadershipFromPreset", true)]
    public async Task LeadershipGrantsOrdersAtSpawn(string prototype, bool expected)
    {
        await Server.WaitAssertion(() =>
        {
            var ent = SEntMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
            Assert.That(SEntMan.HasComponent<MarineOrdersComponent>(ent), Is.EqualTo(expected));
            if (expected)
                AssertActions(ent, true);
            SEntMan.DeleteEntity(ent);
        });
    }

    [Test]
    public async Task LeadershipGainedAfterSpawnGrantsOrdersAndLossRemovesActions()
    {
        await Server.WaitAssertion(() =>
        {
            var ent = SEntMan.SpawnEntity("CMUTestLeadershipZero", MapCoordinates.Nullspace);
            var skills = Server.System<SkillsSystem>();
            skills.SetSkill(ent, "RMCSkillMedical", 1);
            Assert.That(SEntMan.HasComponent<MarineOrdersComponent>(ent), Is.False);

            skills.SetSkill(ent, "RMCSkillLeadership", 1);
            var original = SEntMan.GetComponent<MarineOrdersComponent>(ent);
            AssertActions(ent, true);

            skills.SetSkill(ent, "RMCSkillLeadership", 2);
            Assert.That(SEntMan.GetComponent<MarineOrdersComponent>(ent), Is.SameAs(original));
            AssertActions(ent, true);

            skills.SetSkill(ent, "RMCSkillLeadership", 0);
            AssertActions(ent, false);

            skills.SetSkill(ent, "RMCSkillLeadership", 1);
            AssertActions(ent, true);
            SEntMan.DeleteEntity(ent);
        });
    }

    private void AssertActions(EntityUid ent, bool expected)
    {
        var orders = SEntMan.GetComponent<MarineOrdersComponent>(ent);
        Assert.Multiple(() =>
        {
            Assert.That(orders.MoveActionEntity != null, Is.EqualTo(expected));
            Assert.That(orders.HoldActionEntity != null, Is.EqualTo(expected));
            Assert.That(orders.FocusActionEntity != null, Is.EqualTo(expected));
        });
    }
}
