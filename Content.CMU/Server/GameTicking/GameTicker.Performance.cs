using Content.Server.CMU14.Diagnostics.Performance;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [Dependency] private ICMUServerPerformanceDiagnostics _cmuPerformance = default!;
}
