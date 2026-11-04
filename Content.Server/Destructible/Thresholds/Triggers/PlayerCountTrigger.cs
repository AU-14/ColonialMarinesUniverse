using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Destructible;
using Content.Shared.Destructible.Thresholds.Triggers;
using Robust.Server.Player;
using Robust.Shared.IoC;

namespace Content.Server.Destructible.Thresholds.Triggers;

/// <summary>
///     Triggers when the server player count is within the specified range.
/// </summary>
[Serializable]
[DataDefinition]
public sealed partial class PlayerCountTrigger : IThresholdTrigger
{
    /// <summary>
    ///     Minimum players required to trigger. Ignored if null.
    /// </summary>
    [DataField("minPlayers")] public int? MinPlayers;

    /// <summary>
    ///     Maximum players allowed to trigger. Ignored if null.
    /// </summary>
    [DataField("maxPlayers")] public int? MaxPlayers;

    public bool Reached(Entity<DamageableComponent> damageable, SharedDestructibleSystem system)
    {
        var playerManager = IoCManager.Resolve<IPlayerManager>();
        var count = playerManager.PlayerCount;

        if (MinPlayers.HasValue && count < MinPlayers.Value)
            return false;
        if (MaxPlayers.HasValue && count > MaxPlayers.Value)
            return false;

        return true;
    }

    public int CompareTo(IThresholdTrigger? other) => 0;

    public bool Equals(IThresholdTrigger? other)
    {
        return other is PlayerCountTrigger trigger &&
               MinPlayers == trigger.MinPlayers &&
               MaxPlayers == trigger.MaxPlayers;
    }
}
