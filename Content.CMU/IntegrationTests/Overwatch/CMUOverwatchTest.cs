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
    public async Task FeedsOpenUpToFourAndCloseWithTheConsole()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        EntityUid? original = null;
        EntityUid actor = default, console = default;
        var marines = new List<EntityUid>();
        var helmets = new List<EntityUid>();
        var eyes = new List<EntityUid>();
        SharedUserInterfaceSystem ui = null!;

        void Toggle(EntityUid target)
        {
            SEntMan.EventBus.RaiseLocalEvent(console,
                (object) new CMUOverwatchFeedToggleBuiMsg(SEntMan.GetNetEntity(target))
                    { Actor = actor, UiKey = OverwatchConsoleUI.Key });
        }

        await Server.WaitAssertion(() =>
        {
            ui = SEntMan.System<SharedUserInterfaceSystem>();
            original = session.AttachedEntity;
            actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.System<SkillsSystem>().SetSkill(actor, "RMCSkillOverwatch", 1);
            Server.PlayerMan.SetAttachedEntity(session, actor);

            console = SEntMan.SpawnEntity("RMCOverwatchConsoleGovfor", map.GridCoords);
            SEntMan.RemoveComponent<AccessReaderComponent>(console);
            var squad = SEntMan.SpawnEntity("SquadGovforBravo", MapCoordinates.Nullspace);

            for (var i = 0; i < 5; i++)
            {
                var marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                SEntMan.System<SquadSystem>().AssignSquad(marine, squad, null);
                var helmet = SEntMan.SpawnEntity("ArmorHelmetM10", map.GridCoords);
                Assert.That(SEntMan.System<InventorySystem>().TryEquip(marine, helmet, "head", force: true), Is.True);
                marines.Add(marine);
                helmets.Add(helmet);
            }

            Assert.That(ui.TryOpenUi(console, OverwatchConsoleUI.Key, actor), Is.True);

            foreach (var marine in marines)
            {
                Toggle(marine);
            }

            var feeds = SComp<CMUOverwatchFeedsComponent>(actor);
            Assert.That(feeds.Feeds, Has.Count.EqualTo(CMUOverwatchFeedsComponent.MaxFeeds),
                "a fifth feed is refused while four are open");

            for (var i = 0; i < feeds.Feeds.Count; i++)
            {
                var eye = SEntMan.GetEntity(feeds.Feeds[i].Eye);
                eyes.Add(eye);
                Assert.That(SEntMan.GetComponent<TransformComponent>(eye).ParentUid, Is.EqualTo(helmets[i]),
                    "each feed rides on the marine's helmet camera");
            }

            Toggle(marines[0]);
            Assert.That(feeds.Feeds, Has.Count.EqualTo(CMUOverwatchFeedsComponent.MaxFeeds - 1),
                "toggling an open feed closes it");
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(eyes[0]), Is.True, "a closed feed deletes its eye");
            ui.CloseUi(console, OverwatchConsoleUI.Key, actor);
        });

        await Pair.RunTicksSync(60);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<CMUOverwatchFeedsComponent>(actor), Is.False,
                "closing the console closes every feed");
            foreach (var eye in eyes)
            {
                Assert.That(SEntMan.Deleted(eye), Is.True);
            }

            if (original != null)
                Server.PlayerMan.SetAttachedEntity(session, original);
        });
    }

    [Test]
    public async Task RosterCarriesCallsignAndHealth()
    {
        var map = await Pair.CreateTestMap();
        EntityUid marine = default;

        await Server.WaitAssertion(() =>
        {
            var squad = SEntMan.SpawnEntity("SquadGovforBravo", MapCoordinates.Nullspace);
            marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.System<SquadSystem>().AssignSquad(marine, squad, null);
            SEntMan.EnsureComponent<AU14CallsignComponent>(marine).Callsign = "DAGGER 1-1";
        });

        await Pair.RunTicksSync(60);

        await Server.WaitAssertion(() =>
        {
            var data = SComp<OverwatchDataComponent>(marine).Marine;
            Assert.That(data, Is.Not.Null);
            Assert.That(data!.Value.Callsign, Is.EqualTo("DAGGER 1-1"));
            Assert.That(data.Value.Health, Is.EqualTo(1f).Within(0.001f), "an unhurt marine reads full health");

            SEntMan.System<DamageableSystem>().TryChangeDamage(marine,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 50 } }, ignoreResistances: true);
        });

        await Pair.RunTicksSync(60);

        await Server.WaitAssertion(() =>
        {
            var data = SComp<OverwatchDataComponent>(marine).Marine!.Value;
            Assert.That(data.Health, Is.LessThan(1f), "damage shows on the roster's health");
        });
    }
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
}
