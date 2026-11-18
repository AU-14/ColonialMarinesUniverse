using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Magnetic Resonance (phase E6, A-16; playtest 4; rebalance RB-06): a share of the bullets that strike the
/// Early Warning Joe from the front are turned back, for real. RMC's own reflective component treats an IFF
/// bullet as friendly fire and only zeroes its damage, and every CM gun fires IFF bullets, so the Joe uses
/// this one instead. A reflected bullet keeps its IFF, so it cannot hit the side that fired it. Granted by
/// the level-5 table.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EWJoeResonanceComponent : Component
{
    /// <summary>Chance that a bullet striking the Joe inside <see cref="FrontalArc"/> is reflected (RB-06: 10%).</summary>
    [DataField, AutoNetworkedField]
    public float Chance = 0.1f;

    /// <summary>
    /// RB-06: only bullets arriving from inside this arc, centred on the Joe's facing, can be reflected
    /// (120° = ±60°). Flanking fire always lands.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle FrontalArc = Angle.FromDegrees(120);

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
