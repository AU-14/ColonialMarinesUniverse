using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Power.Components;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Access.Components;
using Content.Shared.Construction.Components;
using Content.Shared.VendingMachines;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.HunterShip;

[TestFixture]
public sealed class HunterShipYautjaVendingTest
{
    [Test]
    public async Task HunterShipExactVendorWrappersExposeRequiredVendingLayers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;

            foreach (var row in ExactVendorWrapperRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);

                Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, row.Id);
                Assert.That(sprite!.LayerMapTryGet(VendingMachineVisualLayers.Base, out var baseLayer), Is.True,
                    $"{row.Id} must expose the Base layer required by VendingMachineSystem.");
                Assert.That(sprite.LayerMapTryGet(VendingMachineVisualLayers.BaseUnshaded, out var activeLayer), Is.True,
                    $"{row.Id} must expose the BaseUnshaded layer required by VendingMachineSystem.");
                Assert.That(sprite.AllLayers.ElementAt(baseLayer).RsiState.Name, Is.EqualTo(row.BaseState), row.Id);
                Assert.That(sprite.AllLayers.ElementAt(activeLayer).RsiState.Name, Is.EqualTo(row.NormalState), row.Id);

                Assert.That(prototype.TryGetComponent<VendingMachineComponent>(out var vending, factory), Is.True, row.Id);
                Assert.Multiple(() =>
                {
                    Assert.That(vending!.OffState, Is.EqualTo(row.BaseState), row.Id);
                    Assert.That(vending.BrokenState, Is.EqualTo(row.BaseState), row.Id);
                    Assert.That(vending.NormalState, Is.EqualTo(row.NormalState), row.Id);
                    Assert.That(vending.DenyState, Is.EqualTo(row.DenyState), row.Id);
                    Assert.That(vending.EjectState, Is.EqualTo(row.NormalState), row.Id);
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipYautjaUtilityVendorsMatchCmss13VendingTypes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var server = pair.Server;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;
            var yautjaMachines = new ResPath("/Textures/_CMU14/HunterShip/obj/structures/machinery/yautja_machines.rsi");

            foreach (var row in VendorRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);

                Assert.That(prototype.Parents, Does.Contain(row.Parent),
                    $"{row.Id} maps CMSS13 {row.SourcePath} from vending_types.dm to a Yautja-specific source-backed parent.");
                Assert.That(prototype.Name, Is.EqualTo(row.Name), row.Id);
                Assert.That(prototype.Description, Is.EqualTo(row.Description), row.Id);

                Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, row.Id);
                Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(yautjaMachines), row.Id);
                Assert.That(sprite.DrawDepth, Is.EqualTo((int) Content.Shared.DrawDepth.DrawDepth.SmallObjects), row.Id);
                Assert.That(sprite.NoRotation, Is.True, row.Id);
                Assert.That(sprite.EnableDirectionOverride, Is.True, row.Id);
                Assert.That(sprite.DirectionOverride, Is.EqualTo(Direction.South), row.Id);
                Assert.That(sprite.Offset, Is.EqualTo(row.Offset), row.Id);

                var states = sprite.AllLayers.Select(layer => layer.RsiState.Name).ToArray();
                Assert.That(states, Is.EqualTo(row.SpriteStates), row.Id);

                Assert.That(prototype.TryGetComponent<IconComponent>(out var icon, factory), Is.True, row.Id);
                var rsiIcon = (SpriteSpecifier.Rsi) icon!.Icon;
                Assert.That(rsiIcon.RsiPath.ToString(), Does.EndWith(yautjaMachines.ToString().Replace("/Textures/", string.Empty)), row.Id);
                Assert.That(rsiIcon.RsiState, Is.EqualTo(row.IconState), row.Id);

                Assert.That(prototype.TryGetComponent<VendingMachineComponent>(out var vending, factory), Is.True, row.Id);
                Assert.That(vending!.PackPrototypeId, Is.EqualTo(row.Pack), row.Id);

                Assert.That(prototype.TryGetComponent<CMAutomatedVendorComponent>(out _, factory), Is.False,
                    $"{row.Id} must not inherit the broader local RMC automated vendor stock.");
                Assert.That(prototype.TryGetComponent<AccessReaderComponent>(out _, factory), Is.False,
                    $"{row.Id} maps CMSS13 checking_id() return FALSE.");
            }
        });

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            foreach (var row in VendorRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);

                Assert.That(prototype.TryGetComponent<PhysicsComponent>(out var physics, factory), Is.True, row.Id);
                Assert.That(physics!.BodyType, Is.EqualTo(BodyType.Static), row.Id);

                Assert.That(prototype.TryGetComponent<FixturesComponent>(out var fixtures, factory), Is.True, row.Id);
                Assert.That(fixtures!.Fixtures.Values.Any(fixture => fixture.Hard), Is.True, row.Id);

                Assert.That(prototype.TryGetComponent<VendingMachineComponent>(out var vending, factory), Is.True, row.Id);
                Assert.That(vending!.PackPrototypeId, Is.EqualTo(row.Pack), row.Id);

                var inventory = prototypes.Index<VendingMachineInventoryPrototype>(row.Pack);
                Assert.That(inventory.StartingInventory, Is.EqualTo(row.StartingInventory), row.Id);
                Assert.That(inventory.ContrabandInventory ?? new Dictionary<string, uint>(), Is.EqualTo(row.ContrabandInventory), row.Id);
                Assert.That(inventory.EmaggedInventory ?? new Dictionary<string, uint>(), Is.Empty, row.Id);
            }

            var dinnerware = prototypes.Index<EntityPrototype>(DinnerwareId);
            Assert.That(dinnerware.TryGetComponent<ApcPowerReceiverComponent>(out var power, factory), Is.True, DinnerwareId);
            Assert.That(power!.NeedsPower, Is.False, DinnerwareId);
            Assert.That(dinnerware.TryGetComponent<TransformComponent>(out var transform, factory), Is.True, DinnerwareId);
            Assert.That(transform!.Anchored, Is.True, DinnerwareId);
            Assert.That(dinnerware.TryGetComponent<AnchorableComponent>(out var anchorable, factory), Is.True, DinnerwareId);
            Assert.That(anchorable!.Flags, Is.EqualTo(AnchorableFlags.None), DinnerwareId);
        });

        await pair.CleanReturnAsync();
    }

    private const string DinnerwareId = "CMUHunterShipPlacedCMUHunterShipYautjaVendorDinnerwareDinnerwareSouthVariant02";

    private static ExactVendorWrapperRow[] ExactVendorWrapperRows()
    {
        return
        [
            new ExactVendorWrapperRow(
                "CMUHunterShipPlacedCMUHunterShipYautjaVendorDinnerwareDinnerwareSouthVariant02",
                "dinnerware",
                "dinnerware-vend",
                "dinnerware-deny"),
            new ExactVendorWrapperRow(
                "CMUHunterShipPlacedCMUHunterShipYautjaVendorNutriNutriSouthOffsetNeg3xNeg1",
                "nutri",
                "nutri",
                "nutri_deny"),
            new ExactVendorWrapperRow(
                "CMUHunterShipPlacedCMUHunterShipYautjaVendorSeedsSeedsSouthOffset3xNeg1",
                "seeds",
                "seeds",
                "seeds"),
        ];
    }

    private static VendorRow[] VendorRows()
    {
        return
        [
            new VendorRow(
                "CMUHunterShipPlacedCMUHunterShipYautjaVendorDinnerwareDinnerwareSouthVariant02",
                "CMUHunterShipYautjaVendorDinnerware",
                "/obj/structure/machinery/vending/dinnerware/yautja",
                "Dinnerplate dispenser",
                "A kitchen and restaurant equipment vendor.",
                "CMUHunterShipYautjaDinnerware",
                Vector2.Zero,
                "dinnerware",
                ["dinnerware", "dinnerware-vend"],
                new Dictionary<string, uint>
                {
                    ["FoodPlateTin"] = 8,
                    ["RMCFork"] = 6,
                    ["RMCKitchenKnifeChef"] = 3,
                    ["DrinkGlass"] = 8,
                    ["ClothingOuterJacketChef"] = 2,
                    ["RMCCondimentSmallSalt"] = 4,
                    ["RMCCondimentSmallPepper"] = 4,
                    ["RMCCondimentEnzyme"] = 1,
                    ["RMCCondiment"] = 8,
                },
                new Dictionary<string, uint>
                {
                    ["RMCSpoon"] = 2,
                    ["RMCKitchenKnife"] = 2,
                    ["RMCRollingPin"] = 2,
                    ["RMCKitchenKnifeButcher"] = 2,
                }),
            new VendorRow(
                "CMUHunterShipPlacedCMUHunterShipYautjaVendorNutriNutriSouthOffsetNeg3xNeg1",
                "CMUHunterShipYautjaVendorNutri",
                "/obj/structure/machinery/vending/hydronutrients/yautja",
                "Nutrient Servitor",
                "A plant nutrients vendor.",
                "CMUHunterShipYautjaNutri",
                new Vector2(-0.0938f, -0.0313f),
                "nutri",
                ["nutri", "nutri"],
                new Dictionary<string, uint>
                {
                    ["ChemistryBottleEZNutrient"] = 35,
                    ["ChemistryBottleLeft4Zed"] = 25,
                    ["ChemistryBottleRobustHarvest"] = 15,
                    ["PestSpray"] = 20,
                    ["Syringe"] = 5,
                    ["RMCStoragePlantBag"] = 5,
                },
                new Dictionary<string, uint>()),
            new VendorRow(
                "CMUHunterShipPlacedCMUHunterShipYautjaVendorSeedsSeedsSouthOffset3xNeg1",
                "CMUHunterShipYautjaVendorSeeds",
                "/obj/structure/machinery/vending/hydroseeds/yautja",
                "Seed Servitor",
                "A plant seeds vendor.",
                "CMUHunterShipYautjaSeeds",
                new Vector2(0.0938f, -0.0313f),
                "seeds",
                ["seeds", "seeds"],
                new Dictionary<string, uint>
                {
                    ["BananaSeeds"] = 3,
                    ["BerrySeeds"] = 3,
                    ["CarrotSeeds"] = 3,
                    ["ChanterelleSeeds"] = 2,
                    ["ChiliSeeds"] = 3,
                    ["CornSeeds"] = 3,
                    ["EggplantSeeds"] = 3,
                    ["PotatoSeeds"] = 3,
                    ["SoybeanSeeds"] = 3,
                    ["SunflowerSeeds"] = 2,
                    ["TomatoSeeds"] = 3,
                    ["WheatSeeds"] = 3,
                    ["AppleSeeds"] = 3,
                    ["PoppySeeds"] = 3,
                    ["SugarcaneSeeds"] = 3,
                    ["PeanutSeeds"] = 3,
                    ["WhiteBeetSeeds"] = 3,
                    ["WatermelonSeeds"] = 3,
                    ["LimeSeeds"] = 3,
                    ["LemonSeeds"] = 3,
                    ["OrangeSeeds"] = 3,
                    ["GrassSeeds"] = 3,
                    ["CocoaSeeds"] = 3,
                    ["PlumpSeeds"] = 2,
                    ["CabbageSeeds"] = 3,
                    ["GrapeSeeds"] = 3,
                    ["PumpkinSeeds"] = 3,
                    ["CherrySeeds"] = 3,
                    ["RiceSeeds"] = 3,
                    ["LingzhiSeeds"] = 3,
                },
                new Dictionary<string, uint>
                {
                    ["AmbrosiaVulgarisSeeds"] = 1,
                    ["NettleSeeds"] = 1,
                }),
        ];
    }

    private sealed record VendorRow(
        string Id,
        string Parent,
        string SourcePath,
        string Name,
        string Description,
        string Pack,
        Vector2 Offset,
        string IconState,
        string[] SpriteStates,
        Dictionary<string, uint> StartingInventory,
        Dictionary<string, uint> ContrabandInventory);

    private sealed record ExactVendorWrapperRow(
        string Id,
        string BaseState,
        string NormalState,
        string DenyState);
}
