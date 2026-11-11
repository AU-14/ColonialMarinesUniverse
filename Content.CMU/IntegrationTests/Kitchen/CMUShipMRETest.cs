using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Marines;
using Content.Shared.Doors.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Kitchen;

public sealed class CMUShipMRETest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task EmptyMessAllowsMREUntilFoodServiceResumes()
    {
        var ship = await Pair.CreateTestMap();
        var otherMap = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            // Pin the clock past the setup seal so this test exercises only the mess-state priority.
            SetRoundDuration(TimeSpan.FromMinutes(11));
            SEntMan.EnsureComponent<WarshipComponent>(ship.MapUid);
            var user = SEntMan.SpawnEntity("CMMobHuman", ship.GridCoords);
            var vendor = SEntMan.SpawnEntity("RMCNutriCoVendor", ship.GridCoords);
            SEntMan.SpawnEntity("RMCNutriCoVendor", otherMap.GridCoords);
            var menu = SEntMan.GetComponent<CMAutomatedVendorComponent>(vendor);
            // Keep stocked cutlery: only edible meals count as an alternative to field rations.
            foreach (var section in menu.Sections.Where(s => s.Name != "Utensils"))
            foreach (var entry in section.Entries)
                entry.Amount = 0;

            var openable = Server.System<OpenableSystem>();
            bool OpenRation() => openable.TryOpen(SEntMan.SpawnEntity("CMMRE", ship.GridCoords), user: user);
            Assert.That(OpenRation(), Is.True, "A stocked vendor on another map must not block hungry crew.");

            menu.Sections.Single(s => s.Name == "Prepared Meals").Entries[0].Amount = 1;
            Assert.That(OpenRation(), Is.False, "Stocked mess meals still take priority.");
            menu.Sections.Single(s => s.Name == "Prepared Meals").Entries[0].Amount = 0;
            Assert.That(OpenRation(), Is.True, "Using the last meal must immediately lift the restriction.");

            var shutters = SEntMan.SpawnEntity("CMUShutterKitchen", ship.GridCoords);
            Server.System<SharedDoorSystem>().StartOpening(shutters);
            Assert.That(OpenRation(), Is.False, "An open foodline provides an alternative even with empty vendors.");
        });
    }

    private void SetRoundDuration(TimeSpan duration)
    {
        var now = Server.ResolveDependency<IGameTiming>().CurTime;
        typeof(SharedGameTicker)
            .GetProperty(nameof(SharedGameTicker.RoundStartTimeSpan),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(Server.System<SharedGameTicker>(), now - duration);
    }
}
