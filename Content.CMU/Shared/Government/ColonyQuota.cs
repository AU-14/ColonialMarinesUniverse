// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// The quota of the metropolis: a plan in money. A sum is set for a time (for example a thousand in twenty minutes) and the mayor pays it into the quota out of the budget, as the town fills the budget (the rents,
// the fees, the duty). If the sum is paid in time the metropolis gives a grant that is greater than the sum; if not, what was paid is given back, the metropolis fines the budget, and the factions think the worse
// of the city. The next quota comes after a pause, a little greater after each that was kept. It is all in the console of the administration: nothing is announced.
[Prototype("colonyQuota")]
public sealed partial class ColonyQuotaPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    // The first sum of the plan, and the time for it.
    [DataField]
    public int Amount = 1000;

    [DataField]
    public int DurationSeconds = 1200;

    // How long it is before the next plan is set.
    [DataField]
    public int PauseSeconds = 300;

    // The grant for a plan that was kept, and the fine for one that was not, as a share of the sum, in per cent.
    [DataField]
    public int GrantPercent = 150;

    [DataField]
    public int FinePercent = 20;

    // What every faction thinks of the city after a plan that was kept, and after one that was not.
    [DataField]
    public int SuccessAttitude = 3;

    [DataField]
    public int FailureAttitude = -4;

    // How much greater the sum is after each plan that was kept, in per cent, and how many times at the most.
    [DataField]
    public int GrowthPercent = 20;

    [DataField]
    public int MaxGrowths = 5;
}

// Everything that the window of the mayor shows of the quota.
[Serializable, NetSerializable]
public sealed class QuotaState(
    bool exists, bool active, string name, string description, int amount, int paid, int leftSeconds, int nextSeconds, int grant, int fine, int level, List<string> log)
{
    public readonly bool Exists = exists;
    public readonly bool Active = active;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int Amount = amount;
    public readonly int Paid = paid;

    // The time left for the plan that goes, and the time until the next is set (when none goes).
    public readonly int LeftSeconds = leftSeconds;
    public readonly int NextSeconds = nextSeconds;

    // What the metropolis gives for the plan that goes, and what it fines if it is not kept.
    public readonly int Grant = grant;
    public readonly int Fine = fine;

    // How many plans in a row were kept.
    public readonly int Level = level;
    public readonly List<string> Log = log;
}

[Serializable, NetSerializable]
public sealed class PayQuotaMessage(int amount) : BoundUserInterfaceMessage
{
    public readonly int Amount = amount;
}
