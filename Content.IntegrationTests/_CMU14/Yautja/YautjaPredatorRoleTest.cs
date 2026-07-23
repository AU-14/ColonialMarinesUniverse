using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using Content.Client.Popups;
using Content.Client.ContextMenu.UI;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client._CMU14.Yautja;
using Content.Client.Clickable;
using Content.Client.Interactable.Components;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Client.Verbs.UI;
using Content.Server._CMU14.Yautja;
using Content.Server.Administration.Logs;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Mind;
using Content.Server.Radio;
using Content.Server.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server.Spawners.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._CMU14.Yautja;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Access.Components;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Preferences;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Roles;
using Content.Shared.Speech;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.UnitTesting;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaPredatorRoleTest
{
    [Test]
    public async Task PredatorSpawnAppliesYautjaProfileInsteadOfNormalHumanProfile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stationSpawning = entMan.System<StationSpawningSystem>();
            var inventory = entMan.System<InventorySystem>();
            var containers = entMan.System<SharedContainerSystem>();

            var yautjaAppearance = new HumanoidCharacterAppearance()
                .WithSkinColor(new Color((byte) 56, (byte) 90, (byte) 48))
                .WithEyeColor(Color.Gold)
                .WithHairColor(new Color((byte) 20, (byte) 14, (byte) 10))
                .WithMarkings(new List<Marking>
                {
                    new("CMUYautjaDreadlocksStandard", new List<Color> { new((byte) 20, (byte) 14, (byte) 10) }),
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
                .WithCapeStyle(YautjaCapeStyle.Damaged)
                .WithTranslatorType(YautjaTranslatorType.Combo)
                .WithInvisibilitySound(YautjaInvisibilitySound.Retro);

            var normalProfile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithName("John Human")
                .WithYautjaProfile(yautja);

            var hunter = stationSpawning.SpawnPlayerMob(map.GridCoords, "CMUYautjaHunter", normalProfile, station: null);
            var meta = entMan.GetComponent<MetaDataComponent>(hunter);
            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(hunter);

            Assert.Multiple(() =>
            {
                Assert.That(meta.EntityName, Is.EqualTo("Kainde Amedha"));
                Assert.That(humanoid.Species, Is.EqualTo("Yautja"));
                Assert.That(humanoid.SkinColor, Is.EqualTo(YautjaCharacterProfile.GetSkinColorColor(YautjaSkinColor.Green)));
                Assert.That(humanoid.EyeColor,
                    Is.EqualTo(YautjaCharacterProfile.GetEyeColorColor(YautjaEyeColor.Gold)));
                Assert.That(humanoid.MarkingSet.Markings.Values.SelectMany(markings => markings),
                    Has.Exactly(1).Matches<Marking>(marking => marking.MarkingId == "CMUYautjaDreadlocksLongCurved"));
                AssertEquippedPrototype(entMan, inventory, hunter, "outerClothing", "CMUYautjaClanArmorBronze3");
                AssertEquippedPrototype(entMan, inventory, hunter, "mask", "CMUYautjaMaskPred12Bone");
                AssertEquippedPrototype(entMan, inventory, hunter, "shoes", "CMUYautjaClanGreavesSilver2");
                AssertEquippedPrototype(entMan, inventory, hunter, "gloves", "CMUYautjaBracerCrimson");
                AssertEquippedPrototype(entMan, inventory, hunter, "back", "CMUYautjaCapeDamaged");
                AssertEquippedPrototype(entMan, inventory, hunter, "ears", "CMUYautjaCommunicator");
                AssertEquippedPrototype(entMan, inventory, hunter, "jumpsuit", "CMUYautjaBodyMesh");
                AssertEquippedPrototype(entMan, inventory, hunter, "belt", "CMUYautjaHuntingPouch");
                AssertEquippedPrototype(entMan, inventory, hunter, "pocket1", "CMUYautjaSmartDisc");
                AssertEquippedPrototype(entMan, inventory, hunter, "pocket2", "CMUYautjaMedicompFull");
                Assert.That(inventory.TryGetSlotEntity(hunter, "id", out _), Is.False,
                    "The starter loadout must leave the id slot free for the bracer chip.");

                Assert.That(inventory.TryGetSlotEntity(hunter, "belt", out var belt), Is.True);
                var beltStorage = entMan.GetComponent<StorageComponent>(belt.Value);
                Assert.That(beltStorage.Container.ContainedEntities.Select(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID),
                    Is.EquivalentTo(new[]
                    {
                        "CMUYautjaHuntingTrap",
                        "CMUYautjaHuntingTrap",
                        "CMUYautjaPolishingRag",
                        "CMUYautjaCleanserGelVial",
                        "CMUYautjaRelayBeacon",
                        "CMUYautjaToolbeltFilled",
                        "CMUYautjaHoundObservationPad",
                    }));

                Assert.That(inventory.TryGetSlotEntity(hunter, "pocket2", out var medicomp), Is.True);
                var medicompStorage = entMan.GetComponent<StorageComponent>(medicomp.Value);
                Assert.That(medicompStorage.Container.ContainedEntities.Select(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID),
                    Is.EquivalentTo(new[]
                    {
                        "CMUYautjaStabilizerGel",
                        "CMUYautjaHealingGun",
                        "CMUYautjaWoundClamp",
                        "CMUYautjaAlienHealthAnalyzer",
                        "CMUYautjaAutoInjector",
                        "CMUYautjaAutoInjector",
                        "CMUYautjaAutoInjector",
                        "CMUYautjaHealingGel",
                    }));
                Assert.That(medicompStorage.Container.ContainedEntities
                        .Where(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "CMUYautjaHealingGel")
                        .Sum(uid => entMan.GetComponent<StackComponent>(uid).Count),
                    Is.EqualTo(6));

                var idCards = new List<EntityUid>();
                var idCardQuery = entMan.EntityQueryEnumerator<MetaDataComponent>();
                while (idCardQuery.MoveNext(out var idCard, out var idCardMeta))
                {
                    if (idCardMeta.EntityPrototype?.ID == "CMUYautjaIdCard")
                        idCards.Add(idCard);
                }

                Assert.That(idCards, Has.Count.EqualTo(0),
                    "Yautja starter gear must not spawn a separate ID card before the bracer chip is deployed.");
                Assert.That(inventory.TryGetSlotEntity(hunter, "back", out var equippedCape), Is.True);
                Assert.That(entMan.GetComponent<YautjaCapeComponent>(equippedCape.Value).Color,
                    Is.EqualTo(YautjaCharacterProfile.Default.CapeColor));

                Assert.That(inventory.TryGetSlotEntity(hunter, "mask", out var mask), Is.True);
                Assert.That(containers.TryGetContainer(mask.Value, "cmu-yautja-mask-accessory", out var maskAccessory), Is.True);
                Assert.That(maskAccessory.ContainedEntities, Has.Count.EqualTo(1));
                Assert.That(entMan.GetComponent<MetaDataComponent>(maskAccessory.ContainedEntities[0]).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaMaskAccessory02Bone"));

                Assert.That(inventory.TryGetSlotEntity(hunter, "gloves", out var bracer), Is.True);
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer.Value);
                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer.Value);
                Assert.That(bracerComp.TranslatorType, Is.EqualTo(YautjaTranslatorType.Combo));
                Assert.That(bracerComp.InvisibilitySound, Is.EqualTo(YautjaInvisibilitySound.Retro));
                Assert.That(bracerComp.OwnerRank, Is.EqualTo(YautjaBracerOwnerRank.Elder));
                Assert.That(gearComp.GearPrototypes[YautjaGearKind.Caster].Id, Is.EqualTo("CMUYautjaPlasmaCasterSilver"));

                var action = entMan.SpawnEntity("CMUActionYautjaToggleBracerIdChip", MapCoordinates.Nullspace);
                var deploy = new YautjaToggleBracerIdChipActionEvent
                {
                    Performer = hunter,
                    Action = (action, entMan.GetComponent<ActionComponent>(action)),
                };
                entMan.EventBus.RaiseLocalEvent(bracer.Value, deploy);

                Assert.That(deploy.Handled, Is.True);
                Assert.That(bracerComp.IdChipDeployed, Is.True);
                var chip = bracerComp.IdChip!.Value;
                Assert.That(inventory.TryGetSlotEntity(hunter, "id", out var id), Is.True);
                Assert.That(id, Is.EqualTo(chip));
                var access = entMan.GetComponent<AccessComponent>(chip).Tags.Select(tag => tag.Id);
                Assert.That(access,
                    Is.EquivalentTo(new[] { "CMUAccessYautjaSecure", "CMUAccessYautjaElite", "CMUAccessYautjaElder" }));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PredatorJobIsRoundPlayableWhitelistedYautjaRole()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var job = prototypes.Index<JobPrototype>("CMUYautjaHunter");
            var threatDepartment = prototypes.Index<DepartmentPrototype>("AU14DepartmentThreat");

            Assert.Multiple(() =>
            {
                Assert.That(job.Hidden, Is.False);
                Assert.That(job.Whitelisted, Is.True);
                Assert.That(job.CanBeAntag, Is.False);
                Assert.That(job.Icon.ToString(), Is.EqualTo("CMUYautjaJobIcon"));
                Assert.That(job.JobEntity, Is.EqualTo("CMUMobYautja"));
                Assert.That(job.JobPreviewEntity?.ToString(), Is.EqualTo("CMUMobYautja"));
                Assert.That(job.StartingGear?.ToString(), Is.EqualTo("CMUYautjaHunterGear"));
                Assert.That(job.UsePlayerProfile, Is.False);
                Assert.That(threatDepartment.Roles, Does.Contain("CMUYautjaHunter"));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaBracerDrainFailureUsesValidBoldMarkup()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var resources = server.ResolveDependency<IResourceManager>();
            foreach (var locale in new[] { "ru-RU", "en-US" })
            {
                using var stream = resources.ContentFileRead(new ResPath($"/Locale/{locale}/_CMU14/yautja/yautja.ftl"));
                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();

                Assert.That(text, Does.Contain("[bold]{$charge}/{$max}[/bold]"), locale);
                Assert.That(text, Does.Contain("[bold]{$amount}[/bold]"), locale);
                Assert.That(text, Does.Not.Contain("<bold>"), locale);
                Assert.That(text, Does.Not.Contain("</bold>"), locale);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodJobSpawnsWithSourceBadBloodFactionGearAndAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var inventory = entMan.System<InventorySystem>();
            var containers = entMan.System<SharedContainerSystem>();
            var stationSpawning = entMan.System<StationSpawningSystem>();

            var job = prototypes.Index<JobPrototype>("CMUYautjaBadBlood");
            Assert.Multiple(() =>
            {
                Assert.That(job.Hidden, Is.True,
                    "CMSS13 Bad Blood is a survivor/rack branch, not a public round-start predator slot.");
                Assert.That(job.Whitelisted, Is.True);
                Assert.That(job.CanBeAntag, Is.False);
                Assert.That(job.Icon.ToString(), Is.EqualTo("CMUYautjaJobIcon"));
                Assert.That(job.JobEntity, Is.EqualTo("CMUMobYautjaBadBlood"));
                Assert.That(job.JobPreviewEntity?.ToString(), Is.EqualTo("CMUMobYautjaBadBlood"));
                Assert.That(job.StartingGear?.ToString(), Is.EqualTo("CMUYautjaBadBloodGear"));
                Assert.That(job.UsePlayerProfile, Is.False);
            });

            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithName("John Human")
                .WithYautjaProfile(YautjaCharacterProfile.Default.WithName("Bad Blood Hunter"));
            var badBlood = stationSpawning.SpawnPlayerMob(map.GridCoords, "CMUYautjaBadBlood", profile, station: null);

            try
            {
                Assert.That(entMan.HasComponent<YautjaComponent>(badBlood), Is.True);
                var faction = entMan.GetComponent<NpcFactionMemberComponent>(badBlood);
                Assert.That(faction.Factions.Select(id => id.Id),
                    Is.EquivalentTo(new[] { "CMUYautjaBadBlood" }),
                    "CMSS13 gates Bad Blood behavior on FACTION_YAUTJA_BADBLOOD.");

                AssertEquippedPrototype(entMan, inventory, badBlood, "ears", "CMUYautjaBadBloodCommunicator");
                AssertEquippedPrototype(entMan, inventory, badBlood, "ears2", "CMUYautjaFalconDroneBadBlood");
                AssertEquippedPrototype(entMan, inventory, badBlood, "gloves", "CMUYautjaBadBloodBracer");
                AssertEquippedPrototype(entMan, inventory, badBlood, "back", "CMUYautjaCloakPack");
                AssertEquippedPrototype(entMan, inventory, badBlood, "outerClothing", "CMUYautjaBadBloodArmorPatchwork");
                AssertEquippedPrototype(entMan, inventory, badBlood, "mask", "CMUYautjaMaskBadBloodPatchwork");
                AssertEquippedPrototype(entMan, inventory, badBlood, "shoes", "CMUYautjaBadBloodGreavesPatchwork");
                AssertEquippedPrototype(entMan, inventory, badBlood, "jumpsuit", "CMUYautjaBodyMeshScalable");
                AssertEquippedPrototype(entMan, inventory, badBlood, "pocket2", "CMUYautjaMedicompSurvivor");

                Assert.That(inventory.TryGetSlotEntity(badBlood, "ears", out var communicator), Is.True);
                Assert.That(containers.TryGetContainer(communicator.Value, EncryptionKeyHolderComponent.KeyContainerName, out var keySlots), Is.True);
                var key = keySlots.ContainedEntities.Single();
                Assert.That(entMan.GetComponent<MetaDataComponent>(key).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaBadBloodEncryptionKey"));
                var keyComp = entMan.GetComponent<EncryptionKeyComponent>(key);
                Assert.That(keyComp.Channels, Is.EquivalentTo(new[] { "CMUYautjaBadBlood" }));
                Assert.That(keyComp.DefaultChannel, Is.EqualTo("CMUYautjaBadBlood"));

                Assert.That(inventory.TryGetSlotEntity(badBlood, "gloves", out var bracer), Is.True);
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer.Value);
                Assert.That(bracerComp.BadBlood, Is.True,
                    "CMSS13 /obj/item/clothing/gloves/yautja/hunter/badblood sets badblood = TRUE.");

                var action = entMan.SpawnEntity("CMUActionYautjaToggleBracerIdChip", MapCoordinates.Nullspace);
                try
                {
                    var deploy = new YautjaToggleBracerIdChipActionEvent
                    {
                        Performer = badBlood,
                        Action = (action, entMan.GetComponent<ActionComponent>(action)),
                    };
                    entMan.EventBus.RaiseLocalEvent(bracer.Value, deploy);

                    Assert.That(deploy.Handled, Is.True);
                    Assert.That(bracerComp.IdChipDeployed, Is.True);
                    var chip = bracerComp.IdChip!.Value;
                    Assert.That(inventory.TryGetSlotEntity(badBlood, "id", out var id), Is.True);
                    Assert.That(id, Is.EqualTo(chip));
                    Assert.That(entMan.GetComponent<MetaDataComponent>(chip).EntityPrototype?.ID,
                        Is.EqualTo("CMUYautjaBadBloodBracerIdChip"));
                    Assert.That(entMan.GetComponent<AccessComponent>(chip).Tags.Select(tag => tag.Id),
                        Is.EquivalentTo(new[] { "CMUAccessYautjaBadBlood" }),
                        "CMSS13 badblood bracer chips set access = list(ACCESS_YAUTJA_BADBLOOD).");
                }
                finally
                {
                    if (!entMan.Deleted(action))
                        entMan.DeleteEntity(action);
                }
            }
            finally
            {
                if (!entMan.Deleted(badBlood))
                    entMan.DeleteEntity(badBlood);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PredatorModeRuleUsesTwoToFourSlotsAndDefaultRandomSchedule()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var rulePrototype = prototypes.Index<EntityPrototype>("CMUYautjaPredatorRound");

            Assert.That(rulePrototype.TryGetComponent<YautjaPredatorRoundComponent>(out var predatorRound, server.EntMan.ComponentFactory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(predatorRound!.ModePredator, Is.True);
                Assert.That(predatorRound.MinSlots, Is.EqualTo(2));
                Assert.That(predatorRound.MaxSlots, Is.EqualTo(4));
                Assert.That(predatorRound.PredatorJob.Id, Is.EqualTo("CMUYautjaHunter"));
                Assert.That(predatorRound.HunterShipMap.Id, Is.EqualTo("CMUYautjaHunterShip"));
                Assert.That(YautjaPredatorRoundCVars.RandomEnabled.DefaultValue, Is.True);
                Assert.That(YautjaPredatorRoundCVars.RandomMinimumRounds.DefaultValue, Is.EqualTo(3));
                Assert.That(YautjaPredatorRoundCVars.RandomMaximumRounds.DefaultValue, Is.EqualTo(5));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipGameMapCanBeLoaded()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });
        var server = pair.Server;
        var ticker = server.EntMan.System<GameTicker>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.That(prototypes.TryIndex<GameMapPrototype>("CMUYautjaHunterShip", out var map), Is.True);
            var options = DeserializationOptions.Default with { InitializeMaps = true };
            Assert.DoesNotThrow(() => ticker.LoadGameMap(map!, out _, options));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PredatorRoundStartAutomaticallyLoadsHunterShipZLevels()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;

        try
        {
            await server.WaitPost(() =>
            {
                var cfg = server.CfgMan;
                cfg.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, false);
                cfg.SetCVar(YautjaPredatorRoundCVars.RandomMinimumRounds, 1);
                cfg.SetCVar(YautjaPredatorRoundCVars.RandomMaximumRounds, 1);
                cfg.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, true);
                server.EntMan.System<GameTicker>().RestartRound();
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var ticker = entMan.System<GameTicker>();
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

                var ruleQuery = entMan.EntityQueryEnumerator<YautjaPredatorRoundComponent>();
                Assert.That(ruleQuery.MoveNext(out _, out var rule), Is.True);
                Assert.That(rule.HunterShipLoaded, Is.True);

                var networks = new List<(EntityUid Uid, CMUZLevelsNetworkComponent Component)>();
                var networkQuery = entMan.EntityQueryEnumerator<CMUZLevelsNetworkComponent>();
                while (networkQuery.MoveNext(out var networkUid, out var network))
                {
                    if (network.ZLevels.Keys.ToHashSet().SetEquals(new[] { -1, 0, 1 }))
                        networks.Add((networkUid, network));
                }

                Assert.That(networks, Has.Count.EqualTo(1));
                var loadedNetwork = networks[0];
                foreach (var (depth, mapUid) in loadedNetwork.Component.ZLevels)
                {
                    Assert.That(mapUid, Is.Not.Null);
                    Assert.That(entMan.TryGetComponent(mapUid!.Value, out CMUZLevelMapComponent? mapLevel), Is.True);
                    Assert.That(mapLevel!.NetworkUid, Is.EqualTo(loadedNetwork.Uid));
                    Assert.That(mapLevel.Depth, Is.EqualTo(depth));
                    Assert.That(entMan.TryGetComponent(mapUid.Value, out MapComponent map), Is.True);
                    Assert.That(map!.MapId, Is.Not.EqualTo(MapId.Nullspace));
                }

                var stationSystem = entMan.System<StationSystem>();
                var predatorStations = new HashSet<EntityUid>();
                var spawnQuery = entMan.EntityQueryEnumerator<YautjaHuntSpawnPointComponent, TransformComponent>();
                var huntSpawnPointCount = 0;
                while (spawnQuery.MoveNext(out var spawnUid, out var spawn, out var transform))
                {
                    huntSpawnPointCount++;

                    if (stationSystem.GetOwningStation(spawnUid, transform) is { } station)
                        predatorStations.Add(station);
                }

                if (predatorStations.Count == 0)
                {
                    var hunterMap = prototypes.Index<GameMapPrototype>("CMUYautjaHunterShip");
                    var stationQuery = entMan.EntityQueryEnumerator<StationDataComponent, MetaDataComponent>();
                    while (stationQuery.MoveNext(out var station, out _, out var metadata))
                    {
                        if (metadata.EntityName == hunterMap.MapName)
                            predatorStations.Add(station);
                    }
                }

                Assert.That(huntSpawnPointCount, Is.GreaterThan(0), "Hunter ship map must contain hunt spawn points.");
                var jobSpawnPointCount = 0;
                var jobSpawnQuery = entMan.EntityQueryEnumerator<SpawnPointComponent>();
                while (jobSpawnQuery.MoveNext(out _, out var spawn))
                {
                    if (spawn.SpawnType == SpawnPointType.Job && spawn.Job?.Id == "CMUYautjaHunter")
                        jobSpawnPointCount++;
                }

                Assert.That(jobSpawnPointCount, Is.GreaterThan(0), "Hunter ship map must contain hunter job spawn points.");

                var hunterShuttles = new List<EntityUid>();
                var shuttleQuery = entMan.EntityQueryEnumerator<ShuttleComponent, TransformComponent, MetaDataComponent>();
                while (shuttleQuery.MoveNext(out var shuttle, out _, out var shuttleTransform, out var shuttleMetadata))
                {
                    if (shuttleMetadata.EntityName == "Hunter Shuttle" &&
                        shuttleTransform.MapUid is { } shuttleMap &&
                        loadedNetwork.Component.ZLevels.Values.Contains(shuttleMap))
                    {
                        hunterShuttles.Add(shuttle);
                    }
                }

                Assert.That(hunterShuttles, Has.Count.EqualTo(1),
                    "Hunt initialization must spawn exactly one Hunter Shuttle on the Hunter Ship.");

                Assert.That(predatorStations, Has.Count.EqualTo(1),
                    "Hunter slots must be attached to the hunter ship station, not duplicated on every station.");

                var hunterSlotStations = new List<(EntityUid Station, int? Slots)>();
                var jobsQuery = entMan.EntityQueryEnumerator<StationJobsComponent>();
                while (jobsQuery.MoveNext(out var station, out var stationJobs) )
                {
                    if (!stationJobs.JobList.TryGetValue("CMUYautjaHunter", out var slots))
                        continue;

                    hunterSlotStations.Add((station, slots));
                }

                Assert.That(hunterSlotStations, Has.Count.EqualTo(1));
                Assert.That(hunterSlotStations[0].Station, Is.EqualTo(predatorStations.Single()));
                Assert.That(hunterSlotStations[0].Slots, Is.EqualTo(rule.Slots));

                // A hunter join must consume a slot. The spawn hook is raised
                // before StationSpawningSystem assigns the job, so it must not
                // reset the remaining count back to the round maximum.
                var hunterStation = predatorStations.Single();
                var hunterJobs = entMan.GetComponent<StationJobsComponent>(hunterStation);
                var stationJobsSystem = entMan.System<StationJobsSystem>();
                Assert.That(stationJobsSystem.TryAdjustJobSlot(
                    hunterStation,
                    "CMUYautjaHunter",
                    -1,
                    false,
                    false,
                    hunterJobs), Is.True);
                var remainingHunterSlots = hunterJobs.JobList["CMUYautjaHunter"];
                var selectedYautjaProfile = YautjaCharacterProfile.Default
                    .WithName("Late Join Kainde Amedha")
                    .WithSkinColor(YautjaSkinColor.Green)
                    .WithEyeColor(YautjaEyeColor.Gold)
                    .WithArmor(YautjaGearMaterial.Bronze, 3);
                var selectedLobbyProfile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                    .WithName("Human Lobby Profile")
                    .WithYautjaProfile(selectedYautjaProfile);
                // SpawnPlayerCharacterOnStation raises the same PlayerSpawningEvent
                // used by GameTicker.MakeJoinGame for a late-joining player.
                var spawned = entMan.System<StationSpawningSystem>().SpawnPlayerCharacterOnStation(
                    hunterStation,
                    "CMUYautjaHunter",
                    selectedLobbyProfile);
                try
                {
                    Assert.That(spawned, Is.Not.Null);
                    var spawnedUid = spawned!.Value;
                    var spawnedTransform = entMan.GetComponent<TransformComponent>(spawnedUid);
                    Assert.That(spawnedTransform.MapUid, Is.Not.Null,
                        "A predator late-join must be placed on a real hunter-ship z-level map.");
                    Assert.That(loadedNetwork.Component.ZLevels.Values,
                        Does.Contain(spawnedTransform.MapUid!.Value),
                        "A predator late-join must use a spawn marker on one of the hunter ship z-levels.");

                    var spawnedYautja = entMan.GetComponent<YautjaAppliedProfileComponent>(spawnedUid);
                    var spawnedHumanoid = entMan.GetComponent<HumanoidAppearanceComponent>(spawnedUid);
                    var spawnedMeta = entMan.GetComponent<MetaDataComponent>(spawnedUid);
                    Assert.Multiple(() =>
                    {
                        Assert.That(spawnedMeta.EntityName, Is.EqualTo(selectedYautjaProfile.Name));
                        Assert.That(spawnedYautja.Profile.Name, Is.EqualTo(selectedYautjaProfile.Name));
                        Assert.That(spawnedHumanoid.Species, Is.EqualTo("Yautja"));
                        Assert.That(spawnedHumanoid.SkinColor,
                            Is.EqualTo(YautjaCharacterProfile.GetSkinColorColor(YautjaSkinColor.Green)));
                        Assert.That(spawnedHumanoid.EyeColor,
                            Is.EqualTo(YautjaCharacterProfile.GetEyeColorColor(YautjaEyeColor.Gold)));
                    });
                    Assert.That(hunterJobs.JobList["CMUYautjaHunter"], Is.EqualTo(remainingHunterSlots));
                }
                finally
                {
                    if (spawned is { } spawnedUid && !entMan.Deleted(spawnedUid))
                        entMan.DeleteEntity(spawnedUid);
                }
            });
        }
        finally
        {
            var cfg = server.CfgMan;
            cfg.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, YautjaPredatorRoundCVars.RandomEnabled.DefaultValue);
            cfg.SetCVar(YautjaPredatorRoundCVars.RandomMinimumRounds, YautjaPredatorRoundCVars.RandomMinimumRounds.DefaultValue);
            cfg.SetCVar(YautjaPredatorRoundCVars.RandomMaximumRounds, YautjaPredatorRoundCVars.RandomMaximumRounds.DefaultValue);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PredatorAdminEditorAppliesChanceSlotsAndInitializesHunterShip()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var cfg = server.CfgMan;
            cfg.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, false);

            var session = server.PlayerMan.Sessions.Single();
            var euiManager = server.ResolveDependency<EuiManager>();
            var editor = new Content.Server._CMU14.Yautja.YautjaPredatorAdminEditorEui();
            EntityUid predatorSpawn = default;
            euiManager.OpenEui(editor, session);

            try
            {
                editor.HandleMessage(new YautjaPredatorAdminEditorSetRandomMessage(true, 3, 5));
                Assert.Multiple(() =>
                {
                    Assert.That(cfg.GetCVar(YautjaPredatorRoundCVars.RandomEnabled), Is.True);
                    Assert.That(cfg.GetCVar(YautjaPredatorRoundCVars.RandomMinimumRounds), Is.EqualTo(3));
                    Assert.That(cfg.GetCVar(YautjaPredatorRoundCVars.RandomMaximumRounds), Is.EqualTo(5));
                });

                editor.HandleMessage(new YautjaPredatorAdminEditorSetHunterSlotsMessage(4));
                Assert.That(entMan.System<YautjaPredatorRoundSystem>().ConfiguredHunterSlots, Is.EqualTo(4));

                // Keep this EUI routing test focused on the admin controls. The
                // first-time map load (including Z-level linking) is covered by
                // PredatorRoundStartAutomaticallyLoadsHunterShipZLevels below.
                var ticker = entMan.System<GameTicker>();
                Assert.That(ticker.StartGameRule("CMUYautjaPredatorRound", out var ruleUid), Is.True);
                // A marker makes EnsurePredatorRound take its already-provisioned
                // spawn-point path without loading the large map into this
                // connected EUI test.
                predatorSpawn = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", MapCoordinates.Nullspace);
                editor.HandleMessage(new YautjaPredatorAdminEditorInitializeMessage());

                var ruleQuery = entMan.EntityQueryEnumerator<YautjaPredatorRoundComponent>();
                Assert.That(ruleQuery.MoveNext(out _, out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rule.Slots, Is.EqualTo(4));
                    Assert.That(rule.MinSlots, Is.EqualTo(4));
                    Assert.That(rule.MaxSlots, Is.EqualTo(4));
                    Assert.That(rule.HunterShipLoaded, Is.True);
                });

                var state = editor.GetNewState();
                Assert.That(state, Is.TypeOf<YautjaPredatorAdminEditorEuiState>());
                var editorState = (YautjaPredatorAdminEditorEuiState) state;
                Assert.Multiple(() =>
                {
                    Assert.That(editorState.HuntInitialized, Is.True);
                    Assert.That(editorState.ActiveHunterSlots, Is.EqualTo(4));
                    Assert.That(editorState.HunterSlots, Is.EqualTo(4));
                    Assert.That(editorState.RandomEnabled, Is.True);
                    Assert.That(editorState.RandomMinimumRounds, Is.EqualTo(3));
                    Assert.That(editorState.RandomMaximumRounds, Is.EqualTo(5));
                });
            }
            finally
            {
                editor.Close();
                if (predatorSpawn.IsValid() && !entMan.Deleted(predatorSpawn))
                    entMan.DeleteEntity(predatorSpawn);
                cfg.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, YautjaPredatorRoundCVars.RandomEnabled.DefaultValue);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipPredatorMarkersAreJobSpawnPoints()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var clan = prototypes.Index<EntityPrototype>("CMUHunterShipMarkerClanSpawn");
            Assert.That(clan.TryGetComponent<SpawnPointComponent>(out var clanSpawn, server.EntMan.ComponentFactory), Is.True);
            Assert.That(clanSpawn!.SpawnType, Is.EqualTo(SpawnPointType.LateJoin));

            var predator = prototypes.Index<EntityPrototype>("CMUHunterShipMarkerPredatorSpawn");
            Assert.That(predator.TryGetComponent<SpawnPointComponent>(out var predatorSpawn, server.EntMan.ComponentFactory), Is.True);
            Assert.That(predatorSpawn!.SpawnType, Is.EqualTo(SpawnPointType.Job));
            Assert.That(predatorSpawn.Job.ToString(), Is.EqualTo("CMUYautjaHunter"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconDestinationOptionsMatchCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.EntMan.ComponentFactory;

            var relay = prototypes.Index<EntityPrototype>("CMUYautjaRelayBeacon");
            Assert.That(relay.TryGetComponent<YautjaRelayBeaconComponent>(out var relayBeacon, componentFactory), Is.True);
            Assert.That(Enum.TryParse<YautjaRelayDestinationKind>("Ground", out var ground), Is.True,
                "The relay beacon needs a distinct multi-point ground destination kind.");
            Assert.That(relayBeacon!.AllowedDestinations, Is.EqualTo(new[]
            {
                YautjaRelayDestinationKind.YautjaShip,
                YautjaRelayDestinationKind.HumanShip,
                ground,
            }));
            Assert.That(relayBeacon.PulseSound, Is.TypeOf<SoundPathSpecifier>());
            var signalPath = new ResPath("/Audio/_CMU14/Yautja/signal.ogg");
            Assert.That(((SoundPathSpecifier) relayBeacon.PulseSound).Path, Is.EqualTo(signalPath),
                "CMSS13 relay beacon attack_self() plays sound/ambience/signal.ogg when starting the teleport do-after.");
            Assert.That(server.ResolveDependency<IResourceManager>().ContentFileExists(signalPath), Is.True,
                "The CMSS13 sound/ambience/signal.ogg relay pulse asset should be imported, not just guarded as a missing file.");

            var simpleRelay = prototypes.Index<EntityPrototype>("CMUYautjaSimpleRelayBeacon");
            Assert.That(simpleRelay.TryGetComponent<YautjaRelayBeaconComponent>(out var simpleBeacon, componentFactory), Is.True);
            Assert.That(simpleBeacon!.AllowedDestinations, Is.EqualTo(new[]
            {
                YautjaRelayDestinationKind.YautjaShip,
            }));

            var clanSpawn = prototypes.Index<EntityPrototype>("CMUHunterShipMarkerClanSpawn");
            Assert.That(clanSpawn.TryGetComponent<YautjaRelayDestinationComponent>(out var clanDestination, componentFactory), Is.True);
            Assert.That(clanDestination!.Kind, Is.EqualTo(YautjaRelayDestinationKind.YautjaShip));

            var predatorSpawn = prototypes.Index<EntityPrototype>("CMUHunterShipMarkerPredatorSpawn");
            Assert.That(predatorSpawn.TryGetComponent<YautjaRelayDestinationComponent>(out var predatorDestination, componentFactory), Is.True);
            Assert.That(predatorDestination!.Kind, Is.EqualTo(YautjaRelayDestinationKind.YautjaShip));

            var humanShip = prototypes.Index<EntityPrototype>("CMUYautjaHumanShipRelayDestination");
            Assert.That(humanShip.TryGetComponent<YautjaRelayDestinationComponent>(out var humanShipDestination, componentFactory), Is.True);
            Assert.That(humanShipDestination!.Kind, Is.EqualTo(YautjaRelayDestinationKind.HumanShip));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GroundRelayDestinationContractExists()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>("CMUYautjaGroundRelayDestination", out var marker), Is.True);
            Assert.That(marker!.TryGetComponent<YautjaRelayDestinationComponent>(out var destination, componentFactory), Is.True);
            Assert.That(destination!.Kind.ToString(), Is.EqualTo("Ground"));

            Assert.That(typeof(YautjaRelayBeaconDestinationEntry)
                    .GetField("DestinationId", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null,
                "Map-placed destinations must be selected by a stable ID rather than an entity-query index.");
            Assert.That(typeof(YautjaRelayBeaconDestinationMsg)
                    .GetField("DestinationId", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null,
                "The relay UI message must carry the selected map marker ID to the server.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconShowsAndUsesMultipleGroundDestinationsById()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid beacon = default;
        EntityUid first = default;
        EntityUid second = default;
        MapCoordinates secondCoordinates = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                first = entMan.SpawnEntity("CMUYautjaGroundRelayDestination", map.GridCoords.Offset(new Vector2(12, 0)));
                second = entMan.SpawnEntity("CMUYautjaGroundRelayDestination", map.GridCoords.Offset(new Vector2(16, 0)));

                entMan.GetComponent<YautjaRelayDestinationComponent>(first).Id = "test-ground-first";
                entMan.GetComponent<YautjaRelayDestinationComponent>(first).DisplayName = "First Ground";
                entMan.GetComponent<YautjaRelayDestinationComponent>(second).Id = "test-ground-second";
                entMan.GetComponent<YautjaRelayDestinationComponent>(second).DisplayName = "Second Ground";
                secondCoordinates = transform.GetMapCoordinates(second);

                Assert.That(hands.TryPickupAnyHand(hunter, beacon), Is.True);
                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(beacon, use);
                Assert.That(use.Handled, Is.True);
                Assert.That(ui.TryGetUiState<YautjaRelayBeaconState>(beacon, YautjaRelayBeaconUIKey.Key, out var state), Is.True);

                var ground = state!.Destinations
                    .Where(destination => destination.Kind.ToString() == "Ground")
                    .ToArray();
                Assert.That(ground, Has.Length.EqualTo(2));
                Assert.That(ground.Select(destination => destination.DestinationId), Is.EquivalentTo(new[]
                {
                    "test-ground-first",
                    "test-ground-second",
                }));

                var selected = ground.Single(destination => destination.DestinationId == "test-ground-second");
                ui.RaiseUiMessage(beacon, YautjaRelayBeaconUIKey.Key,
                    new YautjaRelayBeaconDestinationMsg(selected.Kind, selected.CustomIndex, selected.DestinationId)
                    {
                        Actor = hunter,
                    });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(10.5f));

            await server.WaitAssertion(() =>
            {
                var actual = server.EntMan.System<SharedTransformSystem>().GetMapCoordinates(hunter);
                Assert.That(actual.MapId, Is.EqualTo(secondCoordinates.MapId));
                Assert.That(actual.Position, Is.EqualTo(secondCoordinates.Position));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, beacon, first, second })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InvalidGroundRelayDestinationsAreOmittedAndCannotStartRelay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid beacon = default;
        var destinations = new List<EntityUid>();

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);

                var valid = entMan.SpawnEntity("CMUYautjaGroundRelayDestination", map.GridCoords.Offset(new Vector2(12, 0)));
                var blank = entMan.SpawnEntity("CMUYautjaGroundRelayDestination", map.GridCoords.Offset(new Vector2(14, 0)));
                var duplicate = entMan.SpawnEntity("CMUYautjaGroundRelayDestination", map.GridCoords.Offset(new Vector2(16, 0)));
                var nullspace = entMan.SpawnEntity("CMUYautjaGroundRelayDestination", MapCoordinates.Nullspace);
                destinations.AddRange(new[] { valid, blank, duplicate, nullspace });

                entMan.GetComponent<YautjaRelayDestinationComponent>(valid).Id = "valid-ground";
                entMan.GetComponent<YautjaRelayDestinationComponent>(valid).DisplayName = "Valid Ground";
                entMan.GetComponent<YautjaRelayDestinationComponent>(blank).Id = " ";
                entMan.GetComponent<YautjaRelayDestinationComponent>(blank).DisplayName = "Blank ID";
                entMan.GetComponent<YautjaRelayDestinationComponent>(duplicate).Id = "valid-ground";
                entMan.GetComponent<YautjaRelayDestinationComponent>(duplicate).DisplayName = "Duplicate Ground";
                entMan.GetComponent<YautjaRelayDestinationComponent>(nullspace).Id = "nullspace-ground";
                entMan.GetComponent<YautjaRelayDestinationComponent>(nullspace).DisplayName = "Nullspace Ground";

                Assert.That(hands.TryPickupAnyHand(hunter, beacon), Is.True);
                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(beacon, use);
                Assert.That(use.Handled, Is.True);
                Assert.That(ui.TryGetUiState<YautjaRelayBeaconState>(beacon, YautjaRelayBeaconUIKey.Key, out var state), Is.True);

                var ground = state!.Destinations
                    .Where(destination => destination.Kind == YautjaRelayDestinationKind.Ground)
                    .ToArray();
                Assert.That(ground.Select(destination => destination.DestinationId), Is.EqualTo(new[] { "valid-ground" }));

                foreach (var invalidId in new[] { " ", "nullspace-ground" })
                {
                    ui.RaiseUiMessage(beacon, YautjaRelayBeaconUIKey.Key,
                        new YautjaRelayBeaconDestinationMsg(YautjaRelayDestinationKind.Ground, -1, invalidId)
                        {
                            Actor = hunter,
                        });
                }

                Assert.That(ActiveRelayDoAfters(entMan, hunter), Is.EqualTo(0));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in destinations.Append(hunter).Append(beacon))
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [TestCase(true, false, false, true)]
    [TestCase(false, false, true, true)]
    [TestCase(true, true, false, false)]
    [TestCase(true, true, true, false)]
    [TestCase(false, false, false, false)]
    public void RelayBeaconUsePolicyBlocksYoungbloodLikeCmss13(
        bool yautja,
        bool youngblood,
        bool techAuthorized,
        bool expected)
    {
        Assert.That(YautjaItemSystem.CanUseRelayBeacon(yautja, youngblood, techAuthorized), Is.EqualTo(expected));
    }

    [Test]
    public async Task RelayBeaconGrantsCmss13TeleLocAction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var actions = entMan.System<ActionContainerSystem>();

            var action = prototypes.Index<EntityPrototype>("CMUActionYautjaAddTeleporterLocation");
            Assert.That(action.TryGetComponent<InstantActionComponent>(out var instant, entMan.ComponentFactory), Is.True);
            Assert.That(instant!.Event, Is.TypeOf<YautjaAddTeleporterLocationActionEvent>());

            var relayPrototype = prototypes.Index<EntityPrototype>("CMUYautjaRelayBeacon");
            Assert.That(relayPrototype.TryGetComponent<YautjaRelayBeaconComponent>(out var relayPrototypeComp, entMan.ComponentFactory), Is.True);
            Assert.That(relayPrototypeComp!.AddTeleporterLocationActionId.Id, Is.EqualTo("CMUActionYautjaAddTeleporterLocation"));

            var hands = entMan.System<SharedHandsSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);

            try
            {
                Assert.That(hands.TryPickupAnyHand(hunter, beacon), Is.True);
                var ev = new GetItemActionsEvent(actions, hunter, beacon);
                entMan.EventBus.RaiseLocalEvent(beacon, ev);

                var granted = ev.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
                    .ToHashSet();

                Assert.That(granted, Does.Contain("CMUActionYautjaAddTeleporterLocation"));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(beacon))
                    entMan.DeleteEntity(beacon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocActionRequiresBeaconInUserContentsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var groundBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
            var heldBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);

            try
            {
                var hands = entMan.System<SharedHandsSystem>();
                Assert.That(hands.TryPickupAnyHand(hunter, heldBeacon), Is.True);

                var groundEvent = new GetItemActionsEvent(actions, hunter, groundBeacon);
                entMan.EventBus.RaiseLocalEvent(groundBeacon, groundEvent);

                var heldEvent = new GetItemActionsEvent(actions, hunter, heldBeacon);
                entMan.EventBus.RaiseLocalEvent(heldBeacon, heldEvent);

                Assert.Multiple(() =>
                {
                    Assert.That(ActionPrototypeIds(entMan, groundEvent.Actions),
                        Does.Not.Contain("CMUActionYautjaAddTeleporterLocation"));
                    Assert.That(ActionPrototypeIds(entMan, heldEvent.Actions),
                        Does.Contain("CMUActionYautjaAddTeleporterLocation"));
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(groundBeacon))
                    entMan.DeleteEntity(groundBeacon);
                if (!entMan.Deleted(heldBeacon))
                    entMan.DeleteEntity(heldBeacon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SimpleRelayBeaconDoesNotGrantTeleLocActionLikeCmss13ThrallTeleporter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var bloodedThrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            entMan.EnsureComponent<YautjaTechAuthorizedComponent>(bloodedThrall);
            var simpleBeacon = entMan.SpawnEntity("CMUYautjaSimpleRelayBeacon", map.GridCoords);

            try
            {
                Assert.That(hands.TryPickupAnyHand(bloodedThrall, simpleBeacon), Is.True);

                var ev = new GetItemActionsEvent(actions, bloodedThrall, simpleBeacon);
                entMan.EventBus.RaiseLocalEvent(simpleBeacon, ev);

                Assert.That(ActionPrototypeIds(entMan, ev.Actions),
                    Does.Not.Contain("CMUActionYautjaAddTeleporterLocation"),
                    "CMSS13 /obj/item/device/thrall_teleporter only implements attack_self(); it does not inherit yautja_teleporter/add_tele_loc().");
            }
            finally
            {
                if (!entMan.Deleted(bloodedThrall))
                    entMan.DeleteEntity(bloodedThrall);
                if (!entMan.Deleted(simpleBeacon))
                    entMan.DeleteEntity(simpleBeacon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SimpleRelayBeaconTeleportsDirectlyToYautjaShipLikeCmss13ThrallTeleporter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid bloodedThrall = default;
        EntityUid simpleBeacon = default;
        EntityUid yautjaShipDestination = default;
        EntityUid humanShipDestination = default;
        MapCoordinates yautjaShipCoordinates = default;
        MapCoordinates humanShipCoordinates = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();

                bloodedThrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(bloodedThrall);
                simpleBeacon = entMan.SpawnEntity("CMUYautjaSimpleRelayBeacon", map.GridCoords);
                yautjaShipDestination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(14, 0)));
                humanShipDestination = entMan.SpawnEntity("CMUYautjaHumanShipRelayDestination", map.GridCoords.Offset(new Vector2(20, 0)));
                yautjaShipCoordinates = transform.GetMapCoordinates(yautjaShipDestination);
                humanShipCoordinates = transform.GetMapCoordinates(humanShipDestination);

                Assert.That(hands.TryPickupAnyHand(bloodedThrall, simpleBeacon), Is.True);

                var use = new UseInHandEvent(bloodedThrall);
                entMan.EventBus.RaiseLocalEvent(simpleBeacon, use);

                Assert.Multiple(() =>
                {
                    Assert.That(use.Handled, Is.True);
                    Assert.That(ui.IsUiOpen(simpleBeacon, YautjaRelayBeaconUIKey.Key, bloodedThrall), Is.False,
                        "CMSS13 /obj/item/device/thrall_teleporter/attack_self() does not open the yautja_teleporter ship-selection UI.");
                    Assert.That(ActiveRelayDoAfters(entMan, bloodedThrall), Is.EqualTo(1));
                });

                var doAfter = entMan.GetComponent<DoAfterComponent>(bloodedThrall);
                var active = doAfter.DoAfters.Values.Single(active =>
                    !active.Cancelled &&
                    !active.Completed &&
                    active.Args.Event is YautjaRelayBeaconDoAfterEvent);
                var relayEvent = (YautjaRelayBeaconDoAfterEvent) active.Args.Event;

                Assert.Multiple(() =>
                {
                    Assert.That(active.Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(10)));
                    Assert.That(relayEvent.Destination, Is.EqualTo(YautjaRelayDestinationKind.YautjaShip));
                    Assert.That(relayEvent.CustomIndex, Is.EqualTo(-1));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(10.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var transform = entMan.System<SharedTransformSystem>();
                var actual = transform.GetMapCoordinates(bloodedThrall);

                Assert.Multiple(() =>
                {
                    Assert.That(actual.MapId, Is.EqualTo(yautjaShipCoordinates.MapId));
                    Assert.That(actual.Position, Is.EqualTo(yautjaShipCoordinates.Position));
                    Assert.That(actual.Position, Is.Not.EqualTo(humanShipCoordinates.Position),
                        "The simple thrall relay should resolve only the Yautja ship spawnpoint, never the Human Ship relay destination.");
                    Assert.That(ActiveRelayDoAfters(entMan, bloodedThrall), Is.EqualTo(0));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { bloodedThrall, simpleBeacon, yautjaShipDestination, humanShipDestination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SimpleRelayBeaconSuccessfulDoAfterDecloaksUserLikeCmss13ThrallTeleporter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid bloodedThrall = default;
        EntityUid simpleBeacon = default;
        EntityUid bracer = default;
        EntityUid yautjaShipDestination = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var inventory = entMan.System<InventorySystem>();

                bloodedThrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(bloodedThrall);
                simpleBeacon = entMan.SpawnEntity("CMUYautjaSimpleRelayBeacon", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                yautjaShipDestination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(14, 0)));

                Assert.That(inventory.TryEquip(bloodedThrall, bracer, "gloves", silent: true, force: true), Is.True);
                MakeActivelyCloaked(entMan, bloodedThrall);
                Assert.That(hands.TryPickupAnyHand(bloodedThrall, simpleBeacon), Is.True);

                var use = new UseInHandEvent(bloodedThrall);
                entMan.EventBus.RaiseLocalEvent(simpleBeacon, use);

                Assert.That(use.Handled, Is.True);
                Assert.That(ActiveRelayDoAfters(entMan, bloodedThrall), Is.EqualTo(1));
            });

            await pair.RunTicksSync(pair.SecondsToTicks(10.5f));
            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(bloodedThrall), Is.False,
                    "CMSS13 thrall_teleporter attack_self() sends COMSIG_MOB_EFFECT_CLOAK_CANCEL to the user before teleporting.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { bloodedThrall, simpleBeacon, bracer, yautjaShipDestination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SimpleRelayBeaconSuccessfulDoAfterDecloaksPulledLivingPassengerAndTeleportsTrainLikeCmss13ThrallTeleporter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid bloodedThrall = default;
        EntityUid passenger = default;
        EntityUid simpleBeacon = default;
        EntityUid passengerBracer = default;
        EntityUid yautjaShipDestination = default;
        MapCoordinates yautjaShipCoordinates = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var inventory = entMan.System<InventorySystem>();
                var pulling = entMan.System<PullingSystem>();
                var transform = entMan.System<SharedTransformSystem>();

                bloodedThrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(bloodedThrall);
                passenger = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                simpleBeacon = entMan.SpawnEntity("CMUYautjaSimpleRelayBeacon", map.GridCoords);
                passengerBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                yautjaShipDestination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(14, 0)));
                yautjaShipCoordinates = transform.GetMapCoordinates(yautjaShipDestination);

                Assert.That(inventory.TryEquip(passenger, passengerBracer, "gloves", silent: true, force: true), Is.True);
                MakeActivelyCloaked(entMan, passenger);
                Assert.That(pulling.TryStartPull(bloodedThrall, passenger), Is.True);
                Assert.That(hands.TryPickupAnyHand(bloodedThrall, simpleBeacon), Is.True);

                var use = new UseInHandEvent(bloodedThrall);
                entMan.EventBus.RaiseLocalEvent(simpleBeacon, use);

                Assert.That(use.Handled, Is.True);
                Assert.That(ActiveRelayDoAfters(entMan, bloodedThrall), Is.EqualTo(1));
            });

            await pair.RunTicksSync(pair.SecondsToTicks(10.5f));
            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var transform = entMan.System<SharedTransformSystem>();
                var userCoordinates = transform.GetMapCoordinates(bloodedThrall);
                var passengerCoordinates = transform.GetMapCoordinates(passenger);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(passenger), Is.False,
                        "CMSS13 thrall_teleporter attack_self() sends COMSIG_MOB_EFFECT_CLOAK_CANCEL to a pulled living mob passenger.");
                    Assert.That(userCoordinates.MapId, Is.EqualTo(yautjaShipCoordinates.MapId));
                    Assert.That(userCoordinates.Position, Is.EqualTo(yautjaShipCoordinates.Position));
                    Assert.That(passengerCoordinates.MapId, Is.EqualTo(yautjaShipCoordinates.MapId));
                    Assert.That(passengerCoordinates.Position, Is.EqualTo(yautjaShipCoordinates.Position));
                    Assert.That(entMan.GetComponent<PullerComponent>(bloodedThrall).Pulling, Is.EqualTo(passenger));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { bloodedThrall, passenger, simpleBeacon, passengerBracer, yautjaShipDestination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocStoresCustomDestination()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
            var action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                var actionComp = entMan.EnsureComponent<ActionComponent>(action);
                var ev = new YautjaAddTeleporterLocationActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(beacon, ev);

                Assert.That(ev.Handled, Is.True);

                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords),
                    "Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(beacon, saved);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.That(relay.CustomDestinations.Count, Is.EqualTo(1));
                Assert.That(relay.CustomDestinations[0].Name, Is.EqualTo("Trophy Hall"));
                Assert.That(relay.CustomDestinations[0].Coordinates, Is.EqualTo(map.GridCoords));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(beacon))
                    entMan.DeleteEntity(beacon);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocNamingDialogUsesCmss13TextInput()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
            var action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                var actionComp = entMan.EnsureComponent<ActionComponent>(action);
                var ev = new YautjaAddTeleporterLocationActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(beacon, ev);

                Assert.That(ev.Handled, Is.True);
                Assert.That(entMan.TryGetComponent(beacon, out DialogComponent? dialog), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(dialog!.DialogType, Is.EqualTo(DialogType.Input));
                    Assert.That(dialog.Title, Is.EqualTo("Text"),
                        "CMSS13 add_tele_loc() uses input(\"What would you like to name this location?\", \"Text\") for naming saved destinations.");
                    Assert.That(dialog.Message.Text, Is.EqualTo(Loc.GetString("cmu-yautja-relay-add-destination-prompt")));
                    Assert.That(dialog.LargeInput, Is.False);
                    Assert.That(dialog.MinCharacterLimit, Is.EqualTo(1));
                    Assert.That(dialog.InputEvent, Is.TypeOf<YautjaRelayBeaconNameDestinationEvent>());
                });

                var blank = (YautjaRelayBeaconNameDestinationEvent) dialog!.InputEvent! with { Message = "   " };
                entMan.EventBus.RaiseLocalEvent(beacon, blank);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.That(relay.CustomDestinations, Is.Empty,
                    "CMSS13 add_tele_loc() returns false when the nullable text input produces no name.");
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(beacon))
                    entMan.DeleteEntity(beacon);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocWritesSourceShapedAdminLogAndBroadcastsToYautja()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            AdminLogsEnabled = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var adminLogs = server.ResolveDependency<IAdminLogManager>();

        EntityUid hunter = default;
        EntityUid beacon = default;
        EntityUid? previousAttached = null;
        var expectedArea = string.Empty;
        var expectedBroadcast = string.Empty;
        const string destinationName = "Trophy Hall";
        const string hunterName = "A'ke Ret";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var areas = entMan.System<AreaSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                ClearRelayTeleLocDestinations(entMan);
                entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                metadata.SetEntityName(hunter, hunterName);
                server.PlayerMan.SetAttachedEntity(session, hunter);
                expectedArea = areas.GetAreaName(hunter);
                expectedBroadcast = Loc.GetString(
                    "cmu-yautja-relay-add-destination-broadcast",
                    ("hunter", hunterName),
                    ("name", destinationName),
                    ("location", map.GridCoords.ToString()),
                    ("area", expectedArea));
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords),
                    destinationName);

                entMan.EventBus.RaiseLocalEvent(beacon, saved);
            });

            await pair.ReallyBeIdle(10);

            var logs = await adminLogs.CurrentRoundLogs(new LogFilter
            {
                Types = new HashSet<LogType> { LogType.Action },
            });
            var messages = logs.Select(log => log.Message).ToList();
            var joinedMessages = string.Join("\n", messages);

            Assert.That(
                messages.Any(message =>
                    message.Contains("has created a new teleport location at", StringComparison.OrdinalIgnoreCase) &&
                    message.Contains(expectedArea, StringComparison.OrdinalIgnoreCase)),
                Is.True,
                $"CMSS13 add_tele_loc() logs '[user] ([user.key]) has created a new teleport location at [get_area(user)]'.\nActual logs:\n{joinedMessages}");

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels,
                    Does.Contain(expectedBroadcast),
                    $"CMSS13 add_tele_loc() broadcasts '[user.real_name] has created a new teleport location, [name], at [user.loc] in [get_area(user)]' to Yautja.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, beacon })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocRejectsDeadUserLikeCmss13StatCheck()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);

            try
            {
                mobState.ChangeMobState(hunter, MobState.Dead);

                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords),
                    "Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(beacon, saved);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.That(relay.CustomDestinations, Is.Empty);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(beacon))
                    entMan.DeleteEntity(beacon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocRejectsCriticalUserLikeCmss13StatCheck()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);

            try
            {
                mobState.ChangeMobState(hunter, MobState.Critical);

                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords),
                    "Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(beacon, saved);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.That(relay.CustomDestinations, Is.Empty,
                    "CMSS13 add_tele_loc() returns false for any usr.stat value, including critical/unconscious users.");
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(beacon))
                    entMan.DeleteEntity(beacon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocBroadcastSkipsBadBloodLikeCmss13DefaultHuntingNetwork()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid badBlood = default;
        EntityUid beacon = default;
        EntityUid? previousAttached = null;
        var expectedBroadcast = string.Empty;
        const string destinationName = "Hidden Shrine";
        const string hunterName = "Sek Met";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var areas = entMan.System<AreaSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                ClearRelayTeleLocDestinations(entMan);
                entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                badBlood = entMan.SpawnEntity("CMUMobYautjaBadBlood", map.GridCoords.Offset(new Vector2(1, 0)));
                beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                metadata.SetEntityName(hunter, hunterName);
                server.PlayerMan.SetAttachedEntity(session, badBlood);

                expectedBroadcast = Loc.GetString(
                    "cmu-yautja-relay-add-destination-broadcast",
                    ("hunter", hunterName),
                    ("name", destinationName),
                    ("location", map.GridCoords.ToString()),
                    ("area", areas.GetAreaName(hunter)));
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords),
                    destinationName);

                entMan.EventBus.RaiseLocalEvent(beacon, saved);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels,
                    Does.Not.Contain(expectedBroadcast),
                    $"CMSS13 message_all_yautja() defaults to YAUTJA_NET_HUNTING; pred_can_receive_message() should not deliver that network to FACTION_YAUTJA_BADBLOOD recipients without a hunting-network bracer.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, badBlood, beacon })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }

                ClearRelayTeleLocDestinations(entMan);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocBroadcastSkipsDeadYautjaLikeCmss13PredCanReceiveMessage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid deadYautja = default;
        EntityUid beacon = default;
        EntityUid? previousAttached = null;
        var expectedBroadcast = string.Empty;
        const string destinationName = "Silent Hall";
        const string hunterName = "Kai Dek";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var areas = entMan.System<AreaSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                ClearRelayTeleLocDestinations(entMan);
                entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                deadYautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(1, 0)));
                beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                metadata.SetEntityName(hunter, hunterName);
                mobState.ChangeMobState(deadYautja, MobState.Dead);
                server.PlayerMan.SetAttachedEntity(session, deadYautja);

                expectedBroadcast = Loc.GetString(
                    "cmu-yautja-relay-add-destination-broadcast",
                    ("hunter", hunterName),
                    ("name", destinationName),
                    ("location", map.GridCoords.ToString()),
                    ("area", areas.GetAreaName(hunter)));
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords),
                    destinationName);

                entMan.EventBus.RaiseLocalEvent(beacon, saved);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels,
                    Does.Not.Contain(expectedBroadcast),
                    $"CMSS13 pred_can_receive_message() returns false for dead hunters before checking broadcast networks.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, deadYautja, beacon })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }

                ClearRelayTeleLocDestinations(entMan);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CommunicatorRejectsNonYautjaAndNonThrallSpeakersLikeCmss13TalkInto()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var radio = entMan.System<RadioSystem>();
            var recorder = entMan.System<YautjaTestRadioReceiveRecorderSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var speaker = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var communicator = entMan.SpawnEntity("CMUYautjaCommunicator", map.GridCoords);
            var ordinaryRadio = entMan.SpawnEntity("CMUYautjaCommunicator", map.GridCoords.Offset(new Vector2(1, 0)));
            var regularHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(2, 0)));

            try
            {
                entMan.EnsureComponent<ActiveRadioComponent>(ordinaryRadio).Channels.Add("CMUYautja");
                recorder.Watch(ordinaryRadio);
                recorder.Watch(regularHellhound);

                recorder.Clear();
                radio.SendRadioMessage(
                    speaker,
                    "Stolen words.",
                    prototypes.Index<RadioChannelPrototype>("CMUYautja"),
                    communicator);

                Assert.Multiple(() =>
                {
                    Assert.That(recorder.DeliveredMessageOnChannel("Stolen words.", "CMUYautja"), Is.False,
                        "CMSS13 yautja headset talk_into() returns before parent radio delivery for non-Yautja/non-thrall speakers.");
                    Assert.That(recorder.DeliveredTo(regularHellhound, "Stolen words."), Is.False,
                        "CMSS13 yautja headset talk_into() does not forward rejected speakers to hellhounds.");
                });
            }
            finally
            {
                DeleteEntities(entMan, speaker, communicator, ordinaryRadio, regularHellhound);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CommunicatorYautjaChannelForwardsOnlyToRegularHellhoundsLikeCmss13TalkInto()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var radio = entMan.System<RadioSystem>();
            var recorder = entMan.System<YautjaTestRadioReceiveRecorderSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var speaker = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var communicator = entMan.SpawnEntity("CMUYautjaCommunicator", map.GridCoords);
            var regularHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));
            var badBloodHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(2, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(speaker);
                SetNpcFaction(entMan, badBloodHellhound, "CMUYautjaBadBlood");
                recorder.Watch(regularHellhound);
                recorder.Watch(badBloodHellhound);

                recorder.Clear();
                radio.SendRadioMessage(
                    speaker,
                    "Honor binds the pack.",
                    prototypes.Index<RadioChannelPrototype>("CMUYautja"),
                    communicator);

                Assert.Multiple(() =>
                {
                    Assert.That(recorder.DeliveredTo(regularHellhound, "Honor binds the pack."), Is.True,
                        "CMSS13 yautja headset talk_into() forwards RADIO_CHANNEL_YAUTJA to living hellhounds in FACTION_YAUTJA.");
                    Assert.That(recorder.DeliveredTo(badBloodHellhound, "Honor binds the pack."), Is.False,
                        "CMSS13 yautja headset talk_into() skips Bad Blood hellhounds when check_channel is RADIO_CHANNEL_YAUTJA.");
                });
            }
            finally
            {
                DeleteEntities(entMan, speaker, communicator, regularHellhound, badBloodHellhound);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CommunicatorHellhoundForwardingUsesCmss13CommandsVerbLikeTalkInto()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var radio = entMan.System<RadioSystem>();
            var recorder = entMan.System<YautjaTestRadioReceiveRecorderSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var speaker = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var communicator = entMan.SpawnEntity("CMUYautjaCommunicator", map.GridCoords);
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(speaker);
                recorder.Watch(hellhound);

                recorder.Clear();
                radio.SendRadioMessage(
                    speaker,
                    "Stand down!",
                    prototypes.Index<RadioChannelPrototype>("CMUYautja"),
                    communicator);

                Assert.Multiple(() =>
                {
                    Assert.That(recorder.DeliveredTo(hellhound, "Stand down!"), Is.True,
                        "CMSS13 yautja headset talk_into() directly echoes Yautja radio to matching living hellhounds.");
                    Assert.That(recorder.DeliveredVerb(hellhound, "Stand down!"), Is.EqualTo("commands"),
                        "CMSS13 yautja headset talk_into() defaults the direct Hellhound radio verb to \"commands\" instead of using the speaker's generic speech verb.");
                    Assert.That(recorder.DeliveredWrappedMessage(hellhound, "Stand down!"),
                        Does.Contain("Radio").And.Contain("commands").And.Contain("Stand down!"),
                        "CMSS13 yautja headset talk_into() sends Hellhounds the source-shaped '[Radio]: name commands, message' line.");
                });
            }
            finally
            {
                DeleteEntities(entMan, speaker, communicator, hellhound);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrandedCommunicatorForwardsOnlyToStrandedHellhoundsLikeCmss13TalkInto()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var radio = entMan.System<RadioSystem>();
            var recorder = entMan.System<YautjaTestRadioReceiveRecorderSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var speaker = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var communicator = entMan.SpawnEntity("CMUYautjaStrandedCommunicator", map.GridCoords);
            var regularHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));
            var badBloodHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(2, 0)));
            var strandedHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(3, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(speaker);
                SetNpcFaction(entMan, badBloodHellhound, "CMUYautjaBadBlood");
                SetNpcFaction(entMan, strandedHellhound, "CMUYautjaStranded");
                recorder.Watch(regularHellhound);
                recorder.Watch(badBloodHellhound);
                recorder.Watch(strandedHellhound);

                recorder.Clear();
                radio.SendRadioMessage(
                    speaker,
                    "No clan, only survival.",
                    prototypes.Index<RadioChannelPrototype>("CMUYautjaStranded"),
                    communicator);

                Assert.Multiple(() =>
                {
                    Assert.That(recorder.DeliveredTo(strandedHellhound, "No clan, only survival."), Is.True,
                        "CMSS13 yautja headset talk_into() forwards RADIO_CHANNEL_YAUTJA_STRANDED to living hellhounds in FACTION_YAUTJA_STRANDED.");
                    Assert.That(recorder.DeliveredTo(regularHellhound, "No clan, only survival."), Is.False,
                        "CMSS13 yautja headset talk_into() skips regular Yautja hellhounds when check_channel is RADIO_CHANNEL_YAUTJA_STRANDED.");
                    Assert.That(recorder.DeliveredTo(badBloodHellhound, "No clan, only survival."), Is.False,
                        "CMSS13 yautja headset talk_into() skips Bad Blood hellhounds when check_channel is RADIO_CHANNEL_YAUTJA_STRANDED.");
                });
            }
            finally
            {
                DeleteEntities(entMan, speaker, communicator, regularHellhound, badBloodHellhound, strandedHellhound);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodCommunicatorHivebrokenIntrinsicReceiverGetsSingleRadioReceiveEvent()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid speaker = default;
        EntityUid communicator = default;
        EntityUid hivebrokenXeno = default;
        EntityUid? previousAttached = null;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var radio = entMan.System<RadioSystem>();
            var recorder = entMan.System<YautjaTestRadioReceiveRecorderSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var session = server.PlayerMan.Sessions.Single();
            previousAttached = session.AttachedEntity;

            speaker = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            communicator = entMan.SpawnEntity("CMUYautjaBadBloodCommunicator", map.GridCoords);
            hivebrokenXeno = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));

            entMan.EnsureComponent<YautjaComponent>(speaker);
            SetNpcFaction(entMan, speaker, "CMUYautjaBadBlood");
            SetNpcFaction(entMan, hivebrokenXeno, "CMUYautjaBadBlood");
            entMan.EnsureComponent<YautjaHivebrokenXenoComponent>(hivebrokenXeno);
            entMan.EnsureComponent<IntrinsicRadioReceiverComponent>(hivebrokenXeno);
            entMan.RemoveComponent<YautjaHellhoundComponent>(hivebrokenXeno);
            server.PlayerMan.SetAttachedEntity(session, hivebrokenXeno);

            recorder.Watch(hivebrokenXeno);
            recorder.Clear();

            radio.SendRadioMessage(
                speaker,
                "Only once.",
                prototypes.Index<RadioChannelPrototype>("CMUYautjaBadBlood"),
                communicator);

            Assert.That(recorder.DeliveryCount(hivebrokenXeno, "Only once."), Is.EqualTo(1),
                "Bad Blood hivebroken xenos with intrinsic radio receive should get the communicator forwarding event once.");
        });

        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var history = client.ResolveDependency<IUserInterfaceManager>()
                .GetUIController<ChatUIController>()
                .History
                .Select(entry => entry.Msg)
                .ToList();

            Assert.That(history.Count(message => message.Message == "Only once."), Is.EqualTo(1),
                "A hivebroken xeno with IntrinsicRadioReceiver should receive one client chat message, not one intrinsic delivery plus one direct network send.");
        });

        await server.WaitPost(() =>
        {
            if (previousAttached is { } attached)
                server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), attached);

            DeleteEntities(server.EntMan, speaker, communicator, hivebrokenXeno);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodCommunicatorHivebrokenForwardingUsesCmss13CommandsVerbLikeTalkInto()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var radio = entMan.System<RadioSystem>();
            var recorder = entMan.System<YautjaTestRadioReceiveRecorderSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var speaker = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var communicator = entMan.SpawnEntity("CMUYautjaBadBloodCommunicator", map.GridCoords);
            var hivebrokenXeno = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(speaker);
                SetNpcFaction(entMan, speaker, "CMUYautjaBadBlood");
                SetNpcFaction(entMan, hivebrokenXeno, "CMUYautjaBadBlood");
                entMan.EnsureComponent<YautjaHivebrokenXenoComponent>(hivebrokenXeno);
                entMan.RemoveComponent<YautjaHellhoundComponent>(hivebrokenXeno);
                recorder.Watch(hivebrokenXeno);

                recorder.Clear();
                radio.SendRadioMessage(
                    speaker,
                    "Follow the broken hive.",
                    prototypes.Index<RadioChannelPrototype>("CMUYautjaBadBlood"),
                    communicator);

                Assert.Multiple(() =>
                {
                    Assert.That(recorder.DeliveredTo(hivebrokenXeno, "Follow the broken hive."), Is.True,
                        "CMSS13 yautja headset talk_into() directly echoes Bad Blood radio to living xenos in XENO_HIVE_YAUTJA_BADBLOOD.");
                    Assert.That(recorder.DeliveredVerb(hivebrokenXeno, "Follow the broken hive."), Is.EqualTo("commands"),
                        "CMSS13 yautja headset talk_into() keeps the same default \"commands\" verb for the Bad Blood hivebroken direct echo.");
                    Assert.That(recorder.DeliveredWrappedMessage(hivebrokenXeno, "Follow the broken hive."),
                        Does.Contain("Radio").And.Contain("commands").And.Contain("Follow the broken hive."),
                        "CMSS13 yautja headset talk_into() sends Bad Blood hivebroken xenos the source-shaped '[Radio]: name commands, message' line.");
                });
            }
            finally
            {
                DeleteEntities(entMan, speaker, communicator, hivebrokenXeno);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodCommunicatorForwardsOnlyToBadBloodHellhoundAndHivebrokenXenoLikeCmss13TalkInto()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var radio = entMan.System<RadioSystem>();
            var recorder = entMan.System<YautjaTestRadioReceiveRecorderSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var speaker = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var communicator = entMan.SpawnEntity("CMUYautjaBadBloodCommunicator", map.GridCoords);
            var regularHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));
            var badBloodHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(2, 0)));
            var hivebrokenXeno = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(3, 0)));
            var ordinaryXeno = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(4, 0)));
            var ordinaryYautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(5, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(speaker);
                SetNpcFaction(entMan, speaker, "CMUYautjaBadBlood");
                SetNpcFaction(entMan, badBloodHellhound, "CMUYautjaBadBlood");

                SetNpcFaction(entMan, hivebrokenXeno, "CMUYautjaBadBlood");
                entMan.EnsureComponent<YautjaHivebrokenXenoComponent>(hivebrokenXeno);
                entMan.RemoveComponent<YautjaHellhoundComponent>(hivebrokenXeno);

                SetNpcFaction(entMan, ordinaryXeno, "RMCXeno");
                entMan.RemoveComponent<YautjaHellhoundComponent>(ordinaryXeno);

                recorder.Watch(regularHellhound);
                recorder.Watch(badBloodHellhound);
                recorder.Watch(hivebrokenXeno);
                recorder.Watch(ordinaryXeno);
                recorder.Watch(ordinaryYautja);

                recorder.Clear();
                radio.SendRadioMessage(
                    speaker,
                    "The clan is dead.",
                    prototypes.Index<RadioChannelPrototype>("CMUYautjaBadBlood"),
                    communicator);

                Assert.Multiple(() =>
                {
                    Assert.That(recorder.DeliveredTo(badBloodHellhound, "The clan is dead."), Is.True,
                        "CMSS13 yautja headset talk_into() forwards RADIO_CHANNEL_YAUTJA_BADBLOOD to living hellhounds in FACTION_YAUTJA_BADBLOOD.");
                    Assert.That(recorder.DeliveredTo(hivebrokenXeno, "The clan is dead."), Is.True,
                        "CMSS13 yautja headset talk_into() forwards RADIO_CHANNEL_YAUTJA_BADBLOOD to living xenos in XENO_HIVE_YAUTJA_BADBLOOD.");
                    Assert.That(recorder.DeliveredTo(regularHellhound, "The clan is dead."), Is.False,
                        "CMSS13 yautja headset talk_into() skips regular hellhounds for the Bad Blood channel.");
                    Assert.That(recorder.DeliveredTo(ordinaryXeno, "The clan is dead."), Is.False,
                        "CMSS13 yautja headset talk_into() only forwards Bad Blood channel traffic to the Bad Blood hive, not ordinary xenos.");
                    Assert.That(recorder.DeliveredTo(ordinaryYautja, "The clan is dead."), Is.False,
                        "The Bad Blood communicator encryption key should not leak RADIO_CHANNEL_YAUTJA_BADBLOOD to ordinary Yautja headset listeners.");
                });
            }
            finally
            {
                DeleteEntities(entMan, speaker, communicator, regularHellhound, badBloodHellhound, hivebrokenXeno, ordinaryXeno, ordinaryYautja);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocRejectsNestedUserLikeCmss13NestCheck()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
            var nest = entMan.SpawnEntity("XenoNest", map.GridCoords);

            try
            {
                entMan.EnsureComponent<XenoNestedComponent>(hunter);

                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords),
                    "Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(beacon, saved);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.That(relay.CustomDestinations, Is.Empty,
                    "CMSS13 add_tele_loc() returns false while the user is buckled to /obj/structure/bed/nest.");
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(beacon))
                    entMan.DeleteEntity(beacon);
                if (!entMan.Deleted(nest))
                    entMan.DeleteEntity(nest);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocRejectsNonGroundLevelUserLikeCmss13IsGroundLevel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var groundMap = await pair.CreateTestMap();
        var orbitMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(groundMap.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", orbitMap.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", orbitMap.GridCoords);

            try
            {
                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(orbitMap.GridCoords),
                    "Orbital Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(beacon, saved);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.That(relay.CustomDestinations, Is.Empty,
                    "CMSS13 add_tele_loc() returns false when !is_ground_level(usr.z). Local tele_loc should only save destinations from the RMC planet/ground map equivalent.");
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(beacon))
                    entMan.DeleteEntity(beacon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocAllowsYoungbloodWithTechAuthorizationLikeCmss13Verb()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaYoungbloodComponent>(hunter);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(hunter);
                mobState.ChangeMobState(hunter, MobState.Alive);

                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords),
                    "Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(beacon, saved);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.That(relay.CustomDestinations, Has.Count.EqualTo(1));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(beacon))
                    entMan.DeleteEntity(beacon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocNonTechDeniedPopupUsesCmss13Text()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid beacon = default;
        EntityUid action = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                var actionComp = entMan.EnsureComponent<ActionComponent>(action);

                server.PlayerMan.SetAttachedEntity(session, user);

                var ev = new YautjaAddTeleporterLocationActionEvent
                {
                    Performer = user,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(beacon, ev);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.Multiple(() =>
                {
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(relay.CustomDestinations, Is.Empty);
                });
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain(Loc.GetString("cmu-yautja-relay-add-destination-denied")),
                        "CMSS13 /obj/item/device/yautja_teleporter/verb/add_tele_loc() uses a tele_loc-specific non-tech warning.");
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-denied")),
                        "The relay beacon attack_self() denial must not replace the tele_loc verb denial.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { user, beacon, action })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconUseDeniedPopupsUseCmss13AttackSelfText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid nonTech = default;
        EntityUid youngblood = default;
        EntityUid nonTechBeacon = default;
        EntityUid youngbloodBeacon = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                nonTech = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                youngblood = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                nonTechBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                youngbloodBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(youngblood);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(youngblood);

                server.PlayerMan.SetAttachedEntity(session, nonTech);
                var nonTechUse = new UseInHandEvent(nonTech);
                entMan.EventBus.RaiseLocalEvent(nonTechBeacon, nonTechUse);
                Assert.That(nonTechUse.Handled, Is.True);

                server.PlayerMan.SetAttachedEntity(session, youngblood);
                var youngbloodUse = new UseInHandEvent(youngblood);
                entMan.EventBus.RaiseLocalEvent(youngbloodBeacon, youngbloodUse);
                Assert.That(youngbloodUse.Handled, Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joined = string.Join("\n", labels);

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain(Loc.GetString("cmu-yautja-relay-beacon-denied")),
                        $"CMSS13 relay beacon attack_self() non-tech denial should be exact.\nActual labels:\n{joined}");
                    Assert.That(labels, Does.Contain(Loc.GetString("cmu-yautja-relay-beacon-youngblood-denied")),
                        $"CMSS13 relay beacon attack_self() youngblood denial should be distinct from non-tech denial.\nActual labels:\n{joined}");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { nonTech, youngblood, nonTechBeacon, youngbloodBeacon })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconUseBlockedInteractionUsesCmss13AttackSelfDenialForBothBeaconTypes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid relayBeacon = default;
        EntityUid simpleBeacon = default;
        EntityUid yautjaShipDestination = default;
        EntityUid humanShipDestination = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var mobState = entMan.System<MobStateSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                relayBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                simpleBeacon = entMan.SpawnEntity("CMUYautjaSimpleRelayBeacon", map.GridCoords);
                yautjaShipDestination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(10, 0)));
                humanShipDestination = entMan.SpawnEntity("CMUYautjaHumanShipRelayDestination", map.GridCoords.Offset(new Vector2(12, 0)));
                mobState.ChangeMobState(hunter, MobState.Critical);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var relayUse = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(relayBeacon, relayUse);

                var simpleUse = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(simpleBeacon, simpleUse);

                Assert.Multiple(() =>
                {
                    Assert.That(relayUse.Handled, Is.True);
                    Assert.That(simpleUse.Handled, Is.True);
                    Assert.That(ui.IsUiOpen(relayBeacon, YautjaRelayBeaconUIKey.Key, hunter), Is.False,
                        "CMSS13 yautja_teleporter/attack_self() checks should_block_game_interaction(H) before opening the ship-selection prompt.");
                    Assert.That(ActiveRelayDoAfters(entMan, hunter), Is.EqualTo(0),
                        "CMSS13 yautja_teleporter and thrall_teleporter return before starting the 10 second do_after when should_block_game_interaction(H) is true.");
                });
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joined = string.Join("\n", labels);

                var denied = Loc.GetString("cmu-yautja-relay-beacon-denied");
                Assert.That(labels.Any(label => label.StartsWith(denied, StringComparison.Ordinal)),
                    Is.True,
                    $"CMSS13 relay blocked-interaction denial shares the non-tech attack_self() text.\nActual labels:\n{joined}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, relayBeacon, simpleBeacon, yautjaShipDestination, humanShipDestination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconUseNonHumanReturnsSilentlyLikeCmss13AttackSelf()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid xeno = default;
        EntityUid beacon = default;
        EntityUid destination = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords);
                beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                destination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(12, 0)));
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(xeno);

                server.PlayerMan.SetAttachedEntity(session, xeno);
                var use = new UseInHandEvent(xeno);
                entMan.EventBus.RaiseLocalEvent(beacon, use);

                Assert.Multiple(() =>
                {
                    Assert.That(use.Handled, Is.True);
                    Assert.That(ui.IsUiOpen(beacon, YautjaRelayBeaconUIKey.Key, xeno), Is.False,
                        "CMSS13 attack_self() returns silently before opening the relay prompt when !ishuman(user).");
                    Assert.That(ActiveRelayDoAfters(entMan, xeno), Is.EqualTo(0),
                        "A non-human tech-authorized entity must not start the relay beacon do_after.");
                });
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-denied")),
                        "CMSS13 !ishuman(user) branch returns silently before the non-tech denial.");
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-pulse")),
                        "CMSS13 !ishuman(user) branch returns silently before destination lookup or signal feedback.");
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-start")),
                        "CMSS13 !ishuman(user) branch returns silently before the teleport start message.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { xeno, beacon, destination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconUnavailableDestinationSelectionReturnsSilentlyLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid beacon = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                Assert.That(hands.TryPickupAnyHand(hunter, beacon), Is.True);

                server.PlayerMan.SetAttachedEntity(session, hunter);
                Assert.That(ui.TryOpenUi(beacon, YautjaRelayBeaconUIKey.Key, hunter), Is.True);
                ui.RaiseUiMessage(beacon, YautjaRelayBeaconUIKey.Key,
                    new YautjaRelayBeaconDestinationMsg(YautjaRelayDestinationKind.YautjaShip) { Actor = hunter });

                Assert.That(ActiveRelayDoAfters(entMan, hunter), Is.EqualTo(0),
                    "CMSS13 attack_self() returns silently when the selected relay destination does not resolve to a turf.");
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-pulse")),
                        "CMSS13 missing target_turf returns before signal feedback.");
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-start")),
                        "CMSS13 missing target_turf returns before the shimmery start message.");
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-complete")),
                        "CMSS13 missing target_turf cannot complete a teleport.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, beacon })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SimpleRelayBeaconWithoutYautjaShipDestinationReturnsSilentlyLikeCmss13ThrallTeleporter()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid bloodedThrall = default;
        EntityUid simpleBeacon = default;
        EntityUid? previousAttached = null;
        const string thrallName = "Test Thrall";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                bloodedThrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(bloodedThrall);
                metadata.SetEntityName(bloodedThrall, thrallName);
                simpleBeacon = entMan.SpawnEntity("CMUYautjaSimpleRelayBeacon", map.GridCoords);
                Assert.That(hands.TryPickupAnyHand(bloodedThrall, simpleBeacon), Is.True);
                server.PlayerMan.SetAttachedEntity(session, bloodedThrall);

                var beforeUse = AudioEntities(entMan);
                var use = new UseInHandEvent(bloodedThrall);
                entMan.EventBus.RaiseLocalEvent(simpleBeacon, use);

                Assert.Multiple(() =>
                {
                    Assert.That(use.Handled, Is.True);
                    Assert.That(ui.IsUiOpen(simpleBeacon, YautjaRelayBeaconUIKey.Key, bloodedThrall), Is.False,
                        "CMSS13 simple thrall relay has no ship-selection prompt; it silently returns when no Yautja ship turf exists.");
                    Assert.That(ActiveRelayDoAfters(entMan, bloodedThrall), Is.EqualTo(0),
                        "CMSS13 thrall_teleporter returns before starting do_after when SAFEPICK(GLOB.yautja_spawnpoints) is not a turf.");
                    Assert.That(AudioFileNamesAfter(entMan, beforeUse), Is.Empty,
                        "CMSS13 thrall_teleporter returns before playing signal.ogg when the Yautja ship destination is missing.");
                });
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-denied")),
                        "Missing destination should not be reported as a tech/use denial.");
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-pulse")),
                        "CMSS13 simple relay missing destination returns silently, without pulse feedback.");
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-start")),
                        "CMSS13 simple relay missing destination returns before the shimmery start message.");
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-disappear", ("user", thrallName))),
                        "CMSS13 simple relay missing destination cannot complete a disappear/teleport branch.");
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-complete")),
                        "The local completion text must not appear for a missing destination.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { bloodedThrall, simpleBeacon })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconDuplicateUseDoesNotCancelExistingDoAfterLikeCmss13Timer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
            var destination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(12, 0)));

            try
            {
                entMan.GetComponent<YautjaRelayBeaconComponent>(beacon).PulseSound =
                    new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav");
                Assert.That(hands.TryPickupAnyHand(hunter, beacon), Is.True);

                Assert.That(ui.TryOpenUi(beacon, YautjaRelayBeaconUIKey.Key, hunter), Is.True);
                ui.RaiseUiMessage(beacon, YautjaRelayBeaconUIKey.Key,
                    new YautjaRelayBeaconDestinationMsg(YautjaRelayDestinationKind.YautjaShip) { Actor = hunter });

                var doAfter = entMan.GetComponent<DoAfterComponent>(hunter);
                var activeBefore = doAfter.DoAfters.Values
                    .Single(active => !active.Cancelled && !active.Completed && active.Args.Event is YautjaRelayBeaconDoAfterEvent);

                Assert.That(ui.TryOpenUi(beacon, YautjaRelayBeaconUIKey.Key, hunter), Is.True);
                ui.RaiseUiMessage(beacon, YautjaRelayBeaconUIKey.Key,
                    new YautjaRelayBeaconDestinationMsg(YautjaRelayDestinationKind.YautjaShip) { Actor = hunter });

                var activeAfter = doAfter.DoAfters.Values
                    .Single(active => !active.Cancelled && !active.Completed && active.Args.Event is YautjaRelayBeaconDoAfterEvent);

                Assert.Multiple(() =>
                {
                    Assert.That(activeAfter.Index, Is.EqualTo(activeBefore.Index),
                        "Repeated relay starts should be blocked while busy without cancelling the existing CMSS13 do_after timer.");
                    Assert.That(ActiveRelayDoAfters(entMan, hunter), Is.EqualTo(1));
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, beacon, destination })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconPromptOpenDoesNotPlaySignalLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
            var destination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(12, 0)));

            try
            {
                entMan.GetComponent<YautjaRelayBeaconComponent>(beacon).PulseSound =
                    new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav");
                Assert.That(hands.TryPickupAnyHand(hunter, beacon), Is.True);

                var beforeUse = AudioEntities(entMan);
                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(beacon, use);

                Assert.Multiple(() =>
                {
                    Assert.That(use.Handled, Is.True);
                    Assert.That(ui.IsUiOpen(beacon, YautjaRelayBeaconUIKey.Key, hunter), Is.True);
                    Assert.That(AudioFileNamesAfter(entMan, beforeUse), Is.Empty,
                        "CMSS13 relay beacon attack_self() opens the ship prompt before playsound(signal.ogg); no signal should play just from opening the prompt.");
                });

                ui.RaiseUiMessage(beacon, YautjaRelayBeaconUIKey.Key,
                    new YautjaRelayBeaconDestinationMsg(YautjaRelayDestinationKind.YautjaShip) { Actor = hunter });

                Assert.That(AudioFileNamesAfter(entMan, beforeUse),
                    Is.EqualTo(new[] { "/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav" }),
                    "CMSS13 plays signal.ogg only after the chosen destination resolves to a turf and the do_after starts.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, beacon, destination })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SimpleRelayBeaconPlaysSignalOnceBeforeDoAfterAndNoCompletionPulseLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        const string testPulseSound = "/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav";

        EntityUid bloodedThrall = default;
        EntityUid simpleBeacon = default;
        EntityUid destination = default;
        HashSet<EntityUid> beforeUse = new();
        HashSet<EntityUid> afterStart = new();

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();

                bloodedThrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(bloodedThrall);
                simpleBeacon = entMan.SpawnEntity("CMUYautjaSimpleRelayBeacon", map.GridCoords);
                destination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(12, 0)));
                entMan.GetComponent<YautjaRelayBeaconComponent>(simpleBeacon).PulseSound =
                    new SoundPathSpecifier(testPulseSound);
                Assert.That(hands.TryPickupAnyHand(bloodedThrall, simpleBeacon), Is.True);

                beforeUse = AudioEntities(entMan);
                var use = new UseInHandEvent(bloodedThrall);
                entMan.EventBus.RaiseLocalEvent(simpleBeacon, use);

                Assert.Multiple(() =>
                {
                    Assert.That(use.Handled, Is.True);
                    Assert.That(ActiveRelayDoAfters(entMan, bloodedThrall), Is.EqualTo(1));
                    Assert.That(AudioFileNamesAfter(entMan, beforeUse),
                        Is.EqualTo(new[] { testPulseSound }),
                        "CMSS13 simple thrall relay plays signal.ogg once immediately before the 10 second do_after.");
                });
                afterStart = AudioEntities(entMan);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(10.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(AudioFileNamesAfter(entMan, afterStart),
                    Is.Empty,
                    "CMSS13 relay completion does not play a second signal.ogg pulse after trainteleport().");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { bloodedThrall, simpleBeacon, destination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SimpleRelayBeaconUsesCmss13VisibleStartAndDisappearMessages()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid bloodedThrall = default;
        EntityUid simpleBeacon = default;
        EntityUid destination = default;
        EntityUid? previousAttached = null;
        const string thrallName = "Test Thrall";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                bloodedThrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(bloodedThrall);
                metadata.SetEntityName(bloodedThrall, thrallName);
                simpleBeacon = entMan.SpawnEntity("CMUYautjaSimpleRelayBeacon", map.GridCoords);
                destination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(12, 0)));
                Assert.That(hands.TryPickupAnyHand(bloodedThrall, simpleBeacon), Is.True);
                server.PlayerMan.SetAttachedEntity(session, bloodedThrall);

                var use = new UseInHandEvent(bloodedThrall);
                entMan.EventBus.RaiseLocalEvent(simpleBeacon, use);
                Assert.That(use.Handled, Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();

                Assert.That(labels,
                    Does.Contain(Loc.GetString("cmu-yautja-relay-beacon-start", ("user", thrallName))),
                    "CMSS13 uses user.visible_message(\"[user] starts becoming shimmery and indistinct...\") instead of a self-only local message.");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(10.1f));

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(labels,
                        Does.Contain(Loc.GetString("cmu-yautja-relay-beacon-disappear", ("user", thrallName))),
                        "CMSS13 relay completion sends a visible disappear message before trainteleport().");
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-complete")),
                        "The local stabilizes-your-form completion popup is not present in CMSS13.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { bloodedThrall, simpleBeacon, destination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SimpleRelayBeaconPulledLivingPassengerGetsSeparateDisappearMessageLikeCmss13ThrallTeleporter()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid bloodedThrall = default;
        EntityUid passenger = default;
        EntityUid simpleBeacon = default;
        EntityUid destination = default;
        EntityUid? previousAttached = null;
        const string thrallName = "Test Thrall";
        const string passengerName = "Pulled Passenger";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var pulling = entMan.System<PullingSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                bloodedThrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(bloodedThrall);
                metadata.SetEntityName(bloodedThrall, thrallName);
                passenger = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                metadata.SetEntityName(passenger, passengerName);
                simpleBeacon = entMan.SpawnEntity("CMUYautjaSimpleRelayBeacon", map.GridCoords);
                destination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(12, 0)));
                Assert.That(hands.TryPickupAnyHand(bloodedThrall, simpleBeacon), Is.True);
                Assert.That(pulling.TryStartPull(bloodedThrall, passenger), Is.True);
                server.PlayerMan.SetAttachedEntity(session, bloodedThrall);

                var use = new UseInHandEvent(bloodedThrall);
                entMan.EventBus.RaiseLocalEvent(simpleBeacon, use);
                Assert.That(use.Handled, Is.True);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(10.1f));

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joined = string.Join("\n", labels);

                Assert.Multiple(() =>
                {
                    Assert.That(labels,
                        Does.Contain(Loc.GetString("cmu-yautja-relay-beacon-disappear", ("user", thrallName))),
                        $"CMSS13 thrall_teleporter shows the relay user's disappear message.\nActual labels:\n{joined}");
                    Assert.That(labels,
                        Does.Contain(Loc.GetString("cmu-yautja-relay-beacon-disappear", ("user", passengerName))),
                        $"CMSS13 thrall_teleporter sends a separate visible disappear message for a pulled living passenger before trainteleport().\nActual labels:\n{joined}");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { bloodedThrall, passenger, simpleBeacon, destination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    private static HashSet<string?> ActionPrototypeIds(IEntityManager entMan, IEnumerable<EntityUid> actions)
    {
        return actions
            .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
            .ToHashSet();
    }

    private static int ActiveRelayDoAfters(IEntityManager entMan, EntityUid user)
    {
        return entMan.TryGetComponent(user, out DoAfterComponent? doAfter)
            ? doAfter.DoAfters.Values.Count(active =>
                !active.Cancelled &&
                !active.Completed &&
                active.Args.Event is YautjaRelayBeaconDoAfterEvent)
            : 0;
    }

    private static void ClearRelayTeleLocDestinations(IEntityManager entMan)
    {
        entMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
    }

    private static HashSet<EntityUid> AudioEntities(IEntityManager entMan)
    {
        var audio = new HashSet<EntityUid>();
        var query = entMan.EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            audio.Add(uid);
        }

        return audio;
    }

    private static List<string> AudioFileNamesAfter(IEntityManager entMan, HashSet<EntityUid> before)
    {
        var audio = new List<string>();
        var query = entMan.EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!before.Contains(uid))
                audio.Add(component.FileName);
        }

        return audio;
    }

    [Test]
    public async Task RelayBeaconDestinationSelectionCompletesDoAfterAndTeleports()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid beacon = default;
        EntityUid destination = default;
        MapCoordinates destinationCoordinates = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                destination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(12, 0)));
                destinationCoordinates = transform.GetMapCoordinates(destination);

                Assert.That(hands.TryPickupAnyHand(hunter, beacon), Is.True);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.That(relay.DoAfter, Is.EqualTo(TimeSpan.FromSeconds(10)));

                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(beacon, use);
                Assert.That(use.Handled, Is.True);
                Assert.That(ui.IsUiOpen(beacon, YautjaRelayBeaconUIKey.Key, hunter), Is.True);

                ui.RaiseUiMessage(beacon, YautjaRelayBeaconUIKey.Key,
                    new YautjaRelayBeaconDestinationMsg(YautjaRelayDestinationKind.YautjaShip) { Actor = hunter });

                var doAfter = entMan.GetComponent<DoAfterComponent>(hunter);
                var active = doAfter.DoAfters.Values.Single(active =>
                    !active.Cancelled &&
                    !active.Completed &&
                    active.Args.Event is YautjaRelayBeaconDoAfterEvent);
                var relayEvent = (YautjaRelayBeaconDoAfterEvent) active.Args.Event;

                Assert.Multiple(() =>
                {
                    Assert.That(active.Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(10)));
                    Assert.That(active.Args.Target, Is.EqualTo(hunter));
                    Assert.That(active.Args.Used, Is.EqualTo(beacon));
                    Assert.That(relayEvent.Destination, Is.EqualTo(YautjaRelayDestinationKind.YautjaShip));
                    Assert.That(relayEvent.CustomIndex, Is.EqualTo(-1));
                    Assert.That(transform.GetMapCoordinates(hunter).Position, Is.Not.EqualTo(destinationCoordinates.Position));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(10.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var transform = entMan.System<SharedTransformSystem>();
                var actual = transform.GetMapCoordinates(hunter);

                Assert.Multiple(() =>
                {
                    Assert.That(actual.MapId, Is.EqualTo(destinationCoordinates.MapId));
                    Assert.That(actual.Position, Is.EqualTo(destinationCoordinates.Position));
                    Assert.That(entMan.TryGetComponent(hunter, out DoAfterComponent? doAfter) &&
                                doAfter.DoAfters.Values.Any(active =>
                                    !active.Cancelled &&
                                    !active.Completed &&
                                    active.Args.Event is YautjaRelayBeaconDoAfterEvent),
                        Is.False);
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, beacon, destination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocSavedDestinationIsSharedAcrossBeaconsLikeCmss13Globals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid savingBeacon = default;
        EntityUid travelBeacon = default;
        EntityUid savedLocation = default;
        MapCoordinates savedCoordinates = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                ClearRelayTeleLocDestinations(entMan);
                entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                savingBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                travelBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                savedLocation = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(16, 0)));
                savedCoordinates = transform.GetMapCoordinates(savedLocation);
                Assert.That(hands.TryPickupAnyHand(hunter, travelBeacon), Is.True);

                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(transform.GetMoverCoordinates(savedLocation)),
                    "Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(savingBeacon, saved);

                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(travelBeacon, use);
                Assert.That(use.Handled, Is.True);
                Assert.That(ui.IsUiOpen(travelBeacon, YautjaRelayBeaconUIKey.Key, hunter), Is.True);
                Assert.That(ui.TryGetUiState<YautjaRelayBeaconState>(travelBeacon, YautjaRelayBeaconUIKey.Key, out var state), Is.True);

                var custom = state!.Destinations.Single(destination => destination.Name == "Trophy Hall");
                Assert.Multiple(() =>
                {
                    Assert.That(custom.Available, Is.True);
                    Assert.That(custom.CustomIndex, Is.GreaterThanOrEqualTo(0));
                });

                ui.RaiseUiMessage(travelBeacon, YautjaRelayBeaconUIKey.Key,
                    new YautjaRelayBeaconDestinationMsg(custom.Kind, custom.CustomIndex) { Actor = hunter });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(10.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var transform = entMan.System<SharedTransformSystem>();
                var actual = transform.GetMapCoordinates(hunter);

                Assert.Multiple(() =>
                {
                    Assert.That(actual.MapId, Is.EqualTo(savedCoordinates.MapId));
                    Assert.That(actual.Position, Is.EqualTo(savedCoordinates.Position));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, savingBeacon, travelBeacon, savedLocation })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocKeepsMoreThanSixteenCustomDestinationsLikeCmss13Globals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var savingBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
            var travelBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);

            try
            {
                var expectedNames = Enumerable.Range(1, 17)
                    .Select(i => $"Trophy Hall {i:00}")
                    .ToList();

                foreach (var (name, index) in expectedNames.Select((name, index) => (name, index)))
                {
                    var coordinates = map.GridCoords.Offset(new Vector2(index + 1, 0));
                    var saved = new YautjaRelayBeaconNameDestinationEvent(
                        entMan.GetNetEntity(hunter),
                        entMan.GetNetCoordinates(coordinates),
                        name);

                    entMan.EventBus.RaiseLocalEvent(savingBeacon, saved);
                }

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(savingBeacon);
                Assert.That(relay.CustomDestinations.Select(destination => destination.Name), Is.EqualTo(expectedNames),
                    "CMSS13 add_tele_loc() appends to GLOB.yautja_teleports and GLOB.yautja_teleport_descs without evicting the oldest custom location.");

                Assert.That(hands.TryPickupAnyHand(hunter, travelBeacon), Is.True);

                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(travelBeacon, use);
                Assert.That(use.Handled, Is.True);
                Assert.That(ui.TryGetUiState<YautjaRelayBeaconState>(travelBeacon, YautjaRelayBeaconUIKey.Key, out var state), Is.True);

                var customNames = state!.Destinations
                    .Where(destination => destination.CustomIndex >= 0)
                    .Select(destination => destination.Name)
                    .ToList();

                Assert.That(customNames, Is.EqualTo(expectedNames),
                    "The round-scoped relay destination list should keep every tele_loc entry visible across beacons.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, savingBeacon, travelBeacon })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocSavedDestinationsClearOnRoundRestartLikeCmss13Globals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var savingBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
            var travelBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);

            try
            {
                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords.Offset(new Vector2(4, 0))),
                    "Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(savingBeacon, saved);

                Assert.That(hands.TryPickupAnyHand(hunter, travelBeacon), Is.True);
                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(travelBeacon, use);
                Assert.That(use.Handled, Is.True);
                Assert.That(ui.TryGetUiState<YautjaRelayBeaconState>(travelBeacon, YautjaRelayBeaconUIKey.Key, out var beforeRestart), Is.True);
                Assert.That(beforeRestart!.Destinations.Any(destination => destination.Name == "Trophy Hall"), Is.True);

                ClearRelayTeleLocDestinations(entMan);

                var useAfterRestart = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(travelBeacon, useAfterRestart);
                Assert.That(useAfterRestart.Handled, Is.True);
                Assert.That(ui.TryGetUiState<YautjaRelayBeaconState>(travelBeacon, YautjaRelayBeaconUIKey.Key, out var afterRestart), Is.True);
                Assert.That(
                    afterRestart!.Destinations.Any(destination => destination.Name == "Trophy Hall"),
                    Is.False,
                    "CMSS13 tele_loc destinations live in GLOB.yautja_teleports/GLOB.yautja_teleport_descs and should not survive round restart cleanup.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, savingBeacon, travelBeacon })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipTeleportersMatchPortedCmss13NamesAndDestinations()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.EntMan.ComponentFactory;

            var ship = prototypes.Index<EntityPrototype>("CMUHunterShipTeleporterYautjaShip");
            Assert.That(ship.Name, Is.EqualTo("Hunter ship Yautja hunting ground teleporter"));
            Assert.That(ship.TryGetComponent<YautjaHuntTeleporterComponent>(out var shipTeleporter, componentFactory), Is.True);
            Assert.That(shipTeleporter!.Kind, Is.EqualTo(YautjaHuntTeleporterKind.Ship));

            var young = prototypes.Index<EntityPrototype>("CMUHunterShipTeleporterYautjaYoung");
            Assert.That(young.Name, Is.EqualTo("Hunter ship youngblood hunting ground teleporter"));
            Assert.That(young.TryGetComponent<YautjaHuntTeleporterComponent>(out var youngTeleporter, componentFactory), Is.True);
            Assert.That(youngTeleporter!.Kind, Is.EqualTo(YautjaHuntTeleporterKind.Young));

            AssertHuntDestination(prototypes, componentFactory, "CMUYautjaHuntDestinationJungleMoon", YautjaHuntTeleporterKind.Ship, "jungle_moon", "Jungle Moon");
            AssertHuntDestination(prototypes, componentFactory, "CMUYautjaHuntDestinationDesertMoon", YautjaHuntTeleporterKind.Ship, "desert_moon", "Desert Moon");
            AssertHuntDestination(prototypes, componentFactory, "CMUYautjaYoungbloodDestinationJungleMoon", YautjaHuntTeleporterKind.Young, "jungle_moon", "Jungle Moon");
            AssertHuntDestination(prototypes, componentFactory, "CMUYautjaYoungbloodDestinationDesertMoon", YautjaHuntTeleporterKind.Young, "desert_moon", "Desert Moon");

            AssertPlacedMapPrototypeEntityCount(server, "/Maps/_CMU14/huntership.yml", "CMUHunterShipTeleporterYautjaShip", 2);
            AssertPlacedMapPrototypeEntityCount(server, "/Maps/_CMU14/huntership_upper.yml", "CMUHunterShipTeleporterYautjaShip", 2);
            AssertPlacedMapPrototypeEntityCount(server, "/Maps/_CMU14/huntership_lower.yml", "CMUHunterShipTeleporterYautjaYoung", 4);

            var placedRelays = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => proto.ID.StartsWith("CMUHunterShipPlacedCMUYautjaRelayBeacon", StringComparison.Ordinal))
                .ToArray();
            Assert.That(placedRelays, Has.Length.EqualTo(3));
            Assert.Multiple(() =>
            {
                foreach (var relay in placedRelays)
                {
                    Assert.That(relay.Name, Is.EqualTo("relay beacon"), relay.ID);
                    Assert.That(relay.TryGetComponent<YautjaRelayBeaconComponent>(out var relayComp, componentFactory), Is.True, relay.ID);
                    Assert.That(relayComp!.AllowedDestinations, Is.EqualTo(new[]
                    {
                        YautjaRelayDestinationKind.YautjaShip,
                        YautjaRelayDestinationKind.HumanShip,
                    }), relay.ID);
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PredatorInnateActionsMatchCmss13CoreAbilities()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var leap = prototypes.Index<EntityPrototype>("CMUActionYautjaLeap");
            var mark = prototypes.Index<EntityPrototype>("CMUActionYautjaMarkForHunt");
            var visor = prototypes.Index<EntityPrototype>("CMUActionYautjaToggleVisor");
            var translator = prototypes.Index<EntityPrototype>("CMUActionYautjaTranslator");
            var audioPanel = prototypes.Index<EntityPrototype>("CMUActionYautjaAudioPanel");

            Assert.That(leap.TryGetComponent<ActionComponent>(out var leapAction, server.EntMan.ComponentFactory), Is.True);
            Assert.That(leap.TryGetComponent<TargetActionComponent>(out var leapTarget, server.EntMan.ComponentFactory), Is.True);
            Assert.That(leap.TryGetComponent<WorldTargetActionComponent>(out var leapWorld, server.EntMan.ComponentFactory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(leapAction!.UseDelay, Is.EqualTo(TimeSpan.FromSeconds(15)));
                Assert.That(leapTarget!.Range, Is.EqualTo(7));
                Assert.That(leapTarget.CheckCanAccess, Is.False);
                Assert.That(leapWorld!.Event, Is.TypeOf<YautjaLeapActionEvent>());
            });

            Assert.That(mark.TryGetComponent<ActionComponent>(out var markAction, server.EntMan.ComponentFactory), Is.True);
            Assert.That(mark.TryGetComponent<TargetActionComponent>(out var markTarget, server.EntMan.ComponentFactory), Is.True);
            Assert.That(mark.TryGetComponent<EntityTargetActionComponent>(out var markEntity, server.EntMan.ComponentFactory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(markAction!.RaiseOnUser, Is.True);
                Assert.That(markTarget!.Range, Is.EqualTo(7));
                Assert.That(markTarget.CheckCanAccess, Is.False);
                Assert.That(markEntity!.CanTargetSelf, Is.False);
                Assert.That(markEntity.Event, Is.TypeOf<YautjaMarkForHuntActionEvent>());
            });

            Assert.That(visor.TryGetComponent<ActionComponent>(out var visorAction, server.EntMan.ComponentFactory), Is.True);
            Assert.That(visorAction!.Icon, Is.EqualTo(ActionIcon("visor_framed")));
            Assert.That(visorAction.IconOn, Is.EqualTo(ActionIcon("visor_on_framed")));
            Assert.That(visorAction.BackgroundOn, Is.Null);

            Assert.That(translator.TryGetComponent<ActionComponent>(out var translatorAction, server.EntMan.ComponentFactory), Is.True);
            Assert.That(translatorAction!.Icon, Is.EqualTo(ActionIcon("translator_framed")));
            Assert.That(translatorAction.BackgroundOn, Is.Null);
            Assert.That(translatorAction.UseDelay, Is.Null);

            Assert.That(audioPanel.TryGetComponent<ActionComponent>(out var audioPanelAction, server.EntMan.ComponentFactory), Is.True);
            Assert.That(audioPanelAction!.Icon, Is.EqualTo(ActionIcon("looc_toggle_framed")));
            Assert.That(audioPanel.TryGetComponent<InstantActionComponent>(out var audioPanelInstant, server.EntMan.ComponentFactory), Is.True);
            Assert.That(audioPanelInstant!.Event, Is.TypeOf<YautjaAudioPanelActionEvent>());

            var visibleActionsWithoutMetadata = new[]
            {
                "CMUActionYautjaToggleVisor",
                "CMUActionYautjaToggleMaskZoom",
                "CMUActionYautjaToggleCloak",
                "CMUActionYautjaOpenBracerMenu",
                "CMUActionYautjaToggleBracerLock",
                "CMUActionYautjaToggleBracerIdChip",
                "CMUActionYautjaChangeExplosionType",
                "CMUActionYautjaToggleBracerNotificationSound",
                "CMUActionYautjaToggleBracerName",
                "CMUActionYautjaTrackGear",
                "CMUActionYautjaAddTrackedItem",
                "CMUActionYautjaRemoveTrackedItem",
                "CMUActionYautjaTranslator",
                "CMUActionYautjaAudioPanel",
                "CMUActionYautjaCreateStabilisingCrystal",
                "CMUActionYautjaCreateHumanStabilisingCrystal",
                "CMUActionYautjaCreateHealingCapsule",
                "CMUActionYautjaCreateHuntingTrap",
                "CMUActionYautjaOpenMarkPanel",
                "CMUActionYautjaLinkThrallBracer",
                "CMUActionYautjaTransmitThrallMessage",
                "CMUActionYautjaToggleThrallBracerLock",
                "CMUActionYautjaStunThrall",
                "CMUActionYautjaSelfDestructThrall",
                "CMUActionYautjaRecall",
                "CMUActionYautjaCallDisc",
                "CMUActionYautjaFalconRecall",
                "CMUActionYautjaSelfDestruct",
                "CMUActionYautjaToggleCaster",
                "CMUActionYautjaUsePlasmaCannons",
                "CMUActionYautjaToggleWristBlades",
                "CMUActionYautjaToggleScimitar",
                "CMUActionYautjaToggleShield",
                "CMUActionYautjaToggleChainGauntlet",
                "CMUActionYautjaLeap",
                "CMUActionYautjaMarkForHunt",
            };

            Assert.Multiple(() =>
            {
                foreach (var id in visibleActionsWithoutMetadata)
                {
                    var action = prototypes.Index<EntityPrototype>(id);
                    Assert.That(action.Name, Is.Not.Null.And.Not.Empty, $"{id} action name");
                    Assert.That(action.Description, Is.Not.Null.And.Not.Empty, $"{id} action description");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaActionRsiLoadsEveryReferencedActionState()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var componentFactory = client.ResolveDependency<IComponentFactory>();
            var cache = client.ResolveDependency<IResourceCache>();
            var rsiPath = new ResPath("/Textures/_CMU14/Yautja/actions.rsi");

            Assert.That(cache.TryGetResource<RSIResource>(rsiPath, out var resource), Is.True, $"{rsiPath} must load without falling back to error icons.");

            var referencedStates = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => proto.ID.StartsWith("CMUActionYautja"))
                .Select(proto =>
                {
                    Assert.That(proto.TryGetComponent<ActionComponent>(out var action, componentFactory), Is.True, $"{proto.ID} must have ActionComponent.");
                    return action!;
                })
                .SelectMany(action => new[] { action.Icon, action.IconOn })
                .OfType<SpriteSpecifier.Rsi>()
                .Where(icon => icon.RsiPath == new ResPath("_CMU14/Yautja/actions.rsi"))
                .Select(icon => icon.RsiState)
                .Distinct()
                .OrderBy(state => state)
                .ToList();

            Assert.That(referencedStates, Is.Not.Empty);
            Assert.Multiple(() =>
            {
                foreach (var state in referencedStates)
                    Assert.That(resource!.RSI.TryGetState(new RSI.StateId(state), out _), Is.True, $"Missing Yautja action icon state {state}.");

                Assert.That(resource!.RSI.TryGetState("translator_framed", out var translator), Is.True);
                Assert.That(translator!.IsAnimated, Is.True, "CMSS13 translator action icon should keep its framed animation.");
                Assert.That(translator.DelayCount, Is.EqualTo(6));

                Assert.That(resource.RSI.TryGetState("translator", out var rawTranslator), Is.True);
                Assert.That(rawTranslator!.IsAnimated, Is.True, "The raw CMSS13 translator overlay should keep frames 1-6.");
                Assert.That(rawTranslator.DelayCount, Is.EqualTo(6));
                Assert.That(resource.RSI.TryGetState("pred_template_on", out _), Is.True, "Active Yautja actions should use CMSS13 pred_template_on.");

                foreach (var onState in new[]
                         {
                             "zoom_on_framed",
                             "cloak_on_framed",
                             "plasma_caster_on_framed",
                             "wristblade_on_framed",
                             "combi_on_framed",
                             "self_destruct_on_framed",
                             "visor_on_framed",
                             "scimitar_on_framed",
                             "bracer_shield_on_framed",
                         })
                {
                    Assert.That(resource.RSI.TryGetState(onState, out _), Is.True, $"{onState} should be composed with CMSS13 pred_template_on.");
                }

                Assert.That(resource.RSI.TryGetState("bracer_framed", out _), Is.True, "Local bracer menu should use the same CMSS13 pred_template framing as predator actions.");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaAnimatedItemsUseCmss13HunterGearSprites()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var componentFactory = client.ResolveDependency<IComponentFactory>();
            var cache = client.ResolveDependency<IResourceCache>();
            var hunterGearPath = new ResPath("/Textures/_CMU14/HunterShip/obj/items/hunter/pred_gear.rsi");
            var thrallGearPath = new ResPath("/Textures/_CMU14/HunterShip/obj/items/hunter/thrall_gear.rsi");
            var bracerPath = new ResPath("/Textures/_CMU14/Yautja/bracer.rsi");

            Assert.That(cache.TryGetResource<RSIResource>(hunterGearPath, out var hunterGear), Is.True);
            Assert.That(hunterGear!.RSI.TryGetState("teleporter", out var teleporter), Is.True);
            Assert.That(teleporter!.IsAnimated, Is.True, "CMSS13 relay beacon teleporter should animate.");
            Assert.That(teleporter.DelayCount, Is.EqualTo(4));

            Assert.That(cache.TryGetResource<RSIResource>(thrallGearPath, out var thrallGear), Is.True);
            Assert.That(thrallGear!.RSI.TryGetState("thrall_teleporter", out var thrallTeleporter), Is.True);
            Assert.That(thrallTeleporter!.IsAnimated, Is.True, "CMSS13 simple relay beacon should use the animated thrall teleporter state.");
            Assert.That(thrallTeleporter.DelayCount, Is.EqualTo(4));

            Assert.That(hunterGear.RSI.TryGetState("houndpad", out var houndPad), Is.True);
            Assert.That(houndPad!.IsAnimated, Is.True, "Hellhound observation pad should use the same animated state as the hunter ship pad.");
            Assert.That(houndPad.DelayCount, Is.EqualTo(10));

            Assert.That(cache.TryGetResource<RSIResource>(bracerPath, out var bracer), Is.True);
            Assert.That(bracer!.RSI.TryGetState("bracer1", out var bracerIcon), Is.True);
            Assert.That(bracerIcon!.IsAnimated, Is.False, "Right-click and UI previews need the static CMSS13 bracer1 frame.");
            Assert.That(bracer.RSI.TryGetState("bracer", out var animatedBracer), Is.True);
            Assert.That(animatedBracer!.IsAnimated, Is.True, "CMSS13 hunting bracer world icon should keep its animated bracer frames.");
            Assert.That(animatedBracer.DelayCount, Is.EqualTo(6));

            var relay = prototypes.Index<EntityPrototype>("CMUYautjaRelayBeacon");
            Assert.That(relay.TryGetComponent<SpriteComponent>(out var relaySprite, componentFactory), Is.True);
            Assert.That(relaySprite!.BaseRSI?.Path, Is.EqualTo(hunterGearPath));

            var simpleRelay = prototypes.Index<EntityPrototype>("CMUYautjaSimpleRelayBeacon");
            Assert.That(simpleRelay.TryGetComponent<SpriteComponent>(out var simpleRelaySprite, componentFactory), Is.True);
            Assert.That(simpleRelaySprite!.BaseRSI?.Path, Is.EqualTo(thrallGearPath));

            var pad = prototypes.Index<EntityPrototype>("CMUYautjaHoundObservationPad");
            Assert.That(pad.TryGetComponent<SpriteComponent>(out var padSprite, componentFactory), Is.True);
            Assert.That(padSprite!.BaseRSI?.Path, Is.EqualTo(hunterGearPath));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaActionIconsDoNotUseCooldownFillExceptCmss13Cooldowns()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.EntMan.ComponentFactory;
            var allowedCooldownActions = new HashSet<string>
            {
                "CMUActionYautjaLeap",
                "CMUActionYautjaToggleLantern",
            };

            var yautjaActions = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => proto.ID.StartsWith("CMUActionYautja"))
                .Where(proto => !proto.ID.StartsWith("CMUActionYautjaAbomination"))
                .Where(proto => !proto.ID.StartsWith("CMUActionYautjaHellhound"))
                .Where(proto => proto.TryGetComponent<ActionComponent>(out _, componentFactory))
                .ToList();

            Assert.That(yautjaActions, Is.Not.Empty);
            Assert.Multiple(() =>
            {
                foreach (var proto in yautjaActions)
                {
                    Assert.That(proto.TryGetComponent<ActionComponent>(out var action, componentFactory), Is.True);
                    if (allowedCooldownActions.Contains(proto.ID))
                        Assert.That(action!.UseDelay, Is.Not.Null, proto.ID);
                    else
                        Assert.That(action!.UseDelay, Is.Null, $"{proto.ID} should not draw a cooldown fill over the CMSS13 action icon animation.");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MergedYautjaGearRackRsiLoadsForSingleClickableOutline()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var cache = client.ResolveDependency<IResourceCache>();
            for (var length = 2; length <= 5; length++)
            {
                var rsiPath = new ResPath($"/Textures/_CMU14/HunterShip/obj/items/hunter/pred_vendor_merged_{length}.rsi");
                Assert.That(cache.TryGetResource<RSIResource>(rsiPath, out var resource), Is.True, $"{rsiPath} must load for merged rack outline.");
                Assert.That(resource!.RSI.Size.X, Is.EqualTo(32 * length));
                Assert.That(resource.RSI.Size.Y, Is.EqualTo(64));
                Assert.That(resource.RSI.TryGetState("pred_vendor_merged", out var state), Is.True);
                Assert.That(state!.IsAnimated, Is.True);
                Assert.That(state.DelayCount, Is.EqualTo(5));
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MergedYautjaGearRackRsiComposesEveryAnimationFrameFromSegments()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var resources = client.ResolveDependency<IResourceManager>();
            var variants = new Dictionary<int, string[]>
            {
                [2] = ["pred_vendor_left", "pred_vendor_right"],
                [3] = ["pred_vendor_left", "pred_vendor_centre", "pred_vendor_right"],
                [4] = ["pred_vendor_left", "pred_vendor_lcenter", "pred_vendor_rcentre", "pred_vendor_right"],
                [5] = ["pred_vendor_left", "pred_vendor_lcenter", "pred_vendor_centre", "pred_vendor_rcentre", "pred_vendor_right"],
            };

            foreach (var (length, segmentStates) in variants)
            {
                using var merged = LoadRgbaImage(resources,
                    new ResPath($"/Textures/_CMU14/HunterShip/obj/items/hunter/pred_vendor_merged_{length}.rsi/pred_vendor_merged.png"));
                using var left = LoadRgbaImage(resources,
                    new ResPath("/Textures/_CMU14/HunterShip/obj/items/hunter/pred_vendor.rsi/pred_vendor_left.png"));
                using var lcenter = LoadRgbaImage(resources,
                    new ResPath("/Textures/_CMU14/HunterShip/obj/items/hunter/pred_vendor.rsi/pred_vendor_lcenter.png"));
                using var centre = LoadRgbaImage(resources,
                    new ResPath("/Textures/_CMU14/HunterShip/obj/items/hunter/pred_vendor.rsi/pred_vendor_centre.png"));
                using var rcentre = LoadRgbaImage(resources,
                    new ResPath("/Textures/_CMU14/HunterShip/obj/items/hunter/pred_vendor.rsi/pred_vendor_rcentre.png"));
                using var right = LoadRgbaImage(resources,
                    new ResPath("/Textures/_CMU14/HunterShip/obj/items/hunter/pred_vendor.rsi/pred_vendor_right.png"));

                var sources = new Dictionary<string, SixLabors.ImageSharp.Image<Rgba32>>
                {
                    ["pred_vendor_left"] = left,
                    ["pred_vendor_lcenter"] = lcenter,
                    ["pred_vendor_centre"] = centre,
                    ["pred_vendor_rcentre"] = rcentre,
                    ["pred_vendor_right"] = right,
                };

                Assert.That(merged.Width, Is.EqualTo(32 * length * 2), $"merged width for length {length}");
                Assert.That(merged.Height, Is.EqualTo(64 * 3), $"merged height for length {length}");

                for (var frame = 0; frame < 5; frame++)
                {
                    for (var segment = 0; segment < segmentStates.Length; segment++)
                    {
                        AssertMergedVendorFrameSegment(
                            sources[segmentStates[segment]],
                            merged,
                            segmentStates[segment],
                            length,
                            segment,
                            frame);
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConnectedYautjaGearRackUsesSingleClickableMergedVendor()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid left = default;
        EntityUid centre = default;
        EntityUid right = default;
        EntityUid hunter = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var mapSystem = entMan.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), new Tile(1));
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(2, 0), new Tile(1));
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(3, 0), new Tile(1));
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(4, 0), new Tile(1));

            left = entMan.SpawnEntity("CMUHunterShipPlacedCMUYautjaLoadoutVendorPredVendorLeftSouthOffset0x16", map.GridCoords);
            centre = entMan.SpawnEntity("CMUHunterShipPlacedCMUYautjaLoadoutVendorPredVendorCentreSouthOffset0x16", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            right = entMan.SpawnEntity("CMUHunterShipPlacedCMUYautjaLoadoutVendorPredVendorRightSouthOffset0x16", map.GridCoords.Offset(new System.Numerics.Vector2(2, 0)));
            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(4, 0)));
        });

        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var serverEntMan = server.EntMan;
            var clickable = entMan.System<ClickableSystem>();
            var interaction = entMan.System<SharedInteractionSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var eye = client.ResolveDependency<IEyeManager>().CurrentEye;

            var rackPieces = new[]
            {
                entMan.GetEntity(serverEntMan.GetNetEntity(left)),
                entMan.GetEntity(serverEntMan.GetNetEntity(centre)),
                entMan.GetEntity(serverEntMan.GetNetEntity(right)),
            };

            Assert.Multiple(() =>
            {
                var primary = rackPieces[0];
                Assert.That(entMan.HasComponent<ClickableComponent>(primary), Is.True, primary.ToString());
                Assert.That(entMan.HasComponent<InteractionOutlineComponent>(primary), Is.True, primary.ToString());

                var primarySprite = entMan.GetComponent<SpriteComponent>(primary);
                Assert.That(primarySprite.Visible, Is.True, primary.ToString());

                Assert.That(entMan.HasComponent<ClickableComponent>(rackPieces[1]), Is.False, rackPieces[1].ToString());
                Assert.That(entMan.HasComponent<ClickableComponent>(rackPieces[2]), Is.False, rackPieces[2].ToString());
                Assert.That(entMan.GetComponent<SpriteComponent>(rackPieces[1]).Visible, Is.False, rackPieces[1].ToString());
                Assert.That(entMan.GetComponent<SpriteComponent>(rackPieces[2]).Visible, Is.False, rackPieces[2].ToString());

                for (var i = 0; i < rackPieces.Length; i++)
                {
                    var clickPos = transform.GetWorldPosition(rackPieces[i]) + new System.Numerics.Vector2(0, 0.5f);
                    Assert.That(clickable.CheckClick((primary, null, primarySprite, null), clickPos, eye, false, out _, out _, out _), Is.True, $"segment {i}");
                }

                var rightEdgeClick = transform.GetWorldPosition(rackPieces[2]) + new System.Numerics.Vector2(0.44f, 0.5f);
                Assert.That(clickable.CheckClick((primary, null, primarySprite, null), rightEdgeClick, eye, false, out _, out _, out _), Is.True, "right edge");

                var hunterClient = entMan.GetEntity(serverEntMan.GetNetEntity(hunter));
                Assert.That(interaction.InRangeUnobstructed(hunterClient, primary), Is.True, "right-side two-tile client outline range");
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            if (!entMan.Deleted(left))
                entMan.DeleteEntity(left);
            if (!entMan.Deleted(centre))
                entMan.DeleteEntity(centre);
            if (!entMan.Deleted(right))
                entMan.DeleteEntity(right);
            if (!entMan.Deleted(hunter))
                entMan.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerRightClickVerbMenuCanRender()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;
        List<Verb> serverVerbs = new();
        List<Verb> responseVerbs = new();
        var gotResponse = false;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var verbs = entMan.System<Content.Server.Verbs.VerbSystem>();

                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                serverVerbs = verbs.GetLocalVerbs(bracer, hunter, Verb.VerbTypes, force: true).ToList();
            });

            await pair.RunTicksSync(5);

            await client.WaitPost(() =>
            {
                var entMan = client.EntMan;
                var player = client.ResolveDependency<Robust.Client.Player.IPlayerManager>().LocalEntity;
                Assert.That(player, Is.Not.Null);

                var clientBracer = entMan.GetEntity(server.EntMan.GetNetEntity(bracer));
                var clientVerbs = entMan.System<Content.Client.Verbs.VerbSystem>();
                var netBracer = entMan.GetNetEntity(clientBracer);
                var ui = client.ResolveDependency<Robust.Client.UserInterface.IUserInterfaceManager>();
                ui.GetUIController<ContextMenuUIController>().Setup();
                var verbMenu = ui.GetUIController<VerbMenuUIController>();

                void Handler(VerbsResponseEvent response)
                {
                    if (response.Entity != netBracer)
                        return;

                    responseVerbs = response.Verbs;
                    gotResponse = true;
                    clientVerbs.OnVerbsResponse -= Handler;
                }

                clientVerbs.OnVerbsResponse += Handler;
                Assert.DoesNotThrow(() => verbMenu.OpenVerbMenu(clientBracer, force: true));
            });

            await pair.RunTicksSync(5);

            await client.WaitAssertion(() =>
            {
                var entMan = client.EntMan;
                var player = client.ResolveDependency<Robust.Client.Player.IPlayerManager>().LocalEntity;
                Assert.That(player, Is.Not.Null);
                Assert.That(gotResponse, Is.True);

                var clientBracer = entMan.GetEntity(server.EntMan.GetNetEntity(bracer));
                var localVerbs = entMan.System<Content.Client.Verbs.VerbSystem>()
                    .GetLocalVerbs(clientBracer, player!.Value, Verb.VerbTypes, force: true);

                Assert.Multiple(() =>
                {
                    foreach (var verb in localVerbs.Concat(serverVerbs).Concat(responseVerbs))
                        AssertVerbMenuElementCanRender(verb);
                });
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.SingleOrDefault();
                if (session != null)
                    server.PlayerMan.SetAttachedEntity(session, previousAttached);

                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (bracer != default && !entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodSpawnBracerRightClickVerbMenuCanRender()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var player = pair.Player;
        var map = await pair.CreateTestMap();
        EntityUid youngblood = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;
        List<Verb> serverVerbs = new();
        List<Verb> responseVerbs = new();
        var gotResponse = false;

        Assert.That(player, Is.Not.Null);

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var mind = entMan.System<MindSystem>();
                var verbs = entMan.System<Content.Server.Verbs.VerbSystem>();

                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                youngblood = entMan.SpawnEntity("CMUMobYautjaYoungblood", map.GridCoords);
                entMan.EnsureComponent<YautjaYoungbloodGhostRoleComponent>(youngblood);

                var mindEnt = mind.CreateMind(player!.UserId, "Yautja Youngblood");
                mind.TransferTo(mindEnt.Owner, youngblood);
                server.PlayerMan.SetAttachedEntity(session, youngblood);

                Assert.That(inventory.TryGetSlotEntity(youngblood, "gloves", out var equippedBracer), Is.True);
                bracer = equippedBracer!.Value;
                AssertEquippedPrototype(entMan, inventory, youngblood, "gloves", "CMUYautjaBracer");
                serverVerbs = verbs.GetLocalVerbs(bracer, youngblood, Verb.VerbTypes, force: false).ToList();
            });

            await pair.RunTicksSync(5);

            await client.WaitPost(() =>
            {
                var entMan = client.EntMan;
                var localPlayer = client.ResolveDependency<Robust.Client.Player.IPlayerManager>().LocalEntity;
                Assert.That(localPlayer, Is.Not.Null);

                var clientBracer = entMan.GetEntity(server.EntMan.GetNetEntity(bracer));
                var clientVerbs = entMan.System<Content.Client.Verbs.VerbSystem>();
                var netBracer = entMan.GetNetEntity(clientBracer);
                var ui = client.ResolveDependency<Robust.Client.UserInterface.IUserInterfaceManager>();
                ui.GetUIController<ContextMenuUIController>().Setup();
                var verbMenu = ui.GetUIController<VerbMenuUIController>();

                void Handler(VerbsResponseEvent response)
                {
                    if (response.Entity != netBracer)
                        return;

                    responseVerbs = response.Verbs;
                    gotResponse = true;
                    clientVerbs.OnVerbsResponse -= Handler;
                }

                clientVerbs.OnVerbsResponse += Handler;
                Assert.DoesNotThrow(() => verbMenu.OpenVerbMenu(clientBracer, force: false));
            });

            await pair.RunTicksSync(5);

            await client.WaitAssertion(() =>
            {
                var entMan = client.EntMan;
                var localPlayer = client.ResolveDependency<Robust.Client.Player.IPlayerManager>().LocalEntity;
                Assert.That(localPlayer, Is.Not.Null);
                Assert.That(gotResponse, Is.True);

                var clientBracer = entMan.GetEntity(server.EntMan.GetNetEntity(bracer));
                var localVerbs = entMan.System<Content.Client.Verbs.VerbSystem>()
                    .GetLocalVerbs(clientBracer, localPlayer!.Value, Verb.VerbTypes, force: false);

                Assert.Multiple(() =>
                {
                    foreach (var verb in localVerbs.Concat(serverVerbs).Concat(responseVerbs))
                        AssertVerbMenuElementCanRender(verb);
                });
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.SingleOrDefault();
                if (session != null)
                    server.PlayerMan.SetAttachedEntity(session, previousAttached);

                if (youngblood != default && !entMan.Deleted(youngblood))
                    entMan.DeleteEntity(youngblood);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MarkForHuntActionTogglesPreyMarkThroughWornBracer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<Content.Shared.Inventory.InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaMarkForHunt", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(hunter, new YautjaMarkForHuntActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                    Target = target,
                });

                Assert.That(entMan.TryGetComponent<YautjaMarkComponent>(target, out var mark), Is.True);
                Assert.That(mark!.Marks[YautjaMarkKind.Prey], Is.EqualTo(hunter));

                entMan.EventBus.RaiseLocalEvent(hunter, new YautjaMarkForHuntActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                    Target = target,
                });

                var marks = entMan.System<YautjaMarkSystem>();
                Assert.That(marks.IsMarkedBy(target, YautjaMarkKind.Prey, hunter), Is.False);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(target))
                    entMan.DeleteEntity(target);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaVoiceSoundsStayInEmoteWheelNotActionBar()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);

            try
            {
                var actions = entMan.GetComponent<ActionsComponent>(hunter);
                var actionPrototypeIds = actions.Actions
                    .Select(action => entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID)
                    .Where(id => id != null)
                    .Select(id => id!)
                    .ToHashSet();

                Assert.Multiple(() =>
                {
                    Assert.That(actionPrototypeIds, Does.Contain("CMUActionYautjaLeap"));
                    Assert.That(actionPrototypeIds, Does.Contain("CMUActionYautjaMarkForHunt"));
                    Assert.That(actionPrototypeIds, Does.Contain("CMUActionYautjaAudioPanel"));
                    Assert.That(actionPrototypeIds.Any(id => id.StartsWith("CMUActionYautjaVoice")), Is.False);

                    var speech = entMan.GetComponent<SpeechComponent>(hunter);
                    Assert.That(speech.AllowedEmotes, Does.Contain("CMUYautjaClick"));
                    Assert.That(speech.AllowedEmotes, Does.Contain("CMUYautjaRoar"));
                    Assert.That(speech.AllowedEmotes, Does.Contain("CMUYautjaLaugh"));
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StudentMarkOnYautjaGetsStatusIconCarrier()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<Content.Shared.Inventory.InventorySystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var student = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaComponent>(student);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(student);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);

                var marks = entMan.System<YautjaMarkSystem>();
                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, student, YautjaMarkKind.Student, null), Is.True);
                Assert.That(entMan.HasComponent<YautjaMarkComponent>(student), Is.True);
                Assert.That(entMan.HasComponent<StatusIconComponent>(student), Is.True);
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(student))
                    entMan.DeleteEntity(student);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StudentAndBloodedMarksApplyStatusIconCarriers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<Content.Shared.Inventory.InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var student = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            var thrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(2, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(student);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(student);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, student, YautjaMarkKind.Student, null), Is.True);
                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, thrall, YautjaMarkKind.Thrall, null), Is.True);
                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, thrall, YautjaMarkKind.Blooded, null), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<YautjaMarkComponent>(student).Marks.Keys, Does.Contain(YautjaMarkKind.Student));
                    Assert.That(entMan.HasComponent<StatusIconComponent>(student), Is.True);
                    Assert.That(entMan.GetComponent<YautjaMarkComponent>(thrall).Marks.Keys, Does.Contain(YautjaMarkKind.Blooded));
                    Assert.That(entMan.HasComponent<StatusIconComponent>(thrall), Is.True);
                    Assert.That(entMan.GetComponent<YautjaThrallComponent>(thrall).Blooded, Is.True);
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(student))
                    entMan.DeleteEntity(student);
                if (!entMan.Deleted(thrall))
                    entMan.DeleteEntity(thrall);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaStudentAndBloodedMarkIconsUseCmss13HunterHudStates()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var thrall = prototypes.Index<HealthIconPrototype>("CMUYautjaIconThrall");
            var student = prototypes.Index<HealthIconPrototype>("CMUYautjaIconStudent");
            var blooded = prototypes.Index<HealthIconPrototype>("CMUYautjaIconBlooded");
            var bloodedThrall = prototypes.Index<HealthIconPrototype>("CMUYautjaIconBloodedThrall");

            Assert.That(thrall.Icon, Is.EqualTo(StatusIcon("hunter_thralled")));
            Assert.That(student.Icon, Is.EqualTo(StatusIcon("predhud")));
            Assert.That(blooded.Icon, Is.EqualTo(StatusIcon("hunter_thrall_blooded")));
            Assert.That(bloodedThrall.Icon, Is.EqualTo(StatusIcon("hunter_thralled_blooded")));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodedThrallHudUsesCmss13OverlayStack()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var cache = client.ResolveDependency<IResourceCache>();
            var hud = client.EntMan.System<YautjaHudSystem>();
            var rsiPath = new ResPath("/Textures/_CMU14/Yautja/hud_yautja.rsi");

            Assert.That(cache.TryGetResource<RSIResource>(rsiPath, out var resource), Is.True, $"{rsiPath} must load without falling back to error icons.");
            Assert.That(resource!.RSI.TryGetState("hunter_thralled_blooded", out _), Is.True);

            var icons = new List<StatusIconData>();
            hud.AddIconsForMarks(new[]
            {
                YautjaMarkKind.Thrall,
                YautjaMarkKind.Blooded,
            }, icons);

            var states = icons
                .Select(icon => icon.Icon)
                .OfType<SpriteSpecifier.Rsi>()
                .Select(icon => icon.RsiState)
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(states, Is.EqualTo(new[] { "hunter_thralled_blooded" }));
                Assert.That(states, Does.Not.Contain("hunter_thralled"));
                Assert.That(states, Does.Not.Contain("hunter_thrall_blooded"));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaAudioPanelMatchesCmss13PanelEmotes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var entries = GetAudioPanelEmotesForTest();
            const string yautjaAudioCategory = "cmu-yautja-audio-panel-category-yautja";
            const string voiceSynthCategory = "cmu-yautja-audio-panel-category-voice-synthesizer";
            const string fakeAudioCategory = "cmu-yautja-audio-panel-category-fake-sound";
            var expected = new (string Id, string Emote, string Category)[]
            {
                ("click", "CMUYautjaAudioClick", yautjaAudioCategory),
                ("click2", "CMUYautjaAudioClick2", yautjaAudioCategory),
                ("growl", "CMUYautjaAudioGrowl", yautjaAudioCategory),
                ("laugh1", "CMUYautjaAudioLaugh1", yautjaAudioCategory),
                ("laugh2", "CMUYautjaAudioLaugh2", yautjaAudioCategory),
                ("laugh3", "CMUYautjaAudioLaugh3", yautjaAudioCategory),
                ("laugh4", "CMUYautjaAudioLaugh4", yautjaAudioCategory),
                ("laugh5", "CMUYautjaAudioLaugh5", yautjaAudioCategory),
                ("laugh6", "CMUYautjaAudioLaugh6", yautjaAudioCategory),
                ("roar", "CMUYautjaAudioRoar", yautjaAudioCategory),
                ("roar2", "CMUYautjaAudioRoar2", yautjaAudioCategory),
                ("anytime", "CMUYautjaVoiceSynthAnytime", voiceSynthCategory),
                ("helpme", "CMUYautjaVoiceSynthHelpMe", voiceSynthCategory),
                ("iseeyou", "CMUYautjaVoiceSynthISeeYou", voiceSynthCategory),
                ("itsatrap", "CMUYautjaVoiceSynthItsATrap", voiceSynthCategory),
                ("overhere", "CMUYautjaVoiceSynthOverHere", voiceSynthCategory),
                ("turnaround", "CMUYautjaVoiceSynthTurnAround", voiceSynthCategory),
                ("comeonout", "CMUYautjaVoiceSynthComeOnOut", voiceSynthCategory),
                ("overthere", "CMUYautjaVoiceSynthOverThere", voiceSynthCategory),
                ("uglyfreak", "CMUYautjaVoiceSynthUglyFreak", voiceSynthCategory),
                ("luckyyou", "CMUYautjaVoiceSynthLuckyYou", voiceSynthCategory),
                ("justyou", "CMUYautjaVoiceSynthJustYou", voiceSynthCategory),
                ("tellme", "CMUYautjaVoiceSynthTellMe", voiceSynthCategory),
                ("doitrookie", "CMUYautjaVoiceSynthDoItRookie", voiceSynthCategory),
                ("forwardmarine", "CMUYautjaVoiceSynthForwardMarine", voiceSynthCategory),
                ("burnyoufucker", "CMUYautjaVoiceSynthBurnYouFucker", voiceSynthCategory),
                ("aliengrowl", "CMUYautjaFakeAlienGrowl", fakeAudioCategory),
                ("alienhelp", "CMUYautjaFakeAlienHelp", fakeAudioCategory),
                ("malescream", "CMUYautjaFakeMaleScream", fakeAudioCategory),
                ("femalescream", "CMUYautjaFakeFemaleScream", fakeAudioCategory),
            };

            Assert.That(entries, Is.EqualTo(expected));

            var yautjaSounds = prototypes.Index<EmoteSoundsPrototype>("CMUBaseYautja");
            Assert.Multiple(() =>
            {
                foreach (var (_, emote, _) in expected)
                {
                    Assert.That(prototypes.HasIndex<EmotePrototype>(emote), Is.True, $"{emote} prototype must exist.");
                    Assert.That(yautjaSounds.Sounds.ContainsKey(emote), Is.True, $"{emote} must have a Yautja sound mapping.");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    private static SpriteSpecifier.Rsi ActionIcon(string state)
    {
        return new SpriteSpecifier.Rsi(new ResPath("_CMU14/Yautja/actions.rsi"), state);
    }

    private static SpriteSpecifier.Rsi StatusIcon(string state)
    {
        return new SpriteSpecifier.Rsi(new ResPath("/Textures/_CMU14/Yautja/hud_yautja.rsi"), state);
    }

    private static void MakeActivelyCloaked(IEntityManager entMan, EntityUid user)
    {
        var invisible = entMan.EnsureComponent<EntityActiveInvisibleComponent>(user);
        invisible.Opacity = 0.2f;
        var turnInvisible = entMan.EnsureComponent<EntityTurnInvisibleComponent>(user);
        turnInvisible.Enabled = true;
    }

    private static SixLabors.ImageSharp.Image<Rgba32> LoadRgbaImage(IResourceManager resources, ResPath path)
    {
        using var stream = resources.ContentFileRead(path);
        return SixLabors.ImageSharp.Image.Load<Rgba32>(stream);
    }

    private static void AssertMergedVendorFrameSegment(
        SixLabors.ImageSharp.Image<Rgba32> source,
        SixLabors.ImageSharp.Image<Rgba32> merged,
        string sourceState,
        int length,
        int segment,
        int frame)
    {
        const int tileWidth = 32;
        const int tileHeight = 64;

        var sourceColumns = source.Width / tileWidth;
        var mergedFrameWidth = tileWidth * length;
        var mergedColumns = merged.Width / mergedFrameWidth;
        var sourceX = frame % sourceColumns * tileWidth;
        var sourceY = frame / sourceColumns * tileHeight;
        var mergedX = frame % mergedColumns * mergedFrameWidth + segment * tileWidth;
        var mergedY = frame / mergedColumns * tileHeight;

        for (var y = 0; y < tileHeight; y++)
        {
            for (var x = 0; x < tileWidth; x++)
            {
                var expected = source[sourceX + x, sourceY + y];
                var actual = merged[mergedX + x, mergedY + y];
                if (actual == expected)
                    continue;

                Assert.Fail(
                    $"{sourceState} segment {segment} frame {frame} is not composed into merged length {length} at pixel {x},{y}. Expected {expected}, got {actual}.");
            }
        }
    }

    private static void AssertEquippedPrototype(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid wearer,
        string slot,
        string expectedPrototype)
    {
        Assert.That(inventory.TryGetSlotEntity(wearer, slot, out var equipped), Is.True, slot);
        var meta = entMan.GetComponent<MetaDataComponent>(equipped.Value);
        Assert.That(meta.EntityPrototype?.ID, Is.EqualTo(expectedPrototype), slot);
    }

    private static void SetNpcFaction(IEntityManager entMan, EntityUid uid, ProtoId<NpcFactionPrototype> faction)
    {
        var factions = entMan.EnsureComponent<NpcFactionMemberComponent>(uid);
        factions.Factions.Clear();
        factions.Factions.Add(faction);
    }

    private static void DeleteEntities(IEntityManager entMan, params EntityUid[] uids)
    {
        foreach (var uid in uids)
        {
            if (uid != default && !entMan.Deleted(uid))
                entMan.DeleteEntity(uid);
        }
    }

    private static void AssertVerbMenuElementCanRender(Verb verb)
    {
        Assert.DoesNotThrow(() =>
        {
            var element = new VerbMenuElement(verb);
            _ = element.TooltipSupplier?.Invoke(element);
        }, $"verb {verb.Text}");
    }

    private static void AssertHuntDestination(
        IPrototypeManager prototypes,
        IComponentFactory componentFactory,
        string id,
        YautjaHuntTeleporterKind kind,
        string destinationId,
        string displayName)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);
        Assert.That(prototype.TryGetComponent<YautjaHuntTeleportDestinationComponent>(out var destination, componentFactory), Is.True);
        Assert.That(destination!.Kind, Is.EqualTo(kind), id);
        Assert.That(destination.Id, Is.EqualTo(destinationId), id);
        Assert.That(destination.DisplayName, Is.EqualTo(displayName), id);
    }

    private static void AssertPlacedMapPrototypeEntityCount(RobustIntegrationTest.IntegrationInstance instance, string path, string prototype, int expected)
    {
        var resources = instance.ResolveDependency<IResourceManager>();
        using var stream = resources.ContentFileRead(new ResPath(path));
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        var count = 0;
        var inBlock = false;
        foreach (var line in text.Split('\n'))
        {
            if (line.StartsWith("- proto: "))
            {
                inBlock = line.Trim() == $"- proto: {prototype}";
                continue;
            }

            if (inBlock && line.TrimStart().StartsWith("- uid: "))
                count++;
        }

        Assert.That(count, Is.EqualTo(expected), path);
    }

    private static List<(string Id, string Emote, string Category)> GetAudioPanelEmotesForTest()
    {
        var field = typeof(YautjaVoiceSystem).GetField("AudioPanelEmotes", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(field, Is.Not.Null);

        var value = (System.Collections.IEnumerable) field!.GetValue(null)!;
        var entries = new List<(string Id, string Emote, string Category)>();
        foreach (var entry in value)
        {
            var type = entry.GetType();
            var id = (string) type.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(entry)!;
            var emote = type.GetProperty("Emote", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(entry)!.ToString()!;
            var category = (string) type.GetProperty("Category", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(entry)!;
            entries.Add((id, emote, category));
        }

        return entries;
    }
}

public sealed partial class YautjaTestRadioReceiveRecorderSystem : EntitySystem
{
    public readonly List<(EntityUid Receiver, string Message, string Channel, string? Verb, string WrappedMessage)> Deliveries = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaTestRadioReceiveRecorderComponent, RadioReceiveEvent>(OnRadioReceive);
    }

    public void Clear()
    {
        Deliveries.Clear();
    }

    public bool DeliveredTo(EntityUid receiver, string message)
    {
        return Deliveries.Any(delivery => delivery.Receiver == receiver && delivery.Message == message);
    }

    public int DeliveryCount(EntityUid receiver, string message)
    {
        return Deliveries.Count(delivery => delivery.Receiver == receiver && delivery.Message == message);
    }

    public bool DeliveredMessageOnChannel(string message, string channel)
    {
        return Deliveries.Any(delivery => delivery.Message == message && delivery.Channel == channel);
    }

    public string? DeliveredVerb(EntityUid receiver, string message)
    {
        return Deliveries.FirstOrDefault(delivery => delivery.Receiver == receiver && delivery.Message == message).Verb;
    }

    public string? DeliveredWrappedMessage(EntityUid receiver, string message)
    {
        return Deliveries.FirstOrDefault(delivery => delivery.Receiver == receiver && delivery.Message == message).WrappedMessage;
    }

    public void Watch(EntityUid uid)
    {
        EnsureComp<YautjaTestRadioReceiveRecorderComponent>(uid);
    }

    private void OnRadioReceive(Entity<YautjaTestRadioReceiveRecorderComponent> ent, ref RadioReceiveEvent ev)
    {
        var receiver = ent.Owner;
        if (HasComp<HeadsetComponent>(receiver))
        {
            var parent = Transform(receiver).ParentUid;
            if (parent.IsValid())
                receiver = parent;
        }

        Deliveries.Add((receiver, ev.Message, ev.Channel.ID, ev.ChatMsg.Message.Display?.Verb, ev.ChatMsg.Message.WrappedMessage));
    }
}

[RegisterComponent]
public sealed partial class YautjaTestRadioReceiveRecorderComponent : Component;
