using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Payload.Components;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.CMU14.Trigger;

[TestFixture]
public sealed class KeyedTimerMigrationTest
{


    private const string StartTimerKey = "startTimer";
    private const string FinalTriggerKey = "trigger";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: KeyedTimerMigrationProbe
  components:
  - type: TriggerOnUse
    keyOut: startTimer
  - type: TimerTrigger
    keysIn: [ startTimer ]
    keyOut: trigger
    delay: 0.1
    popup: null
  - type: DeleteOnTrigger
    keysIn: [ trigger ]

- type: entity
  id: KeyedTimerIgniterProbe
  parent: BaseItem
  components:
  - type: TriggerOnIgniterUse
    keyOut: ignite
  - type: TimerTrigger
    keysIn: [ ignite ]
    keyOut: trigger
    delay: 0.1
    examinable: false
    popup: null
  - type: DeleteOnTrigger
    keysIn: [ trigger ]

- type: entity
  id: KeyedTimerBlowtorchProbe
  parent: BaseItem
  components:
  - type: Blowtorch
  - type: ItemToggle
";

    [Test]
    public async Task MinimalTimerPipelineOnlyRunsFinalEffectAfterCompletion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid probe = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var trigger = entities.System<TriggerSystem>();
            probe = entities.SpawnEntity("KeyedTimerMigrationProbe", MapCoordinates.Nullspace);

