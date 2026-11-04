namespace Content.Shared.Access.Components;

/// <summary>Cards bound to this entity, so they can be unbound before its deletion.</summary>
[RegisterComponent]
public sealed partial class OriginalIdCardsComponent : Component
{
    public readonly HashSet<EntityUid> Cards = new();
}
