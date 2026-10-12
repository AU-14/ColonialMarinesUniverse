using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round.Objectives.Type;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.CMU14.Round.Objectives.Type;

namespace Content.IntegrationTests.CMU14.Objectives;

// BUG-863: two objectives that want the same item proto only ever paid out once, because the
// analyzer scan let the first objective eat every matching item in range
[TestFixture]
public sealed class DuplicateFetchObjectiveTest : GameTest
{
    private const string Item = "CMPaper";
    private const string Faction = "govfor";

    [Test]
    public async Task ScanCreditsEachDuplicateObjectiveOnce()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<ObjFetchSystem>();
            var (obj1, fetch1) = SpawnObjective(map.GridCoords);
            var (obj2, fetch2) = SpawnObjective(map.GridCoords);
            var analyzer = SpawnAnalyzer(map.GridCoords);

            var paper1 = SEntMan.SpawnEntity(Item, map.GridCoords);
            var paper2 = SEntMan.SpawnEntity(Item, map.GridCoords);

            Assert.That(system.ScanForFetchItems(analyzer), Is.EqualTo(2));
            Assert.That(fetch1.AmountFetchedPerFaction.GetValueOrDefault(Faction), Is.EqualTo(1));
            Assert.That(fetch2.AmountFetchedPerFaction.GetValueOrDefault(Faction), Is.EqualTo(1));

            foreach (var ent in new[] { paper1, paper2, analyzer, obj1, obj2 })
                SEntMan.DeleteEntity(ent);
        });
    }

    [Test]
    public async Task ClaimedItemOnlyCreditsItsOwnObjective()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<ObjFetchSystem>();
            var (obj1, fetch1) = SpawnObjective(map.GridCoords);
            var (obj2, fetch2) = SpawnObjective(map.GridCoords);
            var analyzer = SpawnAnalyzer(map.GridCoords);

            var paper1 = SEntMan.SpawnEntity(Item, map.GridCoords);
            var paper2 = SEntMan.SpawnEntity(Item, map.GridCoords);
            SEntMan.EnsureComponent<FetchItemComponent>(paper1).ObjectiveUid = obj1;
            SEntMan.EnsureComponent<FetchItemComponent>(paper2).ObjectiveUid = obj2;

            Assert.That(system.TryFetchAtAnalyzer(analyzer, paper2), Is.EqualTo(FetchAnalyzeResult.Fetched));
            Assert.That(fetch1.AmountFetchedPerFaction.GetValueOrDefault(Faction), Is.Zero);
            Assert.That(fetch2.AmountFetchedPerFaction.GetValueOrDefault(Faction), Is.EqualTo(1));

            Assert.That(system.TryFetchAtAnalyzer(analyzer, paper1), Is.EqualTo(FetchAnalyzeResult.Fetched));
            Assert.That(fetch1.AmountFetchedPerFaction.GetValueOrDefault(Faction), Is.EqualTo(1));

            Assert.That(system.TryFetchAtAnalyzer(analyzer, paper1), Is.EqualTo(FetchAnalyzeResult.AlreadyFetched));

            foreach (var ent in new[] { paper1, paper2, analyzer, obj1, obj2 })
                SEntMan.DeleteEntity(ent);
        });
    }

    private (EntityUid, FetchObjectiveComponent) SpawnObjective(Robust.Shared.Map.EntityCoordinates coords)
    {
        var uid = SEntMan.SpawnEntity(null, coords);
        var fetch = SEntMan.EnsureComponent<FetchObjectiveComponent>(uid);
        fetch.TargetPrototype = Item;
        fetch.FetchCount = 1;
        var objective = SEntMan.EnsureComponent<CMUObjectiveComponent>(uid);
        objective.Faction = Faction;
        objective.Active = true;
        return (uid, fetch);
    }

    private EntityUid SpawnAnalyzer(Robust.Shared.Map.EntityCoordinates coords)
    {
        var uid = SEntMan.SpawnEntity(null, coords);
        SEntMan.EnsureComponent<FetchAnalyzerComponent>(uid).Faction = Faction;
        return uid;
    }
}
