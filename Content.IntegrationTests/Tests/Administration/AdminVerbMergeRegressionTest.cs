#nullable enable
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.Verbs;
using Content.Shared._RMC14.Admin;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Prototypes;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using DbAdmin = Content.Server.Database.Admin;
using DbAdminFlag = Content.Server.Database.AdminFlag;

namespace Content.IntegrationTests.Tests.Administration;

[TestFixture]
[TestOf(typeof(Content.Server.Administration.Systems.AdminVerbSystem))]
public sealed class AdminVerbMergeRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.ConsoleLoginLocal), false)]
    public async Task MentorAndAdminVerbSurfacesStaySeparatedAndSpawnJobsStayCmOnly()
    {
        var session = ServerSession!;
        var database = Server.ResolveDependency<IServerDbManager>();
        var adminManager = Server.ResolveDependency<IAdminManager>();
        var mentorFlag = AdminFlagsHelper.FlagsToNames(AdminFlags.MentorHelp).Single();

        if (await database.GetAdminDataForAsync(session.UserId) is not null)
            await database.RemoveAdminAsync(session.UserId);

        var admin = new DbAdmin
        {
            UserId = session.UserId.UserId,
            Flags = new List<DbAdminFlag>(),
        };
        admin.Flags.Add(new DbAdminFlag
        {
            Flag = mentorFlag,
            AdminId = admin.UserId,
            Admin = admin,
        });
        await database.AddAdminAsync(admin);

        EntityUid user = default;
        EntityUid target = default;
        await Server.WaitPost(() =>
        {
            user = SEntMan.Spawn();
            target = SEntMan.Spawn();
            Server.PlayerMan.SetAttachedEntity(session, user);

            // A distinct target session is not needed for the verb callback, but ActorComponent is.
            // Keep the real session attached to the user and install the target actor through the
            // engine-owned setter so the test can discriminate User from Target in the dialog event.
            var targetActor = SEntMan.EnsureComponent<ActorComponent>(target);
            typeof(ActorComponent)
                .GetProperty(nameof(ActorComponent.PlayerSession), BindingFlags.Instance | BindingFlags.Public)!
                .SetValue(targetActor, session);

            adminManager.ReloadAdmin(session);
        });

        await WaitForFlag(adminManager, session, AdminFlags.MentorHelp);

        await Server.WaitAssertion(() =>
        {
            var localization = Server.ResolveDependency<ILocalizationManager>();
            var verbs = Server.System<VerbSystem>();
            var mentorVerbs = verbs.GetLocalVerbs(target, user, typeof(Verb), force: true);
            var mentorAdminVerbs = mentorVerbs
                .Where(verb => verb.Category == VerbCategory.Admin)
                .Select(verb => verb.Text)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(adminManager.HasAdminFlag(session, AdminFlags.MentorHelp), Is.True);
                Assert.That(adminManager.HasAdminFlag(session, AdminFlags.Admin), Is.False);
                Assert.That(mentorAdminVerbs, Is.EqualTo(new[]
                {
                    localization.GetString("prayer-verbs-subtle-message"),
                }), "MentorHelp grants only the LongString subtle-message verb from this system.");
                Assert.That(mentorVerbs.Select(verb => verb.Text), Does.Not.Contain(localization.GetString("toolshed-verb-mark")));
                Assert.That(mentorVerbs.Select(verb => verb.Text), Does.Not.Contain(localization.GetString("admin-player-actions-spawn")));
                Assert.That(mentorVerbs.Select(verb => verb.Text), Does.Not.Contain(localization.GetString("admin-player-actions-clone")));
                Assert.That(mentorVerbs.Select(verb => verb.Text), Does.Not.Contain(localization.GetString("rmc-admin-player-actions-spawn-here-as-job")));
                Assert.That(mentorVerbs.Select(verb => verb.Text), Does.Not.Contain(localization.GetString("rmc-admin-player-actions-random-name")));
                Assert.That(mentorVerbs.Select(verb => verb.Text), Does.Not.Contain(localization.GetString("admin-player-actions-check-afk")));
                Assert.That(mentorVerbs.Select(verb => verb.Text), Does.Not.Contain(localization.GetString("admin-verbs-camera")));
            });
        });

        await Server.WaitPost(() => adminManager.PromoteHost(session));
        await WaitForFlag(adminManager, session, AdminFlags.Admin);

        await Server.WaitAssertion(() =>
        {
            var localization = Server.ResolveDependency<ILocalizationManager>();
            var verbs = Server.System<VerbSystem>();
            var adminVerbs = verbs.GetLocalVerbs(target, user, typeof(Verb), force: true);
            var texts = adminVerbs.Select(verb => verb.Text).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(texts, Does.Contain(localization.GetString("prayer-verbs-subtle-message")));
                Assert.That(texts, Does.Contain(localization.GetString("toolshed-verb-mark")));
                Assert.That(texts, Does.Contain(localization.GetString("admin-player-actions-spawn")));
                Assert.That(texts, Does.Contain(localization.GetString("admin-player-actions-clone")));
                Assert.That(texts, Does.Contain(localization.GetString("rmc-admin-player-actions-spawn-here-as-job")));
                Assert.That(texts, Does.Contain(localization.GetString("admin-player-actions-check-afk")));
                Assert.That(texts, Does.Contain(localization.GetString("admin-verbs-camera")));
                Assert.That(texts, Does.Not.Contain(localization.GetString("rmc-admin-player-actions-random-name")),
                    "Random-name is offered only for HumanoidProfile targets.");
            });

            SEntMan.EnsureComponent<HumanoidProfileComponent>(target);
            adminVerbs = verbs.GetLocalVerbs(target, user, typeof(Verb), force: true);
            Assert.That(adminVerbs.Select(verb => verb.Text),
                Does.Contain(localization.GetString("rmc-admin-player-actions-random-name")));

            var spawnAsJob = adminVerbs.Single(verb =>
                verb.Text == localization.GetString("rmc-admin-player-actions-spawn-here-as-job"));

            var oldFilter = CMPrototypeExtensions.FilterCM;
            try
            {
                CMPrototypeExtensions.FilterCM = true;
                spawnAsJob.Act!();

                var dialog = SEntMan.GetComponent<DialogComponent>(user);
                var expected = SProtoMan.EnumerateCM<JobPrototype>()
                    .Select(job => new
                    {
                        Job = job,
                        Text = job.SpawnMenuRoleName is { } raw
                            ? localization.TryGetString(raw, out var localized) ? localized : raw
                            : job.LocalizedName,
                    })
                    .OrderBy(entry => entry.Text, StringComparer.Ordinal)
                    .ToArray();

                Assert.That(dialog.Options, Has.Count.EqualTo(expected.Length));
                Assert.That(dialog.Options.Select(option => option.Text),
                    Is.EqualTo(expected.Select(entry => entry.Text)),
                    "CM spawn roles use SpawnMenuRoleName when present and sort ordinally.");

                for (var i = 0; i < dialog.Options.Count; i++)
                {
                    var option = dialog.Options[i];
                    var spawn = option.Event as SpawnAsJobDialogEvent;
                    Assert.Multiple(() =>
                    {
                        Assert.That(expected[i].Job.IsCM, Is.True);
                        Assert.That(spawn, Is.Not.Null);
                        Assert.That(spawn!.User, Is.EqualTo(SEntMan.GetNetEntity(user)));
                        Assert.That(spawn.Target, Is.EqualTo(SEntMan.GetNetEntity(target)));
                        Assert.That(spawn.JobId, Is.EqualTo(expected[i].Job.ID));
                    });
                }
            }
            finally
            {
                CMPrototypeExtensions.FilterCM = oldFilter;
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
