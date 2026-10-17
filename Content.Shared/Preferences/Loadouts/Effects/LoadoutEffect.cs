using System.Diagnostics.CodeAnalysis;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences.Loadouts.Effects;

[ImplicitDataDefinitionForInheritors]
public abstract partial class LoadoutEffect
{
    /// <summary>
    /// Tries to validate the effect.
    /// </summary>
    public abstract bool Validate(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        ICommonSession? session,
        IDependencyCollection collection,
        [NotNullWhen(false)] out FormattedMessage? reason);

    public virtual void Apply(RoleLoadout loadout) {}

    /// <summary>
    /// Applies gameplay state from this effect when the selected loadout is equipped.
    /// </summary>
    public virtual void ApplyToEntity(EntityUid entity, IEntityManager entityManager, IPrototypeManager prototypeManager) {}
}
