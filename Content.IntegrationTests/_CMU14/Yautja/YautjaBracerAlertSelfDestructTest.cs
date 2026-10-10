#pragma warning disable RA0002
using System.Linq;
using Content.Shared.Alert;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaBracerAlertSelfDestructTest
{
    [Test]
    public async Task ClickingTheBracerAlertArmsSelfDestructWithoutADialog()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var alerts = server.System<AlertsSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var yautja = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            entMan.EnsureComponent<YautjaComponent>(yautja);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            Assert.That(inventory.TryEquip(yautja, bracer, "gloves", silent: true, force: true), Is.True);

            var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
            Assert.That(bracerComp.SelfDestructArmed, Is.False, "The bracer should start unarmed.");

            var alert = prototypes.Index<AlertPrototype>("CMUYautjaPower");
            Assert.That(alerts.ActivateAlert(yautja, alert), Is.True,
                "Clicking the bracer HUD icon must be handled.");

            Assert.That(bracerComp.SelfDestructArmed, Is.True,
                "Clicking the bracer icon must arm the self-destruct immediately, with no dialog.");

            Assert.That(alerts.ActivateAlert(yautja, alert), Is.True, "Clicking again must be handled.");
            Assert.That(bracerComp.SelfDestructArmed, Is.False,
                "Clicking the icon while the self-destruct is active must cancel it.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClickingWhileHaulingADeadHunterDetonatesTheirBracer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var alerts = server.System<AlertsSystem>();
            var mobState = entMan.System<MobStateSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            entMan.EnsureComponent<YautjaComponent>(hunter);
            var hunterBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            Assert.That(inventory.TryEquip(hunter, hunterBracer, "gloves", silent: true, force: true), Is.True);

            var corpse = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            entMan.EnsureComponent<YautjaComponent>(corpse);
            var corpseBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            Assert.That(inventory.TryEquip(corpse, corpseBracer, "gloves", silent: true, force: true), Is.True);

            var corpseMobState = entMan.GetComponent<MobStateComponent>(corpse);
            mobState.ChangeMobState(corpse, MobState.Dead, corpseMobState, corpse);

            var corpseBracerComp = entMan.GetComponent<YautjaBracerComponent>(corpseBracer);
            Assert.That(corpseBracerComp.SelfDestructArmed, Is.False,
                "The dead hunter's bracer must not be armed before the icon is clicked.");

            entMan.EnsureComponent<PullerComponent>(hunter).Pulling = corpse;

            var alert = prototypes.Index<AlertPrototype>("CMUYautjaPower");
            Assert.That(alerts.ActivateAlert(hunter, alert), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(corpseBracerComp.SelfDestructArmed, Is.True,
                    "Clicking the icon while hauling a dead hunter must trigger that hunter's self-destruct.");
                Assert.That(entMan.GetComponent<YautjaBracerComponent>(hunterBracer).SelfDestructArmed, Is.False,
                    "The clicker's own bracer must be left alone in that case.");
            });

            Assert.That(alerts.ActivateAlert(hunter, alert), Is.True);
            Assert.That(corpseBracerComp.SelfDestructArmed, Is.False,
                "Clicking again while hauling must cancel the hauled hunter's self-destruct.");
        });

        await pair.CleanReturnAsync();
    }
}
