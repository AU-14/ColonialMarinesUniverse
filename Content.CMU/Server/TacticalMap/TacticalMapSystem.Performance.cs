using Content.Server.CMU14.Diagnostics.Performance;

namespace Content.Server._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    [Dependency] private ICMUServerPerformanceDiagnostics _cmuPerformance = default!;
}
