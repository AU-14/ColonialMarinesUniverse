#pragma warning disable RA0002 // This merge regression intentionally inspects serialized component contracts.

using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Atmos.Components;
using Content.Server.Containers;
using Content.Server.Tiles;
using Content.Client.Botany.Components;
using Content.Shared._CMU14.Chemistry.Effects.Positive;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Drink;
using Content.Shared._RMC14.Power;
using Content.Shared.Access.Components;
using Content.Shared.Administration.Systems;
using Content.Shared.Botany;
using Content.Shared.Botany.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;
using Content.Shared.Doors.Components;
using Content.Shared.EntityConditions.Conditions;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Atmos;
using Content.Shared.EntityEffects.Effects.Body;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Nutrition.Components;
using Content.Shared.RatKing.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.SprayPainter.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Trigger.Systems;
using Content.Shared.Tools.Components;
using Content.Shared.Vehicle.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using LegacyStatusEffectsComponent = Content.Shared.StatusEffect.StatusEffectsComponent;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
[TestOf(typeof(EmitSoundOnTriggerComponent))]
public sealed class PrototypeSuccessorBootstrapRegressionTest : GameTest
{
    private static readonly string[] StandardPaintableDoors =
    {
        "RMCAirlockEvacuation",
        "CMAirlock",
        "CMAirlockCommand",
        "CMAirlockEngineer",
        "CMAirlockMedical",
        "CMAirlockSecurity",
        "CMAirlockMaintRequisitionsLocked",
        "CMAirlockKitchenLocked",
        "CMAirlockMaintKitchenLocked",
        "CMAirlockMaintEngineerLocked",
        "CMAirlockMaintMedicalLocked",
        "CMAirlockPressLocked",
        "CMAirlockMaintPressLocked",
        "CMAirlockMaintCommandLocked",
        "CMAirlockMaintBrigLocked",
        "CMDoubleDoorCommandGlass",
        "CMDoubleDoorCommandSolid",
        "CMDoubleDoorEngineerGlass",
        "CMDoubleDoorEngineerSolid",
        "CMDoubleDoorMedicalGlass",
        "CMDoubleDoorMedicalSolid",
        "CMDoubleDoorSecurityGlass",
        "CMDoubleDoorSecuritySolid",
    };

    private static readonly string[] GlassPaintableDoors =
    {
        "CMAirlockRequisitionsLocked",
        "CMAirlockGlass",
        "CMAirlockGlassEngineer",
        "CMAirlockGlassMedical",
        "CMAirlockGlassSecurity",
        "CMAirlockGlassKitchenLocked",
        "CMAirlockGlassPressLocked",
    };

    private static readonly (string Prototype, string Sound, float? Variation)[] TriggerSounds =
    {
        ("AU14SapperShotgunTrap", "/Audio/Weapons/Guns/Gunshots/shotgun.ogg", null),
        ("AU14SapperSnareTrap", "/Audio/Effects/snap.ogg", null),
        ("AU1420MMGrenadeL101A2", "/Audio/Effects/smoke.ogg", null),
        ("CMU14AirBurstProjectileTearGas", "/Audio/Effects/smoke.ogg", null),
        ("RMCAirBurstProjectileSmoke", "/Audio/Effects/smoke.ogg", null),
        ("RMCSharpExplosiveDirectEffect", "/Audio/_RMC14/Weapons/Guns/Gunshots/gun_sharp_explode.ogg", null),
        ("RMCGrenadeCustomMetalFoam", "/Audio/Effects/Chemistry/bubbles.ogg", 0.2f),
        ("RMCGrenadeCustomWeedkiller", "/Audio/Effects/smoke.ogg", 0.2f),
        ("RMCGrenadeFlashBang", "/Audio/Effects/flash_bang.ogg", null),
        ("CMGrenadeSmoke", "/Audio/Effects/smoke.ogg", null),
        ("RMCGrenadeTraining", "/Audio/Effects/snap.ogg", null),
        ("RMCGrenadeWhitePhosphorus", "/Audio/Effects/smoke.ogg", null),
    };

