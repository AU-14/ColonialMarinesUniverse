using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.DragDrop;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using ClientPopupSystem = Content.Client.Popups.PopupSystem;

namespace Content.IntegrationTests.Tests.Nutrition;

public sealed class KitchenSpikeWaitForRotTest : InteractionTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: KitchenSpike
  id: KitchenSpikeWaitForRotTestSpike
  components:
  - type: KitchenSpike
    hookDelay: 5

- type: entity
  id: KitchenSpikeWaitForRotVictim
  components:
  - type: Butcherable
    butcheringType: Spike
    waitForRot: true
    spawned:
    - id: FoodMeatHuman
      amount: 1
  - type: Damageable

- type: entity
  id: KitchenSpikeKnifeVictim
  components:
  - type: Butcherable
    butcheringType: Knife
    spawned:
    - id: FoodMeatHuman
      amount: 1
  - type: Damageable
";

    [Test]
    public async Task SpikeRejectsKnifeRouteVictim()
    {
        var spikeNet = await SpawnTarget("KitchenSpikeWaitForRotTestSpike");
        var victimNet = await Spawn("KitchenSpikeKnifeVictim");
        var spike = ToServer(spikeNet);
        var victim = ToServer(victimNet);
        var spikeComponent = SEntMan.GetComponent<KitchenSpikeComponent>(spike);

        await AssertHookRejectedWithPopup(spike, victim, "using a knife");
        await RaiseDragDrop(spike, victim);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ActiveDoAfters, Is.Empty,
                "A Knife-route victim must not start the kitchen-spike hook path.");
            Assert.That(spikeComponent.BodyContainer.ContainedEntity, Is.Null);
        }
    }

    [Test]
    public async Task SpikeOnlyHooksWaitForRotVictimWhileItIsUnrevivable()
    {
        var spikeNet = await SpawnTarget("KitchenSpikeWaitForRotTestSpike");
        var victimNet = await Spawn("KitchenSpikeWaitForRotVictim");
        var spike = ToServer(spikeNet);
        var victim = ToServer(victimNet);
        var containerSystem = SEntMan.System<SharedContainerSystem>();
        var spikeComponent = SEntMan.GetComponent<KitchenSpikeComponent>(spike);

        await AssertHookRejectedWithPopup(spike, victim, "not rotten enough");
        await RaiseDragDrop(spike, victim);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ActiveDoAfters, Is.Empty,
                "A wait-for-rot victim must not start hooking while it is still revivable.");
            Assert.That(spikeComponent.BodyContainer.ContainedEntity, Is.Null);
        }

        await Server.WaitPost(() => SEntMan.EnsureComponent<UnrevivableComponent>(victim));

        await RaiseDragDrop(spike, victim);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1),
            "The same victim should start exactly one hook do-after once unrevivable.");
        await AwaitDoAfters();
        Assert.That(spikeComponent.BodyContainer.ContainedEntity, Is.EqualTo(victim));

        await Server.WaitPost(() =>
        {
            Assert.That(containerSystem.Remove(victim, spikeComponent.BodyContainer), Is.True);
            SEntMan.EnsureComponent<UnrevivableComponent>(victim);
        });
        await RunTicks(2);

        await RaiseDragDrop(spike, victim);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));
        await Server.WaitPost(() => SEntMan.RemoveComponent<UnrevivableComponent>(victim));
        await AwaitDoAfters();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ActiveDoAfters, Is.Empty);
            Assert.That(spikeComponent.BodyContainer.ContainedEntity, Is.Null,
                "Completion must recheck revivability so a state change cannot bypass the hook gate.");
        }
    }

    private async Task AssertHookRejectedWithPopup(EntityUid spike, EntityUid victim, string expectedText)
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.TryGetComponent<ActorComponent>(SPlayer, out var actor), Is.True);
            Assert.That(actor!.PlayerSession, Is.SameAs(ServerSession));

            var attempt = new KitchenSpikeHookAttemptEvent(SPlayer, victim);
            SEntMan.EventBus.RaiseLocalEvent(spike, ref attempt);
            Assert.That(attempt.Cancelled, Is.True);
        });
        await Pair.RunUntilSynced();

        var popupTexts = CEntMan.System<ClientPopupSystem>().WorldLabels
            .Select(label => label.Text)
            .ToArray();
        Assert.That(popupTexts.Any(text => text.Contains(expectedText, StringComparison.Ordinal)),
            Is.True,
            $"Rejecting the hook must deliver the fork-specific guidance. Actual: {string.Join(" | ", popupTexts)}");
    }

    private Task RaiseDragDrop(EntityUid spike, EntityUid victim)
    {
        return Server.WaitAssertion(() =>
        {
            var dragDrop = new DragDropTargetEvent(SPlayer, victim);
            SEntMan.EventBus.RaiseLocalEvent(spike, ref dragDrop);
            Assert.That(dragDrop.Handled, Is.True);
        });
    }
}
