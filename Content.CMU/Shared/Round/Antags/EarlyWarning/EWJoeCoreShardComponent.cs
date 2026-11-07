using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// The part a dead Early Warning Joe drops once. It is the last step of console repair and is
/// consumed by it. One Joe per round means at most one shard exists, so consoles stay assimilated
/// until the Joe dies (design §8.2).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EWJoeCoreShardComponent : Component;
