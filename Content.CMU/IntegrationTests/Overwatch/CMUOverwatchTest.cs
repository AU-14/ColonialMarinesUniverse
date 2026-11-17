#pragma warning disable RA0002 // The test sets a callsign directly instead of running spawn assignment.

using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Overwatch;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.Callsigns;
using Content.Shared.CMU14.Overwatch;
using Content.Shared.CMU14.Squads;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Overwatch;

[TestFixture]
[TestOf(typeof(CMUOverwatchSystem))]
public sealed class CMUOverwatchTest : GameTest
{

    [Test]
    public async Task FireteamsAreManagedFromTheConsole()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var console = SEntMan.SpawnEntity("RMCOverwatchConsoleGovfor", map.GridCoords);
            var squad = SEntMan.SpawnEntity("SquadGovforBravo", MapCoordinates.Nullspace);
            var other = SEntMan.SpawnEntity("SquadGovforCharlie", MapCoordinates.Nullspace);
            var squads = SEntMan.System<SquadSystem>();

            var alpha = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bravo = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var outsider = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            squads.AssignSquad(alpha, squad, null);
            squads.AssignSquad(bravo, squad, null);
            squads.AssignSquad(outsider, other, null);

            SComp<OverwatchConsoleComponent>(console).Squad = SEntMan.GetNetEntity(squad);

            void Send(BoundUserInterfaceMessage message)
            {
                message.Actor = actor;
                message.UiKey = OverwatchConsoleUI.Key;
                SEntMan.EventBus.RaiseLocalEvent(console, (object) message);
            }

            int? Fireteam(EntityUid marine) =>
                SEntMan.TryGetComponent(marine, out FireteamMemberComponent? member) ? member.Fireteam : null;

            Send(new CMUOverwatchAssignFireteamBuiMsg(SEntMan.GetNetEntity(alpha), 2));
            Send(new CMUOverwatchAssignFireteamBuiMsg(SEntMan.GetNetEntity(bravo), 2));
            Send(new CMUOverwatchFireteamLeaderBuiMsg(SEntMan.GetNetEntity(alpha), true));
            Send(new OverwatchConsoleSetFireteamNicknameBuiMsg(SEntMan.GetNetEntity(squad), 2, "Hammer"));

            Assert.Multiple(() =>
            {
                Assert.That(Fireteam(alpha), Is.EqualTo(2));
                Assert.That(Fireteam(bravo), Is.EqualTo(2));
                Assert.That(SEntMan.HasComponent<FireteamLeaderComponent>(alpha), Is.True, "overwatch can pick a fireteam leader");
            });

            Send(new CMUOverwatchFireteamLeaderBuiMsg(SEntMan.GetNetEntity(bravo), true));
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<FireteamLeaderComponent>(bravo), Is.True);
                Assert.That(SEntMan.HasComponent<FireteamLeaderComponent>(alpha), Is.False, "a fireteam has one leader");
            });

            Send(new CMUOverwatchAssignFireteamBuiMsg(SEntMan.GetNetEntity(bravo), 0));
            Assert.Multiple(() =>
            {
                Assert.That(Fireteam(bravo), Is.EqualTo(0));
                Assert.That(SEntMan.HasComponent<FireteamLeaderComponent>(bravo), Is.False, "a leader who changes fireteam stops leading");
                Assert.That(SComp<SquadTeamComponent>(squad).Fireteams.Fireteams[2]?.Nickname, Is.EqualTo("Hammer"),
                    "moving marines keeps the fireteam nicknames");
            });

            Send(new CMUOverwatchAssignFireteamBuiMsg(SEntMan.GetNetEntity(alpha), null));
            Send(new CMUOverwatchAssignFireteamBuiMsg(SEntMan.GetNetEntity(outsider), 1));
            Assert.Multiple(() =>
            {
                Assert.That(Fireteam(alpha), Is.Null, "a marine can be taken out of their fireteam");
                Assert.That(Fireteam(outsider), Is.Null, "the console only manages the squad it is showing");
            });
        });
    }
    [Test]
    public async Task AutomaticLeadersCanBeTurnedOffPerSquad()
    {
        var map = await Pair.CreateTestMap();
        EntityUid console = default, squad = default, other = default, actor = default;
        var marines = new List<EntityUid>();

        await Server.WaitAssertion(() =>
        {
            actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            console = SEntMan.SpawnEntity("RMCOverwatchConsoleGovfor", map.GridCoords);
            squad = SEntMan.SpawnEntity("SquadGovforBravo", MapCoordinates.Nullspace);
            other = SEntMan.SpawnEntity("SquadGovforCharlie", MapCoordinates.Nullspace);
            SComp<OverwatchConsoleComponent>(console).Squad = SEntMan.GetNetEntity(squad);

            SEntMan.EventBus.RaiseLocalEvent(console,
                (object) new CMUOverwatchAutoLeadersBuiMsg(false) { Actor = actor, UiKey = OverwatchConsoleUI.Key });

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<CMUManualLeadershipComponent>(squad), Is.True);
                Assert.That(SEntMan.HasComponent<CMUManualLeadershipComponent>(other), Is.False,
                    "only the squad the console is showing changes");
            });

            for (var i = 0; i < 3; i++)
            {
                var marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                SEntMan.System<SquadSystem>().AssignSquad(marine, squad, null);
                marines.Add(marine);
            }
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            foreach (var marine in marines)
            {
                Assert.That(SEntMan.HasComponent<FireteamMemberComponent>(marine), Is.True,
                    "marines are still sorted into fireteams");
                Assert.That(SEntMan.HasComponent<FireteamLeaderComponent>(marine), Is.False,
                    "nobody is made a fireteam leader automatically");
            }

            SEntMan.EventBus.RaiseLocalEvent(console,
                (object) new CMUOverwatchAutoLeadersBuiMsg(true) { Actor = actor, UiKey = OverwatchConsoleUI.Key });
            Assert.That(SEntMan.HasComponent<CMUManualLeadershipComponent>(squad), Is.False);
        });
    }

}
