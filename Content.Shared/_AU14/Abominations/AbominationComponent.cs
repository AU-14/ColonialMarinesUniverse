using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Marker on every abomination mob. Also carries the infection chance that
/// every melee hit on a humanoid rolls against, plus the trickle of passive
/// regeneration every abomination gets even when not standing on tendons.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AbominationComponent : Component
{
    /// <summary>
    /// Probability that a successful melee hit on a humanoid applies
    /// <see cref="AbominationInfectionComponent"/>. 0 = never, 1 = every hit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float InfectionChance = 0.2f;

    /// <summary>
    /// How often the passive heal tick fires. Independent of tendons — tendons
    /// provide a much stronger top-up via AbominationFleshKudzu.Heal.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan PassiveHealInterval = TimeSpan.FromSeconds(4);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextPassiveHealAt;

    /// <summary>Damage applied (negative = heal) every PassiveHealInterval.</summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier PassiveHeal = new()
    {
        DamageDict =
        {
            ["Blunt"] = -1,
            ["Slash"] = -1,
            ["Piercing"] = -1,
        },
    };
}
