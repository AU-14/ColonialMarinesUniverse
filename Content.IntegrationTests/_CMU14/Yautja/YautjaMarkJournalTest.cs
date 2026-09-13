using System.Linq;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaMarkJournalTest
{
    [Test]
    public async Task MarkedTargetCanBeUpdatedAfterLeavingSight()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var (hunter, bracer) = SpawnHunter(em, map.GridCoords);
            var target = em.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(2, 0)));
            var marks = em.System<YautjaMarkSystem>();
            Assert.That(marks.TryMark(bracer, hunter, target, YautjaMarkKind.Honored, null), Is.True);
            em.System<SharedTransformSystem>().SetCoordinates(target, map.GridCoords.Offset(new System.Numerics.Vector2(30, 0)));
            Assert.That(marks.TryChangeMark(bracer, hunter, target, YautjaMarkKind.Honored,
                YautjaMarkKind.Dishonored, null), Is.True,
                "A hunter must be able to edit an owned mark outside the proximity list.");
            Assert.Multiple(() =>
            {
                Assert.That(marks.IsMarkedBy(target, YautjaMarkKind.Honored, hunter), Is.False);
                Assert.That(marks.IsMarkedBy(target, YautjaMarkKind.Dishonored, hunter), Is.True);
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterCannotOverwriteAnotherHuntersMark()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var (hunter, bracer) = SpawnHunter(em, map.GridCoords);
            var (other, otherBracer) = SpawnHunter(em, map.GridCoords);
            var target = em.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(2, 0)));
            var marks = em.System<YautjaMarkSystem>();
            Assert.That(marks.TryMark(bracer, hunter, target, YautjaMarkKind.Honored, null), Is.True);
            Assert.That(marks.TryMark(otherBracer, other, target, YautjaMarkKind.Honored, null), Is.False);
            Assert.That(marks.IsMarkedBy(target, YautjaMarkKind.Honored, hunter), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UndefinedMarkKindsAreRejected()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var (hunter, bracer) = SpawnHunter(em, map.GridCoords);
            var target = em.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(2, 0)));
            Assert.That(em.System<YautjaMarkSystem>().TryMark(bracer, hunter, target, (YautjaMarkKind)255, null), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RecentSightingsAreBoundedWhileMarkedHistorySurvivesDeletion()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid marked = default;
        EntityUid hidden = default;
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var spawned = SpawnHunter(em, map.GridCoords);
            hunter = spawned.Hunter;
            marked = em.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            Assert.That(em.System<YautjaMarkSystem>().TryMark(spawned.Bracer, hunter, marked,
                YautjaMarkKind.Honored, null), Is.True);
            hidden = em.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1, 1)));
            em.EnsureComponent<EntityActiveInvisibleComponent>(hidden);
            for (var i = 0; i < 9; i++)
                em.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(2 + i, 0)));
        });

        await pair.RunSeconds(0.6f);
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var journal = em.GetComponent<YautjaHuntJournalComponent>(hunter);
            Assert.That(journal.Recent, Has.Count.EqualTo(8));
            Assert.That(journal.Records.Values.Count(record => record.WasMarked), Is.EqualTo(1));
            Assert.That(journal.Records.Values.Any(record => record.Target == hidden), Is.False);
            em.DeleteEntity(marked);
            var historical = journal.Records.Values.Single(record => record.WasMarked);
            Assert.That(historical.Target, Is.Null,
                "deleting a marked entity must preserve its private history snapshot");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MarkTransfersWhenXenoEvolves()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var (hunter, bracer) = SpawnHunter(em, map.GridCoords);
            var oldXeno = em.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(2, 0)));
            var newXeno = em.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(2, 0)));
            em.EnsureComponent<XenoComponent>(oldXeno);
            em.EnsureComponent<XenoComponent>(newXeno);
            var evolution = em.EnsureComponent<XenoEvolutionComponent>(oldXeno);
            var marks = em.System<YautjaMarkSystem>();

            Assert.That(marks.TryMark(bracer, hunter, oldXeno, YautjaMarkKind.Prey, null), Is.True);

            var evolved = new NewXenoEvolvedEvent((oldXeno, evolution), newXeno, true);
            em.EventBus.RaiseLocalEvent(newXeno, ref evolved, true);

            Assert.Multiple(() =>
            {
                Assert.That(marks.IsMarkedBy(oldXeno, YautjaMarkKind.Prey, hunter), Is.False);
                Assert.That(marks.IsMarkedBy(newXeno, YautjaMarkKind.Prey, hunter), Is.True);
                var journal = em.GetComponent<YautjaHuntJournalComponent>(hunter);
                Assert.That(journal.Records.Values.Single(record => record.WasMarked).Target,
                    Is.EqualTo(newXeno));
            });

            Assert.That(marks.TryChangeMark(bracer, hunter, newXeno, YautjaMarkKind.Prey,
                YautjaMarkKind.Dishonored, null), Is.True);
            Assert.That(marks.TryClearMark(newXeno, YautjaMarkKind.Dishonored, hunter), Is.True);
            Assert.That(marks.TryMark(bracer, hunter, newXeno, YautjaMarkKind.Prey, null), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    private static (EntityUid Hunter, Entity<YautjaBracerComponent> Bracer) SpawnHunter(
        IEntityManager em, EntityCoordinates coords)
    {
        var hunter = em.SpawnEntity("CMMobHuman", coords);
        em.EnsureComponent<YautjaComponent>(hunter);
        var bracer = em.SpawnEntity("CMUYautjaBracer", coords);
        Assert.That(em.System<InventorySystem>().TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
        return (hunter, (bracer, em.GetComponent<YautjaBracerComponent>(bracer)));
    }
}
