using Content.Client.CMU14.Administration.Tickets;
using Content.Server.Administration.Managers;
using Content.Server.CMU14.Administration.Tickets;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Administration.Tickets;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.IntegrationTests._CMU14.Administration;

[TestFixture]
public sealed class CMUTicketSystemTest
{
    [Test]
    public async Task TicketLifecycle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var tickets = server.System<CMUTicketSystem>();
            tickets.Reset();

            var player = new NetUserId(Guid.NewGuid());
            var otherPlayer = new NetUserId(Guid.NewGuid());
            var adminA = new NetUserId(Guid.NewGuid());
            var adminB = new NetUserId(Guid.NewGuid());
            var now = DateTime.Now;

            // The player's first message opens a ticket; later ones append to it.
            var opened = tickets.RecordMessage(player, player, "Player", "help", false, true, now);
            Assert.That(opened, Is.Not.Null);
            Assert.That(opened!.Status, Is.EqualTo(CMUTicketStatus.Open));
            Assert.That(opened.Id, Is.EqualTo(1));
            Assert.That(tickets.RecordMessage(player, player, "Player", "still here", false, true, now), Is.Null);
            Assert.That(tickets.GetTicket(player)!.Id, Is.EqualTo(1));

            // An admin writing first, or a player's admin-only line, opens nothing.
            Assert.That(tickets.RecordMessage(otherPlayer, adminA, "AdminA", "hello", false, true, now), Is.Null);
            Assert.That(tickets.RecordMessage(otherPlayer, otherPlayer, "Other", "x", true, false, now), Is.Null);
            Assert.That(tickets.GetTicket(otherPlayer), Is.Null);
            Assert.That(tickets.TryClaim(otherPlayer, adminA, "AdminA"), Is.EqualTo(CMUTicketActionResult.NoTicket));

            // Claims are exclusive, and a second admin's claim takes it over.
            Assert.That(tickets.TryClaim(player, adminA, "AdminA"), Is.EqualTo(CMUTicketActionResult.Success));
            Assert.That(tickets.GetTicket(player)!.Status, Is.EqualTo(CMUTicketStatus.Claimed));
            Assert.That(tickets.GetTicket(player)!.ClaimedBy, Is.EqualTo(adminA));
            Assert.That(tickets.TryClaim(player, adminA, "AdminA"), Is.EqualTo(CMUTicketActionResult.AlreadyClaimed));
            Assert.That(tickets.TryClaim(player, adminB, "AdminB"), Is.EqualTo(CMUTicketActionResult.Success));
            Assert.That(tickets.GetTicket(player)!.ClaimedBy, Is.EqualTo(adminB));
            Assert.That(tickets.GetTicket(player)!.ClaimedByName, Is.EqualTo("AdminB"));

            // Any admin may release a claim.
            Assert.That(tickets.TryUnclaim(player, adminA, "AdminA"), Is.EqualTo(CMUTicketActionResult.Success));
            Assert.That(tickets.GetTicket(player)!.Status, Is.EqualTo(CMUTicketStatus.Open));
            Assert.That(tickets.GetTicket(player)!.ClaimedBy, Is.Null);
            Assert.That(tickets.TryUnclaim(player, adminA, "AdminA"), Is.EqualTo(CMUTicketActionResult.NotClaimed));

            // A claimer leaving puts the ticket back in the queue.
            tickets.TryClaim(player, adminA, "AdminA");
            tickets.ReleaseClaimsBy(adminA, "AdminA");
            Assert.That(tickets.GetTicket(player)!.Status, Is.EqualTo(CMUTicketStatus.Open));

            // Closing needs a reason.
            Assert.That(tickets.TryClose(player, adminA, "AdminA", "   "), Is.EqualTo(CMUTicketActionResult.EmptyReason));
            Assert.That(tickets.GetTicket(player)!.Status, Is.EqualTo(CMUTicketStatus.Open));
            Assert.That(tickets.TryClose(player, adminA, "AdminA", "  sorted out  "), Is.EqualTo(CMUTicketActionResult.Success));
            var closed = tickets.GetTicket(player)!;
            Assert.That(closed.Status, Is.EqualTo(CMUTicketStatus.Closed));
            Assert.That(closed.CloseReason, Is.EqualTo("sorted out"));
            Assert.That(closed.ClosedByName, Is.EqualTo("AdminA"));
            Assert.That(tickets.TryClaim(player, adminB, "AdminB"), Is.EqualTo(CMUTicketActionResult.AlreadyClosed));
            Assert.That(tickets.TryClose(player, adminB, "AdminB", "again"), Is.EqualTo(CMUTicketActionResult.AlreadyClosed));

