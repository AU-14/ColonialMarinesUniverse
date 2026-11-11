using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Roles;

/// <summary>
/// Enforces <see cref="CMUMilitaryHeightStandardComponent"/> on job selection: height first, then weight for that height.
/// </summary>
public static class CMUMilitaryHeightRequirement
{
    private static readonly Regex HeightRegex = new(@"^(\d)'(\d{1,2})$", RegexOptions.Compiled);

    /// <summary>
    /// US Army weight-for-height screening table (AR 600-9, table B-1), in pounds.
    /// Max columns are by age bracket: 17-20, 21-27, 28-39, 40+. The male table has no entries below 5'0",
    /// so males under that height only have the minimum weight to meet, as in the regulation.
    /// </summary>
    private static readonly Dictionary<int, (int Min, int[]? Male, int[] Female)> WeightForHeight = new()
    {
        [58] = (91, null, [119, 121, 122, 124]),
        [59] = (94, null, [124, 125, 126, 128]),
        [60] = (97, [132, 136, 139, 141], [128, 129, 131, 133]),
        [61] = (100, [136, 140, 144, 146], [132, 134, 135, 137]),
        [62] = (104, [141, 144, 148, 150], [136, 138, 140, 142]),
        [63] = (107, [145, 149, 153, 155], [141, 143, 144, 146]),
        [64] = (110, [150, 154, 158, 160], [145, 147, 149, 151]),
        [65] = (114, [155, 159, 163, 165], [150, 152, 154, 156]),
        [66] = (117, [160, 163, 168, 170], [155, 156, 158, 161]),
        [67] = (121, [165, 169, 174, 176], [159, 161, 163, 166]),
        [68] = (125, [170, 174, 179, 181], [164, 166, 168, 171]),
        [69] = (128, [175, 179, 184, 186], [169, 171, 173, 176]),
        [70] = (132, [180, 185, 189, 192], [174, 176, 178, 181]),
        [71] = (136, [185, 189, 194, 197], [179, 181, 183, 186]),
        [72] = (140, [190, 195, 200, 203], [184, 186, 188, 191]),
        [73] = (144, [195, 200, 205, 208], [189, 191, 194, 197]),
        [74] = (148, [201, 206, 211, 214], [194, 197, 199, 202]),
        [75] = (152, [206, 212, 217, 220], [200, 202, 204, 208]),
        [76] = (156, [212, 217, 223, 226], [205, 207, 210, 213]),
        [77] = (160, [218, 223, 229, 232], [210, 213, 215, 219]),
        [78] = (164, [223, 229, 235, 238], [216, 218, 221, 225]),
        [79] = (168, [229, 235, 241, 244], [221, 224, 227, 230]),
        [80] = (173, [234, 240, 247, 250], [227, 230, 233, 236]),
    };

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

        var age = profile.Age;
        if (age > standard.MaxAge)
        {
            lines.Add(Loc.GetString("cmu-role-military-age", ("max", standard.MaxAge)));
            age = standard.MaxAge;
        }

        // An unset height can't be checked, and weight depends on it.
        if (TryGetInches(profile.Height, out var inches))
        {
            var heightOk = inches >= standard.MinInches && inches <= standard.MaxInches;
            if (!heightOk)
            {
                inches = Math.Clamp(inches, standard.MinInches, standard.MaxInches);
                lines.Add(Loc.GetString("cmu-role-military-height",
                    ("min", FormatHeight(standard.MinInches)),
                    ("max", FormatHeight(standard.MaxInches)),
                    ("closest", FormatHeight(inches))));
            }

            // Weight is judged at the height and age the character would have after the fixes above.
            if (standard.WeightStandard &&
                TryGetWeightRange(standard, inches, profile.Sex, age, out var min, out var max) &&
                (profile.Weight < min || profile.Weight > max))
            {
                lines.Add(Loc.GetString("cmu-role-military-weight",
                    ("range", max is { } limit ? $"{min}-{limit}" : $"{min}+"),
                    ("height", FormatHeight(inches))));

                // Only offer other ways out when height and age are otherwise fine.
                if (heightOk && age == profile.Age)
                {
                    var fixes = new List<string>();
                    if (ClosestHeightFor(standard, inches, profile) is { } height)
                        fixes.Add(Loc.GetString("cmu-role-military-fix-height", ("height", FormatHeight(height))));

                    if (max != null && profile.Weight > max && ClosestAgeFor(standard, inches, profile) is { } olderAge)
                        fixes.Add(Loc.GetString("cmu-role-military-fix-age", ("age", olderAge)));

                    if (fixes.Count > 0)
                    {
                        lines.Add(Loc.GetString("cmu-role-military-weight-alternatives",
                            ("fixes", string.Join(Loc.GetString("cmu-role-military-fix-or"), fixes))));
                    }
                }
            }
        }

        if (lines.Count == 0)
            return true;

        reason = FormattedMessage.FromMarkupPermissive(string.Join('\n', lines));
        return false;
    }

    /// <summary>The allowed weight range for a height, sex and age, with leeway. Max is null where the table has none.</summary>
    private static bool TryGetWeightRange(CMUMilitaryHeightStandardComponent standard, int inches, Sex sex, int age,
        out int min, out int? max)
    {
        min = 0;
        max = null;
        if (!WeightForHeight.TryGetValue(inches, out var row))
            return false;

        // The Army table only has male and female columns; anyone else is held to the male one.
        var maxes = sex == Sex.Female ? row.Female : row.Male;
        min = row.Min - standard.MinWeightLeeway;
        max = maxes?[AgeBracket(age)] + standard.MaxWeightLeeway;
        return true;
    }

    private static bool Fits(CMUMilitaryHeightStandardComponent standard, int inches, HumanoidCharacterProfile profile, int age)
    {
        return TryGetWeightRange(standard, inches, profile.Sex, age, out var min, out var max) &&
               profile.Weight >= min && (max == null || profile.Weight <= max);
    }

    /// <summary>The nearest allowed height at which the character's current weight would pass.</summary>
    private static int? ClosestHeightFor(CMUMilitaryHeightStandardComponent standard, int inches, HumanoidCharacterProfile profile)
    {
        for (var offset = 1; offset <= standard.MaxInches - standard.MinInches; offset++)
        {
            foreach (var candidate in new[] { inches - offset, inches + offset })
            {
                if (candidate >= standard.MinInches && candidate <= standard.MaxInches &&
                    Fits(standard, candidate, profile, profile.Age))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>The youngest older age whose bracket allows the character's current weight, if any.</summary>
    private static int? ClosestAgeFor(CMUMilitaryHeightStandardComponent standard, int inches, HumanoidCharacterProfile profile)
    {
        foreach (var start in BracketStarts)
        {
            if (start > profile.Age && start <= standard.MaxAge && Fits(standard, inches, profile, start))
                return start;
        }

        return null;
    }

    private static readonly int[] BracketStarts = [21, 28, 40];

    /// <summary>The table's age brackets: 17-20, 21-27, 28-39 and 40+. Anyone younger uses the youngest.</summary>
    private static int AgeBracket(int age)
    {
        return age switch
        {
            <= 20 => 0,
            <= 27 => 1,
            <= 39 => 2,
            _ => 3,
        };
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
