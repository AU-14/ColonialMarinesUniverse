using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Roles;

/// <summary>
/// Enforces <see cref="CMUMilitaryHeightStandardComponent"/> on job selection: height and age.
/// </summary>
public static class CMUMilitaryHeightRequirement
{
    private static readonly Regex HeightRegex = new(@"^(\d)'(\d{1,2})$", RegexOptions.Compiled);

    public static bool Check(
        JobPrototype job,
        IComponentFactory factory,
        HumanoidCharacterProfile? profile,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = null;

        // Ghost roles and the like have no profile to check.
        if (profile == null ||
            !job.RoundComponents.TryGetValue(factory.GetComponentName<CMUMilitaryHeightStandardComponent>(), out var entry) ||
            entry.Component is not CMUMilitaryHeightStandardComponent standard)
        {
            return true;
        }

        // Collect every rule the character breaks, one line each, so the player sees everything to change at once.
        var lines = new List<string>();

        if (profile.Age > standard.MaxAge)
            lines.Add(Loc.GetString("cmu-role-military-age", ("max", standard.MaxAge)));

        if (TryGetInches(profile.Height, out var inches) &&
            (inches < standard.MinInches || inches > standard.MaxInches))
        {
            lines.Add(Loc.GetString("cmu-role-military-height",
                ("min", FormatHeight(standard.MinInches)), ("max", FormatHeight(standard.MaxInches)),
                ("closest", FormatHeight(Math.Clamp(inches, standard.MinInches, standard.MaxInches)))));
        }

        if (lines.Count == 0)
            return true;

        reason = FormattedMessage.FromMarkupPermissive(string.Join('\n', lines));
        return false;
    }

    private static string FormatHeight(int inches)
    {
        return $"{inches / 12}'{inches % 12}\"";
    }

    private static bool TryGetInches(string height, out int inches)
    {
        inches = 0;
        var match = HeightRegex.Match(height);
        if (!match.Success)
            return false;

        inches = int.Parse(match.Groups[1].Value) * 12 + int.Parse(match.Groups[2].Value);
        return true;
    }
}
