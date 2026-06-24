using Robust.Shared.Player;

namespace Content.Server._CMU14.Profiling;

[RegisterComponent]
public sealed partial class ServerLogsFollowerComponent : Component
{
    /// <summary>Absolute path to the log file currently being tailed.</summary>
    public string FilePath = string.Empty;

    /// <summary>Where we last read to (file offset in bytes).</summary>
    public long LastPosition;

    public string? Filter;

    /// <summary>The admin session that should receive.</summary>
    [ViewVariables] public ICommonSession? Session;
}
