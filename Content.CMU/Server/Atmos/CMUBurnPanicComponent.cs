using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Atmos;

/// <summary>
///     Screams when set on fire and, if the player does not stop, drop and roll on their own,
///     starts rolling for them after a delay until the fire is out.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUBurnPanicSystem))]
public sealed partial class CMUBurnPanicComponent : Component
{
    [DataField]
    public ProtoId<EmotePrototype> Emote = "CMUBurning";

    [DataField]
    public TimeSpan EmoteCooldown = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan ForceRollDelay = TimeSpan.FromSeconds(10);

    [DataField, AutoPausedField]
    public TimeSpan NextEmoteAt;

    [DataField, AutoPausedField]
    public TimeSpan? ForceRollAt;

    [DataField]
    public bool Forcing;
}
