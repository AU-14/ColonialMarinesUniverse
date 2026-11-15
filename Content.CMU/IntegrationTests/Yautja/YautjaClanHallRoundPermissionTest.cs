using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server.Administration.Managers;
using Content.Server.CMU14.Yautja;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Yautja;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanHallRoundPermissionTest
{
    // the hunt is clan staff business, a clans admin shouldn't need host to set it up
    [TestCase(AdminFlags.Admin | AdminFlags.Clans, true)]
    [TestCase(AdminFlags.Admin | AdminFlags.Host, false)]
    public async Task ClanStaffRunTheHuntSettings(AdminFlags flags, bool allowed)
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

        try
        {
            await server.WaitPost(() => server.CfgMan.SetCVar(CCVars.ConsoleLoginLocal, false));
            await db.AddAdminAsync(new Admin { UserId = session.UserId.UserId, Flags = Flags(flags) });
            await server.WaitPost(() => adminManager.ReloadAdmin(session));
            await pair.RunTicksSync(10);

            await server.WaitAssertion(() =>
            {
                Assert.That(adminManager.GetAdminData(session)?.Flags, Is.EqualTo(flags));
                var round = server.EntMan.System<YautjaPredatorRoundSystem>();
                var before = round.ConfiguredHunterSlots;
                var target = before == 7 ? 8 : 7;

                var hall = new YautjaClanHallEui(YautjaClanHallPage.Round);
                server.ResolveDependency<EuiManager>().OpenEui(hall, session);
                hall.HandleMessage(new YautjaClanHallRoundSlotsMessage(target));

                Assert.That(round.ConfiguredHunterSlots, Is.EqualTo(allowed ? target : before));
                hall.Close();
            });
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
}
