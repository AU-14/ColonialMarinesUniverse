using System.Linq;
using Content.Shared._RMC14.Pulling;
using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.CMU14.Specimens;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Specimens;

[TestFixture]
[TestOf(typeof(CMUSpecimenCaptureSystem))]
public sealed class CMUSpecimenCaptureTest
{
    private static int Ticks(Pair.TestPair pair, double seconds)
    {
        return (int) (seconds / pair.Server.Timing.TickPeriod.TotalSeconds);
    }

    [TestCase("CMXenoDrone")]
    [TestCase("CMU14XenoNeomorph")]
    [TestCase("AU14Wendigo")]
    [TestCase("CMUWendigoLab")]
    [TestCase("CMUXenoAbomination")]
    [TestCase("AU14BiomorphGrunt")]
    [TestCase("AU14BiomorphSpider")]
    public async Task SedateCrateAndRelease(string creature)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var capture = server.System<CMUSpecimenCaptureSystem>();
        var container = server.System<SharedContainerSystem>();
        var actions = server.System<SharedActionsSystem>();

        // Whether every one of the creature's abilities is refused, acid included.
        bool ActionsBlocked(EntityUid uid)
        {
            foreach (var action in actions.GetActions(uid))
            {
                var ev = new ActionAttemptEvent(uid);
                entities.EventBus.RaiseLocalEvent(action.Owner, ref ev);
                if (!ev.Cancelled)
                    return false;
            }

            return true;
        }

        EntityUid specimen = default;
        Entity<CMUSpecimenCrateComponent> crate = default;
        await server.WaitAssertion(() =>
        {
            specimen = entities.SpawnEntity(creature, map.GridCoords);
            Assert.That(capture.IsSpecimen(specimen), Is.True);

            // One hit short of the threshold leaves it standing; the next one puts it under.
            var threshold = (int) capture.GetSedationThreshold(specimen);
            for (var i = 0; i < threshold - 1; i++)
                Assert.That(capture.Sedate(specimen, 1, TimeSpan.FromSeconds(60)), Is.False);

            Assert.That(capture.Sedate(specimen, 1, TimeSpan.FromSeconds(60)), Is.True);
            Assert.That(capture.IsSubdued(specimen), Is.True);
        });

        // Well past any ordinary xeno stun, the sedation still holds it down.
        await server.WaitRunTicks(Ticks(pair, 45));
        await server.WaitAssertion(() =>
        {
            Assert.That(capture.IsSedated(specimen), Is.True);
            Assert.That(capture.IsSubdued(specimen), Is.True);
            Assert.That(ActionsBlocked(specimen), Is.True, "A sedated specimen can still use its abilities.");

            // Dragged onto the open crate (here: already lying on it), closing the lid seals it in.
            var uid = entities.SpawnEntity("CMUSpecimenCrate", map.GridCoords);
            crate = (uid, entities.GetComponent<CMUSpecimenCrateComponent>(uid));
            Assert.That(crate.Comp.State, Is.EqualTo(CMUSpecimenCrateState.Open));
            Assert.That(capture.TryToggle(crate), Is.True);
            Assert.That(container.IsEntityInContainer(specimen), Is.True, "Closing the crate didn't seal the specimen in.");
            Assert.That(capture.GetSpecimen(crate), Is.EqualTo(specimen));
            Assert.That(capture.TryToggle(crate), Is.False, "The lid reversed mid-swing.");
        });

        // Crated, the sedation waits.
        await server.WaitRunTicks(Ticks(pair, 40));
        await server.WaitAssertion(() =>
        {
            Assert.That(crate.Comp.State, Is.EqualTo(CMUSpecimenCrateState.Closed), "The lid never finished closing.");
            Assert.That(capture.IsSedated(specimen), Is.True, "A crated specimen woke up.");
            Assert.That(ActionsBlocked(specimen), Is.True, "A crated specimen can still use its abilities.");

            Assert.That(capture.TryToggle(crate), Is.True);
        });

        // Opening the lid lets it out, still sedated for the time it had left.
        await server.WaitRunTicks(Ticks(pair, 3));
        await server.WaitAssertion(() =>
        {
            Assert.That(crate.Comp.State, Is.EqualTo(CMUSpecimenCrateState.Open));
            Assert.That(container.IsEntityInContainer(specimen), Is.False, "Opening the crate didn't let the specimen out.");
            Assert.That(capture.IsSedated(specimen), Is.True);
        });

