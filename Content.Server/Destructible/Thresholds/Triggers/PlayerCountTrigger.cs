using Content.Shared.Damage;
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

    public bool Reached(DamageableComponent damageable, DestructibleSystem system)
    {
        var playerManager = IoCManager.Resolve<IPlayerManager>();
        var count = playerManager.PlayerCount;

        if (MinPlayers.HasValue && count < MinPlayers.Value)
            return false;
        if (MaxPlayers.HasValue && count > MaxPlayers.Value)
            return false;

        return true;
    }
}
