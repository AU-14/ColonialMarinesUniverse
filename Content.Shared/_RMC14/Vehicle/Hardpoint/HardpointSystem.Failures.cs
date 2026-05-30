using Content.Shared.Tools;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    [Dependency] private IRobustRandom _random = default!;

    private readonly record struct VehicleHardpointFailureRepairStep(
        ProtoId<ToolQualityPrototype> Tool,
        float Time,
        string Instruction,
        bool RequiresWelder = false);

    private static readonly VehicleHardpointFailureRepairStep[] ArmorCompromisedRepairSteps =
    {
        new("Anchoring", 4f, "Tighten the armor fasteners and clamp the plate into alignment."),
        new("Welding", 8f, "Weld and patch the breached armor seams.", true),
    };

    private static readonly VehicleHardpointFailureRepairStep[] FeedJamRepairSteps =
    {
        new("Screwing", 4f, "Open the feed cover and clear bent belt links."),
        new("Pulsing", 5f, "Cycle the feed actuator with a multitool."),
    };

    private static readonly VehicleHardpointFailureRepairStep[] RunawayTriggerRepairSteps =
    {
        new("Screwing", 5f, "Open the trigger housing and isolate the worn sear linkage."),
        new("Pulsing", 6f, "Reset the fire-control relay with a multitool."),
        new("Anchoring", 5f, "Re-seat and tighten the trigger linkage."),
    };

    private static readonly VehicleHardpointFailureRepairStep[] TurretTraverseRepairSteps =
    {
        new("Anchoring", 6f, "Tighten and re-index the traverse ring."),
        new("VehicleServicing", 5f, "Jack the turret bearing clear and re-seat the ring."),
    };

    private static readonly VehicleHardpointFailureRepairStep[] EngineMisfireRepairSteps =
    {
        new("Screwing", 4f, "Open the engine access panel."),
        new("Pulsing", 6f, "Pulse the ignition control circuit with a multitool."),
        new("Anchoring", 4f, "Tighten the engine mounts after the circuit stabilizes."),
    };

    private static readonly VehicleHardpointFailureRepairStep[] TransmissionSlipRepairSteps =
    {
        new("VehicleServicing", 7f, "Lift and re-seat the drivetrain with a maintenance jack."),
        new("Anchoring", 5f, "Tighten the transmission housing bolts."),
    };

    private static readonly VehicleHardpointFailureRepairStep[] WarpedFrameRepairSteps =
    {
        new("VehicleServicing", 8f, "Jack the frame and relieve pressure from the warped section."),
        new("Welding", 12f, "Heat and straighten the warped frame members with a welder.", true),
        new("Anchoring", 6f, "Re-torque the frame braces."),
    };

    private static readonly VehicleHardpointFailureRepairStep[] DamagedMountRepairSteps =
    {
        new("VehicleServicing", 6f, "Jack the hardpoint clear of the damaged mount."),
        new("Anchoring", 6f, "Re-seat and tighten the mount locking hardware."),
    };

    private static readonly VehicleHardpointFailureRepairStep[] TireBlowoutRepairSteps =
    {
        new("Prying", 5f, "Pry the shredded tire casing clear of the rim."),
        new("VehicleServicing", 6f, "Jack the hub up and seat a replacement wheel assembly."),
        new("Anchoring", 5f, "Torque the wheel lugs down in sequence."),
    };

    private static readonly VehicleHardpointFailureRepairStep[] ThrownTreadRepairSteps =
    {
        new("VehicleServicing", 8f, "Jack the running gear up and take tension off the tread."),
        new("Prying", 6f, "Pry the thrown tread links back onto the road wheels."),
        new("Anchoring", 8f, "Lock the tensioner and torque the tread pins."),
    };

    private static readonly VehicleHardpointFailureRepairStep[] EngineOverheatRepairSteps =
    {
        new("Screwing", 4f, "Open the engine shroud and vent trapped heat."),
        new("Prying", 5f, "Pry the warped fan guard away from the radiator."),
        new("Pulsing", 6f, "Pulse the coolant pump controller until flow stabilizes."),
    };

    private static readonly VehicleHardpointFailureRepairStep[] ElectricalShortRepairSteps =
    {
        new("Cutting", 5f, "Cut away the burned wiring from the hardpoint harness."),
        new("Pulsing", 6f, "Trace and reset the control circuit with a multitool."),
        new("Screwing", 4f, "Close the access panel and secure the replacement harness."),
    };

    private static readonly VehicleHardpointFailureRepairStep[] FuelLeakRepairSteps =
    {
        new("Screwing", 4f, "Open the fuel service panel and isolate the ruptured line."),
        new("Welding", 7f, "Patch the leaking fuel line.", true),
        new("Anchoring", 4f, "Tighten the fuel line coupling."),
    };
}
