using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Threats;

/// <summary>Ghost menu state for calling in a random minor third party.</summary>
[Serializable, NetSerializable]
public sealed record GhostThirdPartyCallState(
    bool Available,
    int Dead,
    int Living,
    int RequiredDead,
    int MinutesLeft,
    bool Pending);

/// <summary>Asks the server to call in a random minor third party on the sender's behalf.</summary>
[Serializable, NetSerializable]
public sealed class GhostThirdPartyCallMessage : EuiMessageBase;

public static class GhostThirdPartyCall
{
    /// <summary>
    ///     The dead-count gate. Dead means ghosted or stuck in a dead body. With no living players
    ///     any dead player qualifies, since there is nobody left to abuse the call against.
    /// </summary>
    public static bool MeetsDeadThreshold(int dead, int living, float ratio)
        => living <= 0 ? dead > 0 : dead >= RequiredDead(living, ratio);

    public static int RequiredDead(int living, float ratio)
        => living <= 0 ? 1 : (int) Math.Ceiling(living * Math.Max(0f, ratio) - 1e-3);
}
