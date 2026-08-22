using System.Collections.Generic;
using System.Linq;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Wall;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaRackBundleStaticFactsTest
{
    [Test]
    public async Task YautjaRackBundlePrototypeStaticFactsMatchCmss13Source()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var server = pair.Server;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;

            foreach (var row in BundleWrapperRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);

                Assert.Multiple(() =>
                {
                    Assert.That(prototype.Name, Is.EqualTo(row.Name), $"{row.Id} local source-shaped bundle wrapper name");
                    Assert.That(prototype.Description, Is.EqualTo(row.Description), $"{row.Id} local source-shaped bundle wrapper description");
                    Assert.That(prototype.HideSpawnMenu, Is.True, $"{row.Id} rack-only bundle wrapper should be hidden from spawn menu");
                    Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, $"{row.Id} sprite");
                    Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(row.SpritePath), $"{row.Id} sprite RSI");
                    Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo(row.SpriteState), $"{row.Id} sprite state");
                });
            }
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            foreach (var row in BundleWrapperRows())
            {
                var bundle = entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace);

                try
                {
                    var bundleComp = entMan.GetComponent<CMVendorBundleComponent>(bundle);
                    Assert.That(bundleComp.Bundle.Select(id => id.Id).ToArray(), Is.EqualTo(row.Bundle), $"{row.Id} ordered bundle contents");
                }
                finally
                {
                    if (!entMan.Deleted(bundle))
                        entMan.DeleteEntity(bundle);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaRackBundleRowsExposeCmss13SourceMetadata()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var racks = new Dictionary<string, EntityUid>();

            try
            {
                foreach (var row in RackBundleRows())
                {
                    var rack = GetOrSpawnRack(entMan, row.RackId, racks);
                    var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                    var section = vendor.Sections.Single(section => section.Name == row.Section);
                    var entry = section.Entries.Single(entry => entry.Id.Id == row.Id);
                    var choice = row.SectionChoice ? section.Choices : entry.Choices;
                    var unusedChoice = row.SectionChoice ? entry.Choices : section.Choices;
                    var choiceLocation = row.SectionChoice ? "section" : "entry";

                    Assert.Multiple(() =>
                    {
                        Assert.That(entry.Name, Is.EqualTo(row.Name), $"{row.RackId}/{row.Id} display name");
                        Assert.That(entry.Amount, Is.Null, $"{row.RackId}/{row.Id} shared stock is infinite");
                        Assert.That(entry.MaxPerUser, Is.EqualTo((int?) 1), $"{row.RackId}/{row.Id} personal limit");
                        Assert.That(entry.Points, Is.Null, $"{row.RackId}/{row.Id} source row points");
                        Assert.That(entry.Mandatory, Is.EqualTo(row.Mandatory), $"{row.RackId}/{row.Id} mandatory flag");
                        Assert.That(entry.Recommended, Is.EqualTo(row.Recommended), $"{row.RackId}/{row.Id} recommended flag");
                        Assert.That(entry.ReplaceSlot, Is.Null, $"{row.RackId}/{row.Id} replace slot");

                        Assert.That(choice, Is.Not.Null, $"{row.RackId}/{row.Id} {choiceLocation} choice");
                        Assert.That(choice!.Value.Id, Is.EqualTo(row.Choice), $"{row.RackId}/{row.Id} {choiceLocation} choice id");
                        Assert.That(choice.Value.Amount, Is.EqualTo(1), $"{row.RackId}/{row.Id} {choiceLocation} choice amount");
                        Assert.That(unusedChoice, Is.Null, $"{row.RackId}/{row.Id} unused choice location");
                    });
                }
            }
            finally
            {
                foreach (var rack in racks.Values)
                {
                    if (!entMan.Deleted(rack))
                        entMan.DeleteEntity(rack);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultHuntingBundleItemsAreNotLeftMountedInTheRack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid rack = default;
        EntityUid hunter = default;
        var bundleIds = new[]
        {
            "CMUYautjaHuntingPouch",
            "CMUYautjaRelayBeacon",
            "CMUYautjaCleanserGelVial",
        };
        var existing = new Dictionary<string, HashSet<EntityUid>>();

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);
                hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                entMan.EnsureComponent<YautjaComponent>(hunter);

                // Force the delivery path to exercise the failed auto-grab branch. The regression is about
                // portable output after no slot/hand is available, not about normal auto-equipping.
                var inventory = entMan.System<InventorySystem>();
                var hands = entMan.System<SharedHandsSystem>();
                var beltFiller = entMan.SpawnEntity("CMUYautjaHuntingPouch", MapCoordinates.Nullspace);
                Assert.That(inventory.TryEquip(hunter, beltFiller, "belt", silent: true, force: true), Is.True);
                var leftHandFiller = entMan.SpawnEntity("CMUYautjaRelayBeacon", MapCoordinates.Nullspace);
                var rightHandFiller = entMan.SpawnEntity("CMUYautjaCleanserGelVial", MapCoordinates.Nullspace);
                Assert.That(hands.TryPickupAnyHand(hunter, leftHandFiller), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, rightHandFiller), Is.True);

                foreach (var id in bundleIds)
                    existing[id] = EntitiesByPrototype(entMan, id).ToHashSet();

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionIndex = vendor.Sections.FindIndex(section => section.Name == "Essential Hunting Supplies");
                Assert.That(sectionIndex, Is.GreaterThanOrEqualTo(0));

                var entryIndex = vendor.Sections[sectionIndex].Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaHuntingEquipmentBundle");
                Assert.That(entryIndex, Is.GreaterThanOrEqualTo(0));

                entMan.EventBus.RaiseLocalEvent(rack, new CMVendorVendBuiMsg(sectionIndex, entryIndex, new())
                {
                    Actor = hunter,
                    UiKey = CMAutomatedVendorUI.Key,
                });
            });

            await server.WaitRunTicks(3);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                foreach (var id in bundleIds)
                {
                    var items = EntitiesByPrototype(entMan, id)
                        .Where(uid => !existing[id].Contains(uid))
                        .ToArray();

                    Assert.That(items, Has.Length.EqualTo(1), $"{id} should be spawned exactly once by the bundle.");
                    var item = items[0];
                    Assert.That(entMan.HasComponent<WallMountComponent>(item), Is.False,
                        $"{id} should remain portable after vending instead of staying mounted in the rack.");
                    var itemXform = entMan.GetComponent<TransformComponent>(item);
                    Assert.That(itemXform.ParentUid, Is.Not.EqualTo(rack),
                        $"{id} should not remain parented to the rack after vending.");
                    var clearAxis = Math.Max(MathF.Abs(itemXform.LocalPosition.X), MathF.Abs(itemXform.LocalPosition.Y));
                    Assert.That(clearAxis, Is.GreaterThan(0.45f),
                        $"{id} should clear the rack fixture on at least one axis after auto-grab fails.");
                }
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { rack, hunter })
                {
                    if (uid.IsValid() && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    private static IEnumerable<EntityUid> EntitiesByPrototype(IEntityManager entMan, string id)
    {
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (meta.EntityPrototype?.ID == id)
                yield return uid;
        }
    }

    private static IEnumerable<BundleWrapperRow> BundleWrapperRows()
    {
        yield return new BundleWrapperRow(
            "CMUYautjaHuntingEquipmentBundle",
            "Hunting Equipment",
            "Contains the essential hunting equipment issued to a Yautja hunter.",
            YautjaPath("body_mesh.rsi"),
            "icon",
            [
                "CMUYautjaBodyMesh",
                "CMUYautjaHuntingPouch",
                "CMUYautjaMedicompFull",
                "CMUYautjaRelayBeacon",
                "CMUYautjaCleanserGelVial",
            ]);

        yield return new BundleWrapperRow(
            "CMUYautjaYoungbloodHuntingEquipmentBundle",
            "Hunting Equipment",
            "Contains the essential hunting equipment issued to a young Yautja hunter.",
            YautjaPath("body_mesh.rsi"),
            "icon",
            [
                "CMUYautjaBodyMesh",
                "CMUYautjaHuntingPouch",
                "CMUYautjaMedicompFull",
                "CMUYautjaLantern",
            ]);

        yield return new BundleWrapperRow(
            "CMUYautjaThrallHuntingEquipmentBundle",
            "Hunting Equipment",
            "Contains the essential hunting equipment issued to a Yautja thrall.",
            YautjaPath("body_mesh.rsi"),
            "icon",
            [
                "CMUYautjaThrallChainshirt",
                "CMUYautjaHuntingPouch",
                "CMUYautjaLantern",
                "CMUYautjaCommunicator",
            ]);

        yield return new BundleWrapperRow(
            "CMUYautjaStrandedHuntingEquipmentBundle",
            "Hunting Equipment",
            "Contains the essential hunting equipment issued to a stranded Yautja hunter.",
            YautjaPath("body_mesh.rsi"),
            "icon",
            [
                "CMUYautjaBodyMeshScalable",
                "CMUYautjaHuntingPouch",
                "CMUYautjaMedicompFull",
                "CMUYautjaCleanserGelVial",
            ]);

        yield return new BundleWrapperRow(
            "CMUYautjaArmorBundle",
            "Armor",
            "Contains the armor issued to a Yautja hunter.",
            YautjaPath("armor_clan.rsi"),
            "icon",
            [
                "CMUYautjaClanArmor",
                "CMUYautjaMask",
                "CMUYautjaMaskAccessory01Ebony",
                "CMUYautjaClanGreaves",
            ]);

        yield return new BundleWrapperRow(
            "CMUYautjaStrandedArmorBundle",
            "Armor",
            "Contains the armor issued to a stranded Yautja hunter.",
            YautjaPath("armor_clan.rsi"),
            "icon",
            [
                "CMUYautjaClanArmorScalable",
                "CMUYautjaMaskScalable",
                "CMUYautjaMaskAccessory01Ebony",
                "CMUYautjaClanGreavesScalable",
            ]);

        yield return new BundleWrapperRow(
            "CMUYautjaBadBloodHuntingEquipmentBundle",
            "Hunting Equipment",
            "Contains the essential hunting equipment issued to a Bad Blood hunter.",
            YautjaPath("body_mesh.rsi"),
            "icon",
            [
                "CMUYautjaBodyMeshScalable",
                "CMUYautjaHuntingPouch",
                "CMUYautjaMedicompSurvivor",
                "CMUYautjaCleanserGelVial",
                "CMUYautjaHivebreaker",
            ]);

        foreach (var row in BadBloodArmorRows())
            yield return row;

        foreach (var row in ThrallArmorRows())
            yield return row;

        yield return new BundleWrapperRow(
            "CMUYautjaBloodedThrallEquipmentBundle",
            "Blooded Equipment",
            "Contains the equipment issued to a blooded Yautja thrall.",
            HunterShipPath("thrall_gear.rsi"),
            "thrall_teleporter",
            [
                "CMUYautjaSimpleRelayBeacon",
                "CMUYautjaMedicompThrall",
            ]);

        foreach (var row in BloodedThrallBracerRows())
            yield return row;

        yield return new BundleWrapperRow(
            "CMUYautjaWristBladesBundle",
            "Wrist Blades",
            "Contains the paired wrist blades issued as a Yautja bracer attachment.",
            YautjaPath("wrist_blades.rsi"),
            "icon",
            [
                "CMUYautjaWristBladesAttachment",
                "CMUYautjaWristBladesAttachment",
            ]);

        yield return new BundleWrapperRow(
            "CMUYautjaFearsomeScimitarsBundle",
            "The Fearsome Scimitars",
            "Contains the paired fearsome scimitar bracer attachments.",
            YautjaPath("scimitar.rsi"),
            "icon",
            [
                "CMUYautjaScimitarAttachment",
                "CMUYautjaScimitarAttachment",
            ]);

        yield return new BundleWrapperRow(
            "CMUYautjaSkeweringScimitarsBundle",
            "The Skewering Scimitars",
            "Contains the paired skewering scimitar bracer attachments.",
            YautjaPath("scimitar.rsi"),
            "alt",
            [
                "CMUYautjaScimitarAltAttachment",
                "CMUYautjaScimitarAltAttachment",
            ]);

        yield return new BundleWrapperRow(
            "CMUYautjaChainGauntletsBundle",
            "The Chain Gauntlets",
            "Contains the paired chain gauntlet bracer attachments and a chainwhip.",
            YautjaPath("chain_gauntlet.rsi"),
            "icon",
            [
                "CMUYautjaChainGauntletsAttachment",
                "CMUYautjaChainGauntletsAttachment",
                "CMUYautjaChainwhip",
            ]);
    }

    private static IEnumerable<RackBundleRow> RackBundleRows()
    {
        yield return new RackBundleRow("CMUYautjaLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaHuntingEquipmentBundle", "Hunting Equipment", "CMUYautjaEssentials", Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaArmorBundle", "Armor", "CMUYautjaArmor", Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaElderLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaHuntingEquipmentBundle", "Hunting Equipment", "CMUYautjaEssentials", Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaElderLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaArmorBundle", "Armor", "CMUYautjaArmor", Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaYoungbloodLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaYoungbloodHuntingEquipmentBundle", "Hunting Equipment", "CMUYautjaEssentials", Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaYoungbloodLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaArmorBundle", "Armor", "CMUYautjaArmor", Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaThrallLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaThrallHuntingEquipmentBundle", "Hunting Equipment", "CMUYautjaEssentials", Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaBloodedThrallLoadoutVendor", "Blooded Equipment", "CMUYautjaBloodedThrallEquipmentBundle", "Blooded Equipment", "CMUYautjaRanged", Mandatory: false, Recommended: false);
        yield return new RackBundleRow("CMUYautjaBadBloodLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaBadBloodHuntingEquipmentBundle", "Hunting Equipment", "CMUYautjaEssentials", Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaStrandedLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaStrandedHuntingEquipmentBundle", "Hunting Equipment", "CMUYautjaEssentials", Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaStrandedLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaStrandedArmorBundle", "Armor", "CMUYautjaArmor", Mandatory: true, Recommended: false);

        foreach (var row in RackMaterialRows("CMUYautjaThrallLoadoutVendor", "Armor Material (CHOOSE 1)", "CMUYautjaThrallArmor", "CMUYautjaArmor", recommended: true))
            yield return row;

        foreach (var row in RackMaterialRows("CMUYautjaBloodedThrallLoadoutVendor", "Blooded Bracer Material (CHOOSE 1)", "CMUYautjaBloodedThrallBracer", "CMUYautjaBracer", recommended: true))
            yield return row;

        foreach (var row in BadBloodArmorRackRows())
            yield return row;

        foreach (var rack in SharedBracerAttachmentRacks())
        {
            yield return new RackBundleRow(rack, "Bracer Attachments", "CMUYautjaWristBladesBundle", "Wrist Blades", "CMUYautjaBracer", Mandatory: true, Recommended: false);
            yield return new RackBundleRow(rack, "Bracer Attachments", "CMUYautjaFearsomeScimitarsBundle", "The Fearsome Scimitars", "CMUYautjaPrimary", Mandatory: false, Recommended: true);
            yield return new RackBundleRow(rack, "Bracer Attachments", "CMUYautjaSkeweringScimitarsBundle", "The Skewering Scimitars", "CMUYautjaPrimary", Mandatory: false, Recommended: true);
            yield return new RackBundleRow(rack, "Bracer Attachments", "CMUYautjaChainGauntletsBundle", "The Chain Gauntlets", "CMUYautjaPrimary", Mandatory: false, Recommended: true);
        }
    }

    private static IEnumerable<BundleWrapperRow> BadBloodArmorRows()
    {
        yield return ArmorSet("CMUYautjaBadBloodArmorPatchworkBundle", "Patchwork Armor", "Contains the patchwork armor issued to a Bad Blood hunter.", "armor_badblood_patchwork.rsi", "CMUYautjaBadBloodArmorPatchwork", "CMUYautjaMaskBadBloodPatchwork", "CMUYautjaBadBloodGreavesPatchwork");
        yield return ArmorSet("CMUYautjaBadBloodArmorPatchworkAltBundle", "Patchwork Armor (Alt)", "Contains the alternate patchwork armor issued to a Bad Blood hunter.", "armor_badblood_patchwork_alt.rsi", "CMUYautjaBadBloodArmorPatchworkAlt", "CMUYautjaMaskBadBloodPatchworkAlt", "CMUYautjaBadBloodGreavesPatchworkAlt");
        yield return ArmorSet("CMUYautjaBadBloodArmorLunaticBundle", "Lunatic Armor", "Contains the lunatic armor issued to a Bad Blood hunter.", "armor_badblood_lunatic.rsi", "CMUYautjaBadBloodArmorLunatic", "CMUYautjaMaskBadBloodLunatic", "CMUYautjaBadBloodGreavesLunatic");
        yield return ArmorSet("CMUYautjaBadBloodArmorScavengerBundle", "Scavenger Armor", "Contains the scavenger armor issued to a Bad Blood hunter.", "armor_badblood_scavenger.rsi", "CMUYautjaBadBloodArmorScavenger", "CMUYautjaMaskBadBloodScav", "CMUYautjaBadBloodGreavesScavenger");
        yield return ArmorSet("CMUYautjaBadBloodArmorScavengerAltBundle", "Scavenger Armor (Alt)", "Contains the alternate scavenger armor issued to a Bad Blood hunter.", "armor_badblood_scavenger_alt.rsi", "CMUYautjaBadBloodArmorScavengerAlt", "CMUYautjaMaskBadBloodScavAlt", "CMUYautjaBadBloodGreavesScavengerAlt");
        yield return ArmorSet("CMUYautjaBadBloodArmorVenatorBundle", "Venator Armor", "Contains the venator armor issued to a Bad Blood hunter.", "armor_badblood_venator.rsi", "CMUYautjaBadBloodArmorVenator", "CMUYautjaMaskBadBloodVenator", "CMUYautjaBadBloodGreavesVenator");
        yield return ArmorSet("CMUYautjaBadBloodArmorCommandoBundle", "Commando Armor", "Contains the commando armor issued to a Bad Blood hunter.", "armor_badblood_commando.rsi", "CMUYautjaBadBloodArmorCommando", "CMUYautjaMaskBadBloodCommando", "CMUYautjaBadBloodGreavesCommando");
        yield return ArmorSet("CMUYautjaBadBloodArmorCommandoAltBundle", "Commando Armor (Alt)", "Contains the alternate commando armor issued to a Bad Blood hunter.", "armor_badblood_commando_alt.rsi", "CMUYautjaBadBloodArmorCommandoAlt", "CMUYautjaMaskBadBloodCommandoAlt", "CMUYautjaBadBloodGreavesCommandoAlt");
        yield return ArmorSet("CMUYautjaBadBloodArmorEmissaryBundle", "Emissary Armor", "Contains the emissary armor issued to a Bad Blood hunter.", "armor_emissary_classic.rsi", "CMUYautjaEmissaryArmorCamoConforming", "CMUYautjaMaskBadBloodEmissaryClassic", "CMUYautjaEmissaryGreavesCamoConforming");
    }

    private static IEnumerable<BundleWrapperRow> ThrallArmorRows()
    {
        yield return ThrallArmorSet("Ebony", "ebony", "armor_thrall_ebony.rsi", "CMUYautjaThrallArmorEbony", "CMUYautjaThrallGreavesEbony", "CMUYautjaMaskThrallEbony");
        yield return ThrallArmorSet("Silver", "silver", "armor_thrall_silver.rsi", "CMUYautjaThrallArmorSilver", "CMUYautjaThrallGreavesSilver", "CMUYautjaMaskThrallSilver");
        yield return ThrallArmorSet("Gold", "gold", "armor_thrall_gold.rsi", "CMUYautjaThrallArmorGold", "CMUYautjaThrallGreavesGold", "CMUYautjaMaskThrallGold");
        yield return ThrallArmorSet("Crimson", "crimson", "armor_thrall_crimson.rsi", "CMUYautjaThrallArmorCrimson", "CMUYautjaThrallGreavesCrimson", "CMUYautjaMaskThrallCrimson");
        yield return ThrallArmorSet("Bone", "bone", "armor_thrall_bone.rsi", "CMUYautjaThrallArmorBone", "CMUYautjaThrallGreavesBone", "CMUYautjaMaskThrallBone");
    }

    private static IEnumerable<BundleWrapperRow> BloodedThrallBracerRows()
    {
        yield return BloodedBracerSet("Ebony", "ebony", "bracer", "CMUYautjaBloodedThrallBracer");
        yield return BloodedBracerSet("Silver", "silver", "bracer_silver", "CMUYautjaBloodedThrallBracerSilver");
        yield return BloodedBracerSet("Gold", "gold", "bracer_bronze", "CMUYautjaBloodedThrallBracerGold");
        yield return BloodedBracerSet("Crimson", "crimson", "bracer_crimson", "CMUYautjaBloodedThrallBracerCrimson");
        yield return BloodedBracerSet("Bone", "bone", "bracer_bone", "CMUYautjaBloodedThrallBracerBone");
    }

    private static IEnumerable<RackBundleRow> RackMaterialRows(string rackId, string section, string bundlePrefix, string choice, bool recommended)
    {
        yield return new RackBundleRow(rackId, section, $"{bundlePrefix}EbonyBundle", "Ebony", choice, SectionChoice: true, Mandatory: false, Recommended: recommended);
        yield return new RackBundleRow(rackId, section, $"{bundlePrefix}SilverBundle", "Silver", choice, SectionChoice: true, Mandatory: false, Recommended: recommended);
        yield return new RackBundleRow(rackId, section, $"{bundlePrefix}GoldBundle", "Gold", choice, SectionChoice: true, Mandatory: false, Recommended: recommended);
        yield return new RackBundleRow(rackId, section, $"{bundlePrefix}CrimsonBundle", "Crimson", choice, SectionChoice: true, Mandatory: false, Recommended: recommended);
        yield return new RackBundleRow(rackId, section, $"{bundlePrefix}BoneBundle", "Bone", choice, SectionChoice: true, Mandatory: false, Recommended: recommended);
    }

    private static IEnumerable<RackBundleRow> BadBloodArmorRackRows()
    {
        yield return new RackBundleRow("CMUYautjaBadBloodLoadoutVendor", "Armor Set", "CMUYautjaBadBloodArmorPatchworkBundle", "Patchwork Armor", "CMUYautjaArmor", SectionChoice: true, Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaBadBloodLoadoutVendor", "Armor Set", "CMUYautjaBadBloodArmorPatchworkAltBundle", "Patchwork Armor (Alt)", "CMUYautjaArmor", SectionChoice: true, Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaBadBloodLoadoutVendor", "Armor Set", "CMUYautjaBadBloodArmorLunaticBundle", "Lunatic Armor", "CMUYautjaArmor", SectionChoice: true, Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaBadBloodLoadoutVendor", "Armor Set", "CMUYautjaBadBloodArmorScavengerBundle", "Scavenger Armor", "CMUYautjaArmor", SectionChoice: true, Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaBadBloodLoadoutVendor", "Armor Set", "CMUYautjaBadBloodArmorScavengerAltBundle", "Scavenger Armor (Alt)", "CMUYautjaArmor", SectionChoice: true, Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaBadBloodLoadoutVendor", "Armor Set", "CMUYautjaBadBloodArmorVenatorBundle", "Venator Armor", "CMUYautjaArmor", SectionChoice: true, Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaBadBloodLoadoutVendor", "Armor Set", "CMUYautjaBadBloodArmorCommandoBundle", "Commando Armor", "CMUYautjaArmor", SectionChoice: true, Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaBadBloodLoadoutVendor", "Armor Set", "CMUYautjaBadBloodArmorCommandoAltBundle", "Commando Armor (Alt)", "CMUYautjaArmor", SectionChoice: true, Mandatory: true, Recommended: false);
        yield return new RackBundleRow("CMUYautjaBadBloodLoadoutVendor", "Armor Set", "CMUYautjaBadBloodArmorEmissaryBundle", "Emissary Armor", "CMUYautjaArmor", SectionChoice: true, Mandatory: true, Recommended: false);
    }

    private static string[] SharedBracerAttachmentRacks()
    {
        return
        [
            "CMUYautjaLoadoutVendor",
            "CMUYautjaElderLoadoutVendor",
            "CMUYautjaYoungbloodLoadoutVendor",
            "CMUYautjaBadBloodLoadoutVendor",
            "CMUYautjaStrandedLoadoutVendor",
        ];
    }

    private static BundleWrapperRow ArmorSet(
        string id,
        string name,
        string description,
        string sprite,
        string armor,
        string mask,
        string greaves)
    {
        return new BundleWrapperRow(id, name, description, YautjaPath(sprite), "icon", [armor, mask, greaves]);
    }

    private static BundleWrapperRow ThrallArmorSet(
        string colorName,
        string descriptionColor,
        string sprite,
        string armor,
        string greaves,
        string mask)
    {
        return new BundleWrapperRow(
            $"CMUYautjaThrallArmor{colorName}Bundle",
            colorName,
            $"Contains the {descriptionColor} armor issued to a Yautja thrall.",
            YautjaPath(sprite),
            "icon",
            [armor, greaves, mask]);
    }

    private static BundleWrapperRow BloodedBracerSet(
        string colorName,
        string descriptionColor,
        string state,
        string bracer)
    {
        return new BundleWrapperRow(
            $"CMUYautjaBloodedThrallBracer{colorName}Bundle",
            colorName,
            $"Contains the {descriptionColor} bracer issued to a blooded Yautja thrall.",
            YautjaPath("bracer.rsi"),
            state,
            [
                bracer,
                "CMUYautjaWristBladesAttachment",
                "CMUYautjaWristBladesAttachment",
            ]);
    }

    private static ResPath YautjaPath(string rsi)
    {
        return new ResPath($"/Textures/_CMU14/Yautja/{rsi}");
    }

    private static ResPath HunterShipPath(string rsi)
    {
        return new ResPath($"/Textures/_CMU14/HunterShip/obj/items/hunter/{rsi}");
    }

    private static EntityUid GetOrSpawnRack(
        IEntityManager entMan,
        string rackId,
        Dictionary<string, EntityUid> racks)
    {
        if (racks.TryGetValue(rackId, out var rack))
            return rack;

        rack = entMan.SpawnEntity(rackId, MapCoordinates.Nullspace);
        racks.Add(rackId, rack);
        return rack;
    }

    private readonly record struct BundleWrapperRow(
        string Id,
        string Name,
        string Description,
        ResPath SpritePath,
        string SpriteState,
        string[] Bundle);

    private readonly record struct RackBundleRow(
        string RackId,
        string Section,
        string Id,
        string Name,
        string Choice,
        bool Mandatory,
        bool Recommended,
        bool SectionChoice = false);
}
