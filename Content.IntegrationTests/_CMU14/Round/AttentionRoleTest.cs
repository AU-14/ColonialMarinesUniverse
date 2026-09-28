using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Roles;
using Content.Shared.CMU14.Marines.Orders;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.Roles;

namespace Content.IntegrationTests.CMU14.Round;

[TestFixture]
public sealed class AttentionRoleTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task OnlyMilitaryLeadershipExceptCommanderAndExecutiveCanCallAttention()
    {
        var leadership = new HashSet<string>
        {
            "Advisor", "EngineeringOfficer", "IntelOfficer", "LogisticsOfficer", "CMO", "ChiefMP",
            "JuniorOfficer", "VehicleCommander", "SectionSergeant", "SquadSergeant", "RadioTelephoneOperator",
            "AdjutantDress", "BrigadierGeneral",
        };
        await Server.WaitAssertion(() =>
        {
            foreach (var job in SProtoMan.EnumeratePrototypes<JobPrototype>())
            {
                if (job.Abstract || job.RoundRole == null || job.RoundSide is not (RoundJobSide.Govfor or RoundJobSide.Opfor))
                    continue;

                var present = job.RoundComponents.TryGetValue("AU14CallToAttentionAbility", out var entry);
                var seniorOfficer = job.RoundRole is "PlatoonCommander" or "ExecutiveOfficer";
                Assert.That(present, Is.EqualTo(leadership.Contains(job.RoundRole) || seniorOfficer), job.ID);
                if (present)
                {
                    var ability = (AU14CallToAttentionAbilityComponent) entry!.Component;
                    Assert.That(ability.CanCall, Is.EqualTo(!seniorOfficer), job.ID);
                    if (seniorOfficer)
                        Assert.That(ability.AttentionFocusPriority, Is.GreaterThan(0), job.ID);
                }
            }
        });
    }

    [TestCase("AU14JobGOVFORPlatCo")]
    [TestCase("AU14JobGOVFORAdjutant")]
    [TestCase("AU14JobOPFORPlatCo")]
    [TestCase("AU14JobOPFORAdjutant")]
    public async Task SeniorOfficersReceiveNoActionAndCannotInvokeItDirectly(string jobId)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var officer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            try
            {
                SEntMan.AddComponents(officer, SProtoMan.Index<JobPrototype>(jobId).RoundComponents);
                var ability = SEntMan.GetComponent<AU14CallToAttentionAbilityComponent>(officer);
                Assert.That(ability.ActionEntity, Is.Null);
                var action = new AU14CallToAttentionActionEvent();
                SEntMan.EventBus.RaiseLocalEvent(officer, action);
                Assert.That(action.Handled, Is.False);
            }
            finally
            {
                SEntMan.DeleteEntity(officer);
            }
        });
    }

    [Test]
    public async Task LeadershipCallsStillFocusOnTheImmuneCommander()
    {
        var map = await Pair.CreateTestMap();
        EntityUid leader = default, commander = default, marine = default;
        await Server.WaitAssertion(() =>
        {
            leader = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            commander = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 2)));
            marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            SEntMan.AddComponents(leader, SProtoMan.Index<JobPrototype>("AU14JobGOVFORPlatOp").RoundComponents);
            SEntMan.AddComponents(commander, SProtoMan.Index<JobPrototype>("AU14JobGOVFORPlatCo").RoundComponents);
            SEntMan.EnsureComponent<OriginalRoleComponent>(marine).Job = "AU14JobGOVFORSquadRifleman";
            SEntMan.GetComponent<AU14CallToAttentionAbilityComponent>(leader).ResponseStagger = TimeSpan.Zero;
            var action = new AU14CallToAttentionActionEvent();
            SEntMan.EventBus.RaiseLocalEvent(leader, action);
            Assert.That(action.Handled, Is.True);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            var towardCommander = transform.GetMapCoordinates(commander).Position - transform.GetMapCoordinates(marine).Position;
            var expected = Angle.FromWorldVec(towardCommander);
            Assert.That(Math.Abs(Angle.ShortestDistance(transform.GetWorldRotation(marine), expected).Degrees),
                Is.LessThan(0.01), "Personnel must face the commander even though the commander is immune and cannot call the order.");
            foreach (var uid in new[] { leader, commander, marine })
                SEntMan.DeleteEntity(uid);
        });
    }
}
