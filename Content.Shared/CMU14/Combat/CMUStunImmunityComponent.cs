using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Combat;

/// <summary>
///     CMU14: makes a mob immune to the incapacitating effects of specific non-lethal weapons.
///     Taser immunity covers electrified projectiles and charged drive stuns, baton immunity covers
///     stun batons (both melee hits and thrown batons). It does not reduce any other damage.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUStunImmunityComponent : Component
{
    /// <summary>
    ///     Ignore stamina damage from anything tagged <c>Taser</c>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Taser;

    /// <summary>
    ///     Ignore stamina damage from anything tagged <c>Stunbaton</c>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Baton;
}

/// <summary>
///     CMU14: shared lookup for <see cref="CMUStunImmunityComponent"/> so every stamina damage source
///     (projectiles, drive stuns, melee batons, thrown batons) applies the same rules.
/// </summary>
public sealed partial class CMUStunImmunitySystem : EntitySystem
{
    public static readonly ProtoId<TagPrototype> TaserTag = "Taser";
    public static readonly ProtoId<TagPrototype> BatonTag = "Stunbaton";

    [Dependency] private TagSystem _tags = default!;

    /// <summary>
    ///     Whether <paramref name="target"/> is protected against the stun <paramref name="source"/> would apply.
    /// </summary>
    public bool IsImmune(EntityUid target, EntityUid source)
    {
        if (!TryComp<CMUStunImmunityComponent>(target, out var immunity))
            return false;

        if (immunity.Taser && _tags.HasTag(source, TaserTag))
            return true;

        return immunity.Baton && _tags.HasTag(source, BatonTag);
    }
}
