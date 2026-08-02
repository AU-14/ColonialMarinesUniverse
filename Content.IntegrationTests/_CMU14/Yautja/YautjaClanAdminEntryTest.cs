using System.Collections.Generic;
using System.Linq;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.Administration.UI.Tabs.AdminTab;
using Content.IntegrationTests.Pair;
using Content.Server.Administration;
using Content.Server.Administration.Commands;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Server._CMU14.Yautja;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Shared.Localization;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanAdminEntryTest
{
    [Test]
    public void EveryYautjaConsoleCommandRequiresClanPermission()
    {
        Type[] commandTypes =
        [
            typeof(YautjaClanAdminCommand),
            typeof(YautjaClanInfoCommand),
            typeof(YautjaPredatorAdminEditorCommand),
            typeof(YautjaYoungbloodCallCommand),
            typeof(YautjaClanSetMemberCommand),
            typeof(YautjaClanCreateCommand),
            typeof(YautjaClanWhitelistCommand),
            typeof(YautjaRankCommand),
            typeof(YautjaGetRankCommand),
        ];

        foreach (var commandType in commandTypes)
        {
            var attributes = commandType
                .GetCustomAttributes(typeof(AdminCommandAttribute), false)
                .Cast<AdminCommandAttribute>()
                .ToArray();

            Assert.That(attributes, Has.Exactly(1).Items, commandType.Name);
            Assert.That(attributes[0].Flags, Is.EqualTo(AdminFlags.Clans), commandType.Name);
        }

        Assert.That(YautjaClanAdminEui.RequiredAdminFlag, Is.EqualTo(AdminFlags.Clans));
    }

    [Test]
    public async Task AdminTabProvidesLocalizedClanAdministrationCommand()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Client.WaitAssertion(() =>
        {
            var localization = pair.Client.ResolveDependency<ILocalizationManager>();
            var tab = new AdminTab();
            try
            {
                var button = Descendants(tab)
                    .OfType<CommandButton>()
                    .SingleOrDefault(entry => entry.Command == "yautja_clan_admin");

                Assert.That(button, Is.Not.Null);
                Assert.That(button!.Text, Is.EqualTo(localization.GetString("cmu-yautja-clan-admin-open")));
            }
            finally
            {
                tab.DisposeAllChildren();
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClanPermissionControlsCommandEuiAndMutations()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var session = pair.Player!;
        var db = server.ResolveDependency<IServerDbManager>();
        var adminManager = (AdminManager) server.ResolveDependency<IAdminManager>();
        var euiManager = server.ResolveDependency<EuiManager>();
        var adminRecord = new Admin
        {
            UserId = session.UserId.UserId,
            Flags = Flags(AdminFlags.Admin),
        };

        try
        {
            await server.WaitPost(() => server.CfgMan.SetCVar(CCVars.ConsoleLoginLocal, false));
            await db.AddAdminAsync(adminRecord);
            await ReloadAdmin(pair, adminManager, session, AdminFlags.Admin);

            Assert.That(adminManager.CanCommand(session, "yautja_clan_admin"), Is.False);

            YautjaClanAdminEui? denied = null;
            await server.WaitPost(() =>
            {
                denied = new YautjaClanAdminEui();
                euiManager.OpenEui(denied, session);
            });
            await server.WaitAssertion(() => Assert.That(denied!.IsShutDown, Is.True));

            const string deniedName = "Denied Clan Permission Test";
            await server.WaitPost(() =>
                denied!.HandleMessage(new YautjaClanAdminCreateClanMessage(deniedName, "denied", "#112233")));
            await pair.RunTicksSync(10);
            Assert.That((await db.GetYautjaClansAsync()).Any(clan => clan.Name == deniedName), Is.False);

            adminRecord.Flags = Flags(AdminFlags.Admin | AdminFlags.Clans);
            await db.UpdateAdminAsync(adminRecord);
            await ReloadAdmin(pair, adminManager, session, AdminFlags.Admin | AdminFlags.Clans);

            Assert.That(adminManager.CanCommand(session, "yautja_clan_admin"), Is.True);

            YautjaClanAdminEui? allowed = null;
            await server.WaitPost(() =>
            {
                allowed = new YautjaClanAdminEui();
                euiManager.OpenEui(allowed, session);
            });
            await server.WaitAssertion(() => Assert.That(allowed!.IsShutDown, Is.False));
            await pair.RunTicksSync(20);

            const string allowedName = "Allowed Clan Permission Test";
            await server.WaitPost(() =>
                allowed!.HandleMessage(new YautjaClanAdminCreateClanMessage(allowedName, "allowed", "#445566")));
            await pair.RunTicksSync(20);
            Assert.That((await db.GetYautjaClansAsync()).Any(clan => clan.Name == allowedName), Is.True);

            adminRecord.Flags = Flags(AdminFlags.Admin);
            await db.UpdateAdminAsync(adminRecord);
            await ReloadAdmin(pair, adminManager, session, AdminFlags.Admin);
            await server.WaitAssertion(() => Assert.That(allowed!.IsShutDown, Is.True));

            const string revokedName = "Revoked Clan Permission Test";
            await server.WaitPost(() =>
                allowed!.HandleMessage(new YautjaClanAdminCreateClanMessage(revokedName, "revoked", "#778899")));
            await pair.RunTicksSync(10);
            Assert.That((await db.GetYautjaClansAsync()).Any(clan => clan.Name == revokedName), Is.False);
        }
        finally
        {
            if (await db.GetAdminDataForAsync(session.UserId) != null)
                await db.RemoveAdminAsync(session.UserId);

            await server.WaitPost(() =>
                server.CfgMan.SetCVar(CCVars.ConsoleLoginLocal, CCVars.ConsoleLoginLocal.DefaultValue));
        }

        await pair.CleanReturnAsync();
    }

    private static List<AdminFlag> Flags(AdminFlags flags)
    {
        return AdminFlagsHelper.FlagsToNames(flags)
            .Select(flag => new AdminFlag { Flag = flag })
            .ToList();
    }

    private static async Task ReloadAdmin(
        TestPair pair,
        AdminManager adminManager,
        Robust.Shared.Player.ICommonSession session,
        AdminFlags expected)
    {
        await pair.Server.WaitPost(() => adminManager.ReloadAdmin(session));
        await pair.RunTicksSync(10);
        await pair.Server.WaitAssertion(() =>
            Assert.That(adminManager.GetAdminData(session)?.Flags, Is.EqualTo(expected)));
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
