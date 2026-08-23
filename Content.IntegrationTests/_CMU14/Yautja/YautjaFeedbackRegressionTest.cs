using System.Collections.Generic;
using System.Linq;
using Content.Server.Database;
using Content.Server.Station.Systems;
using Content.Server._CMU14.Yautja;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaFeedbackRegressionTest
{
    [Test]
    public async Task ProfileSkinColorSurvivesDeferredYautjaRandomization()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        EntityUid hunter = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
                var profile = YautjaCharacterProfile.Default.WithSkinColor(YautjaSkinColor.Red);
                entMan.System<YautjaProfileApplySystem>().ApplyProfile(hunter, profile);
            });

            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                var appearance = server.EntMan.GetComponent<HumanoidAppearanceComponent>(hunter);
                Assert.That(appearance.SkinColor,
                    Is.EqualTo(YautjaCharacterProfile.GetSkinColorColor(YautjaSkinColor.Red)));
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task PlayerSpawnBracerPreservesBasePrototypeAndTracksEveryProfileMaterialAndLegacyVisual()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);

        try
        {
            await server.WaitPost(() =>
            {
                var mapSystem = server.System<SharedMapSystem>();
                mapSystem.CreateMap(out var mapId);
                var grid = mapSystem.CreateGridEntity(mapId);
                var inventory = server.EntMan.System<InventorySystem>();
                var spawn = server.EntMan.System<StationSpawningSystem>();
                var capabilities = new YautjaProfileCapabilities(YautjaRank.Ancient, canUseUnique: true, canUseLegacy: true);
                var bracerProfiles = new[]
                {
                    YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Retro),
                    YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Ebony),
                    YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Silver),
                    YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Bronze),
                    YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Crimson),
                    YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Bone),
                    YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Dragon),
                    YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Swamp),
                    YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Enforcer),
                    YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Collector),
                    YautjaCharacterProfile.Default.WithLegacy(YautjaLegacySet.Dragon),
                    YautjaCharacterProfile.Default.WithLegacy(YautjaLegacySet.Swamp),
                    YautjaCharacterProfile.Default.WithLegacy(YautjaLegacySet.Enforcer),
                    YautjaCharacterProfile.Default.WithLegacy(YautjaLegacySet.Collector),
                };

                foreach (var yautjaProfile in bracerProfiles)
                {
                    var hunter = spawn.SpawnPlayerMob(
                        new EntityCoordinates(grid, 0, 0),
                        "CMUYautjaHunter",
                        HumanoidCharacterProfile.DefaultWithSpecies("Human").WithYautjaProfile(yautjaProfile),
                        station: null,
                        authoritativeYautjaRank: YautjaRank.Ancient,
                        authoritativeYautjaCapabilities: capabilities);

                    Assert.That(inventory.TryGetSlotEntity(hunter, "gloves", out var bracer), Is.True, yautjaProfile.BracerPrototype);
                    Assert.That(server.EntMan.GetComponent<MetaDataComponent>(bracer.Value).EntityPrototype?.ID,
                        Is.EqualTo("CMUYautjaBracer"), yautjaProfile.BracerPrototype);
                    AssertBracerVisualProfile(server.EntMan, bracer.Value, yautjaProfile.BracerPrototype);
                    server.EntMan.DeleteEntity(hunter);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task PlayerSpawnBracerRetainsProfileSettingsWhenAttachmentBundleIsVended()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var mapSystem = server.System<SharedMapSystem>();
                mapSystem.CreateMap(out var mapId);
                var grid = mapSystem.CreateGridEntity(mapId);
                var coordinates = new EntityCoordinates(grid, 0, 0);
                var yautjaProfile = YautjaCharacterProfile.Default
                    .WithBracer(YautjaBracerMaterial.Bone)
                    .WithCaster(YautjaBracerMaterial.Silver)
                    .WithOwnerRank(YautjaBracerOwnerRank.Elder)
                    .WithTranslatorType(YautjaTranslatorType.Combo)
                    .WithInvisibilitySound(YautjaInvisibilitySound.Retro);
                var hunter = entMan.System<StationSpawningSystem>().SpawnPlayerMob(
                    coordinates,
                    "CMUYautjaHunter",
                    HumanoidCharacterProfile.DefaultWithSpecies("Human").WithYautjaProfile(yautjaProfile),
                    station: null,
                    authoritativeYautjaRank: YautjaRank.Elder,
                    authoritativeYautjaCapabilities: new YautjaProfileCapabilities(YautjaRank.Elder, true, false));
                var inventory = entMan.System<InventorySystem>();

                Assert.That(inventory.TryGetSlotEntity(hunter, "gloves", out var bracer), Is.True);
                Assert.That(entMan.GetComponent<MetaDataComponent>(bracer.Value).EntityPrototype?.ID, Is.EqualTo("CMUYautjaBracer"));
                AssertBracerVisualProfile(entMan, bracer.Value, yautjaProfile.BracerPrototype);

                var bracerComponent = entMan.GetComponent<YautjaBracerComponent>(bracer.Value);
                var gear = entMan.GetComponent<YautjaGearContainerComponent>(bracer.Value);
                Assert.Multiple(() =>
                {
                    Assert.That(bracerComponent.TranslatorType, Is.EqualTo(YautjaTranslatorType.Combo));
                    Assert.That(bracerComponent.InvisibilitySound, Is.EqualTo(YautjaInvisibilitySound.Retro));
                    Assert.That(bracerComponent.OwnerRank, Is.EqualTo(YautjaBracerOwnerRank.Elder));
                    Assert.That(gear.GearPrototypes[YautjaGearKind.Caster].Id, Is.EqualTo("CMUYautjaPlasmaCasterSilver"));
                });

                var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", coordinates);
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var section = vendor.Sections.FindIndex(entry => entry.Name == "Bracer Attachments");
                Assert.That(section, Is.GreaterThanOrEqualTo(0));
                var entry = vendor.Sections[section].Entries.FindIndex(vendorEntry => vendorEntry.Id.Id == "CMUYautjaWristBladesBundle");
                Assert.That(entry, Is.GreaterThanOrEqualTo(0));

                entMan.EventBus.RaiseLocalEvent(rack, new CMVendorVendBuiMsg(section, entry, new())
                {
                    Actor = hunter,
                    UiKey = CMAutomatedVendorUI.Key,
                });

                var wristBlades = new List<EntityUid>();
                var metadata = entMan.EntityQueryEnumerator<MetaDataComponent>();
                while (metadata.MoveNext(out var uid, out var meta))
                {
                    if (meta.EntityPrototype?.ID == "CMUYautjaWristBladesAttachment")
                        wristBlades.Add(uid);
                }

                Assert.That(wristBlades, Has.Count.EqualTo(2), "The bracer-attachment rack vends the paired wrist blades.");
                Assert.That(inventory.TryGetSlotEntity(hunter, "gloves", out var afterVendBracer), Is.True);
                Assert.That(afterVendBracer.Value, Is.EqualTo(bracer.Value));
                AssertBracerVisualProfile(entMan, afterVendBracer.Value, yautjaProfile.BracerPrototype);
                var afterVendComponent = entMan.GetComponent<YautjaBracerComponent>(afterVendBracer.Value);
                var afterVendGear = entMan.GetComponent<YautjaGearContainerComponent>(afterVendBracer.Value);
                Assert.Multiple(() =>
                {
                    Assert.That(afterVendComponent.TranslatorType, Is.EqualTo(YautjaTranslatorType.Combo));
                    Assert.That(afterVendComponent.InvisibilitySound, Is.EqualTo(YautjaInvisibilitySound.Retro));
                    Assert.That(afterVendComponent.OwnerRank, Is.EqualTo(YautjaBracerOwnerRank.Elder));
                    Assert.That(afterVendGear.GearPrototypes[YautjaGearKind.Caster].Id,
                        Is.EqualTo("CMUYautjaPlasmaCasterSilver"));
                });
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task PlayerSpawnBracerProfileVisualsReplicateToClientForEveryMaterialAndLegacyVariant()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var bracers = new List<(NetEntity NetEntity, string VisualPrototype)>();

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var spawn = entMan.System<StationSpawningSystem>();
            var inventory = entMan.System<InventorySystem>();
            var capabilities = new YautjaProfileCapabilities(YautjaRank.Ancient, canUseUnique: true, canUseLegacy: true);
            var profiles = new[]
            {
                YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Retro),
                YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Ebony),
                YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Silver),
                YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Bronze),
                YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Crimson),
                YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Bone),
                YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Dragon),
                YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Swamp),
                YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Enforcer),
                YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Collector),
                YautjaCharacterProfile.Default.WithLegacy(YautjaLegacySet.Dragon),
                YautjaCharacterProfile.Default.WithLegacy(YautjaLegacySet.Swamp),
                YautjaCharacterProfile.Default.WithLegacy(YautjaLegacySet.Enforcer),
                YautjaCharacterProfile.Default.WithLegacy(YautjaLegacySet.Collector),
            };

            foreach (var profile in profiles)
            {
                var hunter = spawn.SpawnPlayerMob(
                    map.GridCoords,
                    "CMUYautjaHunter",
                    HumanoidCharacterProfile.DefaultWithSpecies("Human").WithYautjaProfile(profile),
                    station: null,
                    authoritativeYautjaRank: YautjaRank.Ancient,
                    authoritativeYautjaCapabilities: capabilities);
                Assert.That(inventory.TryGetSlotEntity(hunter, "gloves", out var bracer), Is.True, profile.BracerPrototype);
                Assert.That(entMan.GetComponent<MetaDataComponent>(bracer.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaBracer"), profile.BracerPrototype);
                bracers.Add((entMan.GetNetEntity(bracer.Value), profile.BracerPrototype));
            }
        });

        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            foreach (var (netEntity, visualPrototype) in bracers)
            {
                Assert.That(client.EntMan.TryGetEntity(netEntity, out var bracer), Is.True, visualPrototype);
                Assert.That(client.EntMan.GetComponent<MetaDataComponent>(bracer.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaBracer"), visualPrototype);
                AssertBracerVisualProfile(client.EntMan, bracer.Value, visualPrototype);
                AssertClientBracerVisualsMatchPrototype(client.EntMan, bracer.Value, visualPrototype);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ColdRankCacheResolvesPersistedRankForCharacterInfo()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var userId = pair.Player!.UserId;
        var db = server.ResolveDependency<IServerDbManager>();
        var ranks = server.ResolveDependency<YautjaRankManager>();

        await db.SetYautjaRank(userId.UserId, YautjaRank.Elite);
        // Refresh the clan-resolution layer, then evict only the rank-manager
        // cache to model character-info opening before rank priming completes.
        await ranks.Refresh(userId);
        ranks.InvalidateCached(userId);

        Assert.That(ranks.ResolveCached(userId), Is.EqualTo(YautjaRank.Elite));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MedicompClampStopsExternalBleeding()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        EntityUid patient = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                patient = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
                var body = entMan.System<SharedBodySystem>();
                EntityUid part = default;
                foreach (var (partUid, _) in body.GetBodyChildren(patient))
                {
                    if (entMan.HasComponent<BodyPartComponent>(partUid))
                    {
                        part = partUid;
                        break;
                    }
                }

                Assert.That(part, Is.Not.EqualTo(EntityUid.Invalid));
                var wounds = entMan.EnsureComponent<BodyPartWoundComponent>(part);
                var woundSystem = entMan.System<CMUWoundLedgerSystem>();
                Assert.That(woundSystem.TryUpdateExternalBleeding(part, ExternalBleedTier.Arterial, wounds), Is.True);

                var surgery = entMan.System<SharedCMSurgerySystem>();
                var step = surgery.GetSingleton("CMUSurgeryStepMcompClampWound");
                Assert.That(step, Is.Not.Null);
                var ev = new CMSurgeryStepEvent(patient, patient, part, new List<EntityUid>());
                entMan.EventBus.RaiseLocalEvent(step!.Value, ref ev);

                Assert.That(entMan.GetComponent<BodyPartWoundComponent>(part).ExternalBleeding,
                    Is.EqualTo(ExternalBleedTier.None));
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task MedicompClampClosesIncisionAndSurgicalBleeding()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        EntityUid patient = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                patient = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
                var body = entMan.System<SharedBodySystem>();
                var part = body.GetBodyChildren(patient)
                    .Select(entry => entry.Id)
                    .First(uid => entMan.HasComponent<BodyPartComponent>(uid));

                entMan.EnsureComponent<CMIncisionOpenComponent>(part);
                entMan.EnsureComponent<CMBleedersClampedComponent>(part);
                entMan.EnsureComponent<CMSkinRetractedComponent>(part);
                var wounds = entMan.EnsureComponent<BodyPartWoundComponent>(part);
                var ledger = entMan.System<CMUWoundLedgerSystem>();
                ledger.TryUpdateExternalBleeding(part, ExternalBleedTier.Severe, wounds);

                var woundSystem = entMan.System<SharedCMUWoundsSystem>();
                woundSystem.SeedSurgicalInternalBleed(part);

                var surgery = entMan.System<SharedCMSurgerySystem>();
                var step = surgery.GetSingleton("CMUSurgeryStepMcompClampWound");
                Assert.That(step, Is.Not.Null);
                var ev = new CMSurgeryStepEvent(patient, patient, part, new List<EntityUid>());
                entMan.EventBus.RaiseLocalEvent(step!.Value, ref ev);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMIncisionOpenComponent>(part), Is.False);
                    Assert.That(entMan.HasComponent<CMBleedersClampedComponent>(part), Is.False);
                    Assert.That(entMan.HasComponent<CMSkinRetractedComponent>(part), Is.False);
                    Assert.That(entMan.HasComponent<CMUSurgicalInternalBleedingComponent>(part), Is.False);
                    Assert.That(entMan.GetComponent<BodyPartWoundComponent>(part).ExternalBleeding,
                        Is.EqualTo(ExternalBleedTier.None));
                });
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public void VendorEntriesExposePerUserLimitMetadata()
    {
        Assert.That(typeof(CMVendorEntry).GetField("MaxPerUser"), Is.Not.Null);
        Assert.That(typeof(CMVendorUserComponent).GetField("PurchaseCounts"), Is.Not.Null);
    }

    [Test]
    public async Task YautjaVendorHasInfiniteSharedStockAndPerPlayerCap()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        EntityUid rack = default;
        EntityUid firstUser = default;
        EntityUid secondUser = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);
                firstUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                secondUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionIndex = vendor.Sections.FindIndex(section => section.Name == "Essential Hunting Supplies");
                Assert.That(sectionIndex, Is.GreaterThanOrEqualTo(0));
                var entryIndex = vendor.Sections[sectionIndex].Entries.FindIndex(
                    entry => entry.Id.Id == "CMUYautjaHuntingEquipmentBundle");
                Assert.That(entryIndex, Is.GreaterThanOrEqualTo(0));
                var entry = vendor.Sections[sectionIndex].Entries[entryIndex];

                Assert.Multiple(() =>
                {
                    Assert.That(entry.Amount, Is.Null);
                    Assert.That(entry.MaxPerUser, Is.EqualTo(1));
                });

                var spareIndex = vendor.Sections.FindIndex(section => section.Name == "Spare Equipment");
                Assert.That(spareIndex, Is.GreaterThanOrEqualTo(0));
                var arrow = vendor.Sections[spareIndex].Entries.Single(
                    spareEntry => spareEntry.Id.Id == "CMUYautjaArrow");
                Assert.Multiple(() =>
                {
                    Assert.That(arrow.Amount, Is.Null);
                    Assert.That(arrow.MaxPerUser, Is.EqualTo(10));
                });

                static void Vend(IEntityManager entityManager, EntityUid vendorUid, EntityUid userUid,
                    int section, int item)
                {
                    entityManager.EventBus.RaiseLocalEvent(vendorUid,
                        new CMVendorVendBuiMsg(section, item, new())
                        {
                            Actor = userUid,
                            UiKey = CMAutomatedVendorUI.Key,
                        });
                }

                Vend(entMan, rack, firstUser, sectionIndex, entryIndex);
                var firstState = entMan.GetComponent<CMVendorUserComponent>(firstUser);
                Assert.That(firstState.PurchaseCounts[entry.Id.Id], Is.EqualTo(1));

                Vend(entMan, rack, firstUser, sectionIndex, entryIndex);
                Assert.That(firstState.PurchaseCounts[entry.Id.Id], Is.EqualTo(1));

                Vend(entMan, rack, secondUser, sectionIndex, entryIndex);
                Assert.That(entMan.GetComponent<CMVendorUserComponent>(secondUser).PurchaseCounts[entry.Id.Id],
                    Is.EqualTo(1));
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    private static void AssertBracerVisualProfile(IEntityManager entMan, EntityUid bracer, string visualPrototype)
    {
        Assert.That(entMan.GetComponent<YautjaBracerProfileVisualComponent>(bracer).VisualPrototype?.Id,
            Is.EqualTo(visualPrototype), $"{visualPrototype} selected visual profile");
    }

    private static void AssertClientBracerVisualsMatchPrototype(IEntityManager entMan, EntityUid bracer, string visualPrototype)
    {
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var factory = IoCManager.Resolve<IComponentFactory>();
        var expected = prototypes.Index<EntityPrototype>(visualPrototype);

        Assert.That(expected.TryGetComponent<SpriteComponent>(out var expectedSprite, factory), Is.True, visualPrototype);
        Assert.That(expected.TryGetComponent<IconComponent>(out var expectedIcon, factory), Is.True, visualPrototype);
        Assert.That(expected.TryGetComponent<ItemComponent>(out var expectedItem, factory), Is.True, visualPrototype);
        Assert.That(expected.TryGetComponent<ClothingComponent>(out var expectedClothing, factory), Is.True, visualPrototype);

        var actualSprite = entMan.GetComponent<SpriteComponent>(bracer);
        var actualIcon = entMan.GetComponent<IconComponent>(bracer);
        var actualItem = entMan.GetComponent<ItemComponent>(bracer);
        var actualClothing = entMan.GetComponent<ClothingComponent>(bracer);
        Assert.Multiple(() =>
        {
            Assert.That(actualSprite.BaseRSI?.Path, Is.EqualTo(expectedSprite!.BaseRSI?.Path), $"{visualPrototype} dropped RSI");
            Assert.That(actualSprite.AllLayers.First().RsiState.Name, Is.EqualTo(expectedSprite.AllLayers.First().RsiState.Name), $"{visualPrototype} dropped state");
            Assert.That(actualIcon.Icon, Is.EqualTo(expectedIcon!.Icon), $"{visualPrototype} icon");
            Assert.That(actualItem.RsiPath, Is.EqualTo(expectedItem!.RsiPath), $"{visualPrototype} in-hand RSI");
            Assert.That(actualItem.HeldPrefix, Is.EqualTo(expectedItem.HeldPrefix), $"{visualPrototype} in-hand prefix");
            Assert.That(actualItem.InhandVisuals, Is.EqualTo(expectedItem.InhandVisuals), $"{visualPrototype} in-hand visuals");
            Assert.That(actualClothing.RsiPath, Is.EqualTo(expectedClothing!.RsiPath), $"{visualPrototype} worn RSI");
            Assert.That(actualClothing.EquippedPrefix, Is.EqualTo(expectedClothing.EquippedPrefix), $"{visualPrototype} worn prefix");
            Assert.That(actualClothing.ClothingVisuals, Is.EqualTo(expectedClothing.ClothingVisuals), $"{visualPrototype} worn visuals");
        });
    }
}
