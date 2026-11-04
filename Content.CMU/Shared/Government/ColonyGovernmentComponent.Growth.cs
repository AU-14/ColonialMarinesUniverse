// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.CMU14.Government;

// What the mayor has spent the great sums on: the levels of the growth, the projects that stand, the heat of the grey business, and the studies.
public sealed partial class ColonyGovernmentComponent
{
    public readonly List<ActiveProject> Projects = new();
    public int NextProjectId = 1;

    // How much the city is known for grey business (0 to 100): it makes the next one go worse, and falls by itself. The log is for the mayor alone.
    public float Heat;
    public readonly List<string> CovertLog = new();
    public readonly Dictionary<string, TimeSpan> CovertReady = new();

    // The quota of the metropolis: the plan that goes (the sum, what is paid, the deadline), the plans kept in a row, when the next plan is set, and what came of the last.
    public bool QuotaStarted;
    public bool QuotaActive;
    public int QuotaAmount;
    public int QuotaPaid;
    public int QuotaLevel;
    public TimeSpan QuotaNext;
    public TimeSpan QuotaDeadline;
    public readonly List<string> QuotaLog = new();
}

// A project that stands: what it is, with whom (the faction or the city), and until when (nothing for an embassy).
public sealed class ActiveProject(int id, string proto, string target, TimeSpan? until)
{
    public readonly int Id = id;
    public readonly string Proto = proto;
    public readonly string Target = target;
    public readonly TimeSpan? Until = until;
}
