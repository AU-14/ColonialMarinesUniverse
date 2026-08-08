namespace Content.Server.GameTicking;

/// <summary>
/// Raised after round rules finish world setup and immediately before normal player bodies are spawned.
/// </summary>
[ByRefEvent]
public readonly record struct RoundStartPlayerSpawnBatchEvent;

/// <summary>
/// Raised after the synchronous round-start player-body loop, including when that loop throws.
/// </summary>
[ByRefEvent]
public readonly record struct RoundStartPlayerSpawnBatchFinishedEvent;
