using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Requisitions.Mail;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;

namespace Content.IntegrationTests.CMU14.Requisitions;

[TestFixture]
[TestOf(typeof(CMUColonyMailSystem))]
public sealed class CMUColonyMailRetryTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: department
          id: CMUMailTestDepartment
          name: cm-job-name-hospital-corpsman
          description: cm-job-name-hospital-corpsman
          color: "#FFFFFF"
          faction: colonist
          # Test prototypes load globally; this auxiliary membership must not replace CMSquad.
          primary: false
          roles: [CMHospitalCorpsman]
        """;

    [Test]
    public async Task FailedStartupMailBacksOffThenSendsOnceAndResetsNextRound()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var ticker = Server.System<GameTicker>();
            var mail = Server.System<CMUColonyMailSystem>();
            // Move only the round clock/run level; starting a complete map is unrelated to mail scheduling.
            typeof(GameTicker).GetField("_runLevel", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(ticker, GameRunLevel.InRound);
            void UpdateAt(int seconds)
            {
                typeof(SharedGameTicker).GetProperty(nameof(SharedGameTicker.RoundStartTimeSpan))!
                    .SetValue(ticker, SGameTiming.CurTime - TimeSpan.FromSeconds(seconds));
                mail.Update(0);
            }

            var lift = SEntMan.SpawnEntity(null, map.GridCoords);
            var elevator = SEntMan.AddComponent<RequisitionsElevatorComponent>(lift);
            elevator.Faction = "colony";
            elevator.Mode = RequisitionsElevatorMode.Raised;
            UpdateAt(30); // No eligible player yet.

            var colonist = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<MobStateComponent>(colonist);
            var minds = Server.System<SharedMindSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, colonist);
            Server.System<SharedRoleSystem>().MindAddJobRole(mind, jobPrototype: "CMHospitalCorpsman");
            Server.PlayerMan.SetAttachedEntity(ServerSession!, colonist);

            UpdateAt(31);
            Assert.That(elevator.Orders, Is.Empty, "a newly available recipient must not bypass the retry delay");
            elevator.Mode = RequisitionsElevatorMode.Lowering;
            UpdateAt(60); // Due, but the lift is busy.
            elevator.Mode = RequisitionsElevatorMode.Raised;
            UpdateAt(61);
            Assert.That(elevator.Orders, Is.Empty, "a busy lift must also back off");
            UpdateAt(90);
            Assert.That(elevator.Orders.Select(order => order.Crate.Id),
                Is.EqualTo(new[] { "CMUColonyMailCrateRoundStart" }));
            UpdateAt(120);
            Assert.That(elevator.Orders, Has.Count.EqualTo(1), "startup mail is sent only once per round");

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            elevator.Orders.Clear();
            UpdateAt(29);
            Assert.That(elevator.Orders, Is.Empty);
            UpdateAt(30);
            Assert.That(elevator.Orders.Select(order => order.Crate.Id),
                Is.EqualTo(new[] { "CMUColonyMailCrateRoundStart" }), "new rounds reset both the sent flag and deadline");
        });
    }
}
