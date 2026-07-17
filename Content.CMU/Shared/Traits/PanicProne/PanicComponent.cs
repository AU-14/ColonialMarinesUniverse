using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Traits.PanicProne;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PanicComponent : Component
{
    [DataField, AutoNetworkedField]
    public double Current;

    [DataField]
    public double Max = 100;

    [DataField]
    public double RegenPerTick = 2;

    [DataField]
    public TimeSpan TimeBetweenChecks = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public TimeSpan NextCheck;

    [DataField, AutoNetworkedField]
    public bool Peaked;

    [DataField]
    public double PeakThreshold = 80;

    [DataField]
    public double ProjectileHitGain = 12;

    [DataField]
    public double SeriousWoundGain = 15;

    [DataField]
    public FixedPoint2 SeriousWoundThreshold = 10;

    [DataField]
    public double ExplosionKnockdownGain = 20;

    [DataField]
    public double NearbyDeathGain = 10;

    [DataField]
    public float NearbyDeathRadius = 8f;

    [DataField]
    public double EmoteThreshold = 80;

    [DataField]
    public TimeSpan EmoteCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public ProtoId<EmotePrototype> Emote = "Scream";

    [ViewVariables]
    public TimeSpan NextEmote;

    [DataField]
    public ProtoId<AlertPrototype> PanicAlert = "CMUPanic";

    [DataField]
    public float SpreadMultiplier = 1.6f;

    [DataField]
    public double WarnThreshold = 40;

    [DataField]
    public TimeSpan WarnMessageCooldown = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public TimeSpan NextWarnMessage;
}
