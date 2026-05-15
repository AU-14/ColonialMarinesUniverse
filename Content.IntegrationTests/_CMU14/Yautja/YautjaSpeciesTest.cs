using System.Collections.Generic;
using System.Linq;
using Content.Server._RMC14.Ghost;
using Content.Server.Humanoid.Systems;
using Content.Server.Chat;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Armor.ThermalCloak;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Dataset;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Movement.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Storage;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Weeds;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaSpeciesTest
{
    [Test]
    public async Task EventSpawnerCreatesYautjaBodyStatsAndSounds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var localization = server.ResolveDependency<ILocalizationManager>();
        EntityUid hunter = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var actions = entMan.System<SharedActionsSystem>();
                var inventory = entMan.System<InventorySystem>();
                var randomHumanoid = entMan.System<RandomHumanoidSystem>();

                hunter = randomHumanoid.SpawnRandomHumanoid("CMUYautjaHunter", EntityCoordinates.Invalid, "Yautja Hunter");

                var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(hunter);
                Assert.That(humanoid.Species.Id, Is.EqualTo("Yautja"));
                AssertNoGhostRole(entMan, hunter);
                AssertFixedYautjaHair(humanoid);
                Assert.That(entMan.HasComponent<YautjaComponent>(hunter), Is.True);
                Assert.That(entMan.HasComponent<IgnoreXenoWeedsSlowdownComponent>(hunter), Is.True);
                AssertAllSkills(entMan, hunter);
                AssertNoFloatingHudIcons(entMan, hunter);
                AssertYautjaPublicName(entMan, prototypes, localization, hunter);
                AssertUnknownSpeechName(entMan, hunter);

                var movement = entMan.GetComponent<MovementSpeedModifierComponent>(hunter);
                Assert.That(movement.BaseWalkSpeed, Is.EqualTo(4.4f).Within(0.001f));
                Assert.That(movement.BaseSprintSpeed, Is.EqualTo(8.4f).Within(0.001f));

                var damageable = entMan.GetComponent<DamageableComponent>(hunter);
                Assert.That(damageable.DamageModifierSetId?.Id, Is.EqualTo("CMUYautja"));

                var bloodstream = entMan.GetComponent<BloodstreamComponent>(hunter);
                Assert.That(bloodstream.BloodReagent.Id, Is.EqualTo("CMUYautjaBlood"));

                var mask = AssertEquipped(entMan, inventory, hunter, "mask", "CMUYautjaMask");
                var armor = AssertEquipped(entMan, inventory, hunter, "outerClothing", "CMUYautjaClanArmor");
                var mesh = AssertEquipped(entMan, inventory, hunter, "jumpsuit", "CMUYautjaBodyMesh");
                var greaves = AssertEquipped(entMan, inventory, hunter, "shoes", "CMUYautjaClanGreaves");
                var cloakPack = AssertEquipped(entMan, inventory, hunter, "back", "CMUYautjaCloakPack");

                Assert.That(entMan.HasComponent<ParasiteResistanceComponent>(mask), Is.True);
                AssertYautjaNightVision(entMan, hunter);

                AssertArmor(entMan.GetComponent<CMArmorComponent>(armor), melee: 35, bullet: 40, bio: 45, explosion: 40);
                AssertArmor(entMan.GetComponent<CMArmorComponent>(mesh), melee: 10, bullet: 10, bio: 10, explosion: 10);
                AssertArmor(entMan.GetComponent<CMArmorComponent>(greaves), melee: 10, bullet: 10, bio: 10, explosion: 10);

                var cloak = entMan.GetComponent<ThermalCloakComponent>(cloakPack);
                Assert.That(cloak.Opacity, Is.EqualTo(0.01f).Within(0.001f));
                Assert.That(cloak.CloakedHideLayers, Does.Contain(HumanoidVisualLayers.Hair));
                Assert.That(cloak.CloakedHideLayers, Does.Contain(HumanoidVisualLayers.Eyes));
                Assert.That(entMan.GetComponent<ClothingComponent>(cloakPack).RsiPath, Is.EqualTo("_CMU14/Yautja/cape_full.rsi"));
                Assert.That(entMan.GetComponent<ItemComponent>(cloakPack).RsiPath, Is.EqualTo("_CMU14/Yautja/cape_full.rsi"));

                var melee = entMan.GetComponent<MeleeWeaponComponent>(hunter);
                Assert.That(melee.AttackRate, Is.EqualTo(1.15f).Within(0.001f));
                Assert.That(melee.Damage.DamageDict["Blunt"], Is.EqualTo(FixedPoint2.New(12)));
                Assert.That(melee.Damage.DamageDict["Structural"], Is.EqualTo(FixedPoint2.New(3)));

                var speech = entMan.GetComponent<SpeechComponent>(hunter);
                Assert.That(speech.SpeechSounds?.Id, Is.EqualTo("CMUYautjaSpeech"));
                var predatorEmotes = new[]
                {
                    "CMUYautjaClick",
                    "CMUYautjaRoar",
                    "CMUYautjaLaugh",
                    "CMUYautjaGrowl",
                    "CMUYautjaPain",
                    "CMUYautjaDeathCry",
                    "CMUYautjaDeathLaugh",
                };
                var allowedEmotes = speech.AllowedEmotes.Select(id => id.Id).ToHashSet();
                Assert.That(allowedEmotes, Is.SupersetOf(predatorEmotes));
                foreach (var emoteId in predatorEmotes)
                {
                    var emote = prototypes.Index<EmotePrototype>(emoteId);
                    Assert.That(emote.Category, Is.EqualTo(EmoteCategory.Vocal));
                    Assert.That(emote.ChatTriggers, Is.Not.Empty);
                }

                var vocal = entMan.GetComponent<VocalComponent>(hunter);
                Assert.That(vocal.Sounds, Is.Not.Null);
                Assert.That(vocal.Sounds![Sex.Male].Id, Is.EqualTo("CMUMaleYautja"));
                Assert.That(vocal.Sounds[Sex.Female].Id, Is.EqualTo("CMUFemaleYautja"));

                var emoteOnDamage = entMan.GetComponent<EmoteOnDamageComponent>(hunter);
                Assert.That(emoteOnDamage.Emotes, Does.Contain("CMUYautjaPain"));

                var actionPrototypeIds = actions.GetActions(hunter)
                    .Select(action => entMan.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID)
                    .Where(id => id != null)
                    .ToHashSet();
                Assert.That(actionPrototypeIds, Does.Not.Contain("CMUActionYautjaVoiceClick"));
                Assert.That(actionPrototypeIds, Does.Not.Contain("CMUActionYautjaVoiceRoar"));
                Assert.That(actionPrototypeIds, Does.Not.Contain("CMUActionYautjaVoiceLaugh"));
                Assert.That(actionPrototypeIds, Does.Not.Contain("CMUActionYautjaVoiceGrowl"));
                Assert.That(actionPrototypeIds, Does.Not.Contain("CMUActionYautjaVoicePain"));
                Assert.That(actionPrototypeIds, Does.Not.Contain("CMUActionYautjaVoiceDeathCry"));
                Assert.That(actionPrototypeIds, Does.Not.Contain("CMUActionYautjaVoiceDeathLaugh"));

                AssertNoLooseStartingGear(entMan, entMan.System<SharedContainerSystem>());
            });

            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var identity = entMan.GetComponent<IdentityComponent>(hunter);
                Assert.That(identity.IdentityEntitySlot.ContainedEntity, Is.Not.Null);
                Assert.That(entMan.GetComponent<MetaDataComponent>(identity.IdentityEntitySlot.ContainedEntity!.Value).EntityName, Is.EqualTo("unknown"));
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                if (hunter.IsValid() && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaHuntingArmorUsesBalancedMeleeAndBulletArmor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var armorIds = new[]
        {
            "CMUYautjaClanArmor",
            "CMUYautjaAncientAlienArmor",
            "CMUYautjaStoneArmor",
            "CMUYautjaHeavyClanArmor",
            "CMUYautjaBadBloodArmorPatchwork",
            "CMUYautjaEmissaryArmorClassic",
        };

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            foreach (var armorId in armorIds)
            {
                var armor = entMan.SpawnEntity(armorId, MapCoordinates.Nullspace);

                try
                {
                    var component = entMan.GetComponent<CMArmorComponent>(armor);
                    Assert.That(component.Melee, Is.EqualTo(35), armorId);
                    Assert.That(component.Bullet, Is.EqualTo(40), armorId);
                }
                finally
                {
                    if (!entMan.Deleted(armor))
                        entMan.DeleteEntity(armor);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DirectYautjaSpawnGetsEventLoadoutAndEffects()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var localization = server.ResolveDependency<ILocalizationManager>();
        EntityUid hunter = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                hunter = server.EntMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();

                var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(hunter);
                Assert.That(humanoid.Species.Id, Is.EqualTo("Yautja"));
                AssertNoGhostRole(entMan, hunter);
                AssertFixedYautjaHair(humanoid);
                Assert.That(entMan.HasComponent<YautjaComponent>(hunter), Is.True);
                Assert.That(entMan.HasComponent<IgnoreXenoWeedsSlowdownComponent>(hunter), Is.True);
                AssertAllSkills(entMan, hunter);
                AssertNoFloatingHudIcons(entMan, hunter);
                AssertYautjaPublicName(entMan, prototypes, localization, hunter);
                AssertUnknownSpeechName(entMan, hunter);

                var mask = AssertEquipped(entMan, inventory, hunter, "mask", "CMUYautjaMask");
                AssertEquipped(entMan, inventory, hunter, "gloves", "CMUYautjaBracer");
                AssertEquipped(entMan, inventory, hunter, "back", "CMUYautjaCloakPack");
                AssertEquipped(entMan, inventory, hunter, "outerClothing", "CMUYautjaClanArmor");
                AssertEquipped(entMan, inventory, hunter, "jumpsuit", "CMUYautjaBodyMesh");
                AssertEquipped(entMan, inventory, hunter, "shoes", "CMUYautjaClanGreaves");
                AssertEquipped(entMan, inventory, hunter, "pocket1", "CMUYautjaSmartDisc");
                AssertEquipped(entMan, inventory, hunter, "pocket2", "CMUYautjaMedicomp");

                Assert.That(entMan.HasComponent<ParasiteResistanceComponent>(mask), Is.True);
                AssertYautjaNightVision(entMan, hunter);
                AssertNoLooseStartingGear(entMan, entMan.System<SharedContainerSystem>());
            });

            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var identity = entMan.GetComponent<IdentityComponent>(hunter);
                Assert.That(identity.IdentityEntitySlot.ContainedEntity, Is.Not.Null);
                Assert.That(entMan.GetComponent<MetaDataComponent>(identity.IdentityEntitySlot.ContainedEntity!.Value).EntityName, Is.EqualTo("unknown"));
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                if (hunter.IsValid() && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
            });
        }

        await pair.CleanReturnAsync();
    }

    private static EntityUid AssertEquipped(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid wearer,
        string slot,
        string prototype)
    {
        Assert.That(inventory.TryGetSlotEntity(wearer, slot, out var equipped), Is.True, slot);
        Assert.That(equipped, Is.Not.Null, slot);

        var meta = entMan.GetComponent<MetaDataComponent>(equipped.Value);
        Assert.That(meta.EntityPrototype?.ID, Is.EqualTo(prototype), slot);

        return equipped.Value;
    }

    private static void AssertAllSkills(IEntityManager entMan, EntityUid hunter)
    {
        var skillsSystem = entMan.System<SkillsSystem>();

        foreach (var skill in skillsSystem.Skills)
        {
            Assert.That(skillsSystem.GetSkill(hunter, skill), Is.EqualTo(4), skill.ToString());
        }
    }

    private static void AssertFixedYautjaHair(HumanoidAppearanceComponent humanoid)
    {
        Assert.That(humanoid.MarkingSet.TryGetCategory(MarkingCategories.Hair, out var hair), Is.True);
        Assert.That(hair, Has.Count.EqualTo(1));
        Assert.That(hair![0].MarkingId, Is.EqualTo("CMUYautjaDreadlocksStandard"));
    }

    private static void AssertYautjaPublicName(
        IEntityManager entMan,
        IPrototypeManager prototypes,
        ILocalizationManager localization,
        EntityUid hunter)
    {
        var name = entMan.GetComponent<MetaDataComponent>(hunter).EntityName;
        Assert.That(name, Is.Not.EqualTo("Yautja Hunter"));

        var firstNames = prototypes.Index<LocalizedDatasetPrototype>("CMUNamesYautjaFirst")
            .Values
            .Select(localization.GetString);
        var lastNames = prototypes.Index<LocalizedDatasetPrototype>("CMUNamesYautjaLast")
            .Values
            .Select(localization.GetString);

        var validNames = firstNames
            .SelectMany(first => lastNames, (first, last) => $"{first} {last}")
            .ToHashSet();

        Assert.That(validNames, Does.Contain(name));
    }

    private static void AssertNoLooseStartingGear(IEntityManager entMan, SharedContainerSystem containers)
    {
        var expectedContained = new HashSet<string>
        {
            "CMUYautjaCommunicator",
            "CMUYautjaMask",
            "CMUYautjaBracer",
            "CMUYautjaCloakPack",
            "CMUYautjaClanArmor",
            "CMUYautjaBodyMesh",
            "CMUYautjaClanGreaves",
            "CMUYautjaTrophyBelt",
            "CMUYautjaSmartDisc",
            "CMUYautjaMedicomp",
            "CMUYautjaHuntingTrap",
            "CMUYautjaPolishingRag",
            "CMUYautjaCleanserGelVial",
            "CMUYautjaRelayBeacon",
            "CMUYautjaHuntingPouch",
            "CMUYautjaToolbeltFilled",
            "CMUYautjaHoundObservationPad",
        };

        var loose = new List<string>();
        var query = entMan.EntityQueryEnumerator<MetaDataComponent, ItemComponent>();
        while (query.MoveNext(out var uid, out var meta, out _))
        {
            var id = meta.EntityPrototype?.ID;
            if (id == null ||
                !expectedContained.Contains(id) ||
                containers.IsEntityInContainer(uid))
            {
                continue;
            }

            loose.Add(id);
        }

        Assert.That(loose, Is.Empty, $"Loose Yautja starting gear spawned on the floor: {string.Join(", ", loose)}");
    }

    private static void AssertUnknownSpeechName(IEntityManager entMan, EntityUid hunter)
    {
        var ev = new TransformSpeakerNameEvent(hunter, "Frank Morgan");
        entMan.EventBus.RaiseLocalEvent(hunter, ev);

        Assert.That(ev.VoiceName, Is.EqualTo("unknown"));
    }

    private static void AssertYautjaNightVision(IEntityManager entMan, EntityUid hunter)
    {
        var nightVision = entMan.GetComponent<NightVisionComponent>(hunter);
        Assert.That(nightVision.State, Is.EqualTo(NightVisionState.Full));
        Assert.That(nightVision.Green, Is.False);
    }

    private static void AssertArmor(CMArmorComponent armor, int melee, int bullet, int bio, int explosion)
    {
        Assert.That(armor.Melee, Is.EqualTo(melee));
        Assert.That(armor.Bullet, Is.EqualTo(bullet));
        Assert.That(armor.Bio, Is.EqualTo(bio));
        Assert.That(armor.ExplosionArmor, Is.EqualTo(explosion));
    }

    private static void AssertNoFloatingHudIcons(IEntityManager entMan, EntityUid hunter)
    {
        Assert.That(entMan.HasComponent<StatusIconComponent>(hunter), Is.False);

        if (entMan.TryGetComponent(hunter, out MarineComponent? marine))
            Assert.That(marine.Icon, Is.Null);
    }

    private static void AssertNoGhostRole(IEntityManager entMan, EntityUid hunter)
    {
        Assert.That(entMan.HasComponent<GhostRoleComponent>(hunter), Is.False);
        Assert.That(entMan.HasComponent<GhostTakeoverAvailableComponent>(hunter), Is.False);
        Assert.That(entMan.HasComponent<GhostRoleApplySpecialComponent>(hunter), Is.False);
    }
}
