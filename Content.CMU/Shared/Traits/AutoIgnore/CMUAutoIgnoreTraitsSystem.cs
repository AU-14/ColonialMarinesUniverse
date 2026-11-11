using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Traits.AutoIgnore;

/// <summary>
///     The "auto-ignore incompatible traits" trait. A character who takes it can join jobs that don't allow some of
///     their traits; those traits are just left off for that job, and everything else applies as normal.
/// </summary>
public sealed class CMUAutoIgnoreTraitsSystem : EntitySystem
{
    public static readonly ProtoId<TraitPrototype> AutoIgnoreTrait = "CMUAutoIgnoreTraits";

    [Dependency] private SharedRoleSystem _roles = default!;

    public static bool HasAutoIgnore(HumanoidCharacterProfile profile)
    {
        return profile.TraitPreferences.Contains(AutoIgnoreTrait);
    }

    /// <summary>
    ///     Whether this trait gets left off because the character auto-ignores traits the job doesn't allow.
    /// </summary>
    public bool IsIgnored(HumanoidCharacterProfile profile, string? job, ProtoId<TraitPrototype> trait)
    {
        if (job == null ||
            !HasAutoIgnore(profile) ||
            _roles.GetRoleRequirements(new ProtoId<JobPrototype>(job)) is not { } requirements)
        {
            return false;
        }

        foreach (var requirement in requirements)
        {
            if (requirement is TraitsRequirement { Inverted: true } traits && traits.Traits.Contains(trait))
                return true;
        }

        return false;
    }
}
