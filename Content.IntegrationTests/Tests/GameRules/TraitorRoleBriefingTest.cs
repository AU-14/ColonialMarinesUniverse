using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
[TestOf(typeof(TraitorRuleSystem))]
public sealed class TraitorRoleBriefingTest
{
    [Test]
    public async Task RepeatedAssignmentReusesBriefingComponent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mindSystem = server.System<MindSystem>();
        var roleSystem = server.System<RoleSystem>();
        var traitorSystem = server.System<TraitorRuleSystem>();

        await server.WaitAssertion(() =>
        {
            var traitor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<MindContainerComponent>(traitor);
            var mind = mindSystem.CreateMind(null);
            mindSystem.TransferTo(mind, traitor, mind: mind.Comp);
            roleSystem.MindAddRole(mind, "MindRoleTraitor", mind.Comp);

            Assert.That(
                roleSystem.MindHasRole<TraitorRoleComponent>(mind, out var traitorRole),
                Is.True);
            var traitorRoleEntity = traitorRole!.Value.Owner;

            var rule = entMan.SpawnEntity("TraitorReinforcement", MapCoordinates.Nullspace);
            var ruleComponent = entMan.GetComponent<TraitorRuleComponent>(rule);

            Assert.That(traitorSystem.MakeTraitor(traitor, ruleComponent), Is.True);
            var firstBriefing = entMan.GetComponent<RoleBriefingComponent>(traitorRoleEntity);

            Assert.That(traitorSystem.MakeTraitor(traitor, ruleComponent), Is.True);
            var secondBriefing = entMan.GetComponent<RoleBriefingComponent>(traitorRoleEntity);

            Assert.That(secondBriefing, Is.SameAs(firstBriefing));
        });

        await pair.CleanReturnAsync();
    }
}
