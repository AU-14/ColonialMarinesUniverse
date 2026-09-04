using System.Linq;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Armor.ThermalCloak;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Stealth;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameStates;
using Robust.Client.Timing;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaCloakPredictionTest
{
    [Test]
    public async Task PredictionRollbackDoesNotDecloakYautja()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        NetEntity hunterNet = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            entMan.EnsureComponent<YautjaComponent>(hunter);
            Assert.That(entMan.System<InventorySystem>().TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
            server.PlayerMan.SetAttachedEntity(pair.Player!, hunter);
            hunterNet = entMan.GetNetEntity(hunter);

            entMan.System<DamageableSystem>().SetDamage(hunter, BluntDamage(10));
            ToggleCloak(entMan, hunter, bracer);
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True);
        });
        await pair.RunUntilSynced();

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var hunter = entMan.GetEntity(hunterNet);
            var timing = client.ResolveDependency<IClientGameTiming>();
            var gameStates = client.ResolveDependency<IClientGameStateManager>();
            var damageable = entMan.GetComponent<DamageableComponent>(hunter);
            Assert.Multiple(() =>
            {
                Assert.That(timing.InPrediction, Is.True);
                Assert.That(damageable.TotalDamage, Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True);
                Assert.That(entMan.HasComponent<RMCNightVisionVisibleComponent>(hunter), Is.False);
            });

            // Restoring the server's damage after predicted healing raises a positive DamageChangedEvent
            // while ResetPredictedEntities is enumerating this entity's networked components.
            entMan.System<DamageableSystem>().SetDamage(hunter, BluntDamage(5));
            Assert.That(damageable.LastModifiedTick, Is.GreaterThan(timing.LastRealTick));
            Assert.DoesNotThrow(() => gameStates.ResetPredictedEntities());

            Assert.Multiple(() =>
            {
                Assert.That(damageable.TotalDamage, Is.EqualTo(FixedPoint2.New(10)),
                    "The real prediction reset must restore the authoritative damage, not skip the entity.");
                Assert.That(entMan.HasComponent<RMCNightVisionVisibleComponent>(hunter), Is.False,
                    "Damage state restoration must not add components during rollback.");
                Assert.That(entMan.GetComponent<EntityTurnInvisibleComponent>(hunter).Enabled, Is.True);
                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True);
                Assert.That(entMan.HasComponent<ThermalCloakUserComponent>(hunter), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PredictedCloakToggleAndServerForcedDecloakStillWork(bool death)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        NetEntity hunterNet = default;
        NetEntity bracerNet = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            entMan.EnsureComponent<YautjaComponent>(hunter);
            Assert.That(entMan.System<InventorySystem>().TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
            server.PlayerMan.SetAttachedEntity(pair.Player!, hunter);
            hunterNet = entMan.GetNetEntity(hunter);
            bracerNet = entMan.GetNetEntity(bracer);
        });
        await pair.RunUntilSynced();

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var hunter = entMan.GetEntity(hunterNet);
            Assert.That(client.ResolveDependency<IClientGameTiming>().InPrediction, Is.True);
            ToggleCloak(entMan, hunter, entMan.GetEntity(bracerNet));
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<EntityTurnInvisibleComponent>(hunter).Enabled, Is.True);
                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True);
                Assert.That(entMan.HasComponent<ThermalCloakUserComponent>(hunter), Is.True);
            });
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.GetEntity(hunterNet);
            ToggleCloak(entMan, hunter, entMan.GetEntity(bracerNet));
            Assert.That(entMan.GetComponent<EntityTurnInvisibleComponent>(hunter).Enabled, Is.True);
        });
        await pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.GetEntity(hunterNet);
            if (death)
            {
                entMan.System<MobStateSystem>().ChangeMobState(hunter, MobState.Dead);
            }
            else
            {
                entMan.System<DamageableSystem>().TryChangeDamage(hunter, BluntDamage(1), ignoreResistances: true);
                Assert.That(entMan.GetComponent<DamageableComponent>(hunter).TotalDamage, Is.GreaterThan(FixedPoint2.Zero));
            }

            Assert.That(entMan.GetComponent<EntityTurnInvisibleComponent>(hunter).Enabled, Is.False);
            var action = entMan.GetComponent<YautjaBracerComponent>(entMan.GetEntity(bracerNet)).ToggleCloakAction;
            Assert.That(action, Is.Not.Null);
            Assert.That(entMan.GetComponent<ActionComponent>(action.Value).Toggled, Is.False);
        });
        await pair.RunUntilSynced();

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var hunter = entMan.GetEntity(hunterNet);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<EntityTurnInvisibleComponent>(hunter).Enabled, Is.False);
                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.False);
                Assert.That(entMan.HasComponent<ThermalCloakUserComponent>(hunter), Is.False);
                Assert.That(entMan.HasComponent<RMCNightVisionVisibleComponent>(hunter), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    private static DamageSpecifier BluntDamage(int amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Blunt", FixedPoint2.New(amount));
        return damage;
    }

    private static void ToggleCloak(IEntityManager entMan, EntityUid hunter, EntityUid bracer)
    {
        // The bracer's action field is server-only; clients use the replicated action list.
        var actions = entMan.System<SharedRMCActionsSystem>()
            .GetActionsWithEvent<YautjaToggleCloakActionEvent>(hunter).ToArray();
        Assert.That(actions, Has.Length.EqualTo(1));
        var toggle = new YautjaToggleCloakActionEvent
        {
            Performer = hunter,
            Action = actions[0],
        };
        entMan.EventBus.RaiseLocalEvent(bracer, toggle);
        Assert.That(toggle.Handled, Is.True);
    }
}
