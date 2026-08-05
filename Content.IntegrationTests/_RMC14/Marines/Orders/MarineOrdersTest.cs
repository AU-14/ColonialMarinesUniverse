#nullable enable

using System.Collections.Generic;
using System.Linq;
using Content.Shared._RMC14.Marines.Orders;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.Player;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Marines.Orders;

[TestFixture, TestOf(typeof(SharedMarineOrdersSystem))]
public sealed class MarineOrdersTest
{
    private static readonly ProtoId<DamageTypePrototype> SlashDamage = "Slash";

    [Test]
    public async Task OrdersApplyTheirGameplayEffects()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var actions = server.System<SharedActionsSystem>();
        var hands = server.System<SharedHandsSystem>();
        var inventory = server.System<InventorySystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var issuer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var receiver = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var armor = entMan.SpawnEntity("CMArmorM3Medium", map.GridCoords);
            var gun = entMan.SpawnEntity("RMCWeaponRifleM54C", map.GridCoords);
            var projectile = entMan.SpawnEntity(null, map.GridCoords);
            var orders = entMan.EnsureComponent<MarineOrdersComponent>(issuer);
            var projectileAccuracy = entMan.EnsureComponent<RMCProjectileAccuracyComponent>(projectile);

            Assert.That(inventory.TryEquip(receiver, armor, "outerClothing", force: true), Is.True);
            Assert.That(hands.TryPickupAnyHand(receiver, gun), Is.True);

            var movement = entMan.GetComponent<MovementSpeedModifierComponent>(receiver);
            var walkSpeedBeforeMoveOrder = movement.WalkSpeedModifier;
            var sprintSpeedBeforeMoveOrder = movement.SprintSpeedModifier;

            var move = actions.GetAction(orders.MoveActionEntity);
            var hold = actions.GetAction(orders.HoldActionEntity);
            var focus = actions.GetAction(orders.FocusActionEntity);

            Assert.Multiple(() =>
            {
                Assert.That(move, Is.Not.Null);
                Assert.That(hold, Is.Not.Null);
                Assert.That(focus, Is.Not.Null);
            });

            actions.PerformAction(issuer, move!.Value);
            actions.PerformAction(issuer, hold!.Value);
            actions.PerformAction(issuer, focus!.Value);

            var damage = new DamageSpecifier(server.ProtoMan.Index(SlashDamage), 10);
            var damageModify = new DamageModifyEvent(damage);
            entMan.EventBus.RaiseLocalEvent(receiver, damageModify);

            var weaponAccuracy = entMan.GetComponent<RMCWeaponAccuracyComponent>(gun);
            var accuracyBeforeFocusOrder = projectileAccuracy.Accuracy;
            var ammoShot = new AmmoShotEvent { FiredProjectiles = new List<EntityUid> { projectile } };
            entMan.EventBus.RaiseLocalEvent(gun, ammoShot);
            var accuracyAfterFocusOrder = projectileAccuracy.Accuracy;

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<MoveOrderComponent>(receiver).Received, Has.Count.EqualTo(1));
                Assert.That(entMan.GetComponent<HoldOrderComponent>(receiver).Received, Has.Count.EqualTo(1));
                Assert.That(entMan.GetComponent<FocusOrderComponent>(receiver).Received, Has.Count.EqualTo(1));
                Assert.That(movement.WalkSpeedModifier, Is.EqualTo(walkSpeedBeforeMoveOrder * 1.1f).Within(0.001f));
                Assert.That(movement.SprintSpeedModifier, Is.EqualTo(sprintSpeedBeforeMoveOrder * 1.1f).Within(0.001f));
                Assert.That(damageModify.Damage.DamageDict["Slash"].Float(), Is.EqualTo(9.5f).Within(0.001f));
                Assert.That(
                    accuracyAfterFocusOrder.Float(),
                    Is.EqualTo((accuracyBeforeFocusOrder * weaponAccuracy.ModifiedAccuracyMultiplier + 1.5).Float()).Within(0.001f));
            });

            entMan.DeleteEntity(projectile);
            entMan.DeleteEntity(gun);
            entMan.DeleteEntity(armor);
            entMan.DeleteEntity(receiver);
            entMan.DeleteEntity(issuer);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClientMoveOrderAppliesAndSynchronizes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var sEntMan = server.EntMan;
        var cEntMan = client.EntMan;
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var serverSession = playerManager.Sessions.Single();
        var map = await pair.CreateTestMap();
        EntityUid sReceiver = default;
        NetEntity receiverNet = default;
        float speedBeforeOrder = default;

        await server.WaitPost(() =>
        {
            var issuer = sEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            sReceiver = sEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var armor = sEntMan.SpawnEntity("CMArmorM3Medium", map.GridCoords);
            sEntMan.EnsureComponent<MarineOrdersComponent>(issuer);

            Assert.That(server.System<InventorySystem>().TryEquip(sReceiver, armor, "outerClothing", force: true), Is.True);
            Assert.That(playerManager.SetAttachedEntity(serverSession, issuer), Is.True);

            receiverNet = sEntMan.GetNetEntity(sReceiver);
            speedBeforeOrder = sEntMan.GetComponent<MovementSpeedModifierComponent>(sReceiver).SprintSpeedModifier;
        });
        await pair.RunTicksSync(5);

        var cIssuer = client.Session?.AttachedEntity ??
            throw new AssertionException("The client must have an attached order issuer.");
        var cReceiver = cEntMan.GetEntity(receiverNet);

        await client.WaitPost(() =>
        {
            var actionSystem = client.System<SharedActionsSystem>();
            var instantQuery = cEntMan.GetEntityQuery<InstantActionComponent>();
            var moveAction = actionSystem.GetActions(cIssuer).Single(action =>
                instantQuery.CompOrNull(action)?.Event is MoveActionEvent);

            client.System<Content.Client.Actions.ActionsSystem>().TriggerAction((moveAction.Owner, moveAction.Comp!));
        });
        await pair.RunTicksSync(5);

        await Task.WhenAll(
            server.WaitAssertion(() =>
            {
                Assert.That(sEntMan.GetComponent<MoveOrderComponent>(sReceiver).Received, Has.Count.EqualTo(1));
                Assert.That(
                    sEntMan.GetComponent<MovementSpeedModifierComponent>(sReceiver).SprintSpeedModifier,
                    Is.EqualTo(speedBeforeOrder * 1.1f).Within(0.001f));
                Assert.That(
                    HasAudioFile(sEntMan, "/Audio/_CMU14/Voice/UpdatedVoice/"),
                    Is.True,
                    "Issuing a marine order should play one of CMU's configured order voice lines.");
            }),
            client.WaitAssertion(() =>
            {
                Assert.That(cEntMan.GetComponent<MoveOrderComponent>(cReceiver).Received, Has.Count.EqualTo(1));
            }));

        await pair.CleanReturnAsync();
    }

    private static bool HasAudioFile(IEntityManager entMan, string prefix)
    {
        var query = entMan.EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out _, out var audio))
        {
            var fileName = audio.FileName;
            if (fileName.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
