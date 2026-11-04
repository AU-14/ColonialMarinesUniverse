using Robust.Shared.Random;

namespace Content.Server.Antag.Selectors;

/// <summary>
/// Spawns a random number of antags between min and max, inclusive.
/// </summary>
public sealed partial class RandomAntagCount : MinMaxAntagCountSelector // CMU14 Class
{
    public override int GetTargetAntagCount(IRobustRandom random, int playerCount)
        => Range.Next(random);
}
