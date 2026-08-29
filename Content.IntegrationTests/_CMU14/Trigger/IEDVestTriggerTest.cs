using System;
using System.Linq;
using Content.Shared.DeviceLinking;
using Content.Shared.Inventory;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Components.Conditions;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Trigger;

[TestFixture]
public sealed class IEDVestTriggerTest
{
    private const string VestPrototype = "AU14IEDVest";
    private const string OuterClothingSlot = "outerClothing";

    [Test]
    public async Task VestPrototypeUsesKeyedWornGateAndLegacyRemotePort()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var vest = prototypes.Index<EntityPrototype>(VestPrototype);

            Assert.That(vest.TryComp<WornSlotTriggerConditionComponent>(out var worn, factory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(worn!.RequiredSlots, Is.EqualTo(SlotFlags.OUTERCLOTHING));
                Assert.That(worn.Keys, Is.EquivalentTo(new[] { "trigger", "stuck" }));
            });

            Assert.That(vest.TryComp<TimerTriggerComponent>(out var timer, factory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(timer!.KeysIn, Is.EquivalentTo(new[] { "trigger", "stuck" }));
                Assert.That(timer.KeyOut, Is.EqualTo("timer"));
                Assert.That(timer.Delay, Is.EqualTo(TimeSpan.FromSeconds(0.1)));
            });

            Assert.That(vest.TryComp<TriggerOnSignalComponent>(out var signal, factory), Is.True);
            Assert.That(signal!.Port.Id, Is.EqualTo("Timer"));
            Assert.That(vest.TryComp<DeviceLinkSinkComponent>(out var sink, factory), Is.True);
            Assert.That(sink!.Ports.Select(port => port.Id), Is.EquivalentTo(new[] { "Timer" }));

            Assert.That(vest.TryComp<ExplodeOnTriggerComponent>(out var explode, factory), Is.True);
            Assert.That(explode!.KeysIn, Is.EquivalentTo(new[] { "timer" }));
            Assert.That(vest.TryComp<GibOnTriggerComponent>(out var gib, factory), Is.True);
            Assert.That(gib!.KeysIn, Is.EquivalentTo(new[] { "timer" }));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TriggerAndStuckRequireWearingButArmedTimerSurvivesRemoval()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid wearer = default;
        EntityUid vest = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var trigger = entities.System<TriggerSystem>();
            wearer = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            vest = entities.SpawnEntity(VestPrototype, MapCoordinates.Nullspace);

            Assert.Multiple(() =>
            {
                Assert.That(trigger.Trigger(vest, wearer, "trigger", predicted: false), Is.False,
                    "Activation started the vest while it was not worn.");
                Assert.That(trigger.Trigger(vest, wearer, "stuck", predicted: false), Is.False,
                    "Sticking started the vest while it was not worn.");
                Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(vest), Is.False);
            });

            var inventory = entities.System<InventorySystem>();
            Assert.That(inventory.TryEquip(wearer, vest, OuterClothingSlot, silent: true, force: true), Is.True);

            Assert.That(trigger.Trigger(vest, wearer, "trigger", predicted: false), Is.True);
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(vest), Is.True);
            Assert.That(trigger.StopTimerTrigger(vest), Is.True);

            Assert.That(trigger.Trigger(vest, wearer, "stuck", predicted: false), Is.True);
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(vest), Is.True);
            Assert.That(trigger.StopTimerTrigger(vest), Is.True);

            Assert.That(trigger.Trigger(vest, wearer, "trigger", predicted: false), Is.True);
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(vest), Is.True);
            Assert.That(inventory.TryUnequip(wearer, OuterClothingSlot, silent: true, force: true), Is.True);
        });

        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(vest), Is.False,
                "The already-armed timer was incorrectly blocked after the vest was removed.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WornGatePredictionMatchesServerAndReconcilesInactive()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var originalAttached = player.AttachedEntity;
        EntityUid serverWearer = default;
        EntityUid serverVest = default;
        NetEntity wearerNet = default;
        NetEntity vestNet = default;

        try
        {
            await server.WaitPost(() =>
            {
                serverWearer = server.EntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                serverVest = server.EntMan.SpawnEntity(VestPrototype, map.GridCoords);
                server.EntMan.System<TriggerSystem>()
                    .SetDelay(serverVest, TimeSpan.FromSeconds(30));
                server.PlayerMan.SetAttachedEntity(player, serverWearer);
                wearerNet = server.EntMan.GetNetEntity(serverWearer);
                vestNet = server.EntMan.GetNetEntity(serverVest);
            });
            await pair.RunUntilSynced();

            var clientWearer = client.EntMan.GetEntity(wearerNet);
            var clientVest = client.EntMan.GetEntity(vestNet);

            await client.WaitAssertion(() =>
            {
                var trigger = client.EntMan.System<TriggerSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(trigger.Trigger(clientVest, clientWearer, "trigger", predicted: true), Is.False);
                    Assert.That(trigger.Trigger(clientVest, clientWearer, "stuck", predicted: true), Is.False);
                    Assert.That(client.EntMan.HasComponent<ActiveTimerTriggerComponent>(clientVest), Is.False);
                });
            });
            await server.WaitAssertion(() =>
            {
                var trigger = server.EntMan.System<TriggerSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(trigger.Trigger(serverVest, serverWearer, "trigger", predicted: false), Is.False);
                    Assert.That(trigger.Trigger(serverVest, serverWearer, "stuck", predicted: false), Is.False);
                    Assert.That(server.EntMan.HasComponent<ActiveTimerTriggerComponent>(serverVest), Is.False);
                });
            });

            await server.WaitAssertion(() =>
            {
                var inventory = server.EntMan.System<InventorySystem>();
                Assert.That(inventory.TryEquip(serverWearer,
                    serverVest,
                    OuterClothingSlot,
                    silent: true,
                    force: true), Is.True);
            });
            await pair.RunUntilSynced();

            foreach (var key in new[] { "trigger", "stuck" })
            {
                var clientHandled = false;
                var serverHandled = false;

                await client.WaitAssertion(() =>
                {
                    var trigger = client.EntMan.System<TriggerSystem>();
                    clientHandled = trigger.Trigger(clientVest, clientWearer, key, predicted: true);
                    Assert.That(client.EntMan.HasComponent<ActiveTimerTriggerComponent>(clientVest), Is.True, key);
                    Assert.That(trigger.StopTimerTrigger(clientVest), Is.True, key);
                });
                await server.WaitAssertion(() =>
                {
                    var trigger = server.EntMan.System<TriggerSystem>();
                    serverHandled = trigger.Trigger(serverVest, serverWearer, key, predicted: false);
                    Assert.That(server.EntMan.HasComponent<ActiveTimerTriggerComponent>(serverVest), Is.True, key);
                    Assert.That(trigger.StopTimerTrigger(serverVest), Is.True, key);
                });

                Assert.Multiple(() =>
                {
                    Assert.That(clientHandled, Is.True, $"client prediction for {key}");
                    Assert.That(serverHandled, Is.EqualTo(clientHandled), $"server result for {key}");
                });
                await pair.RunUntilSynced();
                await client.WaitAssertion(() =>
                    Assert.That(client.EntMan.HasComponent<ActiveTimerTriggerComponent>(clientVest), Is.False, key));
                await server.WaitAssertion(() =>
                    Assert.That(server.EntMan.HasComponent<ActiveTimerTriggerComponent>(serverVest), Is.False, key));
            }
        }
        finally
        {
            await server.WaitPost(() => server.PlayerMan.SetAttachedEntity(player, originalAttached));
            await pair.CleanReturnAsync();
        }
    }
}
