using Content.Shared._CMU14.Yautja;
using Content.Shared.Actions.Components;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaThrallBracerTest
{
    [Test]
    public async Task HuntingMarksCreateHumanThrallAndBloodedState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var thrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var hunterBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, hunterBracer, "gloves", silent: true, force: true), Is.True);

                var hunterBracerComp = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
                Assert.That(marks.TryMark((hunterBracer, hunterBracerComp), hunter, thrall, YautjaMarkKind.Thrall, "test"), Is.True);

                Assert.That(entMan.TryGetComponent(thrall, out YautjaThrallComponent? thrallComp), Is.True);
                Assert.That(thrallComp!.Master, Is.EqualTo(hunter));
                Assert.That(thrallComp.Blooded, Is.False);

                Assert.That(marks.TryMark((hunterBracer, hunterBracerComp), hunter, thrall, YautjaMarkKind.Blooded, null), Is.True);

                Assert.That(thrallComp.Blooded, Is.True);
                Assert.That(thrallComp.TechAuthorized, Is.True);
                Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(thrall), Is.True);
            }
            finally
            {
                foreach (var uid in new[] { hunter, thrall, hunterBracer })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VisibleThrallMarksWithWornThrallBracerCanBeLinked()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var thrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var hunterBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaLinkThrallBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, hunterBracer, "gloves", silent: true, force: true), Is.True);
                var hunterBracerComp = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
                Assert.That(marks.TryMark((hunterBracer, hunterBracerComp), hunter, thrall, YautjaMarkKind.Thrall, null), Is.True);
                Assert.That(marks.TryMark((hunterBracer, hunterBracerComp), hunter, thrall, YautjaMarkKind.Blooded, null), Is.True);
                entMan.RemoveComponent<YautjaThrallComponent>(thrall);

                Assert.That(inventory.TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var link = new YautjaLinkThrallBracerActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(hunterBracer, link);

                Assert.That(entMan.TryGetComponent(thrall, out YautjaThrallComponent? thrallComp), Is.True);
                Assert.That(thrallComp!.Master, Is.EqualTo(hunter));
                Assert.That(thrallComp.Blooded, Is.True);
                Assert.That(thrallComp.TechAuthorized, Is.True);
                Assert.That(thrallComp.BracerLinked, Is.True);
                Assert.That(thrallComp.MasterBracer, Is.EqualTo(hunterBracer));
                Assert.That(thrallComp.ThrallBracer, Is.EqualTo(thrallBracer));

                var linked = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);
                Assert.That(linked.Linked, Is.True);
                Assert.That(linked.Locked, Is.True);
                Assert.That(linked.Master, Is.EqualTo(hunter));
                Assert.That(linked.User, Is.EqualTo(thrall));
            }
            finally
            {
                foreach (var uid in new[] { hunter, thrall, hunterBracer, thrallBracer, action })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodedThrallHuntingBracerCanBeLinked()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var thrall = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var hunterBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var thrallBracer = entMan.SpawnEntity("CMUYautjaBloodedThrallBracer", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaLinkThrallBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var thrallComp = entMan.EnsureComponent<YautjaThrallComponent>(thrall);
                thrallComp.Master = hunter;
                thrallComp.Blooded = true;
                thrallComp.TechAuthorized = true;

                Assert.That(entMan.HasComponent<YautjaBracerComponent>(thrallBracer), Is.True);
                Assert.That(entMan.HasComponent<YautjaThrallBracerComponent>(thrallBracer), Is.True);
                Assert.That(inventory.TryEquip(hunter, hunterBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var link = new YautjaLinkThrallBracerActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(hunterBracer, link);

                Assert.That(thrallComp.BracerLinked, Is.True);
                Assert.That(thrallComp.MasterBracer, Is.EqualTo(hunterBracer));
                Assert.That(thrallComp.ThrallBracer, Is.EqualTo(thrallBracer));

                var linked = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);
                Assert.That(linked.Linked, Is.True);
                Assert.That(linked.Locked, Is.True);
                Assert.That(linked.Master, Is.EqualTo(hunter));
                Assert.That(linked.MasterBracer, Is.EqualTo(hunterBracer));
            }
            finally
            {
                foreach (var uid in new[] { hunter, thrall, hunterBracer, thrallBracer, action })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBracerLockActionRequiresGlovesSlot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleBracerLock", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "pocket1", silent: true, force: true), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.User = hunter;
                bracerComp.Locked = false;

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var toggle = new YautjaToggleBracerLockActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(bracer, toggle);

                Assert.That(bracerComp.Locked, Is.False);
            }
            finally
            {
                foreach (var uid in new[] { hunter, bracer, action })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }
}
