using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Explosion;

public sealed partial class ExplosionPrototype
{
    /// <summary>
    ///     Fallback structural severance multiplier for blast damage. Individual
    ///     explosive entities can override this with
    ///     <see cref="Content.Shared._CMU14.Medical.Anatomy.BodyParts.SeveranceDamageModifierComponent"/>.
    /// </summary>
    [DataField]
    public float SeveranceMultiplier = 4f;
}
