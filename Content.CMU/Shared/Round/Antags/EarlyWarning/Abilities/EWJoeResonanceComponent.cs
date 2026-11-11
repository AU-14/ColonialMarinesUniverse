using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Magnetic Resonance (phase E6, A-16; playtest 4): a share of the bullets that strike the Early Warning
/// Joe are turned back, for real, including bullets fired with IFF. RMC's own reflective component treats
/// an IFF bullet as friendly fire and only zeroes its damage, and every CM gun fires IFF bullets, so the
/// Joe uses this one instead. Granted by the level-5 table.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EWJoeResonanceComponent : Component
{
    /// <summary>Chance that a bullet striking the Joe is reflected.</summary>
    [DataField, AutoNetworkedField]
    public float Chance = 0.2f;

    /// <summary>Spread of the reflected bullet around the reverse of its course.</summary>
    [DataField, AutoNetworkedField]
    public Angle Angle = Angle.FromDegrees(60);

    /// <summary>Range the reflected bullet keeps.</summary>
    [DataField, AutoNetworkedField]
    public float Range = 10;

    /// <summary>Accuracy set on bullets striking the Joe (as RMC's reflective component does).</summary>
    [DataField, AutoNetworkedField]
    public float Accuracy = 100;

    /// <summary>Damage multiplier of a reflected bullet, per reflection.</summary>
    [DataField, AutoNetworkedField]
    public float ReflectionMultiplier = 0.5f;
}
