namespace Content.Shared.Nutrition.EntitySystems;

/// <summary>
/// Compatibility ordering marker for systems that still reference the former utensil system.
/// Utensil interactions are handled by <see cref="IngestionSystem"/>.
/// </summary>
public sealed partial class UtensilSystem : EntitySystem
{
}
