using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// The part a dead Early Warning Joe drops. It is the last step of console repair and is consumed by
/// it. Rebalance RB-32: the Joe drops one shard for each console it still holds assimilated (at least
/// one), once, so every assimilation can be undone; consoles stay assimilated until the Joe dies or
/// stands down (design §8.2).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EWJoeCoreShardComponent : Component;
