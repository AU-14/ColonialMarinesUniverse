namespace Content.Shared.Weapons.Ranged.Components;

public sealed partial class BallisticAmmoProviderComponent
{
    /// <summary>
    /// Delay for directly loading one round into this provider.
    /// </summary>
    [DataField]
    public TimeSpan InsertDelay = TimeSpan.Zero;

    /// <summary>
    /// Delay for manually cycling this provider.
    /// </summary>
    [DataField]
    public TimeSpan CycleDelay = TimeSpan.Zero;
}
