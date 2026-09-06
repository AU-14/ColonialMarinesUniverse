using System.Collections.Generic;
using System.Linq;
using Content.Server.Power;
using Content.Server.Power.Components;
using Content.Server.Silicons.StationAi;
using Content.Server.Wires;
using Content.Server.SurveillanceCamera;
using Content.Shared._RMC14.Camera;
using Content.Shared.Camera;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Wires;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Camera;

[TestFixture]
public sealed class CameraNetworkPrototypeTest
{
    private static readonly (EntProtoId Id, CameraSourceKinds SourceKinds, bool Marker)[] MapSourceCameras =
    {
        ("CMUSurveillanceCameraColonyCMB", CameraSourceKinds.Rmc, true),
        ("CMUSurveillanceCameraColonyGOVFOR", CameraSourceKinds.Rmc, true),
        ("CMUSurveillanceCameraColonyWEYU", CameraSourceKinds.Rmc, true),
        ("RMCSurveillanceCameraAlmayer", CameraSourceKinds.Rmc, true),
        ("RMCMonitorCameraAlamo", CameraSourceKinds.Rmc, true),
        ("RMCMonitorCameraNormandy", CameraSourceKinds.Rmc, true),
        ("VehicleInteriorCamera", CameraSourceKinds.Rmc, true),
    };

    private static readonly string[] NetworkIds =
    {
        "SurveillanceCameraEngineering",
        "SurveillanceCameraSecurity",
        "SurveillanceCameraScience",
        "SurveillanceCameraSupply",
        "SurveillanceCameraCommand",
        "SurveillanceCameraService",
        "SurveillanceCameraMedical",
        "SurveillanceCameraGeneral",
        "SurveillanceCameraEntertainment",
        "SurveillanceCameraCLF",
        "CMUSurveillanceCameraColonyCMB",
        "CMUSurveillanceCameraColonyGOVFOR",
        "CMUSurveillanceCameraColonyWEYU",
        "RMCMonitorCameraAlamo",
        "RMCMonitorCameraLandingZone",
        "RMCMonitorCameraNormandy",
        "RMCSurveillanceCameraAlmayer",
        "RMCTelevision",
    };

