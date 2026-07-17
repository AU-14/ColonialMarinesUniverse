using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._CMU14.Yautja;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaCharacterProfileTest
{
    [Test]
    public void YautjaProfileCopiesWithoutChangingNormalSpecies()
    {
        var yautjaAppearance = new HumanoidCharacterAppearance()
            .WithSkinColor(new Color((byte) 56, (byte) 90, (byte) 48))
            .WithEyeColor(Color.Gold)
            .WithHairColor(new Color((byte) 24, (byte) 18, (byte) 14))
            .WithMarkings(new List<Marking>
            {
                new("CMUYautjaDreadlocksStandard", new List<Color> { new((byte) 24, (byte) 18, (byte) 14) }),
            });

        var yautja = YautjaCharacterProfile.Default
            .WithName("Kainde Amedha")
            .WithAge(420)
            .WithAppearance(yautjaAppearance)
            .WithSkinColor(YautjaSkinColor.Green)
            .WithQuillStyle(YautjaQuillStyle.LongCurved)
            .WithArmor(YautjaGearMaterial.Bronze, 3)
            .WithMask(YautjaGearMaterial.Bone, 12)
            .WithMaskAccessory(2)
            .WithGreaves(YautjaGearMaterial.Silver, 2)
            .WithBracer(YautjaBracerMaterial.Crimson)
            .WithCaster(YautjaBracerMaterial.Silver)
            .WithOwnerRank(YautjaBracerOwnerRank.Elder)
            .WithTranslatorType(YautjaTranslatorType.Combo)
            .WithInvisibilitySound(YautjaInvisibilitySound.Retro)
            .WithUnique(YautjaUniqueSet.Ronin)
            .WithCapeStyle(YautjaCapeStyle.Poncho)
            .WithCapeColor(new Color((byte) 0x2a, (byte) 0x5c, (byte) 0x8a))
            .WithFlavorText("A quiet hunter.");

        var normal = HumanoidCharacterProfile.DefaultWithSpecies("Human")
            .WithName("John Human")
            .WithYautjaProfile(yautja);

        var copied = normal.Clone();

        Assert.Multiple(() =>
        {
            Assert.That(copied.Species, Is.EqualTo("Human"));
            Assert.That(copied.Name, Is.EqualTo("John Human"));
            Assert.That(copied.YautjaProfile.Name, Is.EqualTo("Kainde Amedha"));
            Assert.That(copied.YautjaProfile.Age, Is.EqualTo(420));
            Assert.That(copied.YautjaProfile.Appearance.SkinColor,
                Is.EqualTo(YautjaCharacterProfile.GetSkinColorColor(YautjaSkinColor.Green)));
            Assert.That(copied.YautjaProfile.SkinColor, Is.EqualTo(YautjaSkinColor.Green));
            Assert.That(copied.YautjaProfile.ArmorPrototype, Is.EqualTo("CMUYautjaArmorUniqueRonin"));
            Assert.That(copied.YautjaProfile.MaskPrototype, Is.EqualTo("CMUYautjaMaskUniqueRonin"));
            Assert.That(copied.YautjaProfile.MaskAccessoryPrototype, Is.EqualTo("CMUYautjaMaskAccessory02Bone"));
            Assert.That(copied.YautjaProfile.GreavesPrototype, Is.EqualTo("CMUYautjaGreavesUniqueRonin"));
            Assert.That(copied.YautjaProfile.BracerPrototype, Is.EqualTo("CMUYautjaBracerCrimson"));
            Assert.That(copied.YautjaProfile.CasterPrototype, Is.EqualTo("CMUYautjaPlasmaCasterSilver"));
            Assert.That(copied.YautjaProfile.OwnerRank, Is.EqualTo(YautjaBracerOwnerRank.Elder));
            Assert.That(copied.YautjaProfile.CapePrototype, Is.EqualTo("CMUYautjaCapePoncho"));
            Assert.That(copied.YautjaProfile.CapeColor, Is.EqualTo(new Color((byte) 0x2a, (byte) 0x5c, (byte) 0x8a)));
            Assert.That(copied.YautjaProfile.QuillMarkingId, Is.EqualTo("CMUYautjaDreadlocksLongCurved"));
            Assert.That(copied.YautjaProfile.TranslatorType, Is.EqualTo(YautjaTranslatorType.Combo));
            Assert.That(copied.YautjaProfile.InvisibilitySound, Is.EqualTo(YautjaInvisibilitySound.Retro));
            Assert.That(copied.YautjaProfile.FlavorText, Is.EqualTo("A quiet hunter."));
        });
    }

    [Test]
    public void DefaultYautjaProfileMatchesCmss13PickerDefaults()
    {
        var yautja = YautjaCharacterProfile.Default;

        Assert.Multiple(() =>
        {
            Assert.That(yautja.Name, Is.EqualTo("Неизвестно"));
            Assert.That(yautja.Age, Is.EqualTo(100));
            Assert.That(yautja.QuillStyle, Is.EqualTo(YautjaQuillStyle.Standard));
            Assert.That(yautja.SkinColor, Is.EqualTo(YautjaSkinColor.Green));
            Assert.That(yautja.EyeColor, Is.EqualTo(YautjaEyeColor.Black));
            Assert.That(yautja.TranslatorType, Is.EqualTo(YautjaTranslatorType.Modern));
            Assert.That(yautja.InvisibilitySound, Is.EqualTo(YautjaInvisibilitySound.Modern));
            Assert.That(yautja.Legacy, Is.EqualTo(YautjaLegacySet.None));
            Assert.That(yautja.Unique, Is.EqualTo(YautjaUniqueSet.None));
            Assert.That(yautja.MaskAccessoryStyle, Is.EqualTo(0));
            Assert.That(yautja.CasterMaterial, Is.EqualTo(YautjaBracerMaterial.Ebony));
            Assert.That(yautja.OwnerRank, Is.EqualTo(YautjaBracerOwnerRank.Unblooded));
            Assert.That(yautja.CapeStyle, Is.EqualTo(YautjaCapeStyle.Full));
            Assert.That(yautja.CapePrototype, Is.EqualTo("CMUYautjaCapeFull"));
            Assert.That(yautja.CapeColor, Is.EqualTo(new Color((byte) 0x65, (byte) 0x43, (byte) 0x21)));
            Assert.That(yautja.FlavorText, Is.Empty);
        });
    }

    [Test]
    public void GearDisplayNamesUseCmss13ItemNames()
    {
        Assert.Multiple(() =>
        {
            Assert.That(YautjaCharacterProfile.GetArmorStyleDisplayName(YautjaGearMaterial.Bronze, 3),
                Is.EqualTo("bronze clan armor, pattern 3"));
            Assert.That(YautjaCharacterProfile.GetMaskStyleDisplayName(YautjaGearMaterial.Bone, 12),
                Is.EqualTo("bone clan mask, pattern 12"));
            Assert.That(YautjaCharacterProfile.GetGreavesStyleDisplayName(YautjaGearMaterial.Silver, 2),
                Is.EqualTo("silver clan greaves, pattern 2"));
            Assert.That(YautjaCharacterProfile.CapeStyleOrder,
                Is.EqualTo(new[]
                {
                    YautjaCapeStyle.Full,
                    YautjaCapeStyle.Ceremonial,
                    YautjaCapeStyle.Third,
                    YautjaCapeStyle.Half,
                    YautjaCapeStyle.Quarter,
                    YautjaCapeStyle.Poncho,
                    YautjaCapeStyle.Damaged,
                }));
            Assert.That(YautjaCharacterProfile.GetCapeDisplayName(YautjaCapeStyle.Poncho),
                Is.EqualTo("councilor poncho"));
            Assert.That(YautjaCharacterProfile.Default.WithCapeStyle(YautjaCapeStyle.Damaged).CapePrototype,
                Is.EqualTo("CMUYautjaCapeDamaged"));
        });
    }

    [Test]
    public void BracerDisplayNamesUseCmss13Materials()
    {
        Assert.Multiple(() =>
        {
            Assert.That(YautjaCharacterProfile.BracerMaterialOrder,
                Is.EqualTo(new[]
                {
                    YautjaBracerMaterial.Retro,
                    YautjaBracerMaterial.Ebony,
                    YautjaBracerMaterial.Silver,
                    YautjaBracerMaterial.Bronze,
                    YautjaBracerMaterial.Crimson,
                    YautjaBracerMaterial.Bone,
                    YautjaBracerMaterial.Dragon,
                    YautjaBracerMaterial.Swamp,
                    YautjaBracerMaterial.Enforcer,
                    YautjaBracerMaterial.Collector,
                }));
            Assert.That(YautjaCharacterProfile.GetBracerDisplayName(YautjaBracerMaterial.Silver),
                Is.EqualTo("silver clan bracers"));
            Assert.That(YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Retro).BracerPrototype,
                Is.EqualTo("CMUYautjaBracerRetro"));
            Assert.That(YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Dragon).BracerPrototype,
                Is.EqualTo("CMUYautjaBracerLegacyDragon"));
            Assert.That(YautjaCharacterProfile.GetBracerDisplayName(YautjaBracerMaterial.Collector),
                Is.EqualTo("collector legacy bracers"));
            Assert.That(YautjaCharacterProfile.Default.WithLegacy(YautjaLegacySet.Enforcer).BracerPrototype,
                Is.EqualTo("CMUYautjaBracerLegacyEnforcer"));
        });
    }

    [Test]
    public void ColorCustomizationUsesMutedPresetPalettes()
    {
        var skinColor = YautjaCharacterProfile.GetSkinColorColor(YautjaSkinColor.Green);
        var yautja = YautjaCharacterProfile.Default
            .WithSkinColor(YautjaSkinColor.Green)
            .WithEyeColor(YautjaEyeColor.Copper);
        var quills = yautja.Appearance.Markings.Single(marking => marking.MarkingId == yautja.QuillMarkingId);

        Assert.Multiple(() =>
        {
            Assert.That(YautjaCharacterProfile.SkinColorOrder,
                Is.EqualTo(new[]
                {
                    YautjaSkinColor.Green,
                    YautjaSkinColor.Tan,
                    YautjaSkinColor.Purple,
                    YautjaSkinColor.Blue,
                    YautjaSkinColor.Red,
                    YautjaSkinColor.Black,
                }));
            Assert.That(YautjaCharacterProfile.EyeColorOrder,
                Is.EqualTo(new[]
                {
                    YautjaEyeColor.Black,
                    YautjaEyeColor.Gold,
                    YautjaEyeColor.Amber,
                    YautjaEyeColor.Copper,
                    YautjaEyeColor.Red,
                    YautjaEyeColor.Jade,
                    YautjaEyeColor.Slate,
                }));
            Assert.That(yautja.Appearance.SkinColor, Is.EqualTo(skinColor));
            Assert.That(yautja.Appearance.HairColor, Is.EqualTo(skinColor));
            Assert.That(quills.MarkingColors.Single(), Is.EqualTo(skinColor));
            Assert.That(yautja.Appearance.EyeColor,
                Is.EqualTo(YautjaCharacterProfile.GetEyeColorColor(YautjaEyeColor.Copper)));
            Assert.That(YautjaCharacterProfile.Default.WithEyeColor(YautjaEyeColor.Black).Appearance.EyeColor,
                Is.EqualTo(YautjaCharacterProfile.GetEyeColorColor(YautjaEyeColor.Black)));
        });
    }

    [Test]
    public void EmptyMaskAccessoryDisplayNameFitsVisualSelector()
    {
        Assert.That(YautjaCharacterProfile.GetMaskAccessoryDisplayName(0, YautjaGearMaterial.Ebony),
            Is.EqualTo("None"));
    }

    [Test]
    public async Task MaskAccessoryHasMatchingOnMobSpriteState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var cache = client.ResolveDependency<IResourceCache>();
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;
            var accessory = prototypes.Index<EntityPrototype>("CMUYautjaMaskAccessory02Bronze");

            Assert.That(accessory.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True);
            var state = sprite!.AllLayers.First().RsiState.Name;
            var rsiPath = new ResPath("/Textures/_CMU14/Yautja/mask_accessories_onmob.rsi");

            Assert.Multiple(() =>
            {
                Assert.That(state, Is.EqualTo("pred_accessory2_bronze"));
                Assert.That(cache.TryGetResource<RSIResource>(rsiPath, out var resource), Is.True);
                Assert.That(resource!.RSI.Size, Is.EqualTo(new Vector2i(32, 64)),
                    "CMSS13 mask accessories are 32x64 on-mob overlays; shrinking them to 32x32 moves the preview layer off the helmet.");
                Assert.That(resource!.RSI.TryGetState($"equipped-{state}", out _), Is.True,
                    "The client visual system maps mask accessory icon states to equipped-* on-mob states.");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaMaskAccessoryPrototypesMatchCmss13SourceFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var cache = client.ResolveDependency<IResourceCache>();
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;
            var onMobRsiPath = new ResPath("/Textures/_CMU14/Yautja/mask_accessories_onmob.rsi");

            Assert.That(cache.TryGetResource<RSIResource>(onMobRsiPath, out var onMobResource), Is.True);
            Assert.That(onMobResource!.RSI.Size, Is.EqualTo(new Vector2i(32, 64)),
                "CMSS13 mask accessories use a separate on-mob accessory DMI for WEAR_FACE overlays.");

            var basePrototype = prototypes.Index<EntityPrototype>("CMUYautjaMaskOrnament");
            Assert.Multiple(() =>
            {
                Assert.That(basePrototype.Name, Is.EqualTo("Mask Ornament"));
                Assert.That(basePrototype.Description, Is.EqualTo("An ornate addition to your mask."));
            });

            foreach (var row in MaskAccessoryRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);

                Assert.Multiple(() =>
                {
                    Assert.That(prototype.Name, Is.EqualTo("Mask Ornament"), row.Id);
                    Assert.That(prototype.Description, Is.EqualTo("An ornate addition to your mask."), row.Id);
                    Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, row.Id);
                    Assert.That(sprite!.BaseRSI?.Path,
                        Is.EqualTo(new ResPath("/Textures/_CMU14/Yautja/mask_accessories.rsi")),
                        $"{row.Id} maps CMSS13 icons/obj/items/hunter/pred_mask_accessories.dmi.");
                    Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo(row.State),
                        $"{row.Id} CMSS13 post-vendor icon_state");
                    Assert.That(onMobResource.RSI.TryGetState($"equipped-{row.State}", out _), Is.True,
                        $"{row.Id} maps CMSS13 accessory_icons WEAR_FACE overlay.");
                });
            }
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new List<EntityUid>();

            try
            {
                foreach (var row in MaskAccessoryRows())
                {
                    var uid = entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace);
                    spawned.Add(uid);

                    Assert.Multiple(() =>
                    {
                        Assert.That(entMan.HasComponent<YautjaMaskOrnamentComponent>(uid), Is.True, row.Id);
                        Assert.That(entMan.HasComponent<YautjaTechItemComponent>(uid), Is.False,
                            $"{row.Id} should not invent ITEM_PREDATOR; CMSS13 /obj/item/clothing/accessory/mask does not set flags_item.");
                        Assert.That(entMan.HasComponent<CorrodibleComponent>(uid), Is.False,
                            $"{row.Id} should not invent unacidable; CMSS13 /obj/item/clothing/accessory/mask does not set unacidable.");
                    });
                }
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaProfileMaskPrototypesMatchCmss13PostVendorStaticFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var cache = client.ResolveDependency<IResourceCache>();
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;

            foreach (var row in ProfileMaskRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);
                var rsiPath = new ResPath($"/Textures/_CMU14/Yautja/masks/{row.State}.rsi");

                Assert.Multiple(() =>
                {
                    Assert.That(prototype.Name, Is.EqualTo(row.Name),
                        $"{row.Id} inherits the CMSS13 source item name from /obj/item/clothing/mask/gas/yautja/hunter; post_vendor_spawn_hook only changes icon_state.");
                    Assert.That(prototype.Description, Is.EqualTo(row.Description),
                        $"{row.Id} inherits the CMSS13 source description from /obj/item/clothing/mask/gas/yautja/hunter; post_vendor_spawn_hook only changes icon_state.");
                    Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, row.Id);
                    Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(rsiPath),
                        $"{row.Id} maps CMSS13 pred_mask.dmi icon_state {row.State}.");
                    Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo("icon"), row.Id);
                    Assert.That(prototype.TryGetComponent<ClothingComponent>(out var clothing, factory), Is.True, row.Id);
                    Assert.That(clothing!.RsiPath, Is.EqualTo($"_CMU14/Yautja/masks/{row.State}.rsi"),
                        $"{row.Id} maps CMSS13 item_state_slots WEAR_FACE {row.State}.");
                    Assert.That(cache.TryGetResource<RSIResource>(rsiPath, out var resource), Is.True,
                        $"{row.Id} profile mask RSI exists.");
                    Assert.That(resource!.RSI.TryGetState("equipped-MASK", out _), Is.True,
                        $"{row.Id} maps CMSS13 WEAR_FACE on-mob mask state.");
                });
            }
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new List<EntityUid>();

            try
            {
                foreach (var row in ProfileMaskRows())
                {
                    var uid = entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace);
                    spawned.Add(uid);

                    AssertProfileMaskStaticFacts(entMan, uid, row);
                }
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaSpecialMaskPrototypesMatchCmss13StaticFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var cache = client.ResolveDependency<IResourceCache>();
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;

            foreach (var row in SpecialMaskRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);
                var rsiPath = new ResPath($"/Textures/_CMU14/Yautja/masks/{row.Rsi}.rsi");

                Assert.Multiple(() =>
                {
                    Assert.That(prototype.Name, Is.EqualTo(row.Name), row.Id);
                    Assert.That(prototype.Description, Is.EqualTo(row.Description), row.Id);
                    Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, row.Id);
                    Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(rsiPath), row.Id);
                    Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo("icon"), row.Id);
                    Assert.That(prototype.TryGetComponent<ClothingComponent>(out var clothing, factory), Is.True, row.Id);
                    Assert.That(clothing!.RsiPath, Is.EqualTo($"_CMU14/Yautja/masks/{row.Rsi}.rsi"), row.Id);
                    Assert.That(cache.TryGetResource<RSIResource>(rsiPath, out var resource), Is.True, row.Id);
                    Assert.That(resource!.RSI.Size, Is.EqualTo(row.RsiSize), row.Id);
                    Assert.That(resource.RSI.TryGetState("equipped-MASK", out _), Is.True, row.Id);
                });
            }
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new List<EntityUid>();

            try
            {
                foreach (var row in SpecialMaskRows())
                {
                    var uid = entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace);
                    spawned.Add(uid);

                    AssertSpecialMaskStaticFacts(entMan, uid, row);
                }
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CapePreferenceColorDrivesClientSpriteTint()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid cape = default;
        NetEntity capeNet = default;
        var color = new Color((byte) 0x2a, (byte) 0x5c, (byte) 0x8a);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            cape = entMan.SpawnEntity("CMUYautjaCapeQuarter", map.GridCoords);
            var capeComp = entMan.GetComponent<YautjaCapeComponent>(cape);
            capeComp.Color = color;
            entMan.Dirty(cape, capeComp);
            capeNet = entMan.GetNetEntity(cape);
        });

        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            Assert.That(entMan.TryGetEntity(capeNet, out var clientCape), Is.True);
            Assert.That(entMan.GetComponent<SpriteComponent>(clientCape.Value).Color, Is.EqualTo(color));
        });

        await server.WaitPost(() =>
        {
            if (cape != default && !server.EntMan.Deleted(cape))
                server.EntMan.DeleteEntity(cape);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void MaskAccessoryPreviewLayerIsOffsetToHelmet()
    {
        Assert.That(Content.Client._CMU14.Yautja.YautjaMaskAccessoryVisualSystem.OnMobOffset,
            Is.EqualTo(new Vector2(0f, 0.5f)),
            "CMSS13 mask accessory overlays need to be lifted from the body center onto the helmet in the SS14 preview.");
    }

    [Test]
    public void FlavorTextIsClampedToYautjaLimit()
    {
        var longFlavor = new string('x', YautjaCharacterProfile.MaxFlavorTextLength + 20);
        var yautja = YautjaCharacterProfile.Default.WithFlavorText(longFlavor);

        Assert.That(yautja.FlavorText.Length, Is.EqualTo(YautjaCharacterProfile.MaxFlavorTextLength));
    }

    [Test]
    public void QuillStyleReplacesOnlyYautjaQuillMarking()
    {
        var yautja = YautjaCharacterProfile.Default
            .WithQuillStyle(YautjaQuillStyle.ShortWide);

        Assert.Multiple(() =>
        {
            Assert.That(yautja.QuillMarkingId, Is.EqualTo("CMUYautjaDreadlocksShortWide"));
            Assert.That(yautja.Appearance.Markings,
                Has.Exactly(1).Matches<Marking>(marking => marking.MarkingId == "CMUYautjaDreadlocksShortWide"));
        });
    }

    [Test]
    public void YautjaProfileAlwaysUsesMaleSexAndGender()
    {
        var yautja = YautjaCharacterProfile.Default
            .WithSex(Sex.Female)
            .WithGender(Gender.Female);

        Assert.Multiple(() =>
        {
            Assert.That(yautja.Sex, Is.EqualTo(Sex.Male));
            Assert.That(yautja.Gender, Is.EqualTo(Gender.Male));
        });
    }

    private static IEnumerable<MaskAccessoryRow> MaskAccessoryRows()
    {
        foreach (var material in new[] { "Ebony", "Bronze", "Silver", "Crimson", "Bone" })
        {
            var stateMaterial = material.ToLowerInvariant();
            for (var style = 1; style <= 3; style++)
                yield return new MaskAccessoryRow($"CMUYautjaMaskAccessory{style:00}{material}", $"pred_accessory{style}_{stateMaterial}");
        }
    }

    private sealed record MaskAccessoryRow(string Id, string State);

    private static void AssertProfileMaskStaticFacts(EntityManager entMan, EntityUid uid, ProfileMaskRow row)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(uid);
        var clothing = entMan.GetComponent<ClothingComponent>(uid);
        var armor = entMan.GetComponent<CMArmorComponent>(uid);

        Assert.Multiple(() =>
        {
            Assert.That(meta.EntityName, Is.EqualTo(row.Name), row.Id);
            Assert.That(meta.EntityDescription, Is.EqualTo(row.Description), row.Id);
            Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.MASK | SlotFlags.SUITSTORAGE),
                $"{row.Id} maps source WEAR_FACE and local suit-storage profile replacement slot.");
            Assert.That(entMan.HasComponent<YautjaMaskComponent>(uid), Is.True,
                $"{row.Id} keeps the functional CMSS13 Yautja mask behavior surface.");
            Assert.That(entMan.HasComponent<YautjaMaskAccessoryHolderComponent>(uid), Is.True,
                $"{row.Id} inherits CMSS13 valid_accessory_slots = ACCESSORY_SLOT_YAUTJA_MASK.");
            Assert.That(entMan.HasComponent<YautjaTechItemComponent>(uid), Is.True,
                $"{row.Id} maps source ITEM_PREDATOR.");
            Assert.That(entMan.TryGetComponent<CorrodibleComponent>(uid, out var corrodible), Is.True,
                $"{row.Id} maps source unacidable.");
            Assert.That(corrodible!.IsCorrodible, Is.False, $"{row.Id} maps source unacidable.");
            Assert.That(armor.Melee, Is.EqualTo(40), $"{row.Id} maps hunter mask armor_melee = CLOTHING_ARMOR_MEDIUM.");
            Assert.That(armor.Bullet, Is.EqualTo(50), $"{row.Id} maps hunter mask armor_bullet = CLOTHING_ARMOR_HIGH.");
            Assert.That(armor.Bio, Is.EqualTo(45), $"{row.Id} maps hunter mask armor_bio = CLOTHING_ARMOR_MEDIUMHIGH.");
            Assert.That(armor.ExplosionArmor, Is.EqualTo(50), $"{row.Id} maps hunter mask armor_bomb = CLOTHING_ARMOR_HIGH.");
            Assert.That(entMan.GetComponent<ParasiteResistanceComponent>(uid).MaxCount, Is.EqualTo(100),
                $"{row.Id} inherits CMSS13 hunter anti_hug = 100.");
        });
    }

    private static void AssertSpecialMaskStaticFacts(EntityManager entMan, EntityUid uid, SpecialMaskRow row)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(uid);
        var clothing = entMan.GetComponent<ClothingComponent>(uid);
        var armor = entMan.GetComponent<CMArmorComponent>(uid);

        Assert.Multiple(() =>
        {
            Assert.That(meta.EntityName, Is.EqualTo(row.Name), row.Id);
            Assert.That(meta.EntityDescription, Is.EqualTo(row.Description), row.Id);
            Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.MASK | SlotFlags.SUITSTORAGE), row.Id);
            Assert.That(entMan.HasComponent<YautjaMaskComponent>(uid), Is.True, row.Id);
            Assert.That(entMan.HasComponent<YautjaMaskAccessoryHolderComponent>(uid), Is.True, row.Id);
            Assert.That(entMan.HasComponent<YautjaTechItemComponent>(uid), Is.True, row.Id);
            Assert.That(entMan.TryGetComponent<CorrodibleComponent>(uid, out var corrodible), Is.True, row.Id);
            Assert.That(corrodible!.IsCorrodible, Is.False, row.Id);
            Assert.That(armor.Melee, Is.EqualTo(row.Melee), row.Id);
            Assert.That(armor.Bullet, Is.EqualTo(row.Bullet), row.Id);
            Assert.That(armor.Bio, Is.EqualTo(row.Bio), row.Id);
            Assert.That(armor.ExplosionArmor, Is.EqualTo(row.Explosion), row.Id);
            Assert.That(entMan.GetComponent<ParasiteResistanceComponent>(uid).MaxCount, Is.EqualTo(row.AntiHug), row.Id);
            Assert.That(entMan.GetComponent<RMCImmuneToIgnitionComponent>(uid).IntensityResistance, Is.EqualTo(10),
                $"{row.Id} maps CMSS13 fire_intensity_resistance = 10.");
        });
    }

    private static IEnumerable<ProfileMaskRow> ProfileMaskRows()
    {
        foreach (var material in new[] { "Bone", "Bronze", "Crimson", "Ebony", "Silver" })
        {
            var stateMaterial = material.ToLowerInvariant();
            for (var style = 1; style <= 20; style++)
            {
                var state = $"pred_mask{style}_{stateMaterial}";
                yield return new ProfileMaskRow(
                    $"CMUYautjaMaskPred{style:00}{material}",
                    "clan mask",
                    "A beautifully designed metallic face mask, both ornate and functional.",
                    state);
            }
        }
    }

    private sealed record ProfileMaskRow(string Id, string Name, string Description, string State);

    private static IEnumerable<SpecialMaskRow> SpecialMaskRows()
    {
        const string hunterName = "clan mask";
        const string hunterDescription = "A beautifully designed metallic face mask, both ornate and functional.";
        const string ancientName = "ornate ancient alien mask";
        const string ancientDescription = "An ornate ancient faceplate of an aged alloy, once worn by a revered hunter. Though tarnished by time, its craftsmanship remains exquisite - a fusion of artistry and deadly function.";
        const string thrallName = "alien mask";
        const string thrallDescription = "A simplistic metallic face mask with advanced capabilities.";

        yield return HunterSpecialMask("CMUYautjaMaskAncient", ancientName, ancientDescription, "pred_mask_ancient");
        yield return HunterSpecialMask("CMUYautjaMaskAncientRedGlow", ancientName, ancientDescription, "pred_mask_ancient_redglow");
        yield return HunterSpecialMask("CMUYautjaMaskAncientWhite", ancientName, ancientDescription, "pred_mask_ancient_white");

        foreach (var legacy in new[] { "Collector", "Dragon", "Enforcer", "Swamp" })
            yield return HunterSpecialMask($"CMUYautjaMaskLegacy{legacy}", hunterName, hunterDescription, $"pred_mask_legacy_{legacy.ToLowerInvariant()}");

        yield return HunterSpecialMask("CMUYautjaMaskEliteCleopatra", hunterName, hunterDescription, "pred_mask_elite_cleopatra");
        yield return HunterSpecialMask("CMUYautjaMaskElitePlated", hunterName, hunterDescription, "pred_mask_elite_plated");
        yield return HunterSpecialMask("CMUYautjaMaskUniqueAnubys", hunterName, hunterDescription, "pred_mask_elite_anubys", new Vector2i(32, 64));
        yield return HunterSpecialMask("CMUYautjaMaskUniqueCleopatra", hunterName, hunterDescription, "pred_mask_elite_cleopatra");
        yield return HunterSpecialMask("CMUYautjaMaskUniquePlated", hunterName, hunterDescription, "pred_mask_elite_plated");
        yield return HunterSpecialMask("CMUYautjaMaskUniqueRonin", hunterName, hunterDescription, "pred_mask_elite_ronin", new Vector2i(32, 64));

        foreach (var material in new[] { "Bone", "Crimson", "Ebony", "Gold", "Silver" })
            yield return new SpecialMaskRow(
                $"CMUYautjaMaskThrall{material}",
                thrallName,
                thrallDescription,
                $"thrallmask_{material.ToLowerInvariant()}",
                new Vector2i(32, 32),
                40,
                45,
                40,
                45,
                5);
    }

    private static SpecialMaskRow HunterSpecialMask(
        string id,
        string name,
        string description,
        string rsi,
        Vector2i? rsiSize = null)
    {
        return new SpecialMaskRow(
            id,
            name,
            description,
            rsi,
            rsiSize ?? new Vector2i(32, 32),
            40,
            50,
            45,
            50,
            100);
    }

    private sealed record SpecialMaskRow(
        string Id,
        string Name,
        string Description,
        string Rsi,
        Vector2i RsiSize,
        int Melee,
        int Bullet,
        int Bio,
        int Explosion,
        int AntiHug);
}
