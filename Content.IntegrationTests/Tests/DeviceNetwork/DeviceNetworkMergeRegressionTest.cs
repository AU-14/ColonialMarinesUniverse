using Content.IntegrationTests.Fixtures;
using Content.Server.DeviceNetwork.Systems;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.DeviceNetwork;

[TestFixture]
[TestOf(typeof(DeviceNetworkSystem))]
public sealed class DeviceNetworkMergeRegressionTest : GameTest
{
    private const uint ExcludedFrequency = 321;
    private const uint JammedFrequency = 322;

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: DeviceNetworkMergeAuto
  components:
  - type: DeviceNetwork
    transmitFrequency: 321
    receiveFrequency: 321

- type: entity
  id: DeviceNetworkMergeJammer
  components:
  - type: DeviceNetworkJammer
    range: 10
    jammableNetworks:
    - Private
    frequenciesExcluded:
    - 321
""";

    [Test]
    public async Task OwnerContinuityRecipientCancellationAndJammerExclusion()
    {
        var map = await Pair.CreateTestMap();
        var payload = new NetworkPayload { ["sequence"] = 1 };
        EntityUid sender = default;
        EntityUid autoReceiver = default;
        EntityUid manualReceiver = default;
        DeviceNetworkComponent? senderNetwork = null;
        DeviceNetworkComponent? autoNetwork = null;
        DeviceNetworkComponent? manualNetwork = null;
        DeviceNetworkMergeProbeComponent? manualProbe = null;
        DeviceNetworkMergeProbeComponent? autoProbe = null;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<DeviceNetworkMergeProbeSystem>();
            var entities = Server.EntMan;
            var networks = Server.System<DeviceNetworkSystem>();

            sender = entities.SpawnEntity("DeviceNetworkMergeAuto", map.GridCoords);
            autoReceiver = entities.SpawnEntity("DeviceNetworkMergeAuto", map.GridCoords);
            senderNetwork = entities.GetComponent<DeviceNetworkComponent>(sender);
            autoNetwork = entities.GetComponent<DeviceNetworkComponent>(autoReceiver);
            Assert.Multiple(() =>
            {
                Assert.That(senderNetwork.OwnerEntity, Is.EqualTo(sender));
                Assert.That(autoNetwork.OwnerEntity, Is.EqualTo(autoReceiver),
                    "MapInit must bind each connected component to its owning entity.");
            });

            manualReceiver = entities.SpawnEntity(null, map.GridCoords);
            manualNetwork = entities.AddComponent<DeviceNetworkComponent>(manualReceiver);
            Assert.That(manualNetwork.OwnerEntity, Is.EqualTo(manualReceiver),
                "adding the component to a map-initialized entity must immediately run its MapInit lifecycle");
            typeof(DeviceNetworkComponent)
                .GetField(nameof(DeviceNetworkComponent.OwnerEntity))!
                .SetValue(manualNetwork, null);
            Assert.That(manualNetwork.OwnerEntity, Is.Null,
                "the manual-connect regression must begin from an explicitly cleared owner binding");
            Assert.That(networks.ConnectDevice(manualReceiver, manualNetwork), Is.True);
            networks.SetReceiveFrequency(manualReceiver, ExcludedFrequency, manualNetwork);
            networks.SetTransmitFrequency(manualReceiver, ExcludedFrequency, manualNetwork);
            Assert.Multiple(() =>
            {
                Assert.That(manualNetwork.OwnerEntity, Is.EqualTo(manualReceiver),
                    "Manual ConnectDevice must establish the same owner binding as MapInit.");
                Assert.That(manualNetwork.Address, Is.Not.Empty);
            });

            manualProbe = entities.AddComponent<DeviceNetworkMergeProbeComponent>(manualReceiver);
            autoProbe = entities.AddComponent<DeviceNetworkMergeProbeComponent>(autoReceiver);
            entities.SpawnEntity("DeviceNetworkMergeJammer", map.GridCoords);

            Assert.That(networks.QueuePacket(
                sender,
                manualNetwork.Address,
                payload,
                ExcludedFrequency,
                device: senderNetwork), Is.True);
        });

        await Server.WaitRunTicks(2);
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(manualProbe!.BeforePackets, Is.EqualTo(1));
                Assert.That(manualProbe.LastFrequency, Is.EqualTo(ExcludedFrequency));
                Assert.That(manualProbe.Packets, Is.EqualTo(1),
                    "The jammer's excluded frequency must remain deliverable.");
            });

            manualProbe.Cancel = true;
            payload["sequence"] = 2;
            Assert.That(Server.System<DeviceNetworkSystem>().QueuePacket(
                sender,
                manualNetwork!.Address,
                payload,
                ExcludedFrequency,
                device: senderNetwork), Is.True);
        });

        await Server.WaitRunTicks(2);
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(manualProbe!.BeforePackets, Is.EqualTo(2));
                Assert.That(manualProbe.LastFrequency, Is.EqualTo(ExcludedFrequency),
                    "BeforePacketSent must be raised on the concrete recipient with the packet frequency.");
                Assert.That(manualProbe.Packets, Is.EqualTo(1),
                    "A recipient-local cancellation must suppress only that delivery.");
            });

            manualProbe.Cancel = false;
            var networks = Server.System<DeviceNetworkSystem>();
            networks.SetReceiveFrequency(manualReceiver, JammedFrequency, manualNetwork);
            payload["sequence"] = 3;
            Assert.That(networks.QueuePacket(
                sender,
                manualNetwork!.Address,
                payload,
                JammedFrequency,
                device: senderNetwork), Is.True);
        });

        await Server.WaitRunTicks(2);
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(manualProbe!.BeforePackets, Is.EqualTo(3));
                Assert.That(manualProbe.LastFrequency, Is.EqualTo(JammedFrequency));
                Assert.That(manualProbe.Packets, Is.EqualTo(1),
                    "A non-excluded frequency must remain blocked by the in-range jammer.");
            });

            // Simulate a legacy/corrupt connected entry to exercise the null-owner safety gate without violating
            // DeviceNetworkComponent's Access contract in production code.
            typeof(DeviceNetworkComponent)
                .GetField(nameof(DeviceNetworkComponent.OwnerEntity))!
                .SetValue(autoNetwork, null);
            payload["sequence"] = 4;
            Assert.That(Server.System<DeviceNetworkSystem>().QueuePacket(
                sender,
                autoNetwork!.Address,
                payload,
                ExcludedFrequency,
                device: senderNetwork), Is.True);
        });

        await Server.WaitRunTicks(2);
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(autoProbe!.BeforePackets, Is.Zero);
                Assert.That(autoProbe.Packets, Is.Zero,
                    "A network entry without OwnerEntity must be skipped safely.");
            });
        });
    }
}

[RegisterComponent]
public sealed partial class DeviceNetworkMergeProbeComponent : Component
{
    public bool Cancel;
    public int BeforePackets;
    public int Packets;
    public uint LastFrequency;
}

public sealed class DeviceNetworkMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeviceNetworkMergeProbeComponent, BeforePacketSentEvent>(OnBeforePacket);
        SubscribeLocalEvent<DeviceNetworkMergeProbeComponent, DeviceNetworkPacketEvent>(OnPacket);
    }

    private static void OnBeforePacket(
        Entity<DeviceNetworkMergeProbeComponent> ent,
        ref BeforePacketSentEvent args)
    {
        ent.Comp.BeforePackets++;
        ent.Comp.LastFrequency = args.Frequency;
        if (ent.Comp.Cancel)
            args.Cancel();
    }

    private static void OnPacket(
        Entity<DeviceNetworkMergeProbeComponent> ent,
        ref DeviceNetworkPacketEvent args)
    {
        ent.Comp.Packets++;
    }
}