    [Test]
    [RunOnSide(Side.Server)]
    public void ThermosAndDisposalPrototypesPreserveForkBehavior()
    {
        var factory = SEntMan.ComponentFactory;
        var thermos = SProtoMan.Index<EntityPrototype>("RMCWeYaThermos");
        Assert.Multiple(() =>
        {
            Assert.That(thermos.TryComp<RMCFlaskComponent>(out _, factory), Is.True);
            Assert.That(thermos.TryComp<PressurizedSolutionComponent>(out _, factory), Is.True);
            Assert.That(thermos.TryComp<ShakeableComponent>(out _, factory), Is.True);
            Assert.That(thermos.TryComp<SealableComponent>(out _, factory), Is.True);
            Assert.That(thermos.TryComp<OpenableComponent>(out var openable, factory), Is.True);
            Assert.That(openable!.Closeable, Is.True);
            Assert.That(openable.Sound, Is.TypeOf<SoundCollectionSpecifier>());
            Assert.That(((SoundCollectionSpecifier) openable.Sound!).Collection?.Id,
                Is.EqualTo("flaskOpenSounds"));
            Assert.That(openable.CloseSound, Is.TypeOf<SoundCollectionSpecifier>());
            Assert.That(((SoundCollectionSpecifier) openable.CloseSound!).Collection?.Id,
                Is.EqualTo("flaskCloseSounds"));
            Assert.That(thermos.TryComp<SolutionComponent>(out var solution, factory), Is.True);
            Assert.That(solution!.Solution.MaxVolume, Is.EqualTo(FixedPoint2.New(60)));
        });

        AssertDisposal("CMUFlightDisposalPipe", Direction.South, Direction.North);
        AssertDisposal("CMUFlightDisposalBend", Direction.South, Direction.West);
        AssertDisposalUnitContainer("CMDisposalUnit", 1f);
        AssertDisposalUnitContainer("RMCTrashBinGreen", 0f);
        AssertDisposalUnitContainer("RMCTrashBinBlue", 0f);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void ForkTriggerSoundsUseTheSharedPositionalSuccessor()
    {
        var factory = SEntMan.ComponentFactory;
        Assert.That(TriggerSounds, Has.Length.EqualTo(12));

        foreach (var (prototypeId, expectedSound, expectedVariation) in TriggerSounds)
        {
            var prototype = SProtoMan.Index<EntityPrototype>(prototypeId);
            Assert.That(prototype.TryComp<EmitSoundOnTriggerComponent>(out var sound, factory), Is.True, prototypeId);
            Assert.That(sound!.Sound, Is.TypeOf<SoundPathSpecifier>(), prototypeId);
            var path = (SoundPathSpecifier) sound.Sound!;
            Assert.Multiple(() =>
            {
                Assert.That(sound.Positional, Is.True, prototypeId);
                Assert.That(sound.Predicted, Is.False, prototypeId);
                Assert.That(sound.KeysIn, Is.EquivalentTo(new[] { TriggerSystem.DefaultTriggerKey }), prototypeId);
                Assert.That(path.Path.ToString(), Is.EqualTo(expectedSound), prototypeId);
                Assert.That(path.Params.Volume, Is.EqualTo(AudioParams.Default.Volume), prototypeId);
                Assert.That(path.Params.Pitch, Is.EqualTo(AudioParams.Default.Pitch), prototypeId);
                Assert.That(path.Params.MaxDistance, Is.EqualTo(AudioParams.Default.MaxDistance), prototypeId);
                Assert.That(path.Params.RolloffFactor, Is.EqualTo(AudioParams.Default.RolloffFactor), prototypeId);
                Assert.That(path.Params.ReferenceDistance, Is.EqualTo(AudioParams.Default.ReferenceDistance), prototypeId);
                Assert.That(path.Params.Loop, Is.EqualTo(AudioParams.Default.Loop), prototypeId);
                Assert.That(path.Params.PlayOffsetSeconds, Is.EqualTo(AudioParams.Default.PlayOffsetSeconds), prototypeId);
                Assert.That(path.Params.Variation, Is.EqualTo(expectedVariation), prototypeId);
            });
        }
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void ForkSerializedEffectsUseCurrentBehaviorPreservingTypes()
    {
        AssertSatiate("RMCJuiceBug", 0.125f, 0.4f);
        AssertSatiate("RMCChocolateDrink", 4f, 0.2f);

        foreach (var reagentId in new[] { "CMCryoxadone", "CMClonexadone" })
        {
            var healing = ReagentEffects(reagentId, "Bloodstream")
                .OfType<EqualHealthChange>()
                .Single();
            var temperature = healing.Conditions!.OfType<TemperatureCondition>().Single();
            Assert.Multiple(() =>
            {
                Assert.That(temperature.Min, Is.Zero, reagentId);
                Assert.That(temperature.Max, Is.EqualTo(170f), reagentId);
            });
        }

        foreach (var (reagentId, walk, sprint) in new[]
                 {
                     ("AU14DrugSpeedDemon", 1.3f, 1.34f),
                     ("CMUMethamphetamine", 1.4f, 1.4f),
                 })
        {
            var movement = ReagentEffects(reagentId, "Bloodstream")
                .OfType<MovementSpeedModifier>()
                .Single();
            Assert.Multiple(() =>
            {
                Assert.That(movement.WalkSpeedModifier, Is.EqualTo(walk), reagentId);
                Assert.That(movement.SprintSpeedModifier, Is.EqualTo(sprint), reagentId);
            });
        }

        var imidazoline = ReagentEffects("CMImidazoline", "Bloodstream").ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(imidazoline.OfType<EyeDamage>().Single().Amount, Is.EqualTo(-1));
            Assert.That(imidazoline.OfType<Oculopeutic>(), Has.Exactly(1).Items,
                "the fork organ-healing companion effect must remain alongside upstream eye damage");
        });

        var water = SProtoMan.Index<EntityPrototype>("CMUOverlayWater");
        Assert.That(water.TryComp<TileEntityEffectComponent>(out var tileEffect, SEntMan.ComponentFactory), Is.True);
        Assert.That(tileEffect!.Effects.OfType<Extinguish>().Single().FireStacksAdjustment, Is.EqualTo(-1.5f));
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void ForkToolsAndHydroponicsUseCurrentCollectionAndTrayContracts()
    {
        var factory = SEntMan.ComponentFactory;
        var welder = SProtoMan.Index<EntityPrototype>("CMWelder");
        var jack = SProtoMan.Index<EntityPrototype>("AU14MaintenanceJackSpecial");
        var tray = SProtoMan.Index<EntityPrototype>("CMHydroponicsTray");

        Assert.That(welder.TryComp<ToolComponent>(out var welderTool, factory), Is.True);
        Assert.That(jack.TryComp<MultipleToolComponent>(out var multipleTool, factory), Is.True);
        Assert.That(tray.TryComp<PlantTrayComponent>(out var plantTray, factory), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(welderTool!.Qualities.Select(quality => quality.Id),
                Is.EquivalentTo(new[] { "Welding" }));
            Assert.That(multipleTool!.Entries, Has.Length.EqualTo(2));
            Assert.That(multipleTool.Entries[0].Behavior.Select(quality => quality.Id),
                Is.EquivalentTo(new[] { "Prying" }));
            Assert.That(multipleTool.Entries[1].Behavior.Select(quality => quality.Id),
                Is.EquivalentTo(new[] { "Anchoring" }));
            Assert.That(plantTray!.DrawWarnings, Is.True);
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void RemovedForkComponentsUseTheirBehaviorPreservingSuccessors()
    {
        var factory = SEntMan.ComponentFactory;
        var parasite = SProtoMan.Index<EntityPrototype>("CMXenoParasite");
        Assert.That(parasite.TryComp<RelayedReplacementAccentComponent>(out var accent, factory), Is.True);

        var toilet = SProtoMan.Index<EntityPrototype>("CMToiletEmpty");
        Assert.That(toilet.TryComp<RummageableComponent>(out var rummageable, factory), Is.True);
        Assert.That(rummageable!.Table, Is.TypeOf<NestedSelector>());

        var invasiveCarp = SProtoMan.Index<EntityPrototype>("CMUMobCarpInvasive");
        Assert.That(invasiveCarp.TryComp<PermanentStatusEffectsComponent>(out var permanent, factory), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(accent!.Accent.Id, Is.EqualTo("mumble"));
            Assert.That(parasite.Components.ContainsKey("AddAccentClothing"), Is.False);
            Assert.That(parasite.TryComp<ReplacementAccentComponent>(out _, factory), Is.False,
                "the parasite itself must not receive a direct speech accent");

            Assert.That(((NestedSelector) rummageable.Table).TableId.Id, Is.EqualTo("RatKingLoot"));
            Assert.That(rummageable.RummageDuration, Is.EqualTo(3f));
            Assert.That(rummageable.Looted, Is.False);
            Assert.That(rummageable.Sound, Is.TypeOf<SoundCollectionSpecifier>());
            Assert.That(((SoundCollectionSpecifier) rummageable.Sound!).Collection?.Id,
                Is.EqualTo("storageRustle"));
            Assert.That(toilet.Components.ContainsKey("RatKingRummageable"), Is.False);

            Assert.That(invasiveCarp.Components.ContainsKey("PressureImmunity"), Is.False);
            Assert.That(permanent!.StatusEffects.Select(id => id.Id),
                Is.EquivalentTo(new[] { "StatusEffectPressureImmunity" }));

            Assert.That(StandardPaintableDoors, Has.Length.EqualTo(23));
            Assert.That(GlassPaintableDoors, Has.Length.EqualTo(7));
            Assert.That(StandardPaintableDoors.Concat(GlassPaintableDoors).Distinct().ToArray(), Has.Length.EqualTo(30));
        });

        foreach (var prototypeId in StandardPaintableDoors)
        {
            AssertPaintableDoor(prototypeId, "AirlockStandard");
        }

        foreach (var prototypeId in GlassPaintableDoors)
        {
            AssertPaintableDoor(prototypeId, "AirlockGlass");
        }

        foreach (var prototypeId in new[] { "CMUMobSmallHostCarp", "RMCMobCat", "CMTestDummy" })
        {
            var prototype = SProtoMan.Index<EntityPrototype>(prototypeId);
            Assert.That(prototype.TryComp<LegacyStatusEffectsComponent>(out var legacyStatuses, factory),
                Is.True,
                prototypeId);
            Assert.That(legacyStatuses!.AllowedEffects, Does.Not.Contain("PressureImmunity"), prototypeId);
        }
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void DuplicatePrototypeSuccessorsPreserveVehicleVulpAndMapAuthority()
    {
        var factory = SEntMan.ComponentFactory;
        var wheelchair = SProtoMan.Index<EntityPrototype>("VehicleWheelchair");
        var janicart = SProtoMan.Index<EntityPrototype>("VehicleJanicart");

        Assert.That(wheelchair.TryComp<VehicleComponent>(out var baseVehicleComponent, factory), Is.True);
        Assert.That(janicart.TryComp<VehicleComponent>(out var janicartVehicle, factory), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(baseVehicleComponent!.CanAttack, Is.True,
                "the current vehicle base intentionally allows attacks unless a child opts out");
            Assert.That(janicartVehicle!.CanAttack, Is.False,
                "the janicart's explicit Vehicle component preserves the fork's attack restriction");
            Assert.That(janicart.Components, Contains.Key("VehicleHandBlocker"));
            Assert.That(janicart.Components, Contains.Key("GravityAffected"));
            Assert.That(janicart.Components, Contains.Key("SpriteMovement"));
            Assert.That(SProtoMan.HasIndex<EntityPrototype>("VehicleKeyJanicart"), Is.True);
        });

        var growls = SProtoMan.Index<SoundCollectionPrototype>("VulpkaninGrowls");
        Assert.Multiple(() =>
        {
            Assert.That(growls.PickFiles, Has.Count.EqualTo(6));
            Assert.That(growls.PickFiles.Take(3).Select(path => path.ToString()),
                Is.EqualTo(Enumerable.Range(1, 3)
                    .Select(i => $"/Audio/_RMC14/Voice/Vulpkanin/dog_growl{i}.ogg")));
            Assert.That(growls.PickFiles.Skip(3).Select(path => path.ToString()),
                Is.EqualTo(Enumerable.Range(4, 3)
                    .Select(i => $"/Audio/Voice/Vulpkanin/dog_growl{i}.ogg")));
            Assert.That(SProtoMan.Index<SoundCollectionPrototype>("VulpkaninBarks").PickFiles,
                Has.All.Matches<Robust.Shared.Utility.ResPath>(path =>
                    path.ToString().StartsWith("/Audio/_RMC14/Voice/Vulpkanin/")));
            Assert.That(SProtoMan.Index<SoundCollectionPrototype>("VulpkaninSnarls").PickFiles,
                Has.All.Matches<Robust.Shared.Utility.ResPath>(path =>
                    path.ToString().StartsWith("/Audio/_RMC14/Voice/Vulpkanin/")));
            Assert.That(SProtoMan.Index<SoundCollectionPrototype>("VulpkaninWhines").PickFiles,
                Has.All.Matches<Robust.Shared.Utility.ResPath>(path =>
                    path.ToString().StartsWith("/Audio/_RMC14/Voice/Vulpkanin/")));
            Assert.That(SProtoMan.Index<SoundCollectionPrototype>("VulpkaninHowls").PickFiles,
                Has.Count.EqualTo(1));
        });

        AssertForkVulpEmote("Bark", "rmc-emote-name-bark", "barked", "barking");
        AssertForkVulpEmote("Snarl", "rmc-emote-name-snarl", "snarled", "snarling");
        AssertForkVulpEmote("Whine", "rmc-emote-name-whine", "whined", "whining");
        AssertForkVulpEmote("Growl", "rmc-emote-name-growl", "growled", "growling");
        Assert.That(SProtoMan.HasIndex<EmotePrototype>("Howl"), Is.True);

        var dev = SProtoMan.Index<GameMapPrototype>("Dev");
        var plasma = SProtoMan.Index<GameMapPrototype>("Plasma");
        Assert.Multiple(() =>
        {
            Assert.That(dev.MapName, Is.EqualTo("Dev"));
            Assert.That(dev.MapPath.ToString(), Is.EqualTo("/Maps/Test/dev_map.yml"));
            Assert.That(plasma.MapName, Is.EqualTo("Plasma"));
            Assert.That(plasma.MapPath.ToString(), Is.EqualTo("/Maps/plasma.yml"));
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void ParasiteMumbleOnlyRelaysWhileEquipped()
    {
        const string originalMessage = "merge regression control message";
        var inventory = SEntMan.System<InventorySystem>();
        var replacement = SEntMan.System<ReplacementAccentSystem>();
        var localization = Server.ResolveDependency<ILocalizationManager>();
        var mumbleMessages = Enumerable.Range(1, 3)
            .Select(i => localization.GetString($"accent-words-mumble-{i}"))
            .ToArray();
        var siliconMessages = Enumerable.Range(1, 4)
            .Select(i => localization.GetString($"accent-words-silicon-{i}"))
            .ToArray();
        var wearer = SEntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        var parasite = SEntMan.SpawnEntity("CMXenoParasite", MapCoordinates.Nullspace);

        try
        {
            Assert.That(ApplyAccent(parasite, originalMessage), Is.EqualTo(originalMessage),
                "a free parasite must retain its own unaccented speech");

            Assert.That(inventory.TryEquip(wearer, parasite, "mask", silent: true, force: true), Is.True);
            Assert.That(ApplyAccent(wearer, originalMessage), Is.AnyOf(mumbleMessages));

            replacement.ApplyAccent(wearer, "silicon");
            Assert.That(ApplyAccent(wearer, originalMessage), Is.AnyOf(siliconMessages),
                "a wearer-owned replacement accent must win regardless of relay event ordering");

            Assert.That(inventory.TryUnequip(wearer, "mask", silent: true, force: true), Is.True);
            Assert.That(SEntMan.TryGetComponent<ReplacementAccentComponent>(wearer, out var wearerAccent), Is.True);
            Assert.That(wearerAccent!.Accent.Id, Is.EqualTo("silicon"),
                "unequipping the parasite must not remove a wearer-owned accent");
            Assert.That(ApplyAccent(wearer, originalMessage), Is.AnyOf(siliconMessages));

            SEntMan.RemoveComponent<ReplacementAccentComponent>(wearer);
            Assert.That(ApplyAccent(wearer, originalMessage), Is.EqualTo(originalMessage),
                "the unequipped parasite must stop relaying its accent");

            Assert.That(inventory.TryEquip(wearer, parasite, "mask", silent: true, force: true), Is.True);
            Assert.That(ApplyAccent(wearer, originalMessage), Is.AnyOf(mumbleMessages),
                "re-equipping the same parasite must resume the relay");
        }
        finally
        {
            SEntMan.DeleteEntity(wearer);
            if (SEntMan.EntityExists(parasite))
                SEntMan.DeleteEntity(parasite);
        }
    }

    [Test]
    public async Task InvasiveCarpUsesPermanentPressureImmunityWithoutChangingSmallHosts()
    {
        var map = await Pair.CreateTestMap();
        EntityUid invasive = default;
        EntityUid smallHost = default;

        await Server.WaitPost(() =>
        {
            invasive = SEntMan.SpawnEntity("CMUMobCarpInvasive", map.GridCoords);
            smallHost = SEntMan.SpawnEntity("CMUMobSmallHostCarp", map.GridCoords);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var statuses = SEntMan.System<StatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<PermanentStatusEffectsComponent>(invasive).StatusEffects
                    .Select(id => id.Id), Is.EquivalentTo(new[] { "StatusEffectPressureImmunity" }));
                Assert.That(statuses.HasStatusEffect(invasive, "StatusEffectPressureImmunity"), Is.True);
                Assert.That(SEntMan.HasComponent<BarotraumaComponent>(invasive), Is.False,
                    "the carp's MobAtmosExposed parent deliberately has no barotrauma damage");

                Assert.That(SEntMan.HasComponent<PermanentStatusEffectsComponent>(smallHost), Is.False);
                Assert.That(statuses.HasStatusEffect(smallHost, "StatusEffectPressureImmunity"), Is.False);
                Assert.That(SEntMan.HasComponent<BarotraumaComponent>(smallHost), Is.False);
            });

            SEntMan.System<RejuvenateSystem>().PerformRejuvenate(invasive);
        });
        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<StatusEffectsSystem>()
                .HasStatusEffect(invasive, "StatusEffectPressureImmunity"), Is.True,
                "rejuvenation must preserve the permanent pressure immunity");
            SEntMan.RemoveComponent<PermanentStatusEffectsComponent>(invasive);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<StatusEffectsSystem>()
                .HasStatusEffect(invasive, "StatusEffectPressureImmunity"), Is.False);
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ThermosAndFlightMarkersKeepTheirSpriteStates()
    {
        var factory = CEntMan.ComponentFactory;
        var thermos = CProtoMan.Index<EntityPrototype>("RMCWeYaThermos");
        Assert.That(thermos.TryComp<SpriteComponent>(out var thermosSprite, factory), Is.True);
        Assert.That(thermosSprite!.AllLayers.Single().RsiState.Name, Is.EqualTo("icon"));
        Assert.That(thermos.TryComp<GenericVisualizerComponent>(out var visualizer, factory), Is.True);
        var openStates = visualizer!.Visuals[OpenableVisuals.Opened].Single().Value;
        Assert.Multiple(() =>
        {
            Assert.That(openStates[bool.FalseString].State, Is.EqualTo("icon"));
            Assert.That(openStates[bool.TrueString].State, Is.EqualTo("icon_open"));
            AssertSpriteState("CMUFlightDisposalPipe", "pipe-s");
            AssertSpriteState("CMUFlightDisposalBend", "pipe-c");
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void CmuHydroponicsTrayUsesEveryCurrentWarningVisual()
    {
        var factory = CEntMan.ComponentFactory;
        var tray = CProtoMan.Index<EntityPrototype>("CMHydroponicsTray");

        Assert.That(tray.TryComp<PlantTrayVisualsComponent>(out _, factory), Is.True);
        Assert.That(tray.TryComp<GenericVisualizerComponent>(out var visualizer, factory), Is.True);

        var expected = new Dictionary<PlantTrayVisuals, string>
        {
            [PlantTrayVisuals.HealthLight] = "health_alert",
            [PlantTrayVisuals.WaterLight] = "water_alert",
            [PlantTrayVisuals.NutritionLight] = "nutri_alert",
            [PlantTrayVisuals.AlertLight] = "undefined_alert",
            [PlantTrayVisuals.HarvestLight] = "harvest_alert",
        };

        Assert.That(visualizer!.Visuals.Keys, Is.EquivalentTo(expected.Keys));
        foreach (var (key, layer) in expected)
        {
            Assert.That(visualizer.Visuals[key].Keys, Is.EquivalentTo(new[] { layer }), key.ToString());
            Assert.That(visualizer.Visuals[key][layer].Keys,
                Is.EquivalentTo(new[] { bool.TrueString, bool.FalseString }),
                key.ToString());
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ForkDisposalAndApcVisualsUseCurrentDistinctLayerContracts()
    {
        var sprites = CEntMan.System<SpriteSystem>();
        var entities = new List<EntityUid>();

        try
        {
            var disposal = SpawnClient("CMDisposalUnit");
            AssertMappedLayers(disposal, sprites, Enum.GetValues<DisposalUnitVisualLayers>().Cast<Enum>());
            AssertLayerRsi(disposal, sprites, DisposalUnitVisualLayers.Base,
                "/Textures/_RMC14/Structures/Piping/disposal.rsi");

            foreach (var (prototypeId, state) in new[]
                     {
                         ("RMCTrashBinGreen", "trashgreen"),
                         ("RMCTrashBinBlue", "trashblue"),
                     })
            {
                var trash = SpawnClient(prototypeId);
                AssertMappedLayers(trash, sprites, Enum.GetValues<DisposalUnitVisualLayers>().Cast<Enum>());
                foreach (var layer in Enum.GetValues<DisposalUnitVisualLayers>())
                {
                    Assert.That(GetLayer(trash, sprites, layer).RsiState.Name, Is.EqualTo(state),
                        $"{prototypeId} {layer}");
                }

                var prototype = CProtoMan.Index<EntityPrototype>(prototypeId);
                Assert.That(prototype.TryComp<GenericVisualizerComponent>(out var visualizer,
                    CEntMan.ComponentFactory), Is.True, prototypeId);
                Assert.That(visualizer!.Visuals[DisposalUnitVisuals.IsFlushing],
                    Contains.Key("enum.DisposalUnitVisualLayers.OverlayFlushing"), prototypeId);
                Assert.That(visualizer.Visuals[DisposalUnitVisuals.IsEngaged],
                    Contains.Key("enum.DisposalUnitVisualLayers.OverlayEngaged"), prototypeId);
                Assert.That(visualizer.Visuals[AnchorVisuals.Anchored],
                    Contains.Key("enum.DisposalUnitVisualLayers.Base"), prototypeId);
            }

            var apc = SpawnClient("CMApc");
            AssertMappedLayers(apc, sprites, Enum.GetValues<RMCApcSpriteLayers>().Cast<Enum>());

            var apcPrototype = CProtoMan.Index<EntityPrototype>("CMApc");
            Assert.That(apcPrototype.TryComp<GenericVisualizerComponent>(out var apcVisualizer,
                CEntMan.ComponentFactory), Is.True);
            var stateTargets = apcVisualizer!.Visuals[RMCApcVisualsLayers.Layer];
            foreach (var layer in Enum.GetValues<RMCApcSpriteLayers>())
            {
                Assert.That(stateTargets, Contains.Key($"enum.RMCApcSpriteLayers.{layer}"), layer.ToString());
            }

            Assert.That(apcVisualizer.Visuals[RMCApcVisualsLayers.Lock],
                Contains.Key("enum.RMCApcSpriteLayers.InterfaceLock"));
            Assert.That(apcVisualizer.Visuals[RMCApcVisualsLayers.Power],
                Contains.Key("enum.RMCApcSpriteLayers.ChargeState"));
        }
        finally
        {
            foreach (var entity in entities)
            {
                if (CEntMan.EntityExists(entity))
                    CEntMan.DeleteEntity(entity);
            }
        }

        EntityUid SpawnClient(string prototypeId)
        {
            var entity = CEntMan.SpawnEntity(prototypeId, MapCoordinates.Nullspace);
            entities.Add(entity);
            return entity;
        }
    }

    private void AssertDisposal(string prototypeId, params Direction[] expectedExits)
    {
        var prototype = SProtoMan.Index<EntityPrototype>(prototypeId);
        Assert.That(prototype.TryComp<DisposalTubeComponent>(out var tube, SEntMan.ComponentFactory), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(tube!.Exits, Is.EqualTo(expectedExits), prototypeId);
            Assert.That(prototype.Components.ContainsKey("ContainerContainer"), Is.False, prototypeId);
            Assert.That(prototype.Components.ContainsKey("DisposalTransit"), Is.False, prototypeId);
            Assert.That(prototype.Components.ContainsKey("DisposalBend"), Is.False, prototypeId);
        });
    }

    private void AssertSatiate(string reagentId, float hunger, float thirst)
    {
        var satiation = ReagentEffects(reagentId, "Digestion")
            .OfType<Satiate>()
            .ToDictionary(effect => effect.SatiationType.Id, effect => effect.Factor);
        Assert.Multiple(() =>
        {
            Assert.That(satiation, Has.Count.EqualTo(2), reagentId);
            Assert.That(satiation["Hunger"], Is.EqualTo(hunger), reagentId);
            Assert.That(satiation["Thirst"], Is.EqualTo(thirst), reagentId);
        });
    }

    private IEnumerable<EntityEffect> ReagentEffects(string reagentId, string metabolism)
    {
        var reagent = SProtoMan.Index<ReagentPrototype>(reagentId);
        Assert.That(reagent.Metabolisms.Metabolisms.TryGetValue(metabolism, out var entry), Is.True, reagentId);
        return entry!.Effects;
    }

    private void AssertDisposalUnitContainer(string prototypeId, float expectedThrowProbability)
    {
        var prototype = SProtoMan.Index<EntityPrototype>(prototypeId);
        Assert.That(prototype.TryComp<ThrowInsertContainerComponent>(out var throwInsert,
            SEntMan.ComponentFactory), Is.True, prototypeId);
        Assert.That(prototype.TryComp<ContainerManagerComponent>(out var containers,
            SEntMan.ComponentFactory), Is.True, prototypeId);
        Assert.Multiple(() =>
        {
            Assert.That(throwInsert!.ContainerId, Is.EqualTo(nameof(DisposalUnitComponent)), prototypeId);
            Assert.That(throwInsert.Probability, Is.EqualTo(expectedThrowProbability), prototypeId);
            Assert.That(containers!.Containers, Contains.Key(nameof(DisposalUnitComponent)), prototypeId);
            Assert.That(containers.Containers, Does.Not.ContainKey("disposals"), prototypeId);
        });
    }

    private void AssertMappedLayers(EntityUid entity, SpriteSystem sprites, IEnumerable<Enum> expectedLayers)
    {
        var sprite = CEntMan.GetComponent<SpriteComponent>(entity);
        foreach (var layer in expectedLayers)
        {
            Assert.That(sprites.LayerMapTryGet((entity, sprite), layer, out _, false), Is.True,
                $"{CEntMan.GetComponent<MetaDataComponent>(entity).EntityPrototype?.ID} {layer}");
        }
    }

    private ISpriteLayer GetLayer(EntityUid entity, SpriteSystem sprites, Enum layer)
    {
        var sprite = CEntMan.GetComponent<SpriteComponent>(entity);
        Assert.That(sprites.LayerMapTryGet((entity, sprite), layer, out var index, false), Is.True, layer.ToString());
        return sprite[index];
    }

    private void AssertLayerRsi(EntityUid entity, SpriteSystem sprites, Enum layer, string expectedRsi)
    {
        Assert.That(GetLayer(entity, sprites, layer).ActualRsi?.Path.ToString(), Is.EqualTo(expectedRsi),
            layer.ToString());
    }

    private void AssertSpriteState(string prototypeId, string expectedState)
    {
        var prototype = CProtoMan.Index<EntityPrototype>(prototypeId);
        Assert.That(prototype.TryComp<SpriteComponent>(out var sprite, CEntMan.ComponentFactory), Is.True);
        Assert.That(sprite!.AllLayers.Single().RsiState.Name, Is.EqualTo(expectedState), prototypeId);
        Assert.That(prototype.Components.ContainsKey("GenericVisualizer"), Is.False, prototypeId);
    }

    private void AssertPaintableDoor(string prototypeId, string expectedGroup)
    {
        var prototype = SProtoMan.Index<EntityPrototype>(prototypeId);
        Assert.That(prototype.TryComp<PaintableComponent>(out var paintable, SEntMan.ComponentFactory),
            Is.True,
            prototypeId);
        Assert.Multiple(() =>
        {
            Assert.That(paintable!.Group?.Id, Is.EqualTo(expectedGroup), prototypeId);
            Assert.That(prototype.TryComp<DoorComponent>(out _, SEntMan.ComponentFactory), Is.True, prototypeId);
            Assert.That(prototype.TryComp<AccessReaderComponent>(out _, SEntMan.ComponentFactory), Is.True, prototypeId);
            Assert.That(prototype.Components.ContainsKey("PaintableAirlock"), Is.False, prototypeId);
        });
    }

    private void AssertForkVulpEmote(string prototypeId, string expectedName, params string[] upstreamTriggers)
    {
        var emote = SProtoMan.Index<EmotePrototype>(prototypeId);
        Assert.Multiple(() =>
        {
            Assert.That(emote.Name, Is.EqualTo(expectedName));
            Assert.That(emote.Icon, Is.TypeOf<SpriteSpecifier.Rsi>());
            Assert.That(((SpriteSpecifier.Rsi) emote.Icon).RsiPath.ToString(),
                Does.StartWith("_RMC14/Actions/"));
            Assert.That(emote.Blacklist!.Components, Does.Contain("BorgChassis"));
            Assert.That(emote.Blacklist.Tags!.Select(tag => tag.Id), Does.Contain("SiliconEmotes"));
            Assert.That(emote.ChatTriggers, Is.SupersetOf(upstreamTriggers));
        });

        if (prototypeId == "Growl")
            Assert.That(emote.Whitelist!.Components, Does.Contain("Xeno"));
    }

    private string ApplyAccent(EntityUid entity, string message)
    {
        var accent = new AccentGetEvent(entity, message);
        SEntMan.EventBus.RaiseLocalEvent(entity, ref accent);
        return accent.Message;
    }
}

#pragma warning restore RA0002
