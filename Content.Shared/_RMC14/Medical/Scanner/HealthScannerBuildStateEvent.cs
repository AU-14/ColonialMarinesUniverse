namespace Content.Shared._RMC14.Medical.Scanner;

[ByRefEvent]
public record struct HealthScannerBuildStateEvent(
    EntityUid Scanner,
    EntityUid Patient,
    EntityUid? Examiner,
    HealthScannerBuiState State);