        // Then the sedation wears off.
        await server.WaitRunTicks(Ticks(pair, 20));
        await server.WaitAssertion(() =>
        {
            Assert.That(capture.IsSedated(specimen), Is.False);
            entities.DeleteEntity(crate);
            entities.DeleteEntity(specimen);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HandlersDragSedatedSpecimens()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var capture = server.System<CMUSpecimenCaptureSystem>();

        EntityUid xeno = default;
        EntityUid handler = default;
        await server.WaitAssertion(() =>
        {
            handler = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            xeno = entities.SpawnEntity("CMXenoDrone", map.GridCoords);
            Assert.That(entities.HasComponent<ParalyzeOnPullAttemptComponent>(xeno), Is.True);

            // A live xenonid floors whoever grabs it. Sedated, it can be dragged.
            Assert.That(capture.Sedate(xeno, 10, TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(entities.HasComponent<ParalyzeOnPullAttemptComponent>(xeno), Is.False);
            Assert.That(server.System<PullingSystem>().TryStartPull(handler, xeno), Is.True, "A handler can't drag a sedated specimen.");
        });

        // Once it wakes, grabbing it is dangerous again.
        await server.WaitRunTicks(Ticks(pair, 8));
        await server.WaitAssertion(() =>
        {
            Assert.That(capture.IsSedated(xeno), Is.False);
            Assert.That(entities.HasComponent<ParalyzeOnPullAttemptComponent>(xeno), Is.True);
            entities.DeleteEntity(xeno);
            entities.DeleteEntity(handler);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClosedCrateNeedsSecurityAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var capture = server.System<CMUSpecimenCaptureSystem>();

        Entity<CMUSpecimenCrateComponent> crate = default;
        EntityUid outsider = default;
        await server.WaitAssertion(() =>
        {
            var uid = entities.SpawnEntity("CMUSpecimenCrate", map.GridCoords);
            crate = (uid, entities.GetComponent<CMUSpecimenCrateComponent>(uid));
            outsider = entities.SpawnEntity("CMMobHuman", map.GridCoords);

            // Anyone may close it.
            Assert.That(capture.TryToggle(crate, outsider), Is.True);
        });

        await server.WaitRunTicks(Ticks(pair, 3));
        await server.WaitAssertion(() =>
        {
            Assert.That(crate.Comp.State, Is.EqualTo(CMUSpecimenCrateState.Closed));
            Assert.That(capture.TryToggle(crate, outsider), Is.False, "Someone without security access opened the crate.");
            Assert.That(capture.TryToggle(crate), Is.True);
            entities.DeleteEntity(crate);
            entities.DeleteEntity(outsider);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpawnerPlacesCrate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var spawner = entities.SpawnEntity("CMUSpawnSpecimenCrate", map.GridCoords);
            var crates = entities.EntityQuery<CMUSpecimenCrateComponent>().ToList();
            Assert.That(crates, Has.Count.EqualTo(1), "The spawner didn't place a crate.");
            Assert.That(crates[0].State, Is.EqualTo(CMUSpecimenCrateState.Open));

            entities.DeleteEntity(crates[0].Owner);
            entities.DeleteEntity(spawner);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PeopleCanBeForcedIntoTheCrate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var capture = server.System<CMUSpecimenCaptureSystem>();
        var container = server.System<SharedContainerSystem>();

        Entity<CMUSpecimenCrateComponent> crate = default;
        EntityUid handler = default;
        EntityUid person = default;
        await server.WaitAssertion(() =>
        {
            var uid = entities.SpawnEntity("CMUSpecimenCrate", map.GridCoords);
            crate = (uid, entities.GetComponent<CMUSpecimenCrateComponent>(uid));
            person = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            handler = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));

            // Someone still up and free takes the long put-in time; the lid waits for it.
            Assert.That(capture.IsRestrained(person), Is.False);
            Assert.That(capture.TryToggle(crate, handler), Is.True);
            Assert.That(crate.Comp.State, Is.EqualTo(CMUSpecimenCrateState.Open), "The lid closed before the person was in.");
        });

        await server.WaitRunTicks(Ticks(pair, 4));
        await server.WaitAssertion(() =>
        {
            Assert.That(container.IsEntityInContainer(person), Is.False, "A free person went in faster than the long put-in time.");
        });

        await server.WaitRunTicks(Ticks(pair, 7));
        await server.WaitAssertion(() =>
        {
            Assert.That(container.IsEntityInContainer(person), Is.True, "The person wasn't forced into the crate.");
            Assert.That(capture.GetSpecimen(crate), Is.EqualTo(person));
            Assert.That(entities.HasComponent<Content.Shared.Speech.Components.MumbleAccentComponent>(person), Is.True, "The person inside isn't muffled.");
        });

        await server.WaitRunTicks(Ticks(pair, 3));
        await server.WaitAssertion(() =>
        {
            Assert.That(crate.Comp.State, Is.EqualTo(CMUSpecimenCrateState.Closed));

            // Security can let them out from outside; they can't let themselves out.
            Assert.That(capture.TryToggle(crate), Is.True);
        });

        await server.WaitRunTicks(Ticks(pair, 3));
        await server.WaitAssertion(() =>
        {
            Assert.That(container.IsEntityInContainer(person), Is.False);
            Assert.That(entities.HasComponent<Content.Shared.Speech.Components.MumbleAccentComponent>(person), Is.False, "Getting out didn't take the muffle off.");
            entities.DeleteEntity(crate);
            entities.DeleteEntity(person);
            entities.DeleteEntity(handler);
        });

        await pair.CleanReturnAsync();
    }
}
