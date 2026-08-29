using Robust.Shared.Localization;

namespace Content.Client.Construction;

internal static class ConstructionLocalization
{
    public static string LocalizeOrRaw(string value)
    {
        return Loc.TryGetString(value, out var localized) ? localized : value;
    }
}
