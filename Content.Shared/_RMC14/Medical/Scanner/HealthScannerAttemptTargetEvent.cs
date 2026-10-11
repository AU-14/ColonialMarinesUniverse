namespace Content.Shared._RMC14.Medical.Scanner;

[ByRefEvent]
public record struct HealthScannerAttemptTargetEvent(string? Popup = null, bool Cancelled = false, EntityUid? Scanner = null); // CMU14: Scanner identifies the analyzer
