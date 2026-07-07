using Content.Client.Administration.Managers;
using Robust.Shared.Console;

namespace Content.Client.Vehicle;

public sealed partial class VehicleOverlayCommands : EntitySystem
{
    [Dependency] private IClientAdminManager _adminManager = default!;
    [Dependency] private IConsoleHost _console = default!;
    [Dependency] private GridVehicleMoverSystem _vehicleMover = default!;

    public override void Initialize()
    {
        _console.RegisterCommand("rmc_vehicle_debug", ToggleDebug);
        _console.RegisterCommand("rmc_vehicle_hardpoints", ToggleHardpoints);
        _console.RegisterCommand("rmc_vehicle_collision", ToggleCollision);
        _console.RegisterCommand("rmc_vehicle_movement", ToggleMovement);
    }

    public override void Shutdown()
    {
        _console.UnregisterCommand("rmc_vehicle_debug");
        _console.UnregisterCommand("rmc_vehicle_hardpoints");
        _console.UnregisterCommand("rmc_vehicle_collision");
        _console.UnregisterCommand("rmc_vehicle_movement");
    }

    private bool CheckAdmin(IConsoleShell shell)
    {
        if (_adminManager.IsAdmin())
            return true;

        shell.WriteError("You must be an admin to use this command.");
        return false;
    }

    private void ToggleDebug(IConsoleShell shell, string argstr, string[] args)
    {
        if (!CheckAdmin(shell))
            return;

        var enabled = _vehicleMover.ToggleDebugOverlay();
        shell.WriteLine($"Vehicle debug overlay {(enabled ? "enabled" : "disabled")}.");
    }

    private void ToggleHardpoints(IConsoleShell shell, string argstr, string[] args)
    {
        if (!CheckAdmin(shell))
            return;

        var enabled = _vehicleMover.ToggleHardpointOverlay();
        shell.WriteLine($"Vehicle hardpoint overlay {(enabled ? "enabled" : "disabled")}.");
    }

    private void ToggleCollision(IConsoleShell shell, string argstr, string[] args)
    {
        if (!CheckAdmin(shell))
            return;

        var enabled = _vehicleMover.ToggleCollisionOverlay();
        shell.WriteLine($"Vehicle collision overlay {(enabled ? "enabled" : "disabled")}.");
    }

    private void ToggleMovement(IConsoleShell shell, string argstr, string[] args)
    {
        if (!CheckAdmin(shell))
            return;

        var enabled = _vehicleMover.ToggleMovementOverlay();
        shell.WriteLine($"Vehicle movement overlay {(enabled ? "enabled" : "disabled")}.");
    }
}
