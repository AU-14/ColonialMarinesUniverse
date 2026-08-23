using System.Linq;
using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Shared.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaPostVendorHookTest
{
    private static readonly Color ProfileCapeColor = new((byte) 0x2a, (byte) 0x5c, (byte) 0x8a);

    [Test]
    public async Task RegularArmorBundlePostVendorHookAppliesLegacyAndUniquePrefsAcrossSourceRacks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var rackCases = new[]
            {
                "CMUYautjaLoadoutVendor",
                "CMUYautjaElderLoadoutVendor",
                "CMUYautjaYoungbloodLoadoutVendor",
            };

            var profileCases = new[]
            {
                new ArmorProfileCase(
                    "legacy dragon",
                    YautjaCharacterProfile.Default
                        .WithLegacy(YautjaLegacySet.Dragon)
                        .WithMask(YautjaGearMaterial.Bone, 12)
                        .WithMaskAccessory(2),
                    "CMUYautjaArmorLegacyDragon",
                    "CMUYautjaMaskLegacyDragon",
                    "CMUYautjaGreavesLegacyDragon",
                    "CMUYautjaMaskAccessory02Bone"),
                new ArmorProfileCase(
                    "unique ronin",
                    YautjaCharacterProfile.Default
                        .WithUnique(YautjaUniqueSet.Ronin)
                        .WithMask(YautjaGearMaterial.Bone, 12)
                        .WithMaskAccessory(2),
                    "CMUYautjaArmorUniqueRonin",
                    "CMUYautjaMaskUniqueRonin",
                    "CMUYautjaGreavesUniqueRonin",
                    "CMUYautjaMaskAccessory02Bone"),
            };

            var offset = 0;
            foreach (var rackPrototype in rackCases)
            {
                foreach (var profileCase in profileCases)
                {
                    var rack = entMan.SpawnEntity(rackPrototype, map.GridCoords.Offset(new Vector2(offset++, 0)));
                    var user = SpawnProfileUser(entMan, map.GridCoords.Offset(new Vector2(offset++, 0)), profileCase.Profile);

                    try
                    {
                        ClearSlot(entMan, inventory, user, "outerClothing");
                        ClearSlot(entMan, inventory, user, "mask");
                        ClearSlot(entMan, inventory, user, "shoes");

                        VendEntry(entMan, rack, user, "Essential Hunting Supplies", "CMUYautjaArmorBundle");

                        AssertEquippedPrototype(entMan, inventory, user, "outerClothing", profileCase.ArmorPrototype);
                        AssertEquippedPrototype(entMan, inventory, user, "mask", profileCase.MaskPrototype);
                        AssertEquippedPrototype(entMan, inventory, user, "shoes", profileCase.GreavesPrototype);
                        AssertMaskAccessory(entMan, inventory, user, profileCase.MaskAccessoryPrototype);
                    }
                    finally
                    {
                        DeleteIfAlive(entMan, rack);
                        DeleteIfAlive(entMan, user);
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RegularArmorBundlePostVendorHookCoversEveryCmss13PreferenceBranchAcrossSourceRacks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var rackCases = new[]
            {
                "CMUYautjaLoadoutVendor",
                "CMUYautjaElderLoadoutVendor",
                "CMUYautjaYoungbloodLoadoutVendor",
            };

            var profileCases = new[]
            {
                new ArmorProfileCase(
                    "default style/material",
                    YautjaCharacterProfile.Default
                        .WithArmor(YautjaGearMaterial.Bronze, 3)
                        .WithMask(YautjaGearMaterial.Bone, 12)
                        .WithMaskAccessory(2)
                        .WithGreaves(YautjaGearMaterial.Silver, 2),
                    "CMUYautjaClanArmorBronze3",
                    "CMUYautjaMaskPred12Bone",
                    "CMUYautjaClanGreavesSilver2",
                    "CMUYautjaMaskAccessory02Bone"),
                new ArmorProfileCase(
                    "legacy dragon",
                    YautjaCharacterProfile.Default
                        .WithLegacy(YautjaLegacySet.Dragon)
                        .WithMask(YautjaGearMaterial.Bone, 12)
                        .WithMaskAccessory(2),
                    "CMUYautjaArmorLegacyDragon",
                    "CMUYautjaMaskLegacyDragon",
                    "CMUYautjaGreavesLegacyDragon",
                    "CMUYautjaMaskAccessory02Bone"),
                new ArmorProfileCase(
                    "legacy swamp",
                    YautjaCharacterProfile.Default
                        .WithLegacy(YautjaLegacySet.Swamp)
                        .WithMask(YautjaGearMaterial.Bone, 12)
                        .WithMaskAccessory(2),
                    "CMUYautjaArmorLegacySwamp",
                    "CMUYautjaMaskLegacySwamp",
                    "CMUYautjaGreavesLegacySwamp",
                    "CMUYautjaMaskAccessory02Bone"),
                new ArmorProfileCase(
                    "legacy enforcer",
                    YautjaCharacterProfile.Default
                        .WithLegacy(YautjaLegacySet.Enforcer)
                        .WithMask(YautjaGearMaterial.Bone, 12)
                        .WithMaskAccessory(2),
                    "CMUYautjaArmorLegacyEnforcer",
                    "CMUYautjaMaskLegacyEnforcer",
                    "CMUYautjaGreavesLegacyEnforcer",
                    "CMUYautjaMaskAccessory02Bone"),
                new ArmorProfileCase(
                    "legacy collector",
                    YautjaCharacterProfile.Default
                        .WithLegacy(YautjaLegacySet.Collector)
                        .WithMask(YautjaGearMaterial.Bone, 12)
                        .WithMaskAccessory(2),
                    "CMUYautjaArmorLegacyCollector",
                    "CMUYautjaMaskLegacyCollector",
                    "CMUYautjaGreavesLegacyCollector",
                    "CMUYautjaMaskAccessory02Bone"),
                new ArmorProfileCase(
                    "unique anubys",
                    YautjaCharacterProfile.Default
                        .WithUnique(YautjaUniqueSet.Anubys)
                        .WithMask(YautjaGearMaterial.Bone, 12)
                        .WithMaskAccessory(2),
                    "CMUYautjaArmorUniqueAnubys",
                    "CMUYautjaMaskUniqueAnubys",
                    "CMUYautjaGreavesUniqueAnubys",
                    "CMUYautjaMaskAccessory02Bone"),
                new ArmorProfileCase(
                    "unique cleopatra",
                    YautjaCharacterProfile.Default
                        .WithUnique(YautjaUniqueSet.Cleopatra)
                        .WithMask(YautjaGearMaterial.Bone, 12)
                        .WithMaskAccessory(2),
                    "CMUYautjaArmorUniqueCleopatra",
                    "CMUYautjaMaskUniqueCleopatra",
                    "CMUYautjaGreavesUniqueCleopatra",
                    "CMUYautjaMaskAccessory02Bone"),
                new ArmorProfileCase(
                    "unique plated",
                    YautjaCharacterProfile.Default
                        .WithUnique(YautjaUniqueSet.Plated)
                        .WithMask(YautjaGearMaterial.Bone, 12)
                        .WithMaskAccessory(2),
                    "CMUYautjaArmorUniquePlated",
                    "CMUYautjaMaskUniquePlated",
                    "CMUYautjaGreavesUniquePlated",
                    "CMUYautjaMaskAccessory02Bone"),
                new ArmorProfileCase(
                    "unique ronin",
                    YautjaCharacterProfile.Default
                        .WithUnique(YautjaUniqueSet.Ronin)
                        .WithMask(YautjaGearMaterial.Bone, 12)
                        .WithMaskAccessory(2),
                    "CMUYautjaArmorUniqueRonin",
                    "CMUYautjaMaskUniqueRonin",
                    "CMUYautjaGreavesUniqueRonin",
                    "CMUYautjaMaskAccessory02Bone"),
                new ArmorProfileCase(
                    "legacy wins over unique",
                    YautjaCharacterProfile.Default
                        .WithLegacy(YautjaLegacySet.Swamp)
                        .WithUnique(YautjaUniqueSet.Ronin)
                        .WithMask(YautjaGearMaterial.Bone, 12)
                        .WithMaskAccessory(2),
                    "CMUYautjaArmorLegacySwamp",
                    "CMUYautjaMaskLegacySwamp",
                    "CMUYautjaGreavesLegacySwamp",
                    "CMUYautjaMaskAccessory02Bone"),
            };

            var offset = 0;
            foreach (var rackPrototype in rackCases)
            {
                foreach (var profileCase in profileCases)
                {
                    var rack = entMan.SpawnEntity(rackPrototype, map.GridCoords.Offset(new Vector2(offset++, 0)));
                    var user = SpawnProfileUser(entMan, map.GridCoords.Offset(new Vector2(offset++, 0)), profileCase.Profile);

                    try
                    {
                        ClearSlot(entMan, inventory, user, "outerClothing");
                        ClearSlot(entMan, inventory, user, "mask");
                        ClearSlot(entMan, inventory, user, "shoes");

                        VendEntry(entMan, rack, user, "Essential Hunting Supplies", "CMUYautjaArmorBundle");

                        AssertEquippedPrototype(entMan, inventory, user, "outerClothing", profileCase.ArmorPrototype);
                        AssertEquippedPrototype(entMan, inventory, user, "mask", profileCase.MaskPrototype);
                        AssertEquippedPrototype(entMan, inventory, user, "shoes", profileCase.GreavesPrototype);
                        AssertMaskAccessory(entMan, inventory, user, profileCase.MaskAccessoryPrototype);
                    }
                    finally
                    {
                        DeleteIfAlive(entMan, rack);
                        DeleteIfAlive(entMan, user);
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeavyArmorPostVendorHookAppliesProfileMaterialAcrossAdultElderStrandedAndBadBloodRacks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var racks = new[]
            {
                "CMUYautjaLoadoutVendor",
                "CMUYautjaElderLoadoutVendor",
                "CMUYautjaStrandedLoadoutVendor",
                "CMUYautjaBadBloodLoadoutVendor",
            };

            var yautja = YautjaCharacterProfile.Default
                .WithArmor(YautjaGearMaterial.Bronze, 3);

            for (var i = 0; i < racks.Length; i++)
            {
                var rack = entMan.SpawnEntity(racks[i], map.GridCoords.Offset(new Vector2(i * 2, 0)));
                var user = SpawnProfileUser(entMan, map.GridCoords.Offset(new Vector2(i * 2 + 1, 0)), yautja);

                try
                {
                    ClearSlot(entMan, inventory, user, "outerClothing");

                    VendEntry(entMan, rack, user, "Support Equipment (CHOOSE 2)", "CMUYautjaHeavyClanArmor");

                    AssertEquippedPrototype(entMan, inventory, user, "outerClothing", "CMUYautjaHeavyClanArmor");
                    AssertEquippedVisualsMatchPrototype(entMan, inventory, user, "outerClothing", "CMUYautjaHeavyClanArmorBronze");
                }
                finally
                {
                    DeleteIfAlive(entMan, rack);
                    DeleteIfAlive(entMan, user);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CapePostVendorHookKeepsSelectedSubtypeAndUsesDefaultColorAcrossSourceCapeRacks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var yautja = YautjaCharacterProfile.Default
                .WithCapeColor(ProfileCapeColor);

            var capeCases = new[]
            {
                new CapeRackCase("CMUYautjaLoadoutVendor", new[] { "CMUYautjaCapeQuarter", "CMUYautjaCapeThird", "CMUYautjaCapeHalf", "CMUYautjaCapePoncho" }),
                new CapeRackCase("CMUYautjaElderLoadoutVendor", new[] { "CMUYautjaCapeQuarter", "CMUYautjaCapeThird", "CMUYautjaCapeHalf", "CMUYautjaCapePoncho", "CMUYautjaCapeDamaged", "CMUYautjaCapeFull", "CMUYautjaCapeCeremonial" }),
                new CapeRackCase("CMUYautjaStrandedLoadoutVendor", new[] { "CMUYautjaCapeQuarter", "CMUYautjaCapeThird", "CMUYautjaCapeHalf", "CMUYautjaCapePoncho", "CMUYautjaCapeDamaged", "CMUYautjaCapeFull" }),
                new CapeRackCase("CMUYautjaBadBloodLoadoutVendor", new[] { "CMUYautjaCapeQuarter", "CMUYautjaCapeThird", "CMUYautjaCapeHalf", "CMUYautjaCapePoncho", "CMUYautjaCapeDamaged", "CMUYautjaCapeFull" }),
                new CapeRackCase("CMUYautjaBloodedThrallLoadoutVendor", new[] { "CMUYautjaCapeQuarter", "CMUYautjaCapeThird", "CMUYautjaCapeHalf", "CMUYautjaCapePoncho" }),
            };

            var offset = 0;
            foreach (var capeCase in capeCases)
            {
                foreach (var capePrototype in capeCase.CapePrototypes)
                {
                    var rack = entMan.SpawnEntity(capeCase.RackPrototype, map.GridCoords.Offset(new Vector2(offset++, 0)));
                    var user = SpawnProfileUser(entMan, map.GridCoords.Offset(new Vector2(offset++, 0)), yautja);

                    try
                    {
                        ClearSlot(entMan, inventory, user, "back");

                        VendEntry(entMan, rack, user, "Clothing Accessory (CHOOSE 1)", capePrototype);

                        AssertEquippedPrototype(entMan, inventory, user, "back", capePrototype);
                        Assert.That(inventory.TryGetSlotEntity(user, "back", out var cape), Is.True, $"{capePrototype} equipped");
                        Assert.That(entMan.GetComponent<YautjaCapeComponent>(cape.Value).Color,
                            Is.EqualTo(YautjaCharacterProfile.Default.CapeColor), capePrototype);
                    }
                    finally
                    {
                        DeleteIfAlive(entMan, rack);
                        DeleteIfAlive(entMan, user);
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodArmorSetPostVendorHooksIgnoreProfileForEverySourceArmorBundle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var yautja = YautjaCharacterProfile.Default
                .WithUnique(YautjaUniqueSet.Ronin)
                .WithArmor(YautjaGearMaterial.Bronze, 3)
                .WithMask(YautjaGearMaterial.Bone, 12)
                .WithGreaves(YautjaGearMaterial.Silver, 2);

            var armorCases = new[]
            {
                new BadBloodArmorCase("CMUYautjaBadBloodArmorPatchworkBundle", "CMUYautjaBadBloodArmorPatchwork", "CMUYautjaMaskBadBloodPatchwork", "CMUYautjaBadBloodGreavesPatchwork"),
                new BadBloodArmorCase("CMUYautjaBadBloodArmorPatchworkAltBundle", "CMUYautjaBadBloodArmorPatchworkAlt", "CMUYautjaMaskBadBloodPatchworkAlt", "CMUYautjaBadBloodGreavesPatchworkAlt"),
                new BadBloodArmorCase("CMUYautjaBadBloodArmorLunaticBundle", "CMUYautjaBadBloodArmorLunatic", "CMUYautjaMaskBadBloodLunatic", "CMUYautjaBadBloodGreavesLunatic"),
                new BadBloodArmorCase("CMUYautjaBadBloodArmorScavengerBundle", "CMUYautjaBadBloodArmorScavenger", "CMUYautjaMaskBadBloodScav", "CMUYautjaBadBloodGreavesScavenger"),
                new BadBloodArmorCase("CMUYautjaBadBloodArmorScavengerAltBundle", "CMUYautjaBadBloodArmorScavengerAlt", "CMUYautjaMaskBadBloodScavAlt", "CMUYautjaBadBloodGreavesScavengerAlt"),
                new BadBloodArmorCase("CMUYautjaBadBloodArmorVenatorBundle", "CMUYautjaBadBloodArmorVenator", "CMUYautjaMaskBadBloodVenator", "CMUYautjaBadBloodGreavesVenator"),
                new BadBloodArmorCase("CMUYautjaBadBloodArmorCommandoBundle", "CMUYautjaBadBloodArmorCommando", "CMUYautjaMaskBadBloodCommando", "CMUYautjaBadBloodGreavesCommando"),
                new BadBloodArmorCase("CMUYautjaBadBloodArmorCommandoAltBundle", "CMUYautjaBadBloodArmorCommandoAlt", "CMUYautjaMaskBadBloodCommandoAlt", "CMUYautjaBadBloodGreavesCommandoAlt"),
                new BadBloodArmorCase("CMUYautjaBadBloodArmorEmissaryBundle", "CMUYautjaEmissaryArmorCamoConforming", "CMUYautjaMaskBadBloodEmissaryClassic", "CMUYautjaEmissaryGreavesCamoConforming"),
            };

            for (var i = 0; i < armorCases.Length; i++)
            {
                var armorCase = armorCases[i];
                var rack = entMan.SpawnEntity("CMUYautjaBadBloodLoadoutVendor", map.GridCoords.Offset(new Vector2(i * 2, 0)));
                var user = SpawnProfileUser(entMan, map.GridCoords.Offset(new Vector2(i * 2 + 1, 0)), yautja);

                try
                {
                    ClearSlot(entMan, inventory, user, "outerClothing");
                    ClearSlot(entMan, inventory, user, "mask");
                    ClearSlot(entMan, inventory, user, "shoes");

                    VendEntry(entMan, rack, user, "Armor Set", armorCase.BundlePrototype);

                    AssertEquippedPrototype(entMan, inventory, user, "outerClothing", armorCase.ArmorPrototype);
                    AssertEquippedPrototype(entMan, inventory, user, "mask", armorCase.MaskPrototype);
                    AssertEquippedPrototype(entMan, inventory, user, "shoes", armorCase.GreavesPrototype);
                    AssertEquippedVisualsMatchPrototype(entMan, inventory, user, "outerClothing", armorCase.ArmorPrototype);
                    AssertEquippedVisualsMatchPrototype(entMan, inventory, user, "mask", armorCase.MaskPrototype);
                    AssertEquippedVisualsMatchPrototype(entMan, inventory, user, "shoes", armorCase.GreavesPrototype);
                }
                finally
                {
                    DeleteIfAlive(entMan, rack);
                    DeleteIfAlive(entMan, user);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MaskAccessoryPostVendorHookDeletesPlaceholderWhenProfileHasNoAccessory()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", map.GridCoords);
            var user = SpawnProfileUser(entMan, map.GridCoords.Offset(new Vector2(1, 0)), YautjaCharacterProfile.Default.WithMaskAccessory(0));

            try
            {
                ClearSlot(entMan, inventory, user, "outerClothing");
                ClearSlot(entMan, inventory, user, "mask");
                ClearSlot(entMan, inventory, user, "shoes");

                VendEntry(entMan, rack, user, "Essential Hunting Supplies", "CMUYautjaArmorBundle");

                AssertEquippedPrototype(entMan, inventory, user, "mask", "CMUYautjaMaskPred01Ebony");
                AssertMaskAccessory(entMan, inventory, user, null);
            }
            finally
            {
                DeleteIfAlive(entMan, rack);
                DeleteIfAlive(entMan, user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodedThrallCapePostVendorHookUsesDefaultColorWithoutYautjaComponentLikeCmss13Prefs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var yautja = YautjaCharacterProfile.Default
                .WithCapeColor(ProfileCapeColor);

            var rack = entMan.SpawnEntity("CMUYautjaBloodedThrallLoadoutVendor", map.GridCoords);
            var user = SpawnProfileUser(entMan, map.GridCoords.Offset(new Vector2(1, 0)), yautja);

            try
            {
                Assert.That(entMan.HasComponent<YautjaComponent>(user), Is.False);
                ClearSlot(entMan, inventory, user, "back");

                VendEntry(entMan, rack, user, "Clothing Accessory (CHOOSE 1)", "CMUYautjaCapeQuarter");

                AssertEquippedPrototype(entMan, inventory, user, "back", "CMUYautjaCapeQuarter");
                Assert.That(inventory.TryGetSlotEntity(user, "back", out var cape), Is.True);
                Assert.That(entMan.GetComponent<YautjaCapeComponent>(cape.Value).Color,
                    Is.EqualTo(YautjaCharacterProfile.Default.CapeColor));
            }
            finally
            {
                DeleteIfAlive(entMan, rack);
                DeleteIfAlive(entMan, user);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnProfileUser(IEntityManager entMan, EntityCoordinates coordinates, YautjaCharacterProfile profile)
    {
        var user = entMan.SpawnEntity("CMMobHuman", coordinates);
        entMan.EnsureComponent<YautjaAppliedProfileComponent>(user).Profile = profile;
        return user;
    }

    private static void VendEntry(
        IEntityManager entMan,
        EntityUid rack,
        EntityUid user,
        string sectionName,
        string entryPrototype)
    {
        var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
        var sectionIndex = vendor.Sections.FindIndex(section => section.Name == sectionName);
        Assert.That(sectionIndex, Is.GreaterThanOrEqualTo(0), sectionName);

        var entryIndex = vendor.Sections[sectionIndex].Entries.FindIndex(entry => entry.Id.Id == entryPrototype);
        Assert.That(entryIndex, Is.GreaterThanOrEqualTo(0), entryPrototype);

        Vend(entMan, rack, user, sectionIndex, entryIndex);
    }

    private static void Vend(IEntityManager entMan, EntityUid rack, EntityUid user, int sectionIndex, int entryIndex)
    {
        entMan.EventBus.RaiseLocalEvent(rack, new CMVendorVendBuiMsg(sectionIndex, entryIndex, new())
        {
            Actor = user,
            UiKey = CMAutomatedVendorUI.Key,
        });
    }

    private static void ClearSlot(IEntityManager entMan, InventorySystem inventory, EntityUid user, string slot)
    {
        if (inventory.TryGetSlotEntity(user, slot, out var item) && !entMan.Deleted(item.Value))
            entMan.DeleteEntity(item.Value);
    }

    private static void AssertEquippedPrototype(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid user,
        string slot,
        string prototype)
    {
        Assert.That(inventory.TryGetSlotEntity(user, slot, out var item), Is.True, $"{slot} has equipped item");
        Assert.That(entMan.GetComponent<MetaDataComponent>(item.Value).EntityPrototype?.ID, Is.EqualTo(prototype), slot);
    }

    private static void AssertMaskAccessory(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid user,
        string expectedPrototype)
    {
        Assert.That(inventory.TryGetSlotEntity(user, "mask", out var mask), Is.True, "mask has equipped item");
        Assert.That(entMan.TryGetComponent(mask.Value, out YautjaMaskAccessoryHolderComponent holder), Is.True, "mask has accessory holder");

        var containers = entMan.System<SharedContainerSystem>();
        Assert.That(containers.TryGetContainer(mask.Value, holder.ContainerId, out var container), Is.True, "mask accessory container exists");

        if (expectedPrototype == null)
        {
            Assert.That(container.ContainedEntities, Is.Empty, "mask accessory placeholder should be deleted");
            return;
        }

        Assert.That(container.ContainedEntities, Has.Count.EqualTo(1), "mask has one profile accessory");
        Assert.That(
            entMan.GetComponent<MetaDataComponent>(container.ContainedEntities[0]).EntityPrototype?.ID,
            Is.EqualTo(expectedPrototype),
            "profile mask accessory");
    }

    private static void AssertEquippedVisualsMatchPrototype(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid user,
        string slot,
        string visualPrototype)
    {
        Assert.That(inventory.TryGetSlotEntity(user, slot, out var item), Is.True, $"{slot} has equipped item");

        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var factory = IoCManager.Resolve<IComponentFactory>();
        var expected = prototypes.Index<EntityPrototype>(visualPrototype);

        Assert.That(expected.TryGetComponent<ItemComponent>(out var expectedItem, factory), Is.True, $"{visualPrototype} has item visuals");
        Assert.That(expected.TryGetComponent<ClothingComponent>(out var expectedClothing, factory), Is.True, $"{visualPrototype} has clothing visuals");

        var actualItem = entMan.GetComponent<ItemComponent>(item.Value);
        var actualClothing = entMan.GetComponent<ClothingComponent>(item.Value);

        Assert.That(actualItem.RsiPath, Is.EqualTo(expectedItem!.RsiPath), $"{slot} held visual RSI");
        Assert.That(actualItem.HeldPrefix, Is.EqualTo(expectedItem.HeldPrefix), $"{slot} held prefix");
        Assert.That(actualItem.InhandVisuals, Is.EqualTo(expectedItem.InhandVisuals), $"{slot} inhand visuals");
        Assert.That(actualClothing.RsiPath, Is.EqualTo(expectedClothing!.RsiPath), $"{slot} clothing visual RSI");
        Assert.That(actualClothing.EquippedPrefix, Is.EqualTo(expectedClothing.EquippedPrefix), $"{slot} equipped prefix");
        Assert.That(actualClothing.ClothingVisuals, Is.EqualTo(expectedClothing.ClothingVisuals), $"{slot} clothing visuals");
    }

    private static void DeleteIfAlive(IEntityManager entMan, EntityUid uid)
    {
        if (!entMan.Deleted(uid))
            entMan.DeleteEntity(uid);
    }

    private sealed record ArmorProfileCase(
        string Name,
        YautjaCharacterProfile Profile,
        string ArmorPrototype,
        string MaskPrototype,
        string GreavesPrototype,
        string MaskAccessoryPrototype);

    private sealed record CapeRackCase(string RackPrototype, string[] CapePrototypes);

    private sealed record BadBloodArmorCase(
        string BundlePrototype,
        string ArmorPrototype,
        string MaskPrototype,
        string GreavesPrototype);
}
