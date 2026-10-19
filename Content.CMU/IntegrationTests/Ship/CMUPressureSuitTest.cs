using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Ship;

/// <summary>
///     A pressure suit with its helmet up keeps out vacuum and feeds the wearer from internals, and a breath mask works
///     with an oxygen tank.
/// </summary>
[TestFixture]
public sealed class CMUPressureSuitTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase("RMCSuitEVAUNMC")]
    [TestCase("RMCSuitEVACivilian")]
    public async Task HelmetSealsTheSuit(string suitId)
    {
        var map = await Pair.CreateTestMap();
        EntityUid mob = default;
        EntityUid suit = default;

        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            mob = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.AddComponents(mob, SProtoMan.Index<EntityPrototype>("CMUMapBarotraumaTemplate"), removeExisting: false);
            suit = SEntMan.SpawnEntity(suitId, map.GridCoords);
            Assert.That(inventory.TryEquip(mob, suit, "outerClothing", silent: true, force: true), Is.True, "suit");

            var toggle = SEntMan.GetComponent<ToggleableClothingComponent>(suit);
            var ev = new ToggleClothingEvent { Performer = mob };
            SEntMan.EventBus.RaiseLocalEvent(suit, ev);
            Assert.That(inventory.TryGetSlotEntity(mob, "head", out var head), Is.True, "the helmet didn't go up");
            Assert.That(head, Is.EqualTo(toggle.ClothingUid));
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var internals = SEntMan.GetComponent<InternalsComponent>(mob);
            Assert.That(internals.BreathTools, Is.Not.Empty, "the helmet isn't a breath tool");
            Assert.That(SEntMan.System<SharedInternalsSystem>().FindBestGasTank(mob), Is.Not.Null, "no air supply found");

            var barotrauma = SEntMan.GetComponent<BarotraumaComponent>(mob);
            var felt = SEntMan.System<BarotraumaSystem>().GetFeltLowPressure(mob, barotrauma, 0);
            Assert.That(felt, Is.GreaterThan(Content.Shared.Atmos.Atmospherics.WarningLowPressure), "suit doesn't hold vacuum out");
        });
    }

    [Test]
    public async Task BreathMaskConnects()
    {
        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            var mob = SEntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var mask = SEntMan.SpawnEntity("RMCMaskBreath", MapCoordinates.Nullspace);
            Assert.That(inventory.TryEquip(mob, mask, "mask", silent: true, force: true), Is.True);
            Assert.That(SEntMan.GetComponent<InternalsComponent>(mob).BreathTools, Does.Contain(mask));
        });
    }
}
