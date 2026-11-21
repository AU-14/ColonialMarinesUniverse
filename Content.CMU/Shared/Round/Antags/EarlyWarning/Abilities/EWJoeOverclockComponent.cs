using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// A running Overclock (formerly Smokescreen; phase E5, balance A-9, renamed in playtest 3): the Joe
/// moves faster, vents a fresh cloud every <see cref="PuffInterval"/> and builds heat each second.
/// Networked to the owner only, so the Joe's client predicts its own speed and nobody else learns
/// anything from it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EWJoeOverclockComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [DataField]
    public EntProtoId SmokePrototype = "CMUEWJoeSmoke";

    [DataField]
    public TimeSpan PuffInterval = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan NextPuff;

    /// <summary>The Overclock action, shown toggled while the overclock runs. Server bookkeeping.</summary>
    [DataField]
    public EntityUid? Action;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1.5f;

    /// <summary>RB-28: one activation runs at most this long, then throttles back as a toggle off would.</summary>
    [DataField]
    public TimeSpan MaxDuration = TimeSpan.FromSeconds(15);

    /// <summary>When this activation hits <see cref="MaxDuration"/>; set on startup. Server bookkeeping.</summary>
    [DataField]
    public TimeSpan EndsAt;
}
