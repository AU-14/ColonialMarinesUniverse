using Content.Shared.Alert;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Traits.SubstanceAddiction;

/// <summary>
/// Addiction trait in the style of nicotine addiction: go too long without the substance and the character starts
/// craving it, then shaking. Any listed reagent (or any alcohol, if set) in the bloodstream
/// satisfies the craving. Used by the alcoholic and drug addict traits.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUSubstanceAddictionComponent : Component
{
    /// <summary>Reagents that satisfy the craving.</summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> Reagents = new();

    /// <summary>Whether any alcoholic reagent satisfies the craving.</summary>
    [DataField]
    public bool AnyAlcohol;

    [DataField, AutoNetworkedField]
    public TimeSpan LastUsed;

    [DataField]
    public TimeSpan CravingThreshold = TimeSpan.FromMinutes(20);

    [DataField]
    public TimeSpan ShakeThreshold = TimeSpan.FromMinutes(30);

    [DataField, AutoNetworkedField]
    public bool Craving;

    [DataField(required: true)]
    public ProtoId<AlertPrototype> CravingAlert;

    [DataField(required: true)]
    public LocId OnsetMessage;

    [DataField(required: true)]
    public LocId CravingMessage;

    [DataField(required: true)]
    public LocId ShakeMessage;

    [DataField(required: true)]
    public LocId SatisfiedMessage;

    [DataField]
    public TimeSpan CravingMessageCooldown = TimeSpan.FromSeconds(90);

    [ViewVariables]
    public TimeSpan NextCravingMessage;

    [DataField]
    public TimeSpan ShakeIntervalMin = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan ShakeIntervalMax = TimeSpan.FromSeconds(90);

    [DataField, AutoNetworkedField]
    public TimeSpan NextShake;

    [DataField]
    public TimeSpan ShakeDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan TimeBetweenChecks = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public TimeSpan NextCheck;
}
