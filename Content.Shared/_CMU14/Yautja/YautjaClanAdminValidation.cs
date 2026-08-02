using System.Linq;

namespace Content.Shared._CMU14.Yautja;

public enum YautjaClanAdminValidationError : byte
{
    None,
    MissingNameOrDescription,
    InvalidColor,
}

public readonly record struct YautjaClanAdminFields(
    string Name,
    string Description,
    string Color);

public static class YautjaClanAdminValidation
{
    public static bool TryNormalize(
        string name,
        string description,
        string color,
        out YautjaClanAdminFields fields,
        out YautjaClanAdminValidationError error)
    {
        var normalizedName = name.Trim();
        var normalizedDescription = description.Trim();
        var normalizedColor = string.IsNullOrWhiteSpace(color) ? "#ffffff" : color.Trim();

        if (normalizedName.Length == 0 || normalizedDescription.Length == 0)
        {
            fields = default;
            error = YautjaClanAdminValidationError.MissingNameOrDescription;
            return false;
        }

        if (normalizedColor.Length != 7 ||
            normalizedColor[0] != '#' ||
            !normalizedColor[1..].All(IsHexDigit))
        {
            fields = default;
            error = YautjaClanAdminValidationError.InvalidColor;
            return false;
        }

        fields = new(normalizedName, normalizedDescription, normalizedColor);
        error = YautjaClanAdminValidationError.None;
        return true;
    }

    private static bool IsHexDigit(char value)
    {
        return value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    }
}
