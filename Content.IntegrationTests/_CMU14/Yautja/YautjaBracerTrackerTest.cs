using System.Linq;
using System.Numerics;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaBracerTrackerTest
{
    // the tracker only walks yautja + tracked gear now instead of every entity in the world,
    // so make sure tracked gear still shows up and the filters still apply
    [Test]
    public async Task BracerTrackerListsDroppedTrackedGearOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var dropped = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords.Offset(new Vector2(0, 5)));
            var untracked = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords.Offset(new Vector2(0, 7)));
            var carried = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaTrackGear", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                entMan.EnsureComponent<YautjaTrackedItemComponent>(dropped);
                entMan.EnsureComponent<YautjaTrackedItemComponent>(carried);
                Assert.That(hands.TryPickupAnyHand(hunter, carried), Is.True);

                var ev = new YautjaTrackGearActionEvent
                {
                    Performer = hunter,
                    Action = (action, entMan.GetComponent<ActionComponent>(action)),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, ev);

                Assert.That(ev.Handled, Is.True);
                Assert.That(ui.TryGetUiState<YautjaBracerPanelState>(bracer, YautjaBracerUIKey.Key, out var state), Is.True);

                // carried gear is on a yautja so it's hidden, untracked gear never shows
                var entry = state!.TrackedGear.Single();
                Assert.Multiple(() =>
                {
                    Assert.That(entry.Name, Is.EqualTo(entMan.GetComponent<MetaDataComponent>(dropped).EntityName));
                    Assert.That(entry.Distance, Is.EqualTo(5));
                    Assert.That(entry.Count, Is.EqualTo(1));
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, bracer, dropped, untracked, carried, action })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }
}
