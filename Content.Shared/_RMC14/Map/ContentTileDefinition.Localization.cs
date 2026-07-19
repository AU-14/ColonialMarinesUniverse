using Robust.Shared.Localization;

namespace Content.Shared.Maps;

public sealed partial class ContentTileDefinition
{
    /// <summary>
    /// Gets the localized tile name, or the legacy literal name when the prototype does not contain a localization ID.
    /// </summary>
    public string LocalizedName => Loc.TryGetString(Name, out var localized) ? localized : Name;
}
