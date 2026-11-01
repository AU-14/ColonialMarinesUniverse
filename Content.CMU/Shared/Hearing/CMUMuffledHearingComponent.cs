using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Hearing;

/// <summary>
///     On a player who is unconscious (critical, knocked out or under anesthesia). They are not deaf:
///     they catch scraps of speech from right beside them, and every sound reaches them quiet and
///     drowned in echo. Kept up to date by the server.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUMuffledHearingComponent : Component
{
    /// <summary>
    ///     Speakers further away than this, in tiles, are not heard at all.
    /// </summary>
    public const float Range = 4f;

    /// <summary>
    ///     The chance of each spoken word getting through.
    /// </summary>
    public const float WordChance = 0.3f;
}
