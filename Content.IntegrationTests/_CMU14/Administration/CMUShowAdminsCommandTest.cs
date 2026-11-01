#nullable enable
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Administration.Managers;
using Content.Server.CMU14.Administration;
using Content.Shared.Administration;
using Robust.Server.Console;
using Robust.Shared.Player;

namespace Content.IntegrationTests._CMU14.Administration;

[TestFixture]
[TestOf(typeof(CMUShowAdminsCommand))]
public sealed class CMUShowAdminsCommandTest : GameTest
{
    [Test]
    public async Task FormatHidesStealthAndTagsAdmins()
    {
        var loc = Server.ResolveDependency<ILocalizationManager>();

        await Server.WaitAssertion(() =>
        {
            var output = CMUShowAdminsCommand.Format(loc,
            [
                new CMUShowAdminsEntry("Zulu", "Game Master", Deadminned: false, Afk: false, Ic: true, Stealth: false),
                new CMUShowAdminsEntry("Hidden", "Host", Deadminned: false, Afk: false, Ic: false, Stealth: true),
                new CMUShowAdminsEntry("Alpha", null, Deadminned: true, Afk: true, Ic: false, Stealth: false),
            ]);

            var deadminned = loc.GetString("cmu-showadmins-deadminned");
            var ic = loc.GetString("cmu-showadmins-ic");
            var afk = loc.GetString("cmu-showadmins-afk");
            Assert.That(output.Split('\n'), Is.EqualTo(new[]
            {
                $"Alpha {deadminned} {afk}",
                $"Zulu: [Game Master] {ic}",
            }));
            Assert.That(output, Does.Not.Contain("Hidden"));

            var none = loc.GetString("cmu-showadmins-none");
            Assert.That(CMUShowAdminsCommand.Format(loc, []), Is.EqualTo(none));
            Assert.That(CMUShowAdminsCommand.Format(loc,
                [new CMUShowAdminsEntry("Hidden", null, false, false, Ic: false, Stealth: true)]), Is.EqualTo(none));

            Assert.That(loc.HasString("cmd-showadmins-desc"));
            Assert.That(loc.HasString("cmd-showadmins-help"));
        });
    }

    [Test]
    public async Task LiveEntriesTrackSessionState()
    {
        var session = ServerSession!;
        var command = (CMUShowAdminsCommand) Server.ConsoleHost.AvailableCommands["showadmins"];
        var manager = Server.ResolveDependency<IAdminManager>();
        var playerMan = Server.ResolveDependency<ISharedPlayerManager>();

        var map = await Pair.CreateTestMap();
        EntityUid human = default;
        EntityUid observer = default;
        await Server.WaitPost(() =>
        {
            human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            observer = SEntMan.SpawnEntity("RMCAdminObserver", map.GridCoords);
        });

        await Server.WaitAssertion(() =>
        {
            CMUShowAdminsEntry Entry()
            {
                var entry = command.Collect().Single();
                Assert.That(entry.Name, Is.EqualTo(session.Name));
                return entry;
            }

            Assert.That(session.AttachedEntity, Is.Null, "The test must start unattached for the states to be distinct.");

            var entry = Entry();
            Assert.That(entry.Deadminned, Is.False);
            Assert.That(entry.Ic, Is.False, "A lobby admin is not playing.");

            playerMan.SetAttachedEntity(session, human);
            entry = Entry();
            Assert.That(entry.Deadminned, Is.False);
            Assert.That(entry.Ic, Is.True, "An admin attached to a living character is IC.");

            playerMan.SetAttachedEntity(session, observer);
            entry = Entry();
            Assert.That(entry.Ic, Is.False, "A ghosted admin is observing, not IC.");

            playerMan.SetAttachedEntity(session, human);
            manager.DeAdmin(session);
            entry = Entry();
            Assert.That(entry.Deadminned, Is.True);
            Assert.That(entry.Ic, Is.False, "Deadminned staff are never IC, even while playing a character.");

            manager.ReAdmin(session);
            entry = Entry();
            Assert.That(entry.Deadminned, Is.False);
            Assert.That(entry.Ic, Is.True, "Readminned and back in their character: IC again.");

            playerMan.SetAttachedEntity(session, null);
            entry = Entry();
            Assert.That(entry.Deadminned, Is.False);
            Assert.That(entry.Ic, Is.False, "Detached: powers on, nobody playing.");
        });
    }

    [Test]
    public async Task CommandIsPublicAndRunsFromServerConsole()
    {
        var console = Server.ResolveDependency<IServerConsoleHost>();

        Assert.That(typeof(CMUShowAdminsCommand).GetCustomAttribute<AnyCommandAttribute>(), Is.Not.Null,
            "showadmins must be usable by every player.");

        await Server.WaitAssertion(() =>
        {
            Assert.That(console.AvailableCommands.TryGetValue("showadmins", out var command));
            Assert.That(command, Is.TypeOf<CMUShowAdminsCommand>());
            Assert.DoesNotThrow(() => console.ExecuteCommand("showadmins"));
        });
    }
}
