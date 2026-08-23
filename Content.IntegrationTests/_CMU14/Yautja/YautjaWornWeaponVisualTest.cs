using System.Linq;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaWornWeaponVisualTest
{
    private static readonly (string Id, string Sprite, string State)[] BackWeapons =
    [
        ("CMUYautjaClanSword", "_CMU14/Yautja/pred_gear_worn.rsi", "clansword"),
        ("CMUYautjaRendingSword", "_CMU14/Yautja/pred_gear_custom_worn.rsi", "rending_sword"),
        ("CMUYautjaPiercingSword", "_CMU14/Yautja/pred_gear_custom_worn.rsi", "piercing_sword"),
        ("CMUYautjaSeveringSword", "_CMU14/Yautja/pred_gear_worn.rsi", "clansword_alt3"),
        ("CMUYautjaDualWarScythe", "_CMU14/Yautja/pred_gear_custom_worn.rsi", "dual_war_scythe"),
        ("CMUYautjaDoubleWarScythe", "_CMU14/Yautja/pred_gear_custom_worn.rsi", "double_war_scythe"),
        ("CMUYautjaCruelStaff", "_CMU14/Yautja/pred_gear_custom_worn.rsi", "cruel_staff"),
        ("CMUYautjaCombistick", "_CMU14/Yautja/pred_gear_worn.rsi", "combistick"),
        ("CMUYautjaWarAxe", "_CMU14/Yautja/pred_gear_worn.rsi", "war_axe"),
        ("CMUYautjaClanShield", "_CMU14/Yautja/pred_gear_worn.rsi", "shield"),
        ("CMUYautjaAncientShield", "_CMU14/Yautja/pred_gear_worn.rsi", "ancient_shield"),
        ("CMUYautjaAncientShieldAlt", "_CMU14/Yautja/pred_gear_worn.rsi", "ancient_shield_alt"),
        ("CMUYautjaAncientShieldTemple", "_CMU14/Yautja/pred_gear_custom_worn.rsi", "ancient_shield_temple"),
        ("CMUYautjaHunterSpear", "_CMU14/Yautja/pred_gear_worn.rsi", "spearhunter"),
        ("CMUYautjaWarGlaive", "_CMU14/Yautja/pred_gear_worn.rsi", "glaive"),
        ("CMUYautjaCleavingGlaive", "_CMU14/Yautja/pred_gear_worn.rsi", "glaive_skull"),
        ("CMUYautjaAncientWarGlaive", "_CMU14/Yautja/pred_gear_worn.rsi", "glaive_alt"),
        ("CMUYautjaLongaxe", "_CMU14/Yautja/pred_gear_worn.rsi", "longaxe"),
    ];

    private static readonly string[] HeldWeapons =
    [
        "CMUYautjaHarpoon",
        "CMUYautjaChainwhip",
        "CMUYautjaClanSword",
        "CMUYautjaRendingSword",
        "CMUYautjaPiercingSword",
        "CMUYautjaSeveringSword",
        "CMUYautjaDualWarScythe",
        "CMUYautjaDoubleWarScythe",
        "CMUYautjaCruelStaff",
        "CMUYautjaCombistick",
        "CMUYautjaWarAxe",
        "CMUYautjaCeremonialDagger",
        "CMUYautjaClanShield",
        "CMUYautjaAncientShield",
        "CMUYautjaAncientShieldAlt",
        "CMUYautjaAncientShieldTemple",
        "CMUYautjaHunterSpear",
        "CMUYautjaWarGlaive",
        "CMUYautjaCleavingGlaive",
        "CMUYautjaAncientWarGlaive",
        "CMUYautjaLongaxe",
        "CMUYautjaDuellingBlade",
        "CMUYautjaDuellingClub",
        "CMUYautjaDuellingHatchet",
        "CMUYautjaDuellingKnife",
        "CMUYautjaSpikeLauncher",
        "CMUYautjaPlasmaRifle",
        "CMUYautjaPlasmaCarbine",
        "CMUYautjaHeavyGelDefoliator",
        "CMUYautjaHeavyGelDefoliatorDeathsquad",
        "CMUYautjaPlasmaPistol",
    ];

    private static readonly (string Id, string Prefix)[] BackGuns =
    [
        ("CMUYautjaSpikeLauncher", "spearlauncher"),
        ("CMUYautjaPlasmaRifle", "plasmarifle"),
        ("CMUYautjaPlasmaCarbine", "plasmacarbine"),
        ("CMUYautjaHeavyGelDefoliator", "defoliator"),
        ("CMUYautjaHeavyGelDefoliatorDeathsquad", "defoliator"),
    ];

    [Test]
    public void BackWeaponVisualMappingsAreUnique()
    {
        var mappings = BackWeapons
            .Select(weapon => $"{weapon.Sprite}:{weapon.State}")
            .ToArray();

        Assert.That(mappings.Distinct().Count(), Is.EqualTo(mappings.Length),
            "Every distinct back-slot weapon variant must have its own visual state.");
    }

    [Test]
    public async Task BackWeaponsProvideWornVisualsForBackpackSlot()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var factory = pair.Client.EntMan.ComponentFactory;

            foreach (var (id, expectedSprite, expectedState) in BackWeapons)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.TryGetComponent<ClothingComponent>(out var clothing, factory), Is.True, id);
                Assert.That(clothing!.Slots & SlotFlags.BACK, Is.EqualTo(SlotFlags.BACK),
                    $"{id} must be wearable in the backpack slot.");

