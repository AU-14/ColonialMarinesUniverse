using System.Collections.Generic;
using Content.Shared._RMC14.Medal;

namespace Content.Shared.CMU14.PlayTimeTracking;

public static class CMUPlaytimeMedals
{
    /// <summary>
    ///     Every medal tier from <paramref name="start"/> down to Bronze, then stops.
    ///     The enum is a byte, so a plain `type >= Bronze; type--` loop never ends.
    /// </summary>
    public static IEnumerable<RMCPlaytimeMedalType> Descending(RMCPlaytimeMedalType start)
    {
        for (var type = start; ; type--)
        {
            yield return type;
            if (type == RMCPlaytimeMedalType.Bronze)
                yield break;
        }
    }
}
