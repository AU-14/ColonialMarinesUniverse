namespace Content.Shared.Throwing;

/// <summary>
/// Overrides the normal hand throw range for a specific held item.
/// </summary>
[RegisterComponent]
public sealed partial class ItemThrowRangeComponent : Component
{
    [DataField]
    public float Range = 8f;
}