#pragma warning disable RA0002
                Assert.That(clothing.ClothingVisuals.TryGetValue("back", out var layers), Is.True,
                    $"{id} must define an explicit backpack visual.");
#pragma warning restore RA0002
                Assert.That(layers, Has.Count.EqualTo(1), $"{id} backpack visual layer count.");
                Assert.That(layers![0].State, Is.EqualTo(expectedState), $"{id} backpack visual state.");
                Assert.That(layers[0].RsiPath, Is.EqualTo(expectedSprite), $"{id} must use the directional worn sprite.");
                Assert.That(layers[0].RenderingStrategy, Is.Not.EqualTo(LayerRenderingStrategy.NoRotation),
                    $"{id} must select the worn sprite for the mob's facing direction.");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WornWeaponRsisContainFourDirectionalStates()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Client.WaitAssertion(() =>
        {
            var cache = pair.Client.ResolveDependency<IResourceCache>();
            var directionalRsi = cache.GetResource<RSIResource>(
                new ResPath("/Textures/_CMU14/Yautja/pred_gear_worn.rsi")).RSI;
            var customRsi = cache.GetResource<RSIResource>(
                new ResPath("/Textures/_CMU14/Yautja/pred_gear_custom_worn.rsi")).RSI;

            foreach (var (_, expectedSprite, stateName) in BackWeapons)
            {
                var resource = expectedSprite.EndsWith("pred_gear_custom_worn.rsi") ? customRsi : directionalRsi;
                Assert.That(resource.TryGetState(stateName, out var state), Is.True, stateName);
                Assert.That(state!.RsiDirections, Is.EqualTo(RsiDirectionType.Dir4), stateName);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AllYautjaWeaponHeldVisualsUseDirectionalFrames()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var factory = pair.Client.EntMan.ComponentFactory;
            var cache = pair.Client.ResolveDependency<IResourceCache>();

            foreach (var id in HeldWeapons)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.TryGetComponent<ItemComponent>(out var item, factory), Is.True, id);
                Assert.That(item!.RsiPath, Is.Not.Null, $"{id} must define an in-hand RSI.");
                Assert.That(item.HeldPrefix, Is.Not.Null, $"{id} must define an in-hand prefix.");

                var rsiPath = SpriteSpecifierSerializer.TextureRoot / new ResPath(item.RsiPath!);
                Assert.That(cache.TryGetResource<RSIResource>(rsiPath, out var resource), Is.True,
                    $"{id} in-hand RSI {rsiPath} should load.");

                foreach (var hand in new[] { "left", "right" })
                {
                    var stateName = $"{item.HeldPrefix}-inhand-{hand}";
                    Assert.That(resource!.RSI.TryGetState(stateName, out var state), Is.True,
                        $"{id} must provide {stateName}.");
                    Assert.That(state!.RsiDirections, Is.EqualTo(RsiDirectionType.Dir4),
                        $"{id} {stateName} must have south/north/east/west frames.");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaBackGunVisualsUseDirectionalFrames()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var factory = pair.Client.EntMan.ComponentFactory;
            var cache = pair.Client.ResolveDependency<IResourceCache>();

            foreach (var (id, expectedPrefix) in BackGuns)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.TryGetComponent<ClothingComponent>(out var clothing, factory), Is.True, id);
                Assert.That(clothing!.Slots & SlotFlags.BACK, Is.EqualTo(SlotFlags.BACK),
                    $"{id} must be wearable on the back.");
                Assert.That(clothing.RsiPath, Is.EqualTo("_CMU14/Yautja/pred_guns_back.rsi"), id);
                Assert.That(clothing.EquippedPrefix, Is.EqualTo(expectedPrefix), id);

                var rsiPath = SpriteSpecifierSerializer.TextureRoot / new ResPath(clothing.RsiPath!);
                Assert.That(cache.TryGetResource<RSIResource>(rsiPath, out var resource), Is.True,
                    $"{id} back RSI {rsiPath} should load.");
                var stateName = $"{expectedPrefix}-equipped-BACKPACK";
                Assert.That(resource!.RSI.TryGetState(stateName, out var state), Is.True,
                    $"{id} must provide {stateName}.");
                Assert.That(state!.RsiDirections, Is.EqualTo(RsiDirectionType.Dir4),
                    $"{id} {stateName} must have south/north/east/west frames.");
            }
        });

        await pair.CleanReturnAsync();
    }
}
