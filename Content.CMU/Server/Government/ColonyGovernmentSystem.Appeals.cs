// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;

namespace Content.Server.CMU14.Government;

// The appeals and the votes. A player writes to the mayor at the console of the people (it is the same board for all, and the answers of the mayor are on it); the mayor answers in his window. The mayor
// may ask the players to vote: one vote for an account, and when the time is out the result goes to the board of the past votes.
public sealed partial class ColonyGovernmentSystem
{
    public const int MaxAppealLength = 200;
    public const int MaxReplyLength = 100;
    public const int MaxOpenAppeals = 25;
    public const int MaxClosedAppeals = 30;
    public const int MaxQuestionLength = 100;
    public const int MaxOptionLength = 40;
    public const int MaxPastPolls = 5;

    private static readonly int[] PollMinutes = { 3, 5, 10, 20 };

    #region Appeals

    private void OnSubmitAppeal(Entity<CitizenConsoleComponent> console, ref SubmitAppealMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        var actor = msg.Actor;
        var text = Clean(msg.Text, MaxAppealLength);
        var author = MetaData(actor).EntityName;
        if (text.Length < 5 || comp.Appeals.Count(a => a.Status == AppealStatus.Open) >= MaxOpenAppeals || comp.Appeals.Any(a => a.Status == AppealStatus.Open && a.Author == author))
        {
            _popup.PopupEntity(Loc.GetString("rumc-appeal-refused"), console.Owner, actor);
            return;
        }

        comp.Appeals.Add(new Appeal(comp.NextAppealId++, author, actor, text, _timing.CurTime));
        _popup.PopupEntity(Loc.GetString("rumc-appeal-sent"), console.Owner, actor);
        Refresh();
    }

    // The mayor answers an appeal: it is taken up or turned down, with a few words, and stays on the board.
    private void OnAnswerAppeal(Entity<ColonyGovernmentConsoleComponent> console, ref AnswerAppealMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var appealId = msg.Appeal;
        if (comp.Appeals.FirstOrDefault(a => a.Id == appealId && a.Status == AppealStatus.Open) is not { } appeal)
            return;

        appeal.Status = msg.Accept ? AppealStatus.Accepted : AppealStatus.Rejected;
        appeal.Reply = Clean(msg.Reply, MaxReplyLength);

        if (appeal.AuthorEntity is { } author && Exists(author))
            _popup.PopupEntity(Loc.GetString(msg.Accept ? "rumc-appeal-accepted" : "rumc-appeal-rejected"), console.Owner, author);

        // Only the last of the answered are kept.
        var closed = comp.Appeals.Where(a => a.Status != AppealStatus.Open).ToList();
        for (var i = 0; i < closed.Count - MaxClosedAppeals; i++)
            comp.Appeals.Remove(closed[i]);

        Refresh();
    }

    // The open ones first (the oldest first), then the answered (the newest first).
    private List<AppealEntry> AppealEntries(ColonyGovernmentComponent comp, int limit)
    {
        return comp.Appeals
            .OrderBy(a => a.Status == AppealStatus.Open ? 0 : 1)
            .ThenBy(a => a.Status == AppealStatus.Open ? a.Id : -a.Id)
            .Take(limit)
            .Select(a => new AppealEntry(a.Id, a.Author, a.Text, a.Status, a.Reply))
            .ToList();
    }

    #endregion

    #region Votes

    private void OnStartPoll(Entity<ColonyGovernmentConsoleComponent> console, ref StartPollMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        var question = Clean(msg.Question, MaxQuestionLength);
        var options = msg.Options.Select(o => Clean(o, MaxOptionLength)).Where(o => o.Length > 0).Take(4).ToList();
        if (comp.Poll != null || question.Length < 3 || options.Count < 2 || !PollMinutes.Contains(msg.Minutes))
            return;

        comp.Poll = new Poll(comp.NextPollId++, question, options, _timing.CurTime + TimeSpan.FromMinutes(msg.Minutes));
        Refresh();
    }

    private void OnClosePoll(Entity<ColonyGovernmentConsoleComponent> console, ref ClosePollMessage msg)
    {
        if (!TryGovernment(out var government) || government.Comp.Poll == null)
            return;

        EndPoll(government.Comp);
        Refresh();
    }

    private void OnVote(Entity<CitizenConsoleComponent> console, ref VoteMessage msg)
    {
        if (!TryGovernment(out var government) || government.Comp.Poll is not { } poll || poll.Id != msg.Poll || msg.Option < 0 || msg.Option >= poll.Options.Count)
            return;

        if (UserOf(msg.Actor) is not { } user || poll.Votes.ContainsKey(user))
            return;

        poll.Votes[user] = msg.Option;
        poll.VoterNames[user] = MetaData(msg.Actor).EntityName;
        Refresh();
    }

    // A vote is over: the result goes to the board of the past votes.
    private void EndPoll(ColonyGovernmentComponent comp)
    {
        if (comp.Poll is not { } poll)
            return;

        comp.Poll = null;
        comp.PastPolls.Insert(0, poll);
        if (comp.PastPolls.Count > MaxPastPolls)
            comp.PastPolls.RemoveRange(MaxPastPolls, comp.PastPolls.Count - MaxPastPolls);
    }

    private void UpdatePolls(ColonyGovernmentComponent comp, TimeSpan now)
    {
        if (comp.Poll != null && now >= comp.Poll.Until)
            EndPoll(comp);
    }

    private static List<int> Counts(Poll poll)
    {
        var counts = poll.Options.Select(_ => 0).ToList();
        foreach (var vote in poll.Votes.Values)
            counts[vote]++;

        return counts;
    }

    private PollEntry PollEntryOf(Poll poll, TimeSpan now, bool closed)
    {
        return new PollEntry(poll.Id, poll.Question, poll.Options.ToList(), Counts(poll),
            closed ? 0 : Math.Max(0, (int) Math.Ceiling((poll.Until - now).TotalSeconds)), closed, poll.VoterNames.Values.ToList());
    }

    private PollEntry? PollEntryOrNull(Poll? poll, TimeSpan now, bool closed)
    {
        return poll == null ? null : PollEntryOf(poll, now, closed);
    }

    #endregion
}
