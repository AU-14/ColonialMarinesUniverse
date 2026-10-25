// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.CMU14.Trade;

// What a sale of parcels came to, by what it was sold to: the goods that the companies bought (and their worth), and the goods that an organization outside the law bought (and its worth,
// which is paid with no tax). The quotas of the mother company count the first, the orders of the black market the second.
public sealed class TradeLedger
{
    public readonly Dictionary<string, int> Legal = new();
    public readonly Dictionary<string, int> Illegal = new();

    public double LegalValue;
    public double IllegalValue;

    public void AddLegal(string stack, int count, double value)
    {
        Legal[stack] = Legal.GetValueOrDefault(stack) + count;
        LegalValue += value;
    }

    public void AddIllegal(string stack, int count, double value)
    {
        Illegal[stack] = Illegal.GetValueOrDefault(stack) + count;
        IllegalValue += value;
    }
}
