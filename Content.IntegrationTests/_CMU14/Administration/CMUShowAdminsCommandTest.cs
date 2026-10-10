#nullable enable
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Administration;
using Content.Shared.Administration;
using Robust.Server.Console;

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
                new CMUShowAdminsEntry("Zulu", "Game Master", Deadminned: false, Afk: false, Stealth: false),
                new CMUShowAdminsEntry("Hidden", "Host", Deadminned: false, Afk: false, Stealth: true),
                new CMUShowAdminsEntry("Alpha", null, Deadminned: true, Afk: true, Stealth: false),
            ]);

            var deadminned = loc.GetString("cmu-showadmins-deadminned");
            var afk = loc.GetString("cmu-showadmins-afk");
            Assert.That(output.Split('\n'), Is.EqualTo(new[]
            {
                $"Alpha {deadminned} {afk}",
                "Zulu: [Game Master]",
            }));
            Assert.That(output, Does.Not.Contain("Hidden"));

            var none = loc.GetString("cmu-showadmins-none");
            Assert.That(CMUShowAdminsCommand.Format(loc, []), Is.EqualTo(none));
            Assert.That(CMUShowAdminsCommand.Format(loc,
                [new CMUShowAdminsEntry("Hidden", null, false, false, Stealth: true)]), Is.EqualTo(none));

            Assert.That(loc.HasString("cmd-showadmins-desc"));
            Assert.That(loc.HasString("cmd-showadmins-help"));
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
