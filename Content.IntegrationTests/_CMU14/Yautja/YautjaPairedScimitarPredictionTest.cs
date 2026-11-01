#pragma warning disable RA0002
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Mind;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaPairedScimitarPredictionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    // cmss13 scimitar: attack_speed 1s, speed_bonus_amount -0.4s with another bracer attachment in hand.
    // the client predicts the swing, so it has to agree with the server or the blades feel 1s slow
    [Test]
    public async Task PairedScimitarsFromTheBracerSwingFasterOnServerAndClient()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netHunter = default;
        NetEntity netScimitar = default;
        NetEntity netAlt = default;

        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            var hands = SEntMan.System<SharedHandsSystem>();
            var melee = SEntMan.System<SharedMeleeWeaponSystem>();

            var hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            SEntMan.EnsureComponent<YautjaComponent>(hunter);
            var bracer = SEntMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var holder = SEntMan.SpawnEntity("CMUYautjaScimitarAttachment", map.GridCoords);
            var altHolder = SEntMan.SpawnEntity("CMUYautjaScimitarAltAttachment", map.GridCoords);
            var action = SEntMan.SpawnEntity("CMUActionYautjaToggleScimitar", map.GridCoords);

            if (inventory.TryGetSlotEntity(hunter, "gloves", out var oldGloves))
                SEntMan.DeleteEntity(oldGloves.Value);
            Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
            Assert.That(hands.TryPickupAnyHand(hunter, holder), Is.True);
            Assert.That(hands.TryPickupAnyHand(hunter, altHolder), Is.True);

            var coords = SEntMan.GetComponent<TransformComponent>(bracer).Coordinates;
            SEntMan.EventBus.RaiseLocalEvent(bracer, new InteractUsingEvent(hunter, holder, bracer, coords));
            PickDialogOption(bracer, hunter, "Left");
            SEntMan.EventBus.RaiseLocalEvent(bracer, new InteractUsingEvent(hunter, altHolder, bracer, coords));

            var scimitar = SEntMan.GetComponent<YautjaStoredGearComponent>(holder).AttachedWeapon!.Value;
            var alt = SEntMan.GetComponent<YautjaStoredGearComponent>(altHolder).AttachedWeapon!.Value;

            SEntMan.EventBus.RaiseLocalEvent(bracer, new YautjaToggleScimitarActionEvent
            {
                Performer = hunter,
                Action = (action, SEntMan.GetComponent<ActionComponent>(action)),
            });

            Assert.That(hands.IsHolding(hunter, scimitar), Is.True);
            Assert.That(hands.IsHolding(hunter, alt), Is.True);
            Assert.That(melee.GetAttackRate(scimitar, hunter), Is.EqualTo(1f / 0.6f).Within(0.0001f));
            Assert.That(melee.GetAttackRate(alt, hunter), Is.EqualTo(1f / 0.6f).Within(0.0001f));

            Server.System<MindSystem>().ControlMob(ServerSession!.UserId, hunter);
            netHunter = SEntMan.GetNetEntity(hunter);
            netScimitar = SEntMan.GetNetEntity(scimitar);
            netAlt = SEntMan.GetNetEntity(alt);
        });

        await Pair.RunUntilSynced();
        await Pair.RunTicksSync(10);

        await Client.WaitAssertion(() =>
        {
            var melee = CEntMan.System<SharedMeleeWeaponSystem>();
            var hands = CEntMan.System<SharedHandsSystem>();
            var hunter = CEntMan.GetEntity(netHunter);
            var scimitar = CEntMan.GetEntity(netScimitar);
            var alt = CEntMan.GetEntity(netAlt);

            Assert.Multiple(() =>
            {
                Assert.That(hands.IsHolding(hunter, scimitar), Is.True, "client sees the scimitar held");
                Assert.That(hands.IsHolding(hunter, alt), Is.True, "client sees the alt scimitar held");
                Assert.That(CEntMan.HasComponent<YautjaStoredGearComponent>(scimitar), Is.True,
                    "client needs the gear kind to know the other hand holds a bracer attachment");
                Assert.That(CEntMan.HasComponent<YautjaBracerAttachmentSpeedBonusComponent>(scimitar), Is.True);
                Assert.That(melee.GetAttackRate(scimitar, hunter), Is.EqualTo(1f / 0.6f).Within(0.0001f),
                    "client predicted swing rate must match the server's paired 0.6s");
                Assert.That(melee.GetAttackRate(alt, hunter), Is.EqualTo(1f / 0.6f).Within(0.0001f));
            });
        });
    }

    // the real swing loop: a light attack with one paired blade sets the next swing 0.6s out
    [Test]
    public async Task PairedScimitarSwingsEverySixTenths()
    {
        var map = await Pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid scimitar = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            var hands = SEntMan.System<SharedHandsSystem>();

            hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            SEntMan.EnsureComponent<YautjaComponent>(hunter);
            target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = SEntMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var holder = SEntMan.SpawnEntity("CMUYautjaScimitarAttachment", map.GridCoords);
            var altHolder = SEntMan.SpawnEntity("CMUYautjaScimitarAltAttachment", map.GridCoords);
            var action = SEntMan.SpawnEntity("CMUActionYautjaToggleScimitar", map.GridCoords);

            if (inventory.TryGetSlotEntity(hunter, "gloves", out var oldGloves))
                SEntMan.DeleteEntity(oldGloves.Value);
            Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
            Assert.That(hands.TryPickupAnyHand(hunter, holder), Is.True);
            Assert.That(hands.TryPickupAnyHand(hunter, altHolder), Is.True);
            var coords = SEntMan.GetComponent<TransformComponent>(bracer).Coordinates;
            SEntMan.EventBus.RaiseLocalEvent(bracer, new InteractUsingEvent(hunter, holder, bracer, coords));
            PickDialogOption(bracer, hunter, "Left");
            SEntMan.EventBus.RaiseLocalEvent(bracer, new InteractUsingEvent(hunter, altHolder, bracer, coords));
            scimitar = SEntMan.GetComponent<YautjaStoredGearComponent>(holder).AttachedWeapon!.Value;
            SEntMan.EventBus.RaiseLocalEvent(bracer, new YautjaToggleScimitarActionEvent
            {
                Performer = hunter,
                Action = (action, SEntMan.GetComponent<ActionComponent>(action)),
            });
            Assert.That(hands.IsHolding(hunter, scimitar), Is.True);
        });

        // the toggle's hand-select delay has to clear first
        await Pair.RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            var melee = SEntMan.System<SharedMeleeWeaponSystem>();
            var weapon = SEntMan.GetComponent<MeleeWeaponComponent>(scimitar);
            var now = Server.Timing.CurTime;
            Assert.That(melee.AttemptLightAttack(hunter, scimitar, weapon, target, requireCombatMode: false), Is.True);
            Assert.That((weapon.NextAttack - now).TotalSeconds, Is.EqualTo(0.6).Within(0.05),
                "paired scimitar swing sets the next swing 0.6s out");
        });
    }

    private void PickDialogOption(EntityUid console, EntityUid hunter, string text)
    {
        var dialog = SEntMan.GetComponent<DialogComponent>(console);
        var index = dialog.Options.Select((o, i) => (o, i)).Single(p => p.o.Text == text).i;
        SEntMan.EventBus.RaiseLocalEvent(console, new DialogOptionBuiMsg(index)
        {
            Actor = hunter,
            UiKey = DialogUiKey.Key,
        });
    }
}
