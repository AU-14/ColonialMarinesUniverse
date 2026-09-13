using Content.Server._RMC14.Commendations;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Commendations;
using Content.Shared._RMC14.Marines.Dogtags;
using Content.Shared._RMC14.Rules; // CMU14
using Content.Shared.GameTicking; // CMU14
using Content.Shared.GameTicking.Components; // CMU14
using Content.Shared.Database;
using System.Linq;

namespace Content.Server._RMC14.Rules;
/// <summary>
/// Contains Misc Functions for round end text appending, so it can be used across gamerules.
/// </summary>
public sealed partial class RMCGameRuleExtrasSystem : EntitySystem
{
    [Dependency] private DogtagsSystem _dogtags = default!;
    [Dependency] private CommendationSystem _commendation = default!;

    // CMU14 method: memorial/awards for presets that don't run the classic distress rule
    public override void Initialize()
    {
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend);
    }

    // CMU14 method: the classic rule appends these itself; skip when it is active to avoid doubling them
    private void OnRoundEndTextAppend(RoundEndTextAppendEvent ev)
    {
        var rules = EntityQueryEnumerator<ActiveGameRuleComponent, CMDistressSignalRuleComponent>();
        if (rules.MoveNext(out _, out _))
            return;

        if (MemorialEntry(ref ev))
            ev.AddLine(string.Empty);

        if (MarineAwards(ref ev))
            ev.AddLine(string.Empty);

        XenoAwards(ref ev);
    }

    /// <summary>
    /// Shows names from memorials in the round end text. Returns true if there was any fallen listed.
    /// </summary>
    /// <param name="endEvent"></param>
    /// <returns></returns>
    public bool MemorialEntry(ref RoundEndTextAppendEvent endEvent)
    {
        var memorialQuery = EntityQueryEnumerator<RMCMemorialComponent>();
        List<string> fallen = new();

        while (memorialQuery.MoveNext(out var memorial))
        {
            fallen.AddRange(memorial.Names);
        }

        if (fallen.Count != 0)
        {
            string memorium = Loc.GetString("rmc-distress-signal-fallen", ("fallen", _dogtags.MemorialNamesFormat(fallen)));
            endEvent.AddLine(memorium);
            endEvent.AddLine(string.Empty);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Lists marines who were handed out medals. Returns true if there were any medals given.
    /// </summary>
    /// <param name="endEvent"></param>
    /// <returns></returns>
    public bool MarineAwards(ref RoundEndTextAppendEvent endEvent)
    {
        var commendations = _commendation.GetCommendations();
        var marineAwards = commendations.Where(c => c.Type == CommendationType.Medal).ToArray();
        if (marineAwards.Length > 0)
        {
            endEvent.AddLine(Loc.GetString("cm-distress-signal-medals"));
            foreach (var award in marineAwards)
            {
                endEvent.AddLine(Loc.GetString("rmc-distress-signal-got-medal", ("receiver", award.Receiver), ("award", award.Name),
                    ("awardDescription", award.Text), ("giver", award.Giver)));
            }

            endEvent.AddLine(string.Empty);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Lists xenos who were given a royal jelly. Returns true if there were any jellies given.
    /// </summary>
    /// <param name="endEvent"></param>
    /// <returns></returns>
    public bool XenoAwards(ref RoundEndTextAppendEvent endEvent)
    {
        var commendations = _commendation.GetCommendations();
        var xenoAwards = commendations.Where(c => c.Type == CommendationType.Jelly).ToArray();
        if (xenoAwards.Length > 0)
        {
            endEvent.AddLine(Loc.GetString("cm-distress-signal-jellies"));
            foreach (var award in xenoAwards)
            {
                endEvent.AddLine(Loc.GetString("rmc-distress-signal-got-jelly", ("receiver", award.Receiver), ("award", award.Name),
                    ("awardDescription", award.Text), ("giver", award.Giver)));
            }

            endEvent.AddLine(string.Empty);
            return true;
        }
        return false;
    }
}
