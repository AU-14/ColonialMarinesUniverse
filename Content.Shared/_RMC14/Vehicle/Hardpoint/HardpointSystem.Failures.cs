using Content.Shared.Tools;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

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
}
