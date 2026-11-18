namespace Content.Shared.CMU14.Items;

/// <summary>
/// Marks a foldable whose <see cref="Content.Shared.IdentityManagement.Components.IdentityBlockerComponent"/>
/// is active in the face-covering position.
/// </summary>
[RegisterComponent]
public sealed partial class FoldableIdentityBlockerComponent : Component
{
    [DataField]
    public bool CoversWhenFolded = true;
}
