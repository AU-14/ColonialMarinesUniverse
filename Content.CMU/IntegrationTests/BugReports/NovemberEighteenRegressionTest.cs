using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Paralyzing;
using Content.Shared._RMC14.Xenonids.Reaper;
using Content.Shared._RMC14.Xenonids.Sentinel;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Traits.DrugAllergy;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Smelting;
using Content.Shared.CMU14.Xenomorphs.Pathogen.MycotoxinInject;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Walker;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Content.Shared.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class NovemberEighteenRegressionTest : GameTest
{
    // BUG-1208: a chain gauntlet fitted only to the right wrist said "the bracer cannot find that stored tool"
    [Test]
    public async Task RightWristOnlyChainGauntletDeploys()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            var hands = SEntMan.System<SharedHandsSystem>();

            var hunter = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = SEntMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var gauntlet = SEntMan.SpawnEntity("CMUYautjaChainGauntletsAttachment", map.GridCoords);
            var action = SEntMan.SpawnEntity("CMUActionYautjaToggleChainGauntlet", MapCoordinates.Nullspace);

            SEntMan.EnsureComponent<YautjaComponent>(hunter);
            Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
            Assert.That(hands.TryPickupAnyHand(hunter, gauntlet), Is.True);

            var install = new InteractUsingEvent(hunter, gauntlet, bracer, SEntMan.GetComponent<TransformComponent>(bracer).Coordinates);
            SEntMan.EventBus.RaiseLocalEvent(bracer, install);
            PickDialogOption(bracer, hunter, "Right");

            var container = SEntMan.GetComponent<YautjaGearContainerComponent>(bracer);
            Assert.That(container.SecondaryGear.GetValueOrDefault(YautjaGearKind.ChainGauntlet), Is.EqualTo(gauntlet));
            Assert.That(container.Gear.ContainsKey(YautjaGearKind.ChainGauntlet), Is.False, "nothing sits on the left wrist");

            SEntMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 300;
            SEntMan.EventBus.RaiseLocalEvent(bracer, new YautjaToggleChainGauntletActionEvent
            {
                Performer = hunter,
                Action = (action, SEntMan.GetComponent<ActionComponent>(action)),
            });

            var stored = SEntMan.GetComponent<YautjaStoredGearComponent>(gauntlet);
            Assert.That(stored.Deployed, Is.True);
            Assert.That(stored.AttachedWeapon, Is.Not.Null);
            Assert.That(hands.IsHolding(hunter, stored.AttachedWeapon!.Value), Is.True);

            SEntMan.DeleteEntity(hunter);
            SEntMan.DeleteEntity(action);
        });
    }

    // BUG-1208: Yautja are NO_NEURO in cmss13, the sentinel's toxin stacks still landed on them
    [Test]
    public async Task SentinelToxinSkipsYautja()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var sentinels = SEntMan.System<XenoSentinelSystem>();
            var sentinel = SEntMan.SpawnEntity("CMXenoSentinel", map.GridCoords);
            var hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            Assert.That(sentinels.TryAddIntoxicated(sentinel, hunter, 5), Is.False);
            Assert.That(SEntMan.HasComponent<XenoIntoxicatedComponent>(hunter), Is.False);
            Assert.That(sentinels.TryAddIntoxicated(sentinel, marine, 5), Is.True, "marines still get intoxicated");
        });
    }

    // BUG-1208: the paralyzing slash still paralyzed Yautja
    [Test]
    public async Task ParalyzingSlashIsShruggedOffByYautja()
    {
        var map = await Pair.CreateTestMap();
        EntityUid xeno = default;
        EntityUid hunter = default;
        EntityUid marine = default;
        await Server.WaitPost(() =>
        {
            xeno = SEntMan.SpawnEntity("CMXenoSentinel", map.GridCoords);
            hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            SEntMan.EnsureComponent<XenoActiveParalyzingSlashComponent>(xeno);
            SEntMan.EventBus.RaiseLocalEvent(xeno, new MeleeHitEvent([hunter], xeno, xeno, new DamageSpecifier(), null));
        });
        await Server.WaitRunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<VictimBeingParalyzedComponent>(hunter), Is.False);
            Assert.That(SEntMan.HasComponent<XenoActiveParalyzingSlashComponent>(xeno), Is.False, "the slash is spent on the hunter");

            SEntMan.EnsureComponent<XenoActiveParalyzingSlashComponent>(xeno);
            SEntMan.EventBus.RaiseLocalEvent(xeno, new MeleeHitEvent([marine], xeno, xeno, new DamageSpecifier(), null));
            Assert.That(SEntMan.HasComponent<VictimBeingParalyzedComponent>(marine), Is.True, "marines are still paralyzed");
        });
    }

    // BUG-1172: hellhound emotes were text only
    [Test]
    public async Task HellhoundEmotesHaveSounds()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hound = SEntMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords);
            var vocal = SEntMan.GetComponent<VocalComponent>(hound);
            Assert.That(vocal.EmoteSounds, Is.Not.Null);
            Assert.That(vocal.EmoteAction, Is.Null, "hounds don't get a scream action");

            var sounds = SProtoMan.Index(vocal.EmoteSounds!.Value);
            foreach (var emote in SEntMan.GetComponent<SpeechComponent>(hound).AllowedEmotes)
            {
                Assert.That(sounds.Sounds.ContainsKey(emote), $"{emote} has no sound");
            }
        });
    }

    // BUG-1208: the throw finisher's chain was drawn once and never went away
    [Test]
    public async Task ChainGauntletBeamDespawns()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var beam = SEntMan.SpawnEntity("CMUYautjaChainGauntletBeam", map.GridCoords);
            Assert.That(SEntMan.TryGetComponent<TimedDespawnComponent>(beam, out var despawn), Is.True);
            Assert.That(despawn!.Lifetime, Is.LessThanOrEqualTo(2f));
        });
    }

    // BUG-649/650: carrion mantle refreshed speed and armor before its own removal, so the bonus stuck around
    [Test]
    public async Task CarrionMantleWearsOff()
    {
        var map = await Pair.CreateTestMap();
        EntityUid xeno = default;
        EntityUid control = default;
        await Server.WaitPost(() =>
        {
            xeno = SEntMan.SpawnEntity("CMXenoWarrior", map.GridCoords);
            control = SEntMan.SpawnEntity("CMXenoWarrior", map.GridCoords);
            // a mantle whose time is already up, the expiry pass removes it on the next update
            SEntMan.EnsureComponent<XenoCarrionMantleComponent>(xeno);
            SEntMan.System<MovementSpeedModifierSystem>().RefreshMovementSpeedModifiers(xeno);
        });

        await Server.WaitRunTicks(10);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<XenoCarrionMantleComponent>(xeno), Is.False);
            var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(xeno);
            var controlSpeed = SEntMan.GetComponent<MovementSpeedModifierComponent>(control);
            Assert.That(speed.CurrentWalkSpeed, Is.EqualTo(controlSpeed.CurrentWalkSpeed).Within(0.001f),
                "the mantle's slow outlived the mantle");
        });
    }

    // BUG-285: a player's drug allergy followed them into a Working Joe body
    [Test]
    public async Task SyntheticsDropAllergies()
    {
        var map = await Pair.CreateTestMap();
        EntityUid joe = default;
        EntityUid marine = default;
        await Server.WaitPost(() =>
        {
            joe = SEntMan.SpawnEntity("AU14MobWorkingJoeColony", map.GridCoords);
            marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<DrugAllergyComponent>(joe);
            SEntMan.EnsureComponent<CMUFoodAllergyComponent>(joe);
            SEntMan.EnsureComponent<DrugAllergyComponent>(marine);
        });
        await Server.WaitRunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<DrugAllergyComponent>(joe), Is.False);
            Assert.That(SEntMan.HasComponent<CMUFoodAllergyComponent>(joe), Is.False);
            Assert.That(SEntMan.HasComponent<DrugAllergyComponent>(marine), Is.True);
        });
    }

    // BUG-152: the sporecaster popups showed their raw loc IDs
    [Test]
    public async Task SporecasterPopupsAreLocalized()
    {
        await Server.WaitAssertion(() =>
        {
            var loc = Server.ResolveDependency<ILocalizationManager>();
            Assert.That(loc.HasString("cmu-sporecaster-empty"), Is.True);
            Assert.That(loc.HasString("cmu-sporecaster-destruction-release"), Is.True);
        });
    }

    // BUG-122/378: egg placement had every check cut, xenos planted on plain floor, in walls and on other eggs
    [Test]
    public async Task XenoEggsNeedHiveWeeds()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var egg = SEntMan.SpawnEntity("XenoEgg", map.GridCoords);

            Assert.That(TryStartEggPlant("CMXenoDrone", egg, map.GridCoords), Is.False, "no weeds, no egg");

            SEntMan.SpawnEntity("XenoHiveWeeds", map.GridCoords);
            Assert.That(TryStartEggPlant("CMXenoDrone", egg, map.GridCoords), Is.True, "hive weeds take an egg");
        });
    }

    // BUG-378: humans still plant anywhere open, but not into a wall or on top of another egg
    [Test]
    public async Task EggsAreBlockedByWallsAndOtherEggs()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var egg = SEntMan.SpawnEntity("XenoEgg", map.GridCoords);
            Assert.That(TryStartEggPlant("CMMobHuman", egg, map.GridCoords), Is.True, "a human can plant on bare floor");

            var planted = SEntMan.SpawnEntity("XenoEgg", map.GridCoords);
            SEntMan.System<SharedTransformSystem>().AnchorEntity(planted);
            Assert.That(TryStartEggPlant("CMMobHuman", egg, map.GridCoords), Is.False, "one egg per tile");
            SEntMan.DeleteEntity(planted);

            var wall = SEntMan.SpawnEntity("CMWallMetal", map.GridCoords);
            Assert.That(SEntMan.GetComponent<TransformComponent>(wall).Anchored, Is.True);
            Assert.That(TryStartEggPlant("CMMobHuman", egg, map.GridCoords), Is.False, "no eggs inside walls");
        });
    }

    // BUG-290: recipes take 2, so one odd unit stayed behind and blocked every other material
    [Test]
    public async Task SmeltingPotSpitsOutAnOddLeftover()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hands = SEntMan.System<SharedHandsSystem>();
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pot = SEntMan.SpawnEntity("AU14SmeltingPot", map.GridCoords);
            var ore = SEntMan.SpawnEntity("RMCIronOre1", map.GridCoords);
            var steel = SEntMan.SpawnEntity("CMSheetMetal", map.GridCoords);
            var potComp = SEntMan.GetComponent<SmeltingPotComponent>(pot);

            InsertInto(user, ore, pot);
            Assert.That(potComp.Material?.Id, Is.EqualTo("IronOre"));
            Assert.That(potComp.Amount, Is.EqualTo(1));

            Assert.That(hands.TryPickupAnyHand(user, steel), Is.True);
            InsertInto(user, steel, pot);
            Assert.That(potComp.Material?.Id, Is.EqualTo("CMSteel"), "the lone ore came back out and steel went in");

            SEntMan.DeleteEntity(user);
        });
    }

    // BUG-166: the spike boots had the knife slot but none of the holster bits, so quick draw skipped them
    [Test]
    public async Task SpikeBootsHolsterLikeCombatBoots()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            foreach (var id in new[] { "CMBootsBlackFilled", "CMUSpikeBootsFilled" })
            {
                var boots = SEntMan.SpawnEntity(id, map.GridCoords);
                Assert.That(SEntMan.HasComponent<CMHolsterComponent>(boots), Is.True, id);
                Assert.That(SEntMan.HasComponent<CMItemSlotsComponent>(boots), Is.True, id);
                SEntMan.DeleteEntity(boots);
            }
        });
    }

    // BUG-197: a slot the required loadout always fills can't also be in the starting gear, or the second one ends up in the bag
    [Test]
    public async Task LogisticsTechnicianGetsOneJumpsuit()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ProtoMan;
            var job = protos.Index<JobPrototype>("AU14JobGOVFORAuxTech");
            var gear = protos.Index(job.StartingGear!.Value);
            var roleLoadout = protos.Index<RoleLoadoutPrototype>(LoadoutSystem.GetJobPrototype(job.ID));
            var checkedSlots = 0;

            foreach (var groupId in roleLoadout.Groups)
            {
                var group = protos.Index(groupId);
                if (group.MinLimit < 1 || group.Loadouts.Count == 0)
                    continue;

                var alwaysFilled = group.Loadouts
                    .Select(l => protos.Index(l).Equipment.Keys.ToHashSet())
                    .Aggregate((a, b) => { a.IntersectWith(b); return a; });

                foreach (var slot in alwaysFilled)
                {
                    checkedSlots++;
                    Assert.That(gear.Equipment.ContainsKey(slot), Is.False, $"{groupId} always fills {slot}");
                }
            }

            Assert.That(checkedSlots, Is.GreaterThan(0), "the clothing group should always fill the jumpsuit");
        });
    }

    // BUG-289: the RTO deploys with the squad but let prosthetic limbs through
    [Test]
    public async Task RadioTrafficOperatorLocksOutProsthetics()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ProtoMan;
            var job = protos.Index<JobPrototype>("AU14JobGOVFORRadioTrafficOperator");
            var traits = SEntMan.System<SharedRoleSystem>().GetRoleRequirements(job)!.OfType<TraitsRequirement>().ToList();
            Assert.That(traits, Is.Not.Empty);

            var clean = new HumanoidCharacterProfile();
            Assert.That(traits.All(r => r.Check(SEntMan, protos, clean, new Dictionary<string, TimeSpan>(), out _)), Is.True);

            foreach (var prosthetic in new[] { "CMUProstheticLeftArm", "CMUProstheticRightArm", "CMUProstheticLeftLeg", "CMUProstheticRightLeg" })
            {
                var profile = clean.WithTraitPreference(prosthetic, protos);
                Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>(prosthetic)));
                Assert.That(traits.All(r => r.Check(SEntMan, protos, profile, new Dictionary<string, TimeSpan>(), out _)), Is.False, prosthetic);
            }
        });
    }

    // BUG-344: a reanimated marine kept their dogtag IFF, so their old side's bullets went straight through the walker
    [Test]
    public async Task ReanimatedWalkersLoseTheirIFF()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var iff = SEntMan.System<GunIFFSystem>();
            SEntMan.SpawnEntity("CMUPathogenHive", map.GridCoords);
            var injector = SEntMan.SpawnEntity("CMU14XenoPopper", map.GridCoords);
            var victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            iff.AddUserFaction(victim, "FactionMarine");
            Assert.That(iff.IsInFaction(victim, "FactionMarine"), Is.True);

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new CMUMycotoxinInjectDoReanimateEvent(victim, injector));

            Assert.That(SEntMan.HasComponent<CMUPathogenWalkerComponent>(victim), Is.True);
            Assert.That(iff.IsInFaction(victim, "FactionMarine"), Is.False);
        });
    }

    // BUG-128: most colony reinforced windows could be punched apart, only the almayer ones were immune
    [Test]
    public async Task ReinforcedWindowsShrugOffFists()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            foreach (var id in new[] { "CMWindowWhiteColonyReinforced", "RMCWindowHybrisaReinforced", "RMCWindowKutjevoReinforced", "RMCWindowSPPReinforced" })
                Assert.That(Punch(human, id, map.GridCoords), Is.True, id);

            // plain glass still breaks to fists
            foreach (var id in new[] { "CMWindowWhiteColony", "RMCWindowHybrisa" })
                Assert.That(Punch(human, id, map.GridCoords), Is.False, id);

            SEntMan.DeleteEntity(human);
        });
    }

    // BUG-270: the join-xeno window only got its state when it opened, so the queue spot never showed up until you reopened it
    [Test]
    public async Task JoinXenoWindowRefreshesWhileOpen()
    {
        var map = await Pair.CreateTestMap();
        var previous = ServerSession!.AttachedEntity;
        EntityUid ghost = default;
        EntityUid hive = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                ghost = SEntMan.SpawnEntity("MobObserver", map.GridCoords);
                SEntMan.EnsureComponent<JoinXenoCooldownIgnoreComponent>(ghost);
                hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
                // the window hides hives with nobody alive in them
                var drone = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
                SEntMan.System<SharedXenoHiveSystem>().SetHive(drone, hive);
                Server.PlayerMan.SetAttachedEntity(ServerSession, ghost);

                SEntMan.EventBus.RaiseLocalEvent(ghost, new JoinXenoActionEvent { Performer = ghost });
                Assert.That(JoinXenoEntry(ghost, hive).Status, Is.EqualTo(JoinXenoQueueStatus.NotQueued));

                SEntMan.EventBus.RaiseLocalEvent(ghost, new JoinLarvaQueueEvent(SEntMan.GetNetEntity(hive)));
                Assert.That(JoinXenoEntry(ghost, hive).Status, Is.EqualTo(JoinXenoQueueStatus.NotQueued), "nothing resent yet");
            });

            await RunSeconds(1.5f);

            await Server.WaitAssertion(() =>
            {
                Assert.That(JoinXenoEntry(ghost, hive).Status, Is.EqualTo(JoinXenoQueueStatus.Queued));
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, previous));
        }
    }

    private bool Punch(EntityUid attacker, string window, EntityCoordinates where)
    {
        var target = SEntMan.SpawnEntity(window, where);
        var ev = new GettingAttackedAttemptEvent(attacker, attacker, false);
        SEntMan.EventBus.RaiseLocalEvent(target, ref ev);
        SEntMan.DeleteEntity(target);
        return ev.Cancelled;
    }

    private JoinXenoHiveEntry JoinXenoEntry(EntityUid ghost, EntityUid hive)
    {
        var ui = SEntMan.System<SharedUserInterfaceSystem>();
        Assert.That(ui.TryGetUiState<JoinXenoBuiState>(ghost, JoinXenoUIKey.Key, out var state), Is.True);
        return state!.Entries.Single(e => e.Hive == SEntMan.GetNetEntity(hive));
    }

    // fresh planter each time, BlockDuplicate would otherwise eat the second attempt and hide a broken check
    private bool TryStartEggPlant(string planter, EntityUid egg, EntityCoordinates where)
    {
        var user = SEntMan.SpawnEntity(planter, where);
        var doAfters = SEntMan.EnsureComponent<DoAfterComponent>(user);
        SEntMan.EventBus.RaiseLocalEvent(egg, new AfterInteractEvent(user, egg, null, where, true));
        var started = doAfters.DoAfters.Count > 0;
        SEntMan.DeleteEntity(user);
        return started;
    }

    private void InsertInto(EntityUid user, EntityUid used, EntityUid target)
    {
        var ev = new InteractUsingEvent(user, used, target, SEntMan.GetComponent<TransformComponent>(target).Coordinates);
        SEntMan.EventBus.RaiseLocalEvent(target, ev);
    }

    private void PickDialogOption(EntityUid target, EntityUid actor, string optionText)
    {
        var dialog = SEntMan.GetComponent<DialogComponent>(target);
        var index = dialog.Options
            .Select((option, i) => (option, i))
            .Single(pair => pair.option.Text == optionText)
            .i;

        SEntMan.EventBus.RaiseLocalEvent(target, new DialogOptionBuiMsg(index)
        {
            Actor = actor,
            UiKey = DialogUiKey.Key,
        });
    }
}