            Assert.That(trigger.Trigger(probe, key: StartTimerKey, predicted: false), Is.True);
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(probe), Is.True);
            Assert.That(entities.EntityExists(probe), Is.True, "The final key ran immediately.");
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.EntityExists(probe), Is.True, "The final key ran before the timer elapsed."));

        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.EntityExists(probe), Is.False, "The timer did not emit its final trigger key."));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RmcPayloadTimerInstallsAndRunsBothPipelineStages()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid casing = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var containers = entities.System<SharedContainerSystem>();
            var trigger = entities.System<TriggerSystem>();
            casing = entities.SpawnEntity("ModularGrenade", MapCoordinates.Nullspace);
            var timerDevice = entities.SpawnEntity("RMCTimerTrigger", MapCoordinates.Nullspace);
            var container = containers.EnsureContainer<Container>(casing, "payloadTrigger");

            Assert.That(containers.Insert(timerDevice, container), Is.True);
            Assert.That(entities.TryGetComponent<TriggerOnUseComponent>(casing, out var use), Is.True);
            Assert.That(use!.KeyOut, Is.EqualTo(StartTimerKey));
            Assert.That(entities.TryGetComponent<TimerTriggerComponent>(casing, out var timer), Is.True);
            Assert.That(timer!.KeysIn, Is.EquivalentTo(new[] { StartTimerKey }));
            Assert.That(timer.KeyOut, Is.EqualTo(FinalTriggerKey));
            timer.Delay = TimeSpan.FromSeconds(0.1);
            entities.AddComponent<DeleteOnTriggerComponent>(casing);

            Assert.That(trigger.Trigger(casing, key: StartTimerKey, predicted: false), Is.True);
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(casing), Is.True);
            Assert.That(entities.EntityExists(casing), Is.True);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(casing), Is.True));
        await server.WaitRunTicks(10);
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(casing), Is.False));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChemicalPayloadOnlyArmsWithTwoValidBeakers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var containers = entities.System<SharedContainerSystem>();
            var itemSlots = entities.System<ItemSlotsSystem>();
            var trigger = entities.System<TriggerSystem>();
            var user = entities.SpawnEntity("MobObserver", MapCoordinates.Nullspace);
            var casing = entities.SpawnEntity("ModularGrenade", MapCoordinates.Nullspace);
            var timerDevice = entities.SpawnEntity("RMCTimerTrigger", MapCoordinates.Nullspace);
            var chemicalPayload = entities.SpawnEntity("ChemicalPayload", MapCoordinates.Nullspace);
            var payloadContainer = containers.EnsureContainer<Container>(casing, "payload");
            var triggerContainer = containers.EnsureContainer<Container>(casing, "payloadTrigger");

            Assert.That(containers.Insert(chemicalPayload, payloadContainer), Is.True);
            Assert.That(containers.Insert(timerDevice, triggerContainer), Is.True);
            Assert.That(entities.HasComponent<TimerTriggerComponent>(casing), Is.True);

            Assert.That(trigger.Trigger(casing, user, StartTimerKey, predicted: false), Is.False);
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(casing), Is.False,
                "An incomplete chemical payload must cancel timer activation.");

            var beakerA = entities.SpawnEntity("CMBeaker", MapCoordinates.Nullspace);
            var beakerB = entities.SpawnEntity("CMBeaker", MapCoordinates.Nullspace);
            var chemical = entities.GetComponent<ChemicalPayloadComponent>(chemicalPayload);
            Assert.That(itemSlots.TryInsert(chemicalPayload, chemical.BeakerSlotA, beakerA, user), Is.True);
            Assert.That(itemSlots.TryInsert(chemicalPayload, chemical.BeakerSlotB, beakerB, user), Is.True);

            var attempt = new AttemptTimerTriggerEvent(user, TimeSpan.FromSeconds(3));
            entities.EventBus.RaiseLocalEvent(casing, ref attempt);
            Assert.Multiple(() =>
            {
                Assert.That(attempt.Cancelled, Is.False);
                Assert.That(attempt.LogMessage, Does.Contain("which contains"));
                Assert.That(attempt.LogMessage, Does.Contain("in one beaker"));
                Assert.That(attempt.LogMessage, Does.Contain("in the other"));
            });

            Assert.That(trigger.Trigger(casing, user, StartTimerKey, predicted: false), Is.True);
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(casing), Is.True,
                "A complete two-beaker chemical payload must permit timer activation.");
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CMUPipeBomb", "RMCLighter", "/Audio/Effects/lightburn.ogg")]
    [TestCase("RMCGrenadeMolotov", "KeyedTimerBlowtorchProbe", null)]
    public async Task OnLightSuccessorsRequireAnActiveHeldIgniter(
        string targetPrototype,
        string igniterPrototype,
        string? expectedBeepPath)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid target = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var prototype = prototypes.Index<EntityPrototype>(targetPrototype);
            var factory = entities.ComponentFactory;
            Assert.That(prototype.TryComp<TimerTriggerComponent>(out var prototypeTimer, factory), Is.True);
            Assert.That(prototype.TryComp<RandomTimerTriggerComponent>(out var random, factory), Is.True);
            Assert.That(prototype.TryComp<TriggerOnIgniterUseComponent>(out var prototypeIgniter, factory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(prototypeIgniter!.KeyOut, Is.EqualTo("ignite"));
                Assert.That(prototypeTimer!.KeysIn, Is.EquivalentTo(new[] { "ignite" }));
                Assert.That(prototypeTimer.KeyOut, Is.EqualTo(FinalTriggerKey));
                Assert.That(prototypeTimer.Examinable, Is.False);
                Assert.That(prototypeTimer.InitialBeepDelay, Is.EqualTo(TimeSpan.Zero));
                Assert.That(random!.Min, Is.EqualTo(1));
                Assert.That(random.Max, Is.EqualTo(4));
            });

            if (expectedBeepPath == null)
            {
                Assert.That(prototypeTimer.BeepSound, Is.Null);
            }
            else
            {
                Assert.That(prototypeTimer.BeepSound, Is.TypeOf<SoundPathSpecifier>());
                Assert.That(((SoundPathSpecifier) prototypeTimer.BeepSound!).Path.ToString(), Is.EqualTo(expectedBeepPath));
            }

            var user = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            target = entities.SpawnEntity("KeyedTimerIgniterProbe", MapCoordinates.Nullspace);
            var igniter = entities.SpawnEntity(igniterPrototype, MapCoordinates.Nullspace);
            var hands = entities.System<SharedHandsSystem>();
            var toggleSystem = entities.System<ItemToggleSystem>();
            var timer = entities.GetComponent<TimerTriggerComponent>(target);
            var igniterTrigger = entities.GetComponent<TriggerOnIgniterUseComponent>(target);

            Assert.Multiple(() =>
            {
                Assert.That(igniterTrigger.KeyOut, Is.EqualTo("ignite"));
                Assert.That(timer.KeysIn, Is.EquivalentTo(new[] { "ignite" }));
                Assert.That(timer.KeyOut, Is.EqualTo(FinalTriggerKey));
                Assert.That(timer.Examinable, Is.False);
            });

            var noIgniter = new UseInHandEvent(user);
            entities.EventBus.RaiseLocalEvent(target, noIgniter);
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(target), Is.False);

            Assert.That(hands.TryPickupAnyHand(user, igniter, checkActionBlocker: false), Is.True);
            var inactiveIgniter = new UseInHandEvent(user);
            entities.EventBus.RaiseLocalEvent(target, inactiveIgniter);
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(target), Is.False);

            var toggle = entities.GetComponent<ItemToggleComponent>(igniter);
            if (!toggle.Activated)
                Assert.That(toggleSystem.TryActivate((igniter, toggle), user), Is.True);
            timer.Delay = TimeSpan.FromSeconds(0.1);

            var activeIgniter = new UseInHandEvent(user);
            entities.EventBus.RaiseLocalEvent(target, activeIgniter);
            Assert.That(activeIgniter.Handled, Is.True);
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(target), Is.True);

            var duplicateIgnition = new UseInHandEvent(user);
            entities.EventBus.RaiseLocalEvent(target, duplicateIgnition);
            Assert.That(duplicateIgnition.Handled, Is.False, "An active timer must not arm a second time.");
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(target), Is.True);
            Assert.That(entities.EntityExists(target), Is.True, "The final effect ran during ignition.");
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(target), Is.True));
        await server.WaitRunTicks(10);
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(target), Is.False));

        await pair.CleanReturnAsync();
    }
}
