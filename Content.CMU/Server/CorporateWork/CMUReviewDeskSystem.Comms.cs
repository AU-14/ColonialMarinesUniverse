using System.Linq;
using Content.Shared.CMU14.CorporateWork;
using Robust.Shared.Random;

namespace Content.Server.CMU14.CorporateWork;

/// <summary>
///     Comms Monitoring: intercepted colony messages, passed or flagged for what they say and who
///     they're to and from.
/// </summary>
public sealed partial class CMUReviewDeskSystem
{
    private const int CommsTemplates = 10;
    private const int CommsTopics = 12;
    private const int CommsProjects = 6;
    private const int CommsClfTopics = 4;
    private const int CommsSpecimenTopics = 4;
    private const int CommsRecipients = 6;

    private enum CommsTopic : byte
    {
        Ordinary,
        Project,
        Clf,
        Specimen,
    }

    private sealed class Message
    {
        public string From = string.Empty;
        public string To = string.Empty;
        public bool OffSite;
        public CommsTopic Kind = CommsTopic.Ordinary;
        public string Topic = string.Empty;
        public string Template = string.Empty;
    }

    private CMUReviewDeskCase GenerateComms(int tierIndex, CMUReviewDeskTier tier)
    {
        // The projects named in today's rule, and today's watchlist.
        var projects = Enumerable.Range(1, CommsProjects).ToList();
        _random.Shuffle(projects);
        var classified = projects.Take(2).Select(i => Loc.GetString($"cmu-desk-comms-project-{i}")).ToList();
        var watchlist = RandomNames(2);

        var rules = new List<Rule<Message>>
        {
            new(Loc.GetString("cmu-desk-comms-rule-project", ("projects", string.Join(", ", classified))),
                m => m.Kind != CommsTopic.Project || !classified.Contains(m.Topic),
                m =>
                {
                    m.Kind = CommsTopic.Project;
                    m.Topic = _random.Pick(classified);
                }),
            new(Loc.GetString("cmu-desk-comms-rule-offsite"),
                m => !m.OffSite,
                m =>
                {
                    m.OffSite = true;
                    m.To = Loc.GetString("cmu-desk-comms-offsite");
                }),
            new(Loc.GetString("cmu-desk-comms-rule-clf"),
                m => m.Kind != CommsTopic.Clf,
                m =>
                {
                    m.Kind = CommsTopic.Clf;
                    m.Topic = PickLoc("cmu-desk-comms-clf-", CommsClfTopics);
                }),
            new(Loc.GetString("cmu-desk-comms-rule-watchlist", ("names", string.Join(", ", watchlist))),
                m => !watchlist.Contains(m.From),
                m => m.From = _random.Pick(watchlist)),
            new(Loc.GetString("cmu-desk-comms-rule-specimens"),
                m => m.Kind != CommsTopic.Specimen,
                m =>
                {
                    m.Kind = CommsTopic.Specimen;
                    m.Topic = PickLoc("cmu-desk-comms-specimen-", CommsSpecimenTopics);
                }),
        };

        Message Valid()
        {
            var from = RandomName();
            while (watchlist.Contains(from))
                from = RandomName();

            return new Message
            {
                From = from,
                To = PickLoc("cmu-desk-comms-recipient-", CommsRecipients),
                Topic = PickLoc("cmu-desk-comms-topic-", CommsTopics),
                Template = $"cmu-desk-comms-message-{_random.Next(1, CommsTemplates + 1)}",
            };
        }

        CMUReviewItem Show(Message m)
        {
            return new CMUReviewItem(Loc.GetString("cmu-desk-comms-item", ("from", m.From), ("to", m.To)), new List<string>
            {
                Loc.GetString(m.Template, ("topic", m.Topic)),
            });
        }

        // Option 0 lets it through, option 1 flags it.
        return Build(tierIndex, tier, rules, Valid, Show, pass: 0, fail: 1);
    }
}
