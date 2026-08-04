using System.Collections.Generic;
using System.Linq;
using Content.Server.Database;
using Content.Server.Station.Systems;
using Content.Server._CMU14.Yautja;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
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
    public async Task ProfileBracerMaterialIsAppliedToThePlayerSpawnBracer()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        EntityUid hunter = default;

        try
        {
            EntityCoordinates coordinates = default;
            await server.WaitPost(() =>
            {
                var mapSystem = server.System<SharedMapSystem>();
                mapSystem.CreateMap(out var mapId);
                var grid = mapSystem.CreateGridEntity(mapId);
                coordinates = new EntityCoordinates(grid, 0, 0);

                var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                    .WithYautjaProfile(YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Bone));
                hunter = server.EntMan.System<StationSpawningSystem>().SpawnPlayerMob(
                    coordinates,
                    "CMUYautjaHunter",
                    profile,
                    station: null,
                    authoritativeYautjaRank: YautjaRank.Elite);
            });

            await server.WaitAssertion(() =>
            {
                var inventory = server.EntMan.System<InventorySystem>();
                Assert.That(inventory.TryGetSlotEntity(hunter, "gloves", out var bracer), Is.True);
                Assert.That(server.EntMan.GetComponent<MetaDataComponent>(bracer.Value).EntityPrototype?.ID,
                    Is.EqualTo(YautjaCharacterProfile.Default.WithBracer(YautjaBracerMaterial.Bone).BracerPrototype));
            });
        }
        finally
        {
            server.Dispose();
        }
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
}
