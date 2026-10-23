#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.Verbs;
using Content.Shared.Administration;
using Content.Shared.Verbs;
using Robust.Shared.Localization;
using Robust.Shared.Player;

namespace Content.IntegrationTests._CMU14.Administration;

[TestFixture]
[TestOf(typeof(AdminVerbSystem))]
public sealed class CMUViewLogsVerbTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task ViewLogsFallsBackToAttachedSession()
    {
        var session = ServerSession!;
        var adminManager = Server.ResolveDependency<IAdminManager>();
        var loc = Server.ResolveDependency<ILocalizationManager>();

        EntityUid user = default;
        EntityUid bystander = default;
        await Server.WaitPost(() =>
        {
            // Bare entities have no mind, so the verb can only come from the attached session.
            user = SEntMan.Spawn();
            bystander = SEntMan.Spawn();
            Server.PlayerMan.SetAttachedEntity(session, user);
            adminManager.PromoteHost(session);
        });

        await WaitForFlag(adminManager, session, AdminFlags.Logs);

        await Server.WaitAssertion(() =>
        {
            var viewLogs = loc.GetString("admin-verbs-admin-logs-player");
            var verbs = Server.System<VerbSystem>();

            var selfVerbs = verbs.GetLocalVerbs(user, user, typeof(Verb), force: true)
                .Where(verb => verb.Category == VerbCategory.Admin)
                .Select(verb => verb.Text)
                .ToArray();
            var bystanderVerbs = verbs.GetLocalVerbs(bystander, user, typeof(Verb), force: true)
                .Where(verb => verb.Category == VerbCategory.Admin)
                .Select(verb => verb.Text)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(viewLogs, Is.EqualTo("View Logs"));
                Assert.That(selfVerbs, Does.Contain(viewLogs),
                    "An entity with an attached session but no mind history still offers View Logs.");
                Assert.That(bystanderVerbs, Does.Not.Contain(viewLogs),
                    "Without a mind owner or a session there is no player to filter the logs by.");
                Assert.That(loc.HasString("cmu-admin-logs-title-player"));
                Assert.That(loc.HasString("cmu-lobby-admin-button"));
            });
        });

        // The same admin without the Logs flag still gets admin verbs, but not View Logs.
        await Server.WaitAssertion(() =>
        {
            var data = adminManager.GetAdminData(session)!;
            var fullFlags = data.Flags;
            try
            {
                data.Flags = fullFlags & ~AdminFlags.Logs;
                Assert.That(adminManager.HasAdminFlag(session, AdminFlags.Admin), Is.True);
                Assert.That(adminManager.HasAdminFlag(session, AdminFlags.Logs), Is.False);

                var adminVerbs = Server.System<VerbSystem>().GetLocalVerbs(user, user, typeof(Verb), force: true)
                    .Where(verb => verb.Category == VerbCategory.Admin)
                    .Select(verb => verb.Text)
                    .ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(adminVerbs, Does.Contain(loc.GetString("admin-verbs-freeze")),
                        "Admin verbs are still built for an admin without Logs.");
                    Assert.That(adminVerbs, Does.Not.Contain(loc.GetString("admin-verbs-admin-logs-player")),
                        "View Logs needs the Logs flag.");
                });
            }
            finally
            {
                data.Flags = fullFlags;
            }
        });
    }

    private async Task WaitForFlag(IAdminManager manager, ICommonSession session, AdminFlags flag)
    {
        var hasFlag = false;
        for (var i = 0; i < 30 && !hasFlag; i++)
        {
            await Server.WaitPost(() => hasFlag = manager.HasAdminFlag(session, flag));
            await RunTicksSync(1);
        }

        await Server.WaitAssertion(() => Assert.That(manager.HasAdminFlag(session, flag), Is.True));
    }
}
