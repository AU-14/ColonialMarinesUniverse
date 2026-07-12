using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

/// <summary>
///     Server-only scanner anatomy projection shared by all viewers that request
///     the same patient during one simulation tick.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUBodyScannerReadoutSystem), typeof(CMUBodyScannerCalibrationSystem))]
public sealed partial class CMUBodyScannerAnatomyCacheComponent : Component
{
    internal readonly List<CMUBodyScannerScanLine> Lines = new();
    internal List<CMUBodyScannerPuzzleSignal> PuzzleSignals = new();
    internal List<CMUBodyScannerSliceSignal> PuzzleTargets = new();
    internal GameTick BuiltAt;
    internal GameTick PuzzleBuiltAt;
    internal uint MedicalRevision;
    internal uint PuzzleMedicalRevision;
    internal bool Valid;
    internal bool PuzzleValid;
}
