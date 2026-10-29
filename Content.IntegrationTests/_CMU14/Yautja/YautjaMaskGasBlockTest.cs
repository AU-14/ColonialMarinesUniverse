using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.ChemicalIrritants;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

// cmss13 BLOCKGASEFFECT on the clan mask: chem smoke and nerve gas never reach a masked pred
[TestFixture]
public sealed class YautjaMaskGasBlockTest : GameTest
{
    private const string MaskSlot = "mask";
    private static readonly ProtoId<ReagentPrototype> TestReagent = "YautjaGasBlockTestReagent";

    [TestPrototypes]
    private const string Prototypes = """
- type: reagent
  id: YautjaGasBlockTestReagent
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing
""";

    [Test]
    public async Task ClanMaskKeepsChemSmokeOutOfTheBlood()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var smokes = Server.System<SmokeSystem>();
            var hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var mask = EnsureMask(hunter);

            var smoke = SEntMan.SpawnEntity("Smoke", map.GridCoords);
            smokes.StartSmoke(smoke, new Solution(TestReagent.Id, FixedPoint2.New(50)), duration: 10, spreadAmount: 0);

            smokes.SmokeReact(hunter, smoke);
            Assert.That(BloodReagent(hunter), Is.EqualTo(FixedPoint2.Zero),
                "chem smoke got into a pred wearing the clan mask");

            // same pred, same cloud, mask off: the smoke has to land or the check above proves nothing
            Assert.That(Server.System<InventorySystem>().TryUnequip(hunter, MaskSlot, silent: true, force: true), Is.True);
            smokes.SmokeReact(hunter, smoke);
            Assert.That(BloodReagent(hunter), Is.GreaterThan(FixedPoint2.Zero),
                "chem smoke should reach an unmasked pred");

            SEntMan.DeleteEntity(smoke);
            SEntMan.DeleteEntity(mask);
            SEntMan.DeleteEntity(hunter);
        });
    }

    [Test]
    public async Task ClanMaskKeepsNerveGasOut()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var irritants = Server.System<SharedChemicalIrritantSystem>();
            var hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var mask = EnsureMask(hunter);

            irritants.ApplyIrritantDirect(hunter, 50, new ChemicalIrritantProfile());
            Assert.That(SEntMan.HasComponent<ChemicalIrritantComponent>(hunter), Is.False,
                "nerve gas got through the clan mask");

            Assert.That(Server.System<InventorySystem>().TryUnequip(hunter, MaskSlot, silent: true, force: true), Is.True);
            irritants.ApplyIrritantDirect(hunter, 50, new ChemicalIrritantProfile());
            Assert.That(SEntMan.HasComponent<ChemicalIrritantComponent>(hunter), Is.True,
                "nerve gas should reach an unmasked pred");

            SEntMan.DeleteEntity(mask);
            SEntMan.DeleteEntity(hunter);
        });
    }

    private EntityUid EnsureMask(EntityUid hunter)
    {
        var inventory = Server.System<InventorySystem>();
        if (inventory.TryGetSlotEntity(hunter, MaskSlot, out var worn))
            inventory.TryUnequip(hunter, MaskSlot, silent: true, force: true);

        if (worn != null)
            SEntMan.DeleteEntity(worn.Value);

        // the plain clan mask comes without a filter, so only the gas block can stop anything here
        var mask = SEntMan.SpawnEntity("CMUYautjaMask", SEntMan.GetComponent<TransformComponent>(hunter).Coordinates);
        Assert.That(inventory.TryEquip(hunter, mask, MaskSlot, silent: true, force: true), Is.True);
        return mask;
    }

    private FixedPoint2 BloodReagent(EntityUid hunter)
    {
        var bloodstream = SEntMan.GetComponent<BloodstreamComponent>(hunter);
        var solutions = Server.System<SharedSolutionContainerSystem>();
        Assert.That(solutions.TryGetSolution(hunter, bloodstream.BloodSolutionName, out _, out var blood), Is.True);
        return blood!.GetTotalPrototypeQuantity(TestReagent);
    }
}
