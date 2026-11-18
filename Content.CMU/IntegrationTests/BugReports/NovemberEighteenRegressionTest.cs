using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Xenonids.Paralyzing;
using Content.Shared._RMC14.Xenonids.Reaper;
using Content.Shared._RMC14.Xenonids.Sentinel;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Traits.DrugAllergy;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
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
