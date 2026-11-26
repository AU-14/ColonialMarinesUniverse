using Robust.Shared.Utility;

namespace Content.Shared.CMU14.DroneOperator;

public static class CMUDroneName
{
    public static bool TryNormalize(string name, int maxLength, out string normalized)
    {
        normalized = FormattedMessage.RemoveMarkupPermissive(name).Trim();
        if (normalized.Length == 0 || normalized.Length > maxLength)
            return false;

        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
                return false;
        }

        return true;
    }
}
