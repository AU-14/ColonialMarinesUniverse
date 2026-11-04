using System.Linq;
using Content.Client.Administration.Managers;
using Content.Client.CMU14.Administration;
using Content.Client.CMU14.Administration.Tickets;
using Content.Client.UserInterface.Systems.Bwoink;
using Content.Server.Administration.Managers;
using Content.Server.CMU14.Administration.Tickets;
using Content.Shared.Administration;
using Content.Shared.CMU14.Administration.Tickets;
using Robust.Client.UserInterface;
using Robust.Shared.Network;
using Robust.Shared.Utility;
using ClientAdminSystem = Content.Client.Administration.Systems.AdminSystem;
using ClientBwoinkSystem = Content.Client.Administration.Systems.BwoinkSystem;
using ServerAdminSystem = Content.Server.Administration.Systems.AdminSystem;

namespace Content.IntegrationTests._CMU14.Administration;

/// <summary>
/// In-game admin mode makes the admin inactive, so every admin tool and admin-only check is off, while the AHelp
/// keeps working for them on the server and on the client. Reloads keep the mode; readmin, deadmin or the command end it.
/// </summary>
[TestFixture]
public sealed class CMUIngameAdminTest
{
    [Test]
    public async Task IngameAdminKeepsOnlyAhelp()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var manager = (AdminManager) server.ResolveDependency<IAdminManager>();
        var clientAdmin = client.ResolveDependency<IClientAdminManager>();
        var session = pair.Player!;
        var player = new NetUserId(Guid.NewGuid());
        var fullFlags = AdminFlags.None;

