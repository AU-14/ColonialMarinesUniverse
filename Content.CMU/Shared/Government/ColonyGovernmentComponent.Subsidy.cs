// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.CMU14.Government;

// The subsidies: the applications that wait for the mayor, and the subsidies that are paid in tranches.
public sealed partial class ColonyGovernmentComponent
{
    public readonly List<SubsidyApplication> SubsidyApplications = new();
    public int NextApplicationId = 1;

    public readonly List<Subsidy> Subsidies = new();
    public int NextSubsidyId = 1;
}

// A player's application: who, for what business, as a grant or as a loan.
public sealed class SubsidyApplication(int id, EntityUid person, string name, string business, SubsidyForm form)
{
    public readonly int Id = id;
    public readonly EntityUid Person = person;
    public readonly string Name = name;
    public readonly string Business = business;
    public readonly SubsidyForm Form = form;
}

// A subsidy that is paid: the whole sum was taken out of the budget and is held; a part was paid at once; each stage of the plan pays a share when it is done. The stage that goes counts its own progress.
public sealed class Subsidy(int id, EntityUid person, string name, string business, SubsidyForm form, int total, TimeSpan deadline)
{
    public readonly int Id = id;
    public readonly EntityUid Person = person;
    public readonly string Name = name;
    public readonly string Business = business;
    public readonly SubsidyForm Form = form;
    public readonly int Total = total;
    public readonly TimeSpan Deadline = deadline;

    // What was paid, and the stage that goes with how much of it is done.
    public int Paid;
    public int Stage;
    public int Progress;
}
