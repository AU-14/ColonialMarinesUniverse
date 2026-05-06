using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.StatusEffects;

/// <summary>
///     Sits on a <c>StatusEffectCMUPainSuppression</c> entity. Multiple
///     painkillers are tracked as expiring profiles; the strongest active
///     profile supplies the public resolved fields.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PainSuppressionComponent : Component
{
    [DataField, AutoNetworkedField]
    public float AccumulationSuppression = 0.5f;

    [DataField, AutoNetworkedField]
    public int TierSuppression = 2;

    [DataField, AutoNetworkedField]
    public float DecayBonus = 0.75f;

    [DataField]
    public List<PainSuppressionEntry> ActiveProfiles = new();
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class PainSuppressionEntry
{
    [DataField]
    public float AccumulationSuppression;

    [DataField]
    public int TierSuppression;

    [DataField]
    public float DecayBonus;

    /// <summary>
    ///     Drug profiles compete with each other; non-drug morale/order
    ///     profiles add on top of the strongest drug profile.
    /// </summary>
    [DataField]
    public bool Additive;

    [DataField]
    public TimeSpan ExpiresAt;
}