            // Writing after the close opens a new ticket with the next id.
            var reopened = tickets.RecordMessage(player, player, "Player", "one more thing", false, true, now);
            Assert.That(reopened, Is.Not.Null);
            Assert.That(reopened!.Id, Is.EqualTo(2));
            Assert.That(reopened.Status, Is.EqualTo(CMUTicketStatus.Open));

            // The channel history spans both tickets and keeps the player's lines.
            var history = tickets.GetHistory(player);
            Assert.That(history.Any(e => e.Text == "help" && e.TrueSender == player));
            Assert.That(history.Any(e => e.Text == "one more thing"));
            Assert.That(tickets.GetHistory(otherPlayer).Count, Is.EqualTo(2));

            // Overlong close reasons are cut to the limit.
            var longReason = new string('a', CMUTicketLimits.MaxCloseReasonLength + 50);
            Assert.That(tickets.TryClose(player, adminA, "AdminA", longReason), Is.EqualTo(CMUTicketActionResult.Success));
            Assert.That(tickets.GetTicket(player)!.CloseReason!.Length, Is.EqualTo(CMUTicketLimits.MaxCloseReasonLength));

            tickets.Reset();
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HistoryIsBounded()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var tickets = server.System<CMUTicketSystem>();
            tickets.Reset();

            var player = new NetUserId(Guid.NewGuid());
            for (var i = 0; i < CMUTicketLimits.MaxHistoryPerChannel + 20; i++)
            {
                tickets.RecordMessage(player, player, "Player", $"line {i}", false, true, DateTime.Now);
            }

            var history = tickets.GetHistory(player);
            Assert.That(history.Count, Is.EqualTo(CMUTicketLimits.MaxHistoryPerChannel));
            Assert.That(history[^1].Text, Is.EqualTo($"line {CMUTicketLimits.MaxHistoryPerChannel + 19}"));

            tickets.Reset();
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RoundRestartClearsTickets()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;
        var player = new NetUserId(Guid.NewGuid());

