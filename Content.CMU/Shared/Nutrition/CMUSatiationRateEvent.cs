namespace Content.Shared.CMU14.Nutrition;

/// <summary>
///     Raised on an entity when one of its satiations, like hunger or thirst, works out how fast it changes.
///     <see cref="Rate"/> is per second and negative while it runs down; traits can scale it.
/// </summary>
[ByRefEvent]
public record struct CMUSatiationRateEvent(string Type, float Rate);
