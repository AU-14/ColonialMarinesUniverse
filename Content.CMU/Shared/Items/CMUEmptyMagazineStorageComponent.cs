using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Items;

/// <summary>
///     On a storage with FixedItemSizeStorage, empty magazines take up <see cref="EmptySize"/> instead of the fixed slot size.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUEmptyMagazineStorageComponent : Component
{
    [DataField, AutoNetworkedField]
    public Vector2i EmptySize = new(1, 1);

    public Box2i[]? CachedEmptyShape;
}
