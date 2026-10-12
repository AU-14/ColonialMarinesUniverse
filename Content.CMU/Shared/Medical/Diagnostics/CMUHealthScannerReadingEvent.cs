using Content.Shared._RMC14.Medical.Scanner;

namespace Content.Shared.CMU14.Medical.Diagnostics;

/// <summary>
/// Raised on the scanned patient while a health scanner builds its state, so CMU systems can add readings
/// without the CMU medical diagnostics gates (CVars, <c>CMUHumanMedicalComponent</c>).
/// </summary>
[ByRefEvent]
public readonly record struct CMUHealthScannerReadingEvent(HealthScannerBuiState State, EntityUid? Examiner);
