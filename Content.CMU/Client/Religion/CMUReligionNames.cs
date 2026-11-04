using Content.Shared.CMU14.Religion;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Religion;

public static class CMUReligionNames
{
    /// <summary>
    ///     A record's religion for display; records from before religions read as not available.
    /// </summary>
    public static string Describe(IPrototypeManager prototypes, string? religion)
    {
        return religion != null && prototypes.TryIndex<CMUReligionPrototype>(religion, out var proto)
            ? Loc.GetString(proto.Name)
            : Loc.GetString("generic-not-available-shorthand");
    }
}
