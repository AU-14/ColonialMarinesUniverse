using Content.Client.CMU14.UserInterface;
using System.Collections.Generic;
using System.Linq;
using Content.Client.Administration.Managers;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.Administration.UI.Tabs.AdminTab;
using Content.IntegrationTests.Pair;
using Content.Server.Administration;
using Content.Server.Administration.Commands;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Server.CMU14.Yautja;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanAdminEntryTest
{

    [Test]
    public async Task ClanPermissionControlsClanHallAndMutations()
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

            YautjaClanHallEui? denied = null;
            await server.WaitPost(() =>
            {
                denied = new YautjaClanHallEui();
                euiManager.OpenEui(denied, session);
            });
            await pair.RunTicksSync(20);
            await server.WaitAssertion(() => Assert.That(denied!.IsShutDown, Is.True));

            const string deniedName = "Denied Clan Permission Test";
            await server.WaitPost(() =>
                denied!.HandleMessage(new YautjaClanHallFoundClanMessage(deniedName, "#112233")));
            await pair.RunTicksSync(10);
            Assert.That((await db.GetYautjaClansAsync()).Any(clan => clan.Name == deniedName), Is.False);

            adminRecord.Flags = Flags(AdminFlags.Admin | AdminFlags.Clans);
            await db.UpdateAdminAsync(adminRecord);
            await ReloadAdmin(pair, adminManager, session, AdminFlags.Admin | AdminFlags.Clans);

            Assert.That(adminManager.CanCommand(session, "yautja_clan_admin"), Is.True);

            YautjaClanHallEui? allowed = null;
            await server.WaitPost(() =>
            {
                allowed = new YautjaClanHallEui();
                euiManager.OpenEui(allowed, session);
            });
            await server.WaitAssertion(() => Assert.That(allowed!.IsShutDown, Is.False));
            await pair.RunTicksSync(20);

            const string allowedName = "Allowed Clan Permission Test";
            await server.WaitPost(() =>
                allowed!.HandleMessage(new YautjaClanHallFoundClanMessage(allowedName, "#445566")));
            await pair.RunTicksSync(20);
            Assert.That((await db.GetYautjaClansAsync()).Any(clan => clan.Name == allowedName), Is.True);

            adminRecord.Flags = Flags(AdminFlags.Admin);
            await db.UpdateAdminAsync(adminRecord);
            await ReloadAdmin(pair, adminManager, session, AdminFlags.Admin);
            await pair.RunTicksSync(20);
            await server.WaitAssertion(() => Assert.That(allowed!.IsShutDown, Is.True));

            const string revokedName = "Revoked Clan Permission Test";
            await server.WaitPost(() =>
                allowed!.HandleMessage(new YautjaClanHallFoundClanMessage(revokedName, "#778899")));
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

}
