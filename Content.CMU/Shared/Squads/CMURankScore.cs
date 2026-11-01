using System.Text.RegularExpressions;

namespace Content.Shared.CMU14.Squads;

/// <summary>
/// Compares ranks across every faction's rank set by paygrade.
/// </summary>
public static class CMURankScore
{
    // Paygrades look like "E4", "O-6", "W2" or "E9E" across every faction's rank set.
    private static readonly Regex PaygradeRegex = new(@"^([EWO])-?(\d+)(\w*)$", RegexOptions.Compiled);

    /// <summary>
    /// Orders ranks enlisted, then warrant, then officer, then by grade. A missing or unreadable
    /// paygrade sorts lowest.
    /// </summary>
    public static int FromPaygrade(string? paygrade)
    {
        if (paygrade == null)
            return -1;

        var match = PaygradeRegex.Match(paygrade);
        if (!match.Success)
            return -1;

        var tier = match.Groups[1].Value switch
        {
            "W" => 1,
            "O" => 2,
            _ => 0,
        };

        return tier * 1000 + int.Parse(match.Groups[2].Value) * 10 + (match.Groups[3].Length > 0 ? 1 : 0);
    }
}