    [Test]
    public async Task EveryExplicitCameraNetworkReferenceResolves()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await server.WaitAssertion(() =>
            {
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                foreach (var id in NetworkIds)
                    Assert.That(prototypes.HasIndex<CameraNetworkPrototype>(id), Is.True, id);

                foreach (var prototype in prototypes.EnumeratePrototypes<EntityPrototype>())
                {
                    if (prototype.TryComp<CameraNetworkMemberComponent>(out var member, server.EntMan.ComponentFactory))
                        AssertNetworksResolve(prototypes, prototype, member!.Networks);

                    if (prototype.TryComp<CameraNetworkReceiverComponent>(out var receiver, server.EntMan.ComponentFactory))
                        AssertNetworksResolve(prototypes, prototype, receiver!.Networks);

                    if (prototype.TryComp<CameraSignalGranterComponent>(out var granter, server.EntMan.ComponentFactory))
                        AssertNetworksResolve(prototypes, prototype, granter!.ProtoIds);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task CoreCameraPrototypesHaveLogicalComponents()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await server.WaitAssertion(() =>
            {
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var factory = server.EntMan.ComponentFactory;

            AssertReceiver(prototypes, factory, "AU14CLFCameraMonitor", ["SurveillanceCameraCLF"], CameraSourceKinds.Standard);
            AssertMember(prototypes, factory, "AU14CLFSpyCamera", ["SurveillanceCameraCLF"], CameraSourceKinds.Standard, false);

            AssertMember(prototypes, factory, "RMCSurveillanceCameraAlmayer", ["RMCSurveillanceCameraAlmayer"], CameraSourceKinds.Rmc, true);
            AssertReceiver(prototypes, factory, "RMCMonitorCameraAlmayer", ["RMCSurveillanceCameraAlmayer", "RMCMonitorCameraAlamo", "RMCMonitorCameraNormandy"], CameraSourceKinds.Rmc);
            AssertMember(prototypes, factory, "RMCMonitorCameraAlamo", ["RMCMonitorCameraAlamo"], CameraSourceKinds.Rmc, true);
            AssertMember(prototypes, factory, "RMCMonitorCameraNormandy", ["RMCMonitorCameraNormandy"], CameraSourceKinds.Rmc, true);
            AssertMember(prototypes, factory, "RMCMonitorCameraLandingZone", ["RMCMonitorCameraLandingZone"], CameraSourceKinds.Rmc, false);
            AssertMember(prototypes, factory, "RMCCameraBroadcasting", ["RMCTelevision"], CameraSourceKinds.Rmc, false);
            AssertReceiver(prototypes, factory, "CMComputerDropshipCamerasAlamo", ["RMCMonitorCameraAlamo"], CameraSourceKinds.Rmc);
            AssertReceiver(prototypes, factory, "RMCComputerDropshipCamerasNormandy", ["RMCMonitorCameraNormandy"], CameraSourceKinds.Rmc);
            AssertReceiver(prototypes, factory, "RMCTelevision", ["RMCTelevision"], CameraSourceKinds.Rmc);
            AssertReceiver(prototypes, factory, "RMCTelevisionWallMount", ["RMCTelevision"], CameraSourceKinds.Rmc);
            AssertMember(prototypes, factory, "SurveillanceCameraConstructed", [], CameraSourceKinds.Standard, true);
            AssertMember(prototypes, factory, "CMUSurveillanceCameraColonyCMB", ["CMUSurveillanceCameraColonyCMB"], CameraSourceKinds.Rmc, true);
            AssertMember(prototypes, factory, "CMUSurveillanceCameraColonyGOVFOR", ["CMUSurveillanceCameraColonyGOVFOR"], CameraSourceKinds.Rmc, true);
            AssertMember(prototypes, factory, "CMUSurveillanceCameraColonyWEYU", ["CMUSurveillanceCameraColonyWEYU"], CameraSourceKinds.Rmc, true);
            AssertSurveillanceCamera(prototypes, factory, "CMUSurveillanceCameraColonyCMB");
            AssertSurveillanceCamera(prototypes, factory, "CMUSurveillanceCameraColonyGOVFOR");
            AssertSurveillanceCamera(prototypes, factory, "CMUSurveillanceCameraColonyWEYU");
            AssertCameraWires(prototypes, factory, "CMUSurveillanceCameraColonyCMB");
            AssertCameraWires(prototypes, factory, "RMCSurveillanceCameraAlmayer");
            AssertCameraDoesNotRequirePower(prototypes, factory, "CMUSurveillanceCameraColonyCMB");
            AssertCameraDoesNotRequirePower(prototypes, factory, "CMUSurveillanceCameraColonyGOVFOR");
            AssertCameraDoesNotRequirePower(prototypes, factory, "CMUSurveillanceCameraColonyWEYU");
            AssertCameraDoesNotRequirePower(prototypes, factory, "RMCSurveillanceCameraAlmayer");
            AssertCameraRequiresPower(prototypes, factory, "RMCMonitorCameraAlamo");
            AssertCameraRequiresPower(prototypes, factory, "RMCMonitorCameraNormandy");
            AssertCameraRequiresPower(prototypes, factory, "VehicleInteriorCamera");
            AssertAnchored(prototypes, factory, "CMUSurveillanceCameraColonyCMB");
            AssertAnchored(prototypes, factory, "CMUSurveillanceCameraColonyGOVFOR");
            AssertAnchored(prototypes, factory, "CMUSurveillanceCameraColonyWEYU");
            AssertAnchored(prototypes, factory, "CMUMonitorCameraColonyCMB");
            AssertReceiver(prototypes, factory, "CMUMonitorCameraColonyWEYUSpy", ["CMUSurveillanceCameraColonyCMB", "CMUSurveillanceCameraColonyWEYU"], CameraSourceKinds.Rmc);
            foreach (var id in new[]
            {
                "RMCMortarCamera",
                "RMCMortarKit",
                "AU14MortarCameraRMC",
                "AU14MortarKitRMC",
                "RMCFlareCAS",
                "RMCAirFlareCAS",
                "RMCLaserDesignatorTarget",
            })
            {
                AssertNoCameraNetworkComponents(prototypes, factory, id);
            }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task RestoredConstructionGraphsProduceGenericEquipment()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await server.WaitAssertion(() =>
            {
                var prototypes = server.ResolveDependency<IPrototypeManager>();

                Assert.Multiple(() =>
                {
                    Assert.That(prototypes.Index<ConstructionGraphPrototype>("SurveillanceCamera")
                            .Nodes["camera"].Entity.GetId(null, null, new(server.EntMan)),
                        Is.EqualTo("SurveillanceCameraConstructed"));
                    Assert.That(prototypes.Index<ConstructionGraphPrototype>("WallmountTelescreen")
                            .Nodes["Telescreen"].Entity.GetId(null, null, new(server.EntMan)),
                        Is.EqualTo("WallmountTelescreen"));
                    Assert.That(prototypes.Index<ConstructionGraphPrototype>("WallmountTelevision")
                            .Nodes["Television"].Entity.GetId(null, null, new(server.EntMan)),
                        Is.EqualTo("WallmountTelevision"));
                });
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task CmuAndRmcCamerasExposeSelectableLogicalNetworks()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await server.WaitAssertion(() =>
            {
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var factory = server.EntMan.ComponentFactory;

                foreach (var id in new[] { "CMUSurveillanceCameraColonyCMB", "RMCSurveillanceCameraAlmayer" })
                {
                    var prototype = prototypes.Index<EntityPrototype>(id);
                    Assert.That(prototype.TryComp<SurveillanceCameraComponent>(out var camera, factory), Is.True, id);
                    Assert.That(camera!.AvailableNetworks.Count, Is.GreaterThan(1), id);
                    Assert.That(camera.NetworkSet, Is.False, id);
                    Assert.That(camera.AvailableNetworks.All(network =>
                        prototypes.TryIndex<CameraNetworkPrototype>(network, out var networkPrototype) && networkPrototype.Configurable),
                        Is.True,
                        id);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task CmuAndRmcCameraNetworkAssignmentPreservesRmcSourceKind()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var cameras = entMan.System<SurveillanceCameraSystem>();
                var cases = new[]
                {
                    (Prototype: "CMUSurveillanceCameraColonyCMB", Network: (ProtoId<CameraNetworkPrototype>) "CMUSurveillanceCameraColonyGOVFOR"),
                    (Prototype: "RMCSurveillanceCameraAlmayer", Network: (ProtoId<CameraNetworkPrototype>) "RMCMonitorCameraAlamo"),
                };

                foreach (var (prototype, network) in cases)
                {
                    var camera = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
                    try
                    {
                        var component = entMan.GetComponent<SurveillanceCameraComponent>(camera);
                        Assert.That(cameras.TrySetNetwork((camera, component), network), Is.True, prototype);

                        var member = entMan.GetComponent<CameraNetworkMemberComponent>(camera);
                        Assert.Multiple(() =>
                        {
                            Assert.That(component.NetworkSet, Is.True, prototype);
                            Assert.That(member.Networks, Is.EquivalentTo(new[] { network }), prototype);
                            Assert.That(member.SourceKinds, Is.EqualTo(CameraSourceKinds.Rmc), prototype);
                        });
                    }
                    finally
                    {
                        entMan.DeleteEntity(camera);
                    }
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task MapSourceCameraPrototypesHaveLogicalComponents()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await server.WaitAssertion(() =>
            {
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var factory = server.EntMan.ComponentFactory;

                foreach (var (id, sourceKinds, marker) in MapSourceCameras)
                {
                    var prototype = prototypes.Index<EntityPrototype>(id);
                    Assert.That(prototype.TryComp<CameraNetworkMemberComponent>(out var member, factory), Is.True, id.ToString());
                    Assert.That(member!.Networks, Is.Not.Empty, id.ToString());
                    AssertNetworksResolve(prototypes, prototype, member.Networks);
                    Assert.That(member.SourceKinds, Is.EqualTo(sourceKinds), id.ToString());
                    Assert.That(prototype.TryComp<CameraMapMarkerComponent>(out _, factory), Is.EqualTo(marker), id.ToString());
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task GenericStationCameraAndMonitorPrototypesAreNotRmcSources()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await server.WaitAssertion(() =>
            {
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var generic = new[]
                {
                    "SurveillanceCameraEngineering",
                    "SurveillanceCameraSecurity",
                    "SurveillanceCameraScience",
                    "SurveillanceCameraSupply",
                    "SurveillanceCameraCommand",
                    "SurveillanceCameraService",
                    "SurveillanceCameraMedical",
                    "SurveillanceCameraGeneral",
                    "ComputerSurveillanceCameraMonitor",
                    "ComputerSurveillanceWirelessCameraMonitor",
                    "WallmountTelescreen",
                    "WallmountTelevision",
                    "ComputerTelevision",
                    "CameraBug",
                    "PonderingOrbWizard",
                };

                foreach (var id in generic)
                {
                    if (!prototypes.TryIndex<EntityPrototype>(id, out var prototype))
                        continue;

                    Assert.That(prototype.TryComp<RMCCameraComputerComponent>(out _, server.EntMan.ComponentFactory),
                        Is.False,
                        id);
                    if (prototype.TryComp<CameraNetworkMemberComponent>(out var member, server.EntMan.ComponentFactory))
                        Assert.That(member!.SourceKinds & CameraSourceKinds.Rmc, Is.EqualTo(CameraSourceKinds.None), id);
                    if (prototype.TryComp<CameraNetworkReceiverComponent>(out var receiver, server.EntMan.ComponentFactory))
                        Assert.That(receiver!.SupportedSources & CameraSourceKinds.Rmc, Is.EqualTo(CameraSourceKinds.None), id);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task MapSourceInventoryContainsOnlyColonialAndCmuRmcCameras()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await server.WaitAssertion(() =>
            {
                foreach (var (id, sourceKinds, marker) in MapSourceCameras)
                {
                    Assert.That(sourceKinds, Is.EqualTo(CameraSourceKinds.Rmc), id.ToString());
                    Assert.That(marker, Is.True, id.ToString());
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task CmuAndRmcCameraWireLayoutsInstantiateVanillaActions()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var cmu = entMan.SpawnEntity("CMUSurveillanceCameraColonyCMB", MapCoordinates.Nullspace);
                var rmc = entMan.SpawnEntity("RMCSurveillanceCameraAlmayer", MapCoordinates.Nullspace);

                try
                {
                    Assert.Multiple(() =>
                    {
                        AssertCameraWireActions(entMan.GetComponent<WiresComponent>(cmu), "CMU");
                        AssertCameraWireActions(entMan.GetComponent<WiresComponent>(rmc), "RMC");
                    });
                }
                finally
                {
                    entMan.DeleteEntity(cmu);
                    entMan.DeleteEntity(rmc);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task ExcludedCameraLikeDevicesHaveNoLogicalCameraMembership()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await server.WaitAssertion(() =>
            {
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var factory = server.EntMan.ComponentFactory;
                foreach (var id in new[]
                {
                    "RMCMortarCamera",
                    "RMCMortarKit",
                    "AU14MortarCameraRMC",
                    "AU14MortarKitRMC",
                    "RMCFlareCAS",
                    "RMCAirFlareCAS",
                    "RMCLaserDesignatorTarget",
                })
                {
                    var prototype = prototypes.Index<EntityPrototype>(id);
                    Assert.That(prototype.TryComp<CameraNetworkMemberComponent>(out _, factory), Is.False, id);
                    Assert.That(prototype.TryComp<CameraNetworkReceiverComponent>(out _, factory), Is.False, id);
                    Assert.That(prototype.TryComp<RMCCameraComputerComponent>(out _, factory), Is.False, id);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    private static void AssertMember(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id,
        IEnumerable<string> networks,
        CameraSourceKinds sourceKinds,
        bool marker)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);
        Assert.That(prototype.TryComp<CameraNetworkMemberComponent>(out var member, factory), Is.True, id.ToString());
        Assert.That(member!.Networks.Select(network => network.Id), Is.EquivalentTo(networks), id.ToString());
        Assert.That(member.SourceKinds, Is.EqualTo(sourceKinds), id.ToString());
        Assert.That(prototype.TryComp<CameraMapMarkerComponent>(out _, factory), Is.EqualTo(marker), id.ToString());
    }

    private static void AssertReceiver(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id,
        IEnumerable<string> networks,
        CameraSourceKinds sourceKinds)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);
        Assert.That(prototype.TryComp<CameraNetworkReceiverComponent>(out var receiver, factory), Is.True, id.ToString());
        Assert.That(receiver!.Networks.Select(network => network.Id), Is.EquivalentTo(networks), id.ToString());
        Assert.That(receiver.SupportedSources, Is.EqualTo(sourceKinds), id.ToString());
    }

    private static void AssertAnchored(IPrototypeManager prototypes, IComponentFactory factory, EntProtoId id)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);
        Assert.That(prototype.TryComp<TransformComponent>(out var transform, factory), Is.True, id.ToString());
        Assert.That(transform!.Anchored, Is.True, id.ToString());
    }

    private static void AssertSurveillanceCamera(IPrototypeManager prototypes, IComponentFactory factory, EntProtoId id)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);
        Assert.That(prototype.TryComp<SurveillanceCameraComponent>(out var camera, factory), Is.True, id.ToString());
        Assert.That(camera!.NetworkSet, Is.False, id.ToString());
        Assert.That(camera.AvailableNetworks.Count, Is.GreaterThan(1), id.ToString());
    }

    private static void AssertCameraWires(IPrototypeManager prototypes, IComponentFactory factory, EntProtoId id)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);
        Assert.That(prototype.TryComp<WiresPanelComponent>(out _, factory), Is.True, $"{id}: wires panel");
        Assert.That(prototype.TryComp<WiresComponent>(out var wires, factory), Is.True, $"{id}: wires component");
        Assert.That(wires!.LayoutId, Is.EqualTo("SurveillanceCamera"), $"{id}: camera wire layout");
    }

    private static void AssertCameraDoesNotRequirePower(IPrototypeManager prototypes, IComponentFactory factory, EntProtoId id)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);
        Assert.That(prototype.TryComp<ApcPowerReceiverComponent>(out var power, factory), Is.True,
            $"{id}: APC power receiver");
        Assert.That(power!.NeedsPower, Is.False, $"{id}: does not require APC power");
    }

    private static void AssertCameraRequiresPower(IPrototypeManager prototypes, IComponentFactory factory, EntProtoId id)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);
        Assert.That(prototype.TryComp<ApcPowerReceiverComponent>(out var power, factory), Is.True,
            $"{id}: APC power receiver");
        Assert.That(power!.NeedsPower, Is.True, $"{id}: requires APC power");
    }

    private static void AssertCameraWireActions(WiresComponent wires, string label)
    {
        Assert.That(wires.WiresList, Has.One.With.Property("Action").InstanceOf<PowerWireAction>(), $"{label}: power wire");
        Assert.That(wires.WiresList, Has.One.With.Property("Action").InstanceOf<AiVisionWireAction>(), $"{label}: AI vision wire");
        Assert.That(wires.WiresList, Has.One.With.Property("Action").InstanceOf<CameraMapVisibilityWireAction>(),
            $"{label}: map wire");
    }

    private static void AssertNoCameraNetworkComponents(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);
        Assert.That(prototype.TryComp<CameraNetworkMemberComponent>(out _, factory), Is.False, id.ToString());
        Assert.That(prototype.TryComp<CameraNetworkReceiverComponent>(out _, factory), Is.False, id.ToString());
    }

    private static void AssertNetworksResolve(
        IPrototypeManager prototypes,
        EntityPrototype prototype,
        IEnumerable<ProtoId<CameraNetworkPrototype>> networks)
    {
        foreach (var network in networks)
            Assert.That(prototypes.HasIndex<CameraNetworkPrototype>(network.Id), Is.True, $"{prototype.ID}: {network.Id}");
    }

    private static void AssertReceiverSharesMemberNetwork(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId receiverId,
        EntProtoId memberId)
    {
        var receiverPrototype = prototypes.Index<EntityPrototype>(receiverId);
        var memberPrototype = prototypes.Index<EntityPrototype>(memberId);
        Assert.That(receiverPrototype.TryComp<CameraNetworkReceiverComponent>(out var receiver, factory), Is.True, receiverId.ToString());
        Assert.That(memberPrototype.TryComp<CameraNetworkMemberComponent>(out var member, factory), Is.True, memberId.ToString());
        Assert.That(receiver!.Networks.Overlaps(member!.Networks), Is.True, $"{receiverId} cannot access {memberId}");
    }
}
