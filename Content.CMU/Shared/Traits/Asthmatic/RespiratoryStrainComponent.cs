using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Traits.Asthmatic;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RespiratoryStrainComponent : Component
{
    [DataField, AutoNetworkedField]
    public double Current;

    [DataField]
    public double Max = 100;

    [DataField]
    public double SprintGainPerTick = 1.5;

    [DataField]
    public double ArmoredGainMultiplier = 1.1;

    [DataField]
    public double RegenPerTick = 0.84;

    [DataField]
    public double InternalsDecayMultiplier = 2;

    [DataField, AutoNetworkedField]
    public bool Peaked;

    [DataField]
    public double PeakThreshold = 90;

    [DataField]
    public float PeakSpeedModifier = 0.85f;

    [DataField]
    public TimeSpan EffectTime = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan TimeBetweenChecks = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public TimeSpan NextCheck;

    [DataField]
    public double CoughThreshold = 60;

    [DataField]
    public TimeSpan CoughCooldown = TimeSpan.FromSeconds(8);

    [DataField]
    public ProtoId<EmotePrototype> CoughEmote = "Cough";

    [ViewVariables]
    public TimeSpan NextCough;

    [DataField]
    public FixedPoint2 AsphyxiationDamagePerTick = 2;

    [DataField]
    public double WarnThreshold = 45;

    [DataField]
    public TimeSpan WarnMessageCooldown = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public TimeSpan NextWarnMessage;

    [DataField]
    public ProtoId<AlertPrototype> RespiratoryAlert = "CMURespiratoryStrain";
}
