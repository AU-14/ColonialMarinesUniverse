namespace Content.Shared.Damage;

/// <summary>
/// Keeps legacy damage-result call sites source-compatible while the upstream API uses boolean success checks.
/// </summary>
public sealed partial class DamageSpecifier
{
    public static implicit operator bool(DamageSpecifier? damage)
    {
        return damage is { Empty: false };
    }
}
