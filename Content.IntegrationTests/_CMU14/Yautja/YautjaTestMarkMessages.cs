using Content.Shared.CMU14.Yautja;

namespace Content.IntegrationTests.CMU14.Yautja;

internal static class YautjaTestMarkMessages
{
    public static YautjaMarkPanelUnmarkMsg Unmark(IEntityManager entities, EntityUid actor, EntityUid target, YautjaMarkKind kind)
    {
        var (record, revision) = Observe(entities, actor, target);
        return new YautjaMarkPanelUnmarkMsg(record, revision, kind) { Actor = actor };
    }

    public static YautjaMarkPanelMarkMsg Mark(IEntityManager entities, EntityUid actor, EntityUid target, YautjaMarkKind kind, string? reason)
    {
        var (record, revision) = Observe(entities, actor, target);
        return new YautjaMarkPanelMarkMsg(record, revision, kind, reason) { Actor = actor };
    }

    private static (int Record, uint Revision) Observe(IEntityManager entities, EntityUid actor, EntityUid target)
    {
        Assert.That(entities.System<YautjaPowerSystem>().TryGetWornBracer(actor, out var bracer), Is.True);
        Assert.That(entities.System<YautjaMarkSystem>().TryOpenMarkPanel(bracer, actor), Is.True);
        var journal = entities.GetComponent<YautjaHuntJournalComponent>(actor);
        // The fixture needs the private identity to construct a current UI command for this exact target.
#pragma warning disable RA0002
        Assert.That(journal.Targets.TryGetValue(target, out var record), Is.True);
#pragma warning restore RA0002
        return (record, journal.Revision);
    }
}
