// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// The subsidies for starting a business. A player applies at the console of the people for a grant or for a loan to open a business (a stall, a shop, a workshop); the mayor approves. The money is kept under control:
// the whole sum is taken out of the budget and held, a part is paid at once, and the rest is paid in tranches as the plan is done (so many pieces delivered to the orders of the city, so many rents paid for the premises,
// so many deals sealed by the notary). When the time is out and the plan is not done, what is left of the sum goes back to the budget and what was paid becomes a loan that must be paid back. The tables are here;
// what is done with them is in Server/Government.

[Serializable, NetSerializable]
public enum SubsidyForm : byte
{
    Grant,
    Loan,
}

// What a stage of the plan asks of the player: pieces delivered to the orders of the city, rents of the premises paid, deals sealed by the notary.
[Serializable, NetSerializable]
public enum PlanKind : byte
{
    Deliver,
    Rent,
    Deals,
}

// A stage of the plan: when the player has done this much of this, he gets this share of the sum.
[DataDefinition]
public sealed partial class BusinessStage
{
    [DataField(required: true)]
    public PlanKind Kind;

    [DataField(required: true)]
    public int Target;

    [DataField(required: true)]
    public int Percent;
}

// A kind of business that can be opened with a subsidy.
[Prototype("colonyBusiness")]
public sealed partial class ColonyBusinessPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    // The kind of zone that the premises must be in: the player must hold a plot there to apply.
    [DataField(required: true)]
    public ZoneKind Zone;

    // The sum of a grant, and the greater sum of a loan.
    [DataField]
    public int GrantAmount = 2000;

    [DataField]
    public int LoanAmount = 4000;

    // The share of the sum that is paid at once, in per cent; the stages share the rest (their shares must make up what is left of a hundred).
    [DataField]
    public int UpfrontPercent = 30;

    [DataField]
    public List<BusinessStage> Stages = new();

    // The time to do the whole plan.
    [DataField]
    public int DeadlineSeconds = 2400;
}

// ---------------------------------------------------------------- What the windows show

[Serializable, NetSerializable]
public sealed class BusinessEntry(string id, string name, string description, string zone, int grant, int loan, int upfront, string plan, int deadlineSeconds)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly string Zone = zone;
    public readonly int Grant = grant;
    public readonly int Loan = loan;
    public readonly int Upfront = upfront;
    public readonly string Plan = plan;
    public readonly int DeadlineSeconds = deadlineSeconds;
}

[Serializable, NetSerializable]
public sealed class ApplicationEntry(int id, NetEntity person, string name, string business, SubsidyForm form, int amount, string blocked)
{
    public readonly int Id = id;
    public readonly NetEntity Person = person;
    public readonly string Name = name;
    public readonly string Business = business;
    public readonly SubsidyForm Form = form;
    public readonly int Amount = amount;

    // Why the mayor cannot approve it now (empty if he can).
    public readonly string Blocked = blocked;
}

[Serializable, NetSerializable]
public sealed class SubsidyEntry(int id, string name, string business, SubsidyForm form, int total, int paid, int held, string stage, int progress, int target, int leftSeconds)
{
    public readonly int Id = id;
    public readonly string Name = name;
    public readonly string Business = business;
    public readonly SubsidyForm Form = form;
    public readonly int Total = total;
    public readonly int Paid = paid;

    // What is still held in the budget for the stages that are not done.
    public readonly int Held = held;

    // The stage that goes now, in words, how much of it is done, and how much it asks (0 if every stage is done).
    public readonly string Stage = stage;
    public readonly int Progress = progress;
    public readonly int Target = target;
    public readonly int LeftSeconds = leftSeconds;
}

[Serializable, NetSerializable]
public sealed class SubsidyState(List<BusinessEntry> businesses, List<ApplicationEntry> applications, List<SubsidyEntry> active)
{
    public readonly List<BusinessEntry> Businesses = businesses;
    public readonly List<ApplicationEntry> Applications = applications;
    public readonly List<SubsidyEntry> Active = active;
}

// ---------------------------------------------------------------- From the windows

[Serializable, NetSerializable]
public sealed class ApplySubsidyMessage(string business, SubsidyForm form) : BoundUserInterfaceMessage
{
    public readonly string Business = business;
    public readonly SubsidyForm Form = form;
}

[Serializable, NetSerializable]
public sealed class WithdrawApplicationMessage(int application) : BoundUserInterfaceMessage
{
    public readonly int Application = application;
}

[Serializable, NetSerializable]
public sealed class ApproveSubsidyMessage(int application) : BoundUserInterfaceMessage
{
    public readonly int Application = application;
}

[Serializable, NetSerializable]
public sealed class RejectSubsidyMessage(int application) : BoundUserInterfaceMessage
{
    public readonly int Application = application;
}

[Serializable, NetSerializable]
public sealed class CancelSubsidyMessage(int subsidy) : BoundUserInterfaceMessage
{
    public readonly int Subsidy = subsidy;
}