        await server.WaitAssertion(() =>
        {
            var tickets = server.System<CMUTicketSystem>();
            tickets.RecordMessage(player, player, "Player", "help", false, true, DateTime.Now);
            Assert.That(tickets.GetTicket(player), Is.Not.Null);

            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            Assert.That(tickets.GetTicket(player), Is.Null);
            Assert.That(tickets.GetHistory(player), Is.Empty);

            var next = tickets.RecordMessage(player, player, "Player", "help again", false, true, DateTime.Now);
            Assert.That(next!.Id, Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The server must ignore ticket requests from sessions without Adminhelp. The same request is then
    /// accepted after readmin, which proves the permission gate (not something else) blocked it.
    /// </summary>
    [Test]
    public async Task RequestsRequireAdminhelp()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var player = new NetUserId(Guid.NewGuid());
        var adminManager = server.ResolveDependency<IAdminManager>();
        var session = pair.Player!;

        await server.WaitPost(() =>
        {
            var tickets = server.System<CMUTicketSystem>();
            tickets.Reset();
            tickets.RecordMessage(player, player, "Player", "help", false, true, DateTime.Now);
        });
        // Let the admin-only "ticket opened" line reach the client before it stops being an admin.
        await pair.RunTicksSync(5);

        await server.WaitPost(() => adminManager.DeAdmin(session));
        await pair.RunTicksSync(5);

        await client.WaitPost(() =>
        {
            var clientTickets = client.System<CMUTicketClientSystem>();
            clientTickets.Acknowledge(player);
            clientTickets.Claim(player);
            clientTickets.Close(player, "not allowed");
        });
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var ticket = server.System<CMUTicketSystem>().GetTicket(player)!;
            Assert.That(ticket.Status, Is.EqualTo(CMUTicketStatus.Open));
            Assert.That(ticket.ClaimedBy, Is.Null);
            Assert.That(ticket.AcknowledgedBy, Is.Null);
            adminManager.ReAdmin(session);
        });
        await pair.RunTicksSync(5);

        await client.WaitPost(() =>
        {
            var clientTickets = client.System<CMUTicketClientSystem>();
            clientTickets.Acknowledge(player);
            clientTickets.Claim(player);
        });
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var tickets = server.System<CMUTicketSystem>();
            Assert.That(tickets.GetTicket(player)!.AcknowledgedBy, Is.EqualTo(session.UserId));
            Assert.That(tickets.GetTicket(player)!.ClaimedBy, Is.EqualTo(session.UserId));
            tickets.Reset();
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClaimNoticeShowsClaimerByDefault()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var adminManager = server.ResolveDependency<IAdminManager>();
        var session = pair.Player!;

        await server.WaitAssertion(() =>
        {
            var tickets = server.System<CMUTicketSystem>();
            var cfg = server.CfgMan;
            var original = cfg.GetCVar(CCVars.CMUAhelpTicketShowClaimer);
            var originalOverride = cfg.GetCVar(CCVars.AdminAhelpOverrideClientName);
            Assert.That(CCVars.CMUAhelpTicketShowClaimer.DefaultValue, Is.True);
            cfg.SetCVar(CCVars.AdminAhelpOverrideClientName, string.Empty);

            var offline = new NetUserId(Guid.NewGuid());
            cfg.SetCVar(CCVars.CMUAhelpTicketShowClaimer, true);
            Assert.That(tickets.BuildPlayerClaimText(offline, "VisibleAdminName"), Does.Contain("VisibleAdminName"));
            Assert.That(tickets.BuildPlayerClaimText(session.UserId, "VisibleAdminName"), Does.Contain("VisibleAdminName"));

            // A stealthed claimer stays anonymous even with the CVar on.
            adminManager.Stealth(session);
            Assert.That(tickets.BuildPlayerClaimText(session.UserId, "StealthAdminName"), Does.Not.Contain("StealthAdminName"));
            adminManager.UnStealth(session);
            Assert.That(tickets.BuildPlayerClaimText(session.UserId, "StealthAdminName"), Does.Contain("StealthAdminName"));

            cfg.SetCVar(CCVars.CMUAhelpTicketShowClaimer, false);
            Assert.That(tickets.BuildPlayerClaimText(offline, "SecretAdminName"), Does.Not.Contain("SecretAdminName"));

            cfg.SetCVar(CCVars.CMUAhelpTicketShowClaimer, original);
            cfg.SetCVar(CCVars.AdminAhelpOverrideClientName, originalOverride);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AcknowledgeRules()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var tickets = server.System<CMUTicketSystem>();
            tickets.Reset();

            var player = new NetUserId(Guid.NewGuid());
            var adminA = new NetUserId(Guid.NewGuid());
            var adminB = new NetUserId(Guid.NewGuid());
            var now = DateTime.Now;

            Assert.That(tickets.TryAcknowledge(player, adminA, "AdminA"), Is.EqualTo(CMUTicketActionResult.NoTicket));

            // Acknowledging records who did it, claims the ticket for them, and happens once.
            tickets.RecordMessage(player, player, "Player", "help", false, true, now);
            Assert.That(tickets.TryAcknowledge(player, adminA, "AdminA"), Is.EqualTo(CMUTicketActionResult.Success));
            var acked = tickets.GetTicket(player)!;
            Assert.That(acked.Status, Is.EqualTo(CMUTicketStatus.Claimed));
            Assert.That(acked.ClaimedBy, Is.EqualTo(adminA));
            Assert.That(acked.ClaimedByName, Is.EqualTo("AdminA"));
            Assert.That(acked.AcknowledgedBy, Is.EqualTo(adminA));
            Assert.That(acked.AcknowledgedByName, Is.EqualTo("AdminA"));
            Assert.That(tickets.TryAcknowledge(player, adminB, "AdminB"), Is.EqualTo(CMUTicketActionResult.AlreadyClaimed));
            Assert.That(tickets.GetTicket(player)!.AcknowledgedBy, Is.EqualTo(adminA));

            // The canned reply is stored in the channel history as a line from the admin.
            Assert.That(tickets.GetHistory(player).Any(e => e.TrueSender == adminA && !e.AdminOnly));

            // Another admin can take over an acknowledged ticket; the acknowledgement is kept.
            Assert.That(tickets.TryClaim(player, adminB, "AdminB"), Is.EqualTo(CMUTicketActionResult.Success));
            Assert.That(tickets.GetTicket(player)!.ClaimedBy, Is.EqualTo(adminB));
            Assert.That(tickets.GetTicket(player)!.AcknowledgedBy, Is.EqualTo(adminA));

            // Releasing the claim reopens the ticket, but it cannot be acknowledged a second time.
            Assert.That(tickets.TryUnclaim(player, adminB, "AdminB"), Is.EqualTo(CMUTicketActionResult.Success));
            Assert.That(tickets.GetTicket(player)!.Status, Is.EqualTo(CMUTicketStatus.Open));
            Assert.That(tickets.TryAcknowledge(player, adminB, "AdminB"), Is.EqualTo(CMUTicketActionResult.AlreadyAcknowledged));

            // A claimed ticket cannot be acknowledged.
            var other = new NetUserId(Guid.NewGuid());
            tickets.RecordMessage(other, other, "Other", "help", false, true, now);
            tickets.TryClaim(other, adminA, "AdminA");
            Assert.That(tickets.TryAcknowledge(other, adminB, "AdminB"), Is.EqualTo(CMUTicketActionResult.AlreadyClaimed));
            Assert.That(tickets.GetTicket(other)!.AcknowledgedBy, Is.Null);

            // Nor a closed one, and the next ticket in the channel starts unacknowledged.
            tickets.TryClose(player, adminB, "AdminB", "done");
            Assert.That(tickets.TryAcknowledge(player, adminA, "AdminA"), Is.EqualTo(CMUTicketActionResult.AlreadyClosed));
            var reopened = tickets.RecordMessage(player, player, "Player", "again", false, true, now);
            Assert.That(reopened!.AcknowledgedBy, Is.Null);
            Assert.That(reopened.AnsweredBy, Is.Null);
            Assert.That(tickets.TryAcknowledge(player, adminB, "AdminB"), Is.EqualTo(CMUTicketActionResult.Success));

            // An admin-only note does not answer a ticket; a plain admin reply does, and blocks acknowledging.
            var answered = new NetUserId(Guid.NewGuid());
            tickets.RecordMessage(answered, answered, "Answered", "help", false, true, now);
            tickets.RecordMessage(answered, adminA, "AdminA", "note", true, false, now);
            Assert.That(tickets.GetTicket(answered)!.AnsweredBy, Is.Null);
            tickets.RecordMessage(answered, adminA, "AdminA", "on it", false, true, now);
            Assert.That(tickets.GetTicket(answered)!.AnsweredBy, Is.EqualTo(adminA));
            Assert.That(tickets.GetTicket(answered)!.AnsweredByName, Is.EqualTo("AdminA"));
            Assert.That(tickets.TryAcknowledge(answered, adminB, "AdminB"), Is.EqualTo(CMUTicketActionResult.AlreadyAnswered));
            Assert.That(tickets.GetTicket(answered)!.AcknowledgedBy, Is.Null);

            tickets.Reset();
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnapshotIsBounded()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var tickets = server.System<CMUTicketSystem>();
            tickets.Reset();

            var admin = new NetUserId(Guid.NewGuid());
            var start = DateTime.Now.AddHours(-1);

            // The oldest channel holds the only open ticket; it must survive the cap.
            var open = new NetUserId(Guid.NewGuid());
            tickets.RecordMessage(open, open, "Open", "help", false, true, start);

            // A channel an admin wrote to without a ticket is left out.
            var noTicket = new NetUserId(Guid.NewGuid());
            tickets.RecordMessage(noTicket, admin, "Admin", "hello", false, true, start);

            var total = CMUTicketLimits.MaxSnapshotChannels + 5;
            for (var i = 0; i < total; i++)
            {
                var player = new NetUserId(Guid.NewGuid());
                tickets.RecordMessage(player, player, "Player", "help", false, true, start.AddSeconds(i));
                tickets.TryClose(player, admin, "Admin", "done");
            }

            var snapshot = tickets.BuildSnapshot();
            Assert.That(snapshot.Tickets.Count, Is.EqualTo(total + 1));
            Assert.That(snapshot.History.Count, Is.EqualTo(CMUTicketLimits.MaxSnapshotChannels));
            Assert.That(snapshot.History.ContainsKey(open));
            Assert.That(snapshot.History.ContainsKey(noTicket), Is.False);

            tickets.Reset();
        });

        await pair.CleanReturnAsync();
    }
}
