using System.Linq;
using System.Numerics;
using Content.Client.Kitchen.Visualizers;
using Content.Client.Medical.Cryogenics;
using Content.Client.Power.SMES;
using Content.Client.Wires.Visualizers;
using Content.Server.Kitchen.Components;
using Content.Server.Medical.Components;
using Content.Server.Power.Components;
using Content.Server.Power.SMES;
using Content.Shared._RMC14.Components;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Power;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Medical.Cryogenics;
using Content.Shared.NodeContainer;
using Content.Shared.NodeContainer.NodeGroups;
using Content.Shared.Power;
using Content.Shared.RCD.Components;
using Content.Shared.SubFloor;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using ServerSmesComponent = Content.Server.Power.SMES.SmesComponent;

namespace Content.IntegrationTests._CMU14.HunterShip;

[TestFixture]
public sealed class HunterShipYautjaMachineryTest
{
    [Test]
    public async Task HunterShipYautjaMachineryWrappersUseSourceBackedFunctionalSurfaces()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var server = pair.Server;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;

            foreach (var row in MachineryRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);

                Assert.That(prototype.Parents, Does.Contain(row.FunctionalParent),
                    $"{row.Id} maps CMSS13 {row.SourcePath} to the local gameplay backend.");
                Assert.That(prototype.Parents, Does.Contain(row.YautjaParent),
                    $"{row.Id} also inherits the local Yautja source/static surface for {row.SourcePath}.");
                Assert.That(prototype.Parents, Does.Not.Contain("CMUHunterShipVisualBase"),
                    $"{row.Id} must not remain a generated visual-only placeholder.");
                Assert.That(prototype.Name, Is.EqualTo(row.Name), row.Id);
                Assert.That(prototype.Description, Is.EqualTo(row.Description), row.Id);

                Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, row.Id);
                Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(row.Sprite), row.Id);
                Assert.That(sprite.DrawDepth, Is.EqualTo(row.DrawDepth), row.Id);
                Assert.That(sprite.NoRotation, Is.True, row.Id);
                Assert.That(sprite.EnableDirectionOverride, Is.True, row.Id);
                Assert.That(sprite.DirectionOverride, Is.EqualTo(Direction.South), row.Id);
                Assert.That(Vector2.Distance(sprite.Offset, row.Offset), Is.LessThan(0.001f), row.Id);

                var layers = sprite.AllLayers.ToArray();
                Assert.That(layers, Has.Length.EqualTo(row.States.Length), row.Id);
                Assert.That(layers.Select(layer => layer.RsiState.Name).ToArray(), Is.EqualTo(row.States), row.Id);
                Assert.That(layers[0].Color, Is.EqualTo(row.Color), row.Id);

                Assert.That(prototype.TryGetComponent<IconComponent>(out var icon, factory), Is.True, row.Id);
                var rsiIcon = (SpriteSpecifier.Rsi) icon!.Icon;
                Assert.That(rsiIcon.RsiPath.ToString(), Does.EndWith(row.Sprite.ToString().Replace("/Textures/", string.Empty)),
                    $"{row.Id} icon RSI");
                Assert.That(rsiIcon.RsiState, Is.EqualTo(row.IconState), row.Id);
                Assert.That(prototype.TryGetComponent<GenericVisualizerComponent>(out _, factory), Is.EqualTo(row.HasGenericVisualizer),
                    $"{row.Id} should only keep inherited visualizers on the prototype when runtime state changes can use the imported layer maps.");
                Assert.That(prototype.TryGetComponent<RemoveComponentsComponent>(out _, factory), Is.EqualTo(row.RemovesInheritedVisualizer),
                    $"{row.Id} should strip inherited visualizers at runtime when the imported Hunter Ship state is fixed.");
            }

            var microwave = prototypes.Index<EntityPrototype>(MicrowaveId);
            Assert.That(microwave.TryGetComponent<SpriteComponent>(out var microwaveSprite, factory), Is.True, MicrowaveId);
            Assert.That(microwaveSprite!.LayerMapTryGet(MicrowaveVisualizerLayers.Base, out var baseLayer), Is.True, MicrowaveId);
            Assert.That(baseLayer, Is.EqualTo(0), MicrowaveId);
            Assert.That(microwaveSprite.LayerMapTryGet(MicrowaveVisualizerLayers.BaseUnlit, out var unlitLayer), Is.True, MicrowaveId);
            Assert.That(unlitLayer, Is.EqualTo(1), MicrowaveId);
            Assert.That(microwave.TryGetComponent<GenericVisualizerComponent>(out var microwaveVisualizer, factory), Is.True, MicrowaveId);
            // The Hunter Ship RSI uses the CMSS13 DMM unlit state for all microwave modes.
            Assert.That(microwaveVisualizer!.Visuals[PowerDeviceVisuals.VisualState]["enum.MicrowaveVisualizerLayers.BaseUnlit"]["Idle"].State, Is.EqualTo("mwo"), MicrowaveId);
            Assert.That(microwaveVisualizer.Visuals[PowerDeviceVisuals.VisualState]["enum.MicrowaveVisualizerLayers.BaseUnlit"]["Broken"].State, Is.EqualTo("mwo"), MicrowaveId);
            Assert.That(microwaveVisualizer.Visuals[PowerDeviceVisuals.VisualState]["enum.MicrowaveVisualizerLayers.BaseUnlit"]["Cooking"].State, Is.EqualTo("mwo"), MicrowaveId);
            Assert.That(microwaveVisualizer.Visuals[PowerDeviceVisuals.VisualState]["bloodyunshaded"]["Idle"].Visible, Is.False, MicrowaveId);
            Assert.That(microwaveVisualizer.Visuals[PowerDeviceVisuals.VisualState]["bloodyunshaded"]["Broken"].Visible, Is.False, MicrowaveId);

            var smes = prototypes.Index<EntityPrototype>(SmesBaseId);
            Assert.That(smes.TryGetComponent<SpriteComponent>(out var smesSprite, factory), Is.True, SmesBaseId);
            Assert.That(smesSprite!.LayerMapTryGet(SmesVisualLayers.Charge, out var chargeLayer), Is.True, SmesBaseId);
            Assert.That(chargeLayer, Is.EqualTo(1), SmesBaseId);
            Assert.That(smesSprite.LayerMapTryGet(SmesVisualLayers.Input, out var inputLayer), Is.True, SmesBaseId);
            Assert.That(inputLayer, Is.EqualTo(2), SmesBaseId);
            Assert.That(smesSprite.LayerMapTryGet(SmesVisualLayers.Output, out var outputLayer), Is.True, SmesBaseId);
            Assert.That(outputLayer, Is.EqualTo(3), SmesBaseId);
            Assert.That(smesSprite.LayerMapTryGet(WiresVisualLayers.MaintenancePanel, out var panelLayer), Is.True, SmesBaseId);
            Assert.That(panelLayer, Is.EqualTo(4), SmesBaseId);

            var cryo = prototypes.Index<EntityPrototype>(CryoId);
            Assert.That(cryo.TryGetComponent<SpriteComponent>(out var cryoSprite, factory), Is.True, CryoId);
            Assert.That(cryoSprite!.LayerMapTryGet(CryoPodVisualLayers.Base, out var cryoBase), Is.True, CryoId);
            Assert.That(cryoBase, Is.EqualTo(0), CryoId);
            Assert.That(cryoSprite.LayerMapTryGet(CryoPodVisualLayers.Cover, out var cryoCover), Is.True, CryoId);
            Assert.That(cryoCover, Is.EqualTo(1), CryoId);
        });

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            foreach (var row in MachineryRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);

                Assert.That(prototype.TryGetComponent<RMCMesonsNonviewableComponent>(out _, factory), Is.True,
                    $"{row.Id} inherits the local Yautja structure surface.");
                Assert.That(prototype.TryGetComponent<PhysicsComponent>(out var physics, factory), Is.True, row.Id);
                Assert.That(physics!.BodyType, Is.EqualTo(BodyType.Static), row.Id);
                Assert.That(prototype.TryGetComponent<FixturesComponent>(out var fixtures, factory), Is.True, row.Id);
                if (row.SourcePath == "/obj/structure/machinery/juicer/yautja")
                {
                    Assert.That(prototype.TryGetComponent<RemoveComponentsComponent>(out _, factory), Is.True,
                        $"{row.Id} removes inherited structure collision to preserve CMSS13 density = FALSE.");
                }
                else
                {
                    Assert.That(fixtures!.Fixtures.Values.Any(fixture => fixture.Hard), Is.EqualTo(row.HasHardFixture),
                        $"{row.Id} follows the Hunter Ship DMM collision placement.");
                }
            }

            AssertSmes(prototypes.Index<EntityPrototype>(SmesBaseId), factory);
            AssertSmes(prototypes.Index<EntityPrototype>(SmesOffsetEastId), factory);
            AssertSmes(prototypes.Index<EntityPrototype>(SmesOffsetWestId), factory);

            foreach (var row in MachineryRows().Where(row => row.FunctionalParent == "RMCKitchenReagentGrinder"))
            {
                var grinderPrototype = prototypes.Index<EntityPrototype>(row.Id);
                Assert.That(grinderPrototype.TryGetComponent<ReagentGrinderComponent>(out var grinder, factory), Is.True, row.Id);
                Assert.That(grinder!.StorageMaxEntities, Is.EqualTo(10), row.Id);
                Assert.That(grinderPrototype.TryGetComponent<RMCPowerReceiverComponent>(out var grinderPower, factory), Is.True, row.Id);
                Assert.That(grinderPower!.IdleLoad, Is.EqualTo(row.IdleLoad), row.Id);
                Assert.That(grinderPower.ActiveLoad, Is.EqualTo(row.ActiveLoad), row.Id);
                Assert.That(grinderPower.Channel, Is.EqualTo(RMCPowerChannel.Equipment), row.Id);
                Assert.That(grinderPrototype.TryGetComponent<ApcPowerReceiverComponent>(out var grinderApc, factory), Is.True, row.Id);
                Assert.That(grinderApc!.NeedsPower, Is.False, row.Id);
                Assert.That(grinderApc.Load, Is.Zero, row.Id);
            }

            var microwave = prototypes.Index<EntityPrototype>(MicrowaveId);
            Assert.That(microwave.TryGetComponent<MicrowaveComponent>(out var microwaveComp, factory), Is.True, MicrowaveId);
            Assert.That(microwaveComp!.Capacity, Is.EqualTo(10), MicrowaveId);
            Assert.That(microwave.TryGetComponent<ApcPowerReceiverComponent>(out var microwaveApc, factory), Is.True, MicrowaveId);
            Assert.That(microwaveApc!.NeedsPower, Is.False, MicrowaveId);
            Assert.That(microwaveApc.Load, Is.Zero, MicrowaveId);

            var cryo = prototypes.Index<EntityPrototype>(CryoId);
            Assert.That(cryo.TryGetComponent<CryoPodComponent>(out var cryoPod, factory), Is.True, CryoId);
            Assert.That(cryoPod!.OpenState, Is.EqualTo("pred_cell"), CryoId);
            Assert.That(cryoPod.OnState, Is.EqualTo("pred_cell-on-empty"), CryoId);
            Assert.That(cryoPod.OffState, Is.EqualTo("pred_cell-off-empty"), CryoId);
            Assert.That(cryoPod.CoverOnState, Is.EqualTo("pred_cell-on-occupied"), CryoId);
            Assert.That(cryoPod.CoverOffState, Is.EqualTo("pred_cell-off-occupied"), CryoId);
            Assert.That(cryoPod.EntryDelay, Is.EqualTo(2f), CryoId);
            Assert.That(cryo.TryGetComponent<HealthAnalyzerComponent>(out var health, factory), Is.True, CryoId);
            Assert.That(health!.ScanDelay, Is.EqualTo(TimeSpan.Zero), CryoId);
            Assert.That(cryo.TryGetComponent<ItemSlotsComponent>(out var slots, factory), Is.True, CryoId);
            Assert.That(slots!.Slots.Keys, Does.Contain("beakerSlot"), CryoId);
            Assert.That(cryo.TryGetComponent<ContainerManagerComponent>(out var containers, factory), Is.True, CryoId);
            Assert.That(containers!.Containers.Keys, Does.Contain("scanner-body"), CryoId);
            Assert.That(containers.Containers.Keys, Does.Contain("beakerSlot"), CryoId);
            Assert.That(cryo.TryGetComponent<ApcPowerReceiverComponent>(out var cryoApc, factory), Is.True, CryoId);
            Assert.That(cryoApc!.NeedsPower, Is.False, CryoId);
            Assert.That(cryoApc.Load, Is.Zero, CryoId);

            foreach (var transformerId in TransformerIds)
            {
                var transformer = prototypes.Index<EntityPrototype>(transformerId);
                Assert.That(transformer.TryGetComponent<RMCMesonsNonviewableComponent>(out _, factory), Is.True,
                    $"{transformerId} inherits the local Yautja structure surface.");
                Assert.That(transformer.TryGetComponent<TransformComponent>(out var transform, factory), Is.True, transformerId);
                Assert.That(transform!.Anchored, Is.True, transformerId);
                Assert.That(transformer.TryGetComponent<ApcPowerReceiverComponent>(out _, factory), Is.False,
                    $"{transformerId} is a passive prop, not a powered machine endpoint.");
                Assert.That(transformer.TryGetComponent<RMCPowerReceiverComponent>(out _, factory), Is.False, transformerId);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipCableTerminalsUseCmss13PowerTerminalSurface()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var server = pair.Server;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;
            var spritePath = new ResPath("/Textures/_CMU14/HunterShip/obj/structures/machinery/power.rsi");

            foreach (var row in CableTerminalRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);

                Assert.That(prototype.Parents, Does.Contain("CMUHunterShipPowerTerminal"),
                    $"{row.Id} maps CMSS13 /obj/structure/terminal to a map-scoped source visual plus local power-terminal backend.");
                Assert.That(prototype.Parents, Does.Not.Contain("CMUHunterShipVisualBase"),
                    $"{row.Id} must not regress to a generated visual-only placeholder.");
                Assert.That(prototype.Name, Is.EqualTo("Terminal"), row.Id);
                Assert.That(prototype.Description, Is.EqualTo("It's an underfloor wiring terminal for power equipment."), row.Id);

                Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, row.Id);
                Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(spritePath), row.Id);
                Assert.That(sprite.DrawDepth, Is.EqualTo((int) Content.Shared.DrawDepth.DrawDepth.FloorObjects), row.Id);
                Assert.That(sprite.NoRotation, Is.True, row.Id);
                Assert.That(sprite.SnapCardinals, Is.False, row.Id);
                Assert.That(sprite.EnableDirectionOverride, Is.True, row.Id);
                Assert.That(sprite.DirectionOverride, Is.EqualTo(row.Direction), row.Id);
                Assert.That(Vector2.Distance(sprite.Offset, row.Offset), Is.LessThan(0.001f), row.Id);

                var layers = sprite.AllLayers.ToArray();
                Assert.That(layers, Has.Length.EqualTo(1), row.Id);
                Assert.That(layers[0].RsiState.Name, Is.EqualTo("term"), row.Id);

                Assert.That(prototype.TryGetComponent<IconComponent>(out var icon, factory), Is.True, row.Id);
                var rsiIcon = (SpriteSpecifier.Rsi) icon!.Icon;
                Assert.That(rsiIcon.RsiPath.ToString(), Does.EndWith("_CMU14/HunterShip/obj/structures/machinery/power.rsi"), row.Id);
                Assert.That(rsiIcon.RsiState, Is.EqualTo("term"), row.Id);
            }
        });

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            foreach (var row in CableTerminalRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);

                Assert.That(prototype.TryGetComponent<TransformComponent>(out var transform, factory), Is.True, row.Id);
                Assert.That(transform!.Anchored, Is.True, row.Id);

                Assert.That(prototype.TryGetComponent<PhysicsComponent>(out _, factory), Is.False,
                    $"{row.Id} is an underfloor terminal and must not have a physics body.");
                Assert.That(prototype.TryGetComponent<FixturesComponent>(out _, factory), Is.False,
                    $"{row.Id} must not declare an empty fixture state without physics.");

                Assert.That(prototype.TryGetComponent<SubFloorHideComponent>(out var subfloor, factory), Is.True, row.Id);
                Assert.That(subfloor!.BlockInteractions, Is.False, row.Id);
                Assert.That(subfloor.BlockAmbience, Is.False, row.Id);

                Assert.That(prototype.TryGetComponent<VisibilityComponent>(out var visibility, factory), Is.True, row.Id);
                Assert.That(visibility!.Layer, Is.EqualTo(1), row.Id);

                Assert.That(prototype.TryGetComponent<RCDDeconstructableComponent>(out var rcd, factory), Is.True, row.Id);
                Assert.That(rcd!.Cost, Is.EqualTo(2), row.Id);
                Assert.That(rcd.Delay, Is.Zero, row.Id);
                Assert.That(rcd.Effect?.Id, Is.EqualTo("EffectRCDConstruct0"), row.Id);

                Assert.That(prototype.TryGetComponent<NodeContainerComponent>(out var nodes, factory), Is.True, row.Id);
                Assert.That(nodes!.Nodes.Keys, Is.EquivalentTo(new[] { "powerHV", "powerMV" }), row.Id);
                Assert.That(nodes.Nodes["powerHV"].NodeGroupID, Is.EqualTo(NodeGroupID.HVPower), row.Id);
                Assert.That(nodes.Nodes["powerMV"].NodeGroupID, Is.EqualTo(NodeGroupID.MVPower), row.Id);
            }
        });

        await pair.CleanReturnAsync();
    }

    private const string SmesBaseId = "CMUHunterShipPlacedCMSMESBasicSmesSouth";
    private const string SmesOffsetEastId = "CMUHunterShipPlacedCMSMESBasicSmesSouthOffset3x1";
    private const string SmesOffsetWestId = "CMUHunterShipPlacedCMSMESBasicSmesSouthOffsetNeg3x1";
    private const string ProcessorId = "CMUHunterShipPlacedRMCKitchenReagentGrinderProcessorSouthOffset0x5";
    private const string MicrowaveId = "CMUHunterShipPlacedCMMicrowaveLockedPoweredMwSouthOffset0x10";
    private const string CryoId = "CMUHunterShipPlacedCryoPodPredCellSouthOffset1x16";
    private const string JuicerOffsetEastId = "CMUHunterShipPlacedRMCKitchenReagentGrinderJuicer1SouthOffset5x6";
    private const string JuicerOffsetWestId = "CMUHunterShipPlacedRMCKitchenReagentGrinderJuicer1SouthOffsetNeg6x6";
    private const string JuicerOffsetNorthWestId = "CMUHunterShipPlacedRMCKitchenReagentGrinderJuicer1SouthOffsetNeg6x7";
    private const string JuicerOffsetFarNorthWestId = "CMUHunterShipPlacedRMCKitchenReagentGrinderJuicer1SouthOffsetNeg9x16";

    private static readonly string[] TransformerIds =
    [
        "CMUHunterShipPlacedCMUHunterShipYautjaPassivePowerTransformerTransformerSouthOffset16x16",
    ];

    private static void AssertSmes(EntityPrototype prototype, IComponentFactory factory)
    {
        Assert.That(prototype.TryGetComponent<ServerSmesComponent>(out var smes, factory), Is.True, prototype.ID);
        Assert.That(smes!.StaticOverlayStates, Is.True, prototype.ID);
        Assert.That(prototype.TryGetComponent<BatteryComponent>(out var battery, factory), Is.True, prototype.ID);
        Assert.That(battery!.MaxCharge, Is.EqualTo(8000000f), prototype.ID);
        Assert.That(battery.CurrentCharge, Is.EqualTo(8000000f), prototype.ID);
        Assert.That(prototype.TryGetComponent<PowerMonitoringDeviceComponent>(out var monitoring, factory), Is.True, prototype.ID);
        Assert.That(monitoring!.SpritePath, Is.EqualTo("_CMU14/HunterShip/obj/structures/machinery/yautja_machines.rsi"), prototype.ID);
        Assert.That(monitoring.SpriteState, Is.EqualTo("smes"), prototype.ID);
    }

    private static MachineryRow[] MachineryRows()
    {
        var yautjaMachines = new ResPath("/Textures/_CMU14/HunterShip/obj/structures/machinery/yautja_machines.rsi");
        var cryo = new ResPath("/Textures/_CMU14/HunterShip/obj/structures/machinery/cryogenics2.rsi");
        var transformer = new ResPath("/Textures/_CMU14/HunterShip/obj/structures/props/industrial/power_transformer.rsi");

        return
        [
            new MachineryRow(
                SmesBaseId,
                "CMSMESBasic",
                "CMUYautjaStructureYautjaMachinesSmes",
                "/obj/structure/machinery/power/smes/magical/yautja",
                "Yautja Energy Core",
                "A highly advanced power source of Yautja design, utilizing unknown technology to generate and distribute energy efficiently throughout the vessel.",
                yautjaMachines,
                "smes",
                ["smes", "smes", "smes", "smes", "smes"],
                Vector2.Zero,
                Color.White),
            new MachineryRow(
                SmesOffsetEastId,
                "CMSMESBasic",
                "CMUYautjaStructureYautjaMachinesSmes",
                "/obj/structure/machinery/power/smes/magical/yautja",
                "Yautja Energy Core",
                "A highly advanced power source of Yautja design, utilizing unknown technology to generate and distribute energy efficiently throughout the vessel.",
                yautjaMachines,
                "smes",
                ["smes", "smes", "smes", "smes", "smes"],
                new Vector2(0.09375f, 0.03125f),
                Color.White),
            new MachineryRow(
                SmesOffsetWestId,
                "CMSMESBasic",
                "CMUYautjaStructureYautjaMachinesSmes",
                "/obj/structure/machinery/power/smes/magical/yautja",
                "Yautja Energy Core",
                "A highly advanced power source of Yautja design, utilizing unknown technology to generate and distribute energy efficiently throughout the vessel.",
                yautjaMachines,
                "smes",
                ["smes", "smes", "smes", "smes", "smes"],
                new Vector2(-0.09375f, 0.03125f),
                Color.White),
            new MachineryRow(
                JuicerOffsetEastId,
                "RMCKitchenReagentGrinder",
                "CMUYautjaStructureYautjaMachinesJuicer1",
                "/obj/structure/machinery/juicer/yautja",
                "Bone grinder",
                "A functional object aboard the Yautja Hunter Ship.",
                yautjaMachines,
                "juicer1",
                ["juicer1"],
                new Vector2(0.15625f, 0.1875f),
                Color.White,
                true,
                true,
                false,
                5,
                100),
            new MachineryRow(
                JuicerOffsetWestId,
                "RMCKitchenReagentGrinder",
                "CMUYautjaStructureYautjaMachinesJuicer1",
                "/obj/structure/machinery/juicer/yautja",
                "Bone grinder",
                "A functional object aboard the Yautja Hunter Ship.",
                yautjaMachines,
                "juicer1",
                ["juicer1"],
                new Vector2(-0.1875f, 0.1875f),
                Color.White,
                true,
                true,
                false,
                5,
                100),
            new MachineryRow(
                JuicerOffsetNorthWestId,
                "RMCKitchenReagentGrinder",
                "CMUYautjaStructureYautjaMachinesJuicer1",
                "/obj/structure/machinery/juicer/yautja",
                "Bone grinder",
                "A functional object aboard the Yautja Hunter Ship.",
                yautjaMachines,
                "juicer1",
                ["juicer1"],
                new Vector2(-0.1875f, 0.21875f),
                Color.White,
                true,
                true,
                false,
                5,
                100),
            new MachineryRow(
                JuicerOffsetFarNorthWestId,
                "RMCKitchenReagentGrinder",
                "CMUYautjaStructureYautjaMachinesJuicer1",
                "/obj/structure/machinery/juicer/yautja",
                "Bone grinder",
                "A functional object aboard the Yautja Hunter Ship.",
                yautjaMachines,
                "juicer1",
                ["juicer1"],
                new Vector2(-0.28125f, 0.5f),
                Color.White,
                true,
                true,
                false,
                5,
                100),
            new MachineryRow(
                ProcessorId,
                "RMCKitchenReagentGrinder",
                "CMUYautjaStructureYautjaMachinesProcessor",
                "/obj/structure/machinery/processor/yautja",
                "Food grinder",
                "A functional object aboard the Yautja Hunter Ship.",
                yautjaMachines,
                "processor",
                ["processor"],
                new Vector2(0f, 0.15625f),
                Color.White,
                true,
                true,
                true,
                5,
                50),
            new MachineryRow(
                MicrowaveId,
                "CMMicrowaveLockedPowered",
                "CMUYautjaStructureYautjaMachinesMw",
                "/obj/structure/machinery/microwave/yautja",
                "Alien microwave",
                "Dark alloy sinister machine that heats up cold food.",
                yautjaMachines,
                "mw",
                ["mw", "mwo", "mwbloody0", "mwbloodyo"],
                new Vector2(0f, 0.3125f),
                Color.White,
                true),
            new MachineryRow(
                CryoId,
                "CryoPod",
                "CMUYautjaStructureCryogenics2PredCell",
                "/obj/structure/machinery/cryo_cell/yautja",
                "Cryo cell",
                "A donation from the old A.W. project, using cryogenic technology. It slowly heals whoever is inside the tube.",
                cryo,
                "pred_cell",
                ["pred_cell", "pred_cell-on-occupied", "pred_cell-off-empty"],
                new Vector2(0.03125f, 0.5f),
                Color.White,
                DrawDepth: (int) Content.Shared.DrawDepth.DrawDepth.Mobs),
            new MachineryRow(
                "CMUHunterShipPlacedCMUHunterShipYautjaPassivePowerTransformerTransformerSouthOffset16x16",
                "CMUHunterShipYautjaPassivePowerTransformer",
                "CMUHunterShipYautjaPassivePowerTransformer",
                "/obj/structure/prop/power_transformer",
                "Alien machinery",
                "A passive electrical component that controls where and which circuits power flows into.",
                transformer,
                "transformer",
                ["transformer"],
                new Vector2(0.5f, 0.5f),
                Color.FromHex("#c0baae"),
                false,
                true),
        ];
    }

    private static CableTerminalRow[] CableTerminalRows()
    {
        return
        [
            new(
                "CMUHunterShipPlacedCableTerminalTermNorthOffset3x0",
                Direction.North,
                new Vector2(3f / 32f, 0f)),
            new(
                "CMUHunterShipPlacedCableTerminalTermNorthOffsetNeg3x0",
                Direction.North,
                new Vector2(-3f / 32f, 0f)),
            new(
                "CMUHunterShipPlacedCableTerminalTermSouthOffset3x0",
                Direction.South,
                new Vector2(3f / 32f, 0f)),
            new(
                "CMUHunterShipPlacedCableTerminalTermSouthOffsetNeg3x0",
                Direction.South,
                new Vector2(-3f / 32f, 0f)),
        ];
    }

    private readonly record struct MachineryRow(
        string Id,
        string FunctionalParent,
        string YautjaParent,
        string SourcePath,
        string Name,
        string Description,
        ResPath Sprite,
        string IconState,
        string[] States,
        Vector2 Offset,
        Color Color,
        bool HasGenericVisualizer = false,
        bool RemovesInheritedVisualizer = false,
        bool HasHardFixture = true,
        int IdleLoad = 0,
        int ActiveLoad = 0,
        int DrawDepth = (int) Content.Shared.DrawDepth.DrawDepth.SmallObjects);

    private readonly record struct CableTerminalRow(
        string Id,
        Direction Direction,
        Vector2 Offset);
}
