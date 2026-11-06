using System.Collections;
using System.Reflection;
using Content.Client.CMU14.Yautja;
using Content.Server.Station.Systems;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaMarkWindowLayoutTest
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    // the list column holds a scroll container, which desires 0 height. HudColumns arranged it at that, so the
    // server sent the nearby prey and the client built the rows, but the list was 0px tall and looked empty
    [Test]
    public async Task NearbyPreyRowsAreActuallyOnScreen()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid bracer = default;
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            hunter = em.SpawnEntity("CMUMobYautja", map.GridCoords);
            var gear = pair.Server.ProtoMan.Index<StartingGearPrototype>("CMUYautjaHunterSpawnGear");
            em.System<StationSpawningSystem>().EquipStartingGear(hunter, gear);
            em.System<SharedMindSystem>().ControlMob(pair.Player!.UserId, hunter);
            em.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(3, 0)));
            em.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new System.Numerics.Vector2(0, 3)));
        });
        await pair.RunSeconds(1f);
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            Assert.That(em.System<YautjaPowerSystem>().TryGetWornBracer(hunter, out var worn), Is.True);
            bracer = worn.Owner;
            Assert.That(em.System<YautjaMarkSystem>().TryOpenMarkPanel(worn, hunter), Is.True);
        });
        await pair.RunSeconds(1f);
        await pair.Client.WaitAssertion(() =>
        {
            var cem = pair.Client.EntMan;
            var clientBracer = cem.GetEntity(pair.Server.EntMan.GetNetEntity(bracer));
            Assert.That(cem.System<SharedUserInterfaceSystem>().TryGetOpenUi<YautjaMarkBui>(clientBracer,
                YautjaMarkUIKey.Key, out var bui), Is.True);
            var window = (YautjaMarkWindow) typeof(YautjaMarkBui).GetField("_window", Private)!.GetValue(bui)!;
            var entries = (IList) typeof(YautjaMarkWindow).GetField("_entries", Private)!.GetValue(window)!;
            var targets = (Control) typeof(YautjaMarkWindow).GetField("_targets", Private)!.GetValue(window)!;
            var scroll = targets.Parent!;
            Assert.Multiple(() =>
            {
                Assert.That(entries, Has.Count.EqualTo(2), "the marine and the drone should both be listed");
                Assert.That(targets.ChildCount, Is.EqualTo(2));
                Assert.That(targets.DesiredSize.Y, Is.GreaterThan(0));
                Assert.That(scroll.Size.Y, Is.GreaterThanOrEqualTo(targets.DesiredSize.Y),
                    $"the target list is {scroll.Size.Y}px tall, the rows need {targets.DesiredSize.Y}px");
            });
        });
        await pair.CleanReturnAsync();
    }
}