        try
        {
            await server.WaitAssertion(() =>
            {
                var data = manager.GetAdminData(session)!;
                fullFlags = data.Flags;
                Assert.That(data.HasFlag(AdminFlags.Adminhelp), "The test admin must start with Adminhelp.");
                Assert.That(data.HasFlag(AdminFlags.Admin), "The test admin must hold more than Adminhelp, or nothing is proven.");

                server.ConsoleHost.ExecuteCommand(session, "ingameadmin");

                // Every "is an active admin" check is off: that is what removes the admin tools.
                Assert.That(manager.CMUIsIngameAdmin(session));
                Assert.That(manager.GetAdminData(session), Is.Null);
                Assert.That(manager.IsAdmin(session), Is.False);
                Assert.That(manager.ActiveAdmins, Does.Not.Contain(session));
                Assert.That(manager.GetAdminData(session, includeDeAdmin: true)!.Flags, Is.EqualTo(fullFlags),
                    "The rank's flags are kept for when the mode ends.");

                // Admin commands are refused; the toggle itself and deadmin, the other way out, still run.
                Assert.That(manager.CanCommand(session, "kick"), Is.False);
                Assert.That(manager.CanCommand(session, "readmin"));
                Assert.That(manager.CanCommand(session, "deadmin"));
                Assert.That(manager.CanCommand(session, "ingameadmin"));

                // The AHelp alone is given back.
                Assert.That(manager.CMUHasAhelp(session));
                Assert.That(manager.CMUAhelpAdmins, Does.Contain(session));

                server.System<CMUTicketSystem>().Reset();
                server.System<CMUTicketSystem>().RecordMessage(player, player, "Player", "help", false, true, DateTime.Now);
            });
            await pair.RunTicksSync(10);

            // The client sees an inactive admin, but keeps the admin AHelp window and its player list.
            await client.WaitAssertion(() =>
            {
                Assert.That(clientAdmin.IsActive(), Is.False);
                Assert.That(clientAdmin.HasFlag(AdminFlags.Admin), Is.False);
                Assert.That(clientAdmin.CanCommand("kick"), Is.False);
                Assert.That(clientAdmin.CanCommand("deadmin"), "The client refuses commands the server did not list.");
                Assert.That(client.System<CMUIngameAdminClientSystem>().IngameAdmin);
                Assert.That(client.ResolveDependency<IUserInterfaceManager>().GetUIController<AHelpUIController>().CMUHasAhelp());
                Assert.That(client.System<ClientAdminSystem>().PlayerList, Is.Not.Empty,
                    "The AHelp window lists channels from the player list, so in-game admins must get one.");

                // Ticket actions and replies into another player's channel still go through the server.
                client.System<CMUTicketClientSystem>().Claim(player);
                client.System<ClientBwoinkSystem>().Send(player, "on it", true, false);
            });
            await pair.RunTicksSync(10);
            await server.WaitAssertion(() =>
            {
                var tickets = server.System<CMUTicketSystem>();
                var ticket = tickets.GetTicket(player)!;
                Assert.That(ticket.Status, Is.EqualTo(CMUTicketStatus.Claimed));
                Assert.That(ticket.ClaimedBy, Is.EqualTo(session.UserId));
                Assert.That(ticket.AnsweredBy, Is.EqualTo(session.UserId), "The in-game admin's reply must be accepted.");
                Assert.That(tickets.GetHistory(player).Any(l => l.TrueSender == session.UserId && l.Text.Contains("on it")));
            });

            // A rank reload replaces the admin data; the mode must survive it.
            await server.WaitPost(() => manager.ReloadAdmin(session));
            await pair.RunTicksSync(10);
            await server.WaitAssertion(() =>
            {
                Assert.That(manager.CMUIsIngameAdmin(session));
                Assert.That(manager.GetAdminData(session), Is.Null);
                Assert.That(server.System<CMUTicketSystem>().GetTicket(player)!.ClaimedBy, Is.EqualTo(session.UserId),
                    "Entering the mode or reloading must not release the in-game admin's claims.");
            });

            // Running the command again restores the full admin.
            await server.WaitAssertion(() =>
            {
                server.ConsoleHost.ExecuteCommand(session, "ingameadmin");
                Assert.That(manager.CMUIsIngameAdmin(session), Is.False);
                Assert.That(manager.GetAdminData(session)!.Flags, Is.EqualTo(fullFlags));
            });
            await pair.RunTicksSync(10);
            await client.WaitAssertion(() =>
            {
                Assert.That(clientAdmin.HasFlag(AdminFlags.Admin));
                Assert.That(client.System<CMUIngameAdminClientSystem>().IngameAdmin, Is.False);
            });

            // Readmin also ends the mode.
            await server.WaitAssertion(() =>
            {
                Assert.That(manager.CMUSetIngameAdmin(session, true));
                manager.ReAdmin(session);
                Assert.That(manager.CMUIsIngameAdmin(session), Is.False);
                Assert.That(manager.GetAdminData(session)!.HasFlag(AdminFlags.Admin));
            });

            // Deadmin, typed into the client console, drops straight from the mode to a deadminned player.
            await server.WaitPost(() => Assert.That(manager.CMUSetIngameAdmin(session, true)));
            await pair.RunTicksSync(10);
            await client.WaitPost(() => client.ConsoleHost.ExecuteCommand("deadmin"));
            await pair.RunTicksSync(10);
            await server.WaitAssertion(() =>
            {
                Assert.That(manager.CMUIsIngameAdmin(session), Is.False);
                Assert.That(manager.GetAdminData(session), Is.Null);
                Assert.That(manager.CMUHasAhelp(session), Is.False, "A deadminned admin no longer handles AHelps.");
                Assert.That(manager.CanCommand(session, "deadmin"), Is.False);
                Assert.That(manager.GetAdminData(session, includeDeAdmin: true)!.Flags, Is.EqualTo(fullFlags));
            });
            await client.WaitAssertion(() =>
            {
                Assert.That(client.System<CMUIngameAdminClientSystem>().IngameAdmin, Is.False);
                Assert.That(clientAdmin.CanCommand("deadmin"), Is.False);
            });

            // Readmin restores the full admin after that.
            await server.WaitAssertion(() =>
            {
                manager.ReAdmin(session);
                Assert.That(manager.GetAdminData(session)!.Flags, Is.EqualTo(fullFlags));
            });
        }
        finally
        {
            // The mode is kept per user id; never leave it set on a pooled pair.
            await server.WaitPost(() =>
            {
                if (manager.CMUIsIngameAdmin(session))
                    manager.CMUSetIngameAdmin(session, false);
                if (manager.GetAdminData(session) == null && manager.GetAdminData(session, includeDeAdmin: true) != null)
                    manager.ReAdmin(session);
                server.System<CMUTicketSystem>().Reset();
            });
            await pair.RunTicksSync(5);
        }

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The player list in-game admins get carries nothing that marks an antagonist.
    /// </summary>
    [Test]
    public void IngamePlayerListHasNoAntagDetails()
    {
        var antag = new PlayerInfo("user", "Character", "Identity", "Colonist", true, "SoloAntagonist",
            "cmu-role-subtype-cannibal", 5, null, new NetUserId(Guid.NewGuid()), true, true, null,
            new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "hudmutineer"));

        var stripped = ServerAdminSystem.CMUStrip(antag);

        Assert.That(stripped.Antag, Is.False);
        Assert.That(stripped.RoleProto, Is.Null);
        Assert.That(stripped.Subtype, Is.Null);
        Assert.That(stripped.AntagIcon, Is.Null);
        Assert.That(stripped.SortWeight, Is.Zero);
        // What the AHelp window needs to name the channel is kept.
        Assert.That(stripped.Username, Is.EqualTo("user"));
        Assert.That(stripped.CharacterName, Is.EqualTo("Character"));
        Assert.That(stripped.SessionId, Is.EqualTo(antag.SessionId));
    }
}
