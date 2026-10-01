using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chat.Managers;
using Content.Server.CMU14.Yautja;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.CMU14.Yautja;
using Moq;
using Robust.Shared.Localization;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YoungbloodRaffleEligibilityTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, InLobby = true, Dirty = true };

    [TestCase("AU14JobGOVFORSquadRifleman", "youngblood_three_inexperienced")]
    [TestCase("AU14JobOPFORSquadRifleman", "youngblood_solo")]
    public async Task CurrentSquadPlaytimeAllowsJoiningTheYoungbloodRaffle(string tracker, string call)
    {
        var map = await Pair.CreateTestMap();
        EntityUid role = default;
        var chat = new Mock<IChatManager>();
        await Server.WaitAssertion(() =>
        {
            var consoleUid = SEntMan.SpawnEntity("CMUHunterShipBloodingConsole", map.GridCoords);
            var console = SEntMan.GetComponent<YautjaHuntConsoleComponent>(consoleUid);
            console.DestinationId = "jungle_moon";
            SEntMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", map.GridCoords);
            SEntMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords);
            var option = console.BloodingCallOptions.Single(o => o.Id == call);
            Assert.That(Server.System<YautjaHuntConsoleSystem>().TryCreateYoungbloodCall(
                (consoleUid, console), consoleUid, option, bypassEligibility: false), Is.True);
            var query = SEntMan.EntityQueryEnumerator<YautjaYoungbloodGhostRoleComponent, GhostRoleComponent>();
            Assert.That(query.MoveNext(out role, out _, out _), Is.True);
            var playtime = Server.ResolveDependency<PlayTimeTrackingManager>();
            playtime.AddTimeToTracker(ServerSession!, tracker, TimeSpan.FromHours(5) - TimeSpan.FromMinutes(1));
            playtime.AddTimeToTracker(ServerSession!, "CMJobXenoDrone", TimeSpan.FromHours(5));
        });
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            var youngblood = Server.System<YautjaYoungbloodSystem>();
            var chatField = typeof(YautjaYoungbloodSystem).GetField("_chat", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var originalChat = chatField.GetValue(youngblood);
            chatField.SetValue(youngblood, chat.Object);
            try
            {
                var roles = Server.System<GhostRoleSystem>();
                var component = SEntMan.GetComponent<GhostRoleComponent>(role);
                var id = component.Identifier;
                var passiveCheck = new GhostRoleRequestAttemptEvent(ServerSession!, role, component);
                SEntMan.EventBus.RaiseLocalEvent(role, ref passiveCheck);
                Assert.That(passiveCheck.Cancelled, Is.True);
                Assert.That(chat.Invocations, Is.Empty, "Passive eligibility checks must not spam the player's chat.");
                roles.Request(ServerSession!, id);
                Assert.That(SEntMan.HasComponent<GhostRoleRaffleComponent>(role), Is.False);
                Assert.That(chat.Invocations.Where(i => i.Method.Name == nameof(IChatManager.ChatMessageToOne))
                        .Select(i => i.Arguments[1]),
                    Does.Contain(Loc.GetString("cmu-yautja-youngblood-raffle-experience", ("hours", 5))));
                Server.ResolveDependency<PlayTimeTrackingManager>()
                    .AddTimeToTracker(ServerSession!, tracker, TimeSpan.FromMinutes(1));
                roles.Request(ServerSession!, id);
                Assert.That(SEntMan.TryGetComponent<GhostRoleRaffleComponent>(role, out var raffle), Is.True,
                    "Confirming the role with five hours of CMU squad and xeno time must start its raffle.");
                Assert.That(raffle!.CurrentMembers, Does.Contain(ServerSession));
            }
            finally
            {
                chatField.SetValue(youngblood, originalChat);
            }
        });
    }
}
