using System.Collections.Generic;
using System.IO;
using Content.Server._CMU14.Yautja;
using Content.Shared._CMU14.Yautja;
using NUnit.Framework;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanAdminStateStoreTest
{
    [Test]
    public void ReturnsCachedStateWithoutLoadingDatabase()
    {
        var store = new YautjaClanAdminStateStore();
        var state = new YautjaClanAdminEuiState(
            [],
            "player",
            "summary",
            "status",
            4,
            12,
            YautjaClanAdminMutationKind.Updated);

        store.Set(state);

        Assert.That(store.Get(), Is.SameAs(state));
    }

    [Test]
    public void PendingMutationIsAcknowledgedOnlyWithFreshSnapshot()
    {
        var oldClans = new List<YautjaClanAdminClanState>
        {
            Clan(1, "Old"),
        };
        var store = new YautjaClanAdminStateStore();
        store.Set(State(oldClans, "old player", "old summary", "old status", 4, 1,
            YautjaClanAdminMutationKind.Updated));

        store.StageMutation(2, YautjaClanAdminMutationKind.Created, "created");
        var error = store.PublishRefreshFailure("refresh failed");

        Assert.Multiple(() =>
        {
            Assert.That(store.CanStartMutation, Is.False);
            Assert.That(error.Clans, Is.SameAs(oldClans));
            Assert.That(error.InspectedPlayer, Is.EqualTo("old player"));
            Assert.That(error.InspectedSummary, Is.EqualTo("old summary"));
            Assert.That(error.StatusMessage, Is.EqualTo("refresh failed"));
            Assert.That(error.ClanMutationVersion, Is.EqualTo(4));
            Assert.That(error.LastMutatedClanId, Is.EqualTo(1));
            Assert.That(error.LastMutationKind, Is.EqualTo(YautjaClanAdminMutationKind.Updated));
        });

        var freshClans = new List<YautjaClanAdminClanState>
        {
            Clan(1, "Old"),
            Clan(2, "Created"),
        };
        var refreshed = store.PublishFreshSnapshot(
            freshClans,
            "new player",
            "new summary",
            "refresh recovered");

        Assert.Multiple(() =>
        {
            Assert.That(store.CanStartMutation, Is.False);
            Assert.That(refreshed.Clans, Is.SameAs(freshClans));
            Assert.That(refreshed.InspectedPlayer, Is.EqualTo("new player"));
            Assert.That(refreshed.InspectedSummary, Is.EqualTo("new summary"));
            Assert.That(refreshed.StatusMessage, Is.EqualTo("created"));
            Assert.That(refreshed.ClanMutationVersion, Is.EqualTo(5));
            Assert.That(refreshed.LastMutatedClanId, Is.EqualTo(2));
            Assert.That(refreshed.LastMutationKind, Is.EqualTo(YautjaClanAdminMutationKind.Created));
        });

        Assert.That(store.GetForDelivery(), Is.SameAs(refreshed));
        Assert.That(store.CanStartMutation, Is.True);
    }

    [Test]
    public void RecoveredAcknowledgementMustBeDeliveredBeforeNextMutationCanStart()
    {
        var store = new YautjaClanAdminStateStore();
        store.StageMutation(2, YautjaClanAdminMutationKind.Created, "created");
        store.PublishRefreshFailure("refresh failed");

        var recovered = store.PublishFreshSnapshot(
            [Clan(2, "Created")],
            "",
            "",
            "refresh recovered");

        Assert.Multiple(() =>
        {
            Assert.That(store.CanStartMutation, Is.False);
            Assert.That(recovered.LastMutationKind, Is.EqualTo(YautjaClanAdminMutationKind.Created));
            Assert.That(() => store.StageMutation(2, YautjaClanAdminMutationKind.Updated, "updated"),
                Throws.InvalidOperationException);
        });

        var delivered = store.GetForDelivery();
        Assert.Multiple(() =>
        {
            Assert.That(delivered, Is.SameAs(recovered));
            Assert.That(delivered.LastMutationKind, Is.EqualTo(YautjaClanAdminMutationKind.Created));
            Assert.That(store.CanStartMutation, Is.True);
        });

        Assert.DoesNotThrow(() =>
            store.StageMutation(2, YautjaClanAdminMutationKind.Updated, "updated"));
    }

    [Test]
    public async Task RussianClanAdminLocaleIsReadableUtf8()
    {
        await using var pair = await PoolManager.GetServerClient();
        var resources = pair.Server.ResolveDependency<IResourceManager>();

        using var stream = resources.ContentFileRead(
            new ResPath("/Locale/ru-RU/_CMU14/yautja/admin_clan.ftl"));
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();
        await pair.CleanReturnAsync();

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("cmu-yautja-clan-admin-title = Администрирование кланов яутжа"));
            Assert.That(text, Does.Not.Contain("\u0420\u0459"));
        });
    }

    private static YautjaClanAdminClanState Clan(int id, string name)
    {
        return new(id, name, $"{name} description", 0, "#ffffff", 0);
    }

    private static YautjaClanAdminEuiState State(
        List<YautjaClanAdminClanState> clans,
        string inspectedPlayer,
        string inspectedSummary,
        string status,
        long version,
        int? clanId,
        YautjaClanAdminMutationKind kind)
    {
        return new(clans, inspectedPlayer, inspectedSummary, status, version, clanId, kind);
    }
}
