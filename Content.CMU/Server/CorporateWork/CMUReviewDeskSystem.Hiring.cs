using System.Linq;
using Content.Shared.CMU14.CorporateWork;
using Robust.Shared.Random;

namespace Content.Server.CMU14.CorporateWork;

/// <summary>
///     Hiring Desk: applicants checked against the job posting, their own employment history and
///     what their references say.
/// </summary>
public sealed partial class CMUReviewDeskSystem
{
    private const int HiringPositions = 6;
    private const int HiringQualifications = 6;
    private const int HiringEmployers = 8;

    private sealed class Applicant
    {
        public string Name = string.Empty;
        public int Qualification;
        public int Years;
        public string FirstEmployer = string.Empty;
        public int FirstStart;
        public int FirstEnd;
        public string SecondEmployer = string.Empty;
        public int SecondStart;
        public int SecondEnd;
        public bool Confirmed = true;
        public bool Union;
    }

    private CMUReviewDeskCase GenerateHiring(int tierIndex, CMUReviewDeskTier tier)
    {
        var position = PickLoc("cmu-desk-hiring-position-", HiringPositions);
        var required = _random.Next(1, HiringQualifications + 1);
        var minimumYears = _random.Next(2, 7);
        const int thisYear = 2182;

        var rules = new List<Rule<Applicant>>
        {
            new(Loc.GetString("cmu-desk-hiring-rule-qualification",
                    ("position", position), ("qualification", Loc.GetString($"cmu-desk-hiring-qualification-{required}"))),
                a => a.Qualification == required,
                a => a.Qualification = _random.Pick(Enumerable.Range(1, HiringQualifications).Where(q => q != required).ToList())),
            new(Loc.GetString("cmu-desk-hiring-rule-experience", ("years", minimumYears)),
                a => a.Years >= minimumYears,
                a => a.Years = _random.Next(0, minimumYears)),
            new(Loc.GetString("cmu-desk-hiring-rule-history"),
                a => a.SecondStart >= a.FirstEnd,
                a => a.SecondStart = _random.Next(a.FirstStart + 1, a.FirstEnd)),
            new(Loc.GetString("cmu-desk-hiring-rule-references"),
                a => a.Confirmed,
                a => a.Confirmed = false),
            new(Loc.GetString("cmu-desk-hiring-rule-union"),
                a => !a.Union,
                a => a.Union = true),
        };

        Applicant Valid()
        {
            var employers = Enumerable.Range(1, HiringEmployers).ToList();
            _random.Shuffle(employers);
            var firstStart = thisYear - _random.Next(8, 16);
            var firstEnd = firstStart + _random.Next(2, 5);
            var secondStart = firstEnd + _random.Next(0, 2);

            return new Applicant
            {
                Name = RandomName(),
                Qualification = required,
                Years = _random.Next(minimumYears, minimumYears + 8),
                FirstEmployer = Loc.GetString($"cmu-desk-hiring-employer-{employers[0]}"),
                FirstStart = firstStart,
                FirstEnd = firstEnd,
                SecondEmployer = Loc.GetString($"cmu-desk-hiring-employer-{employers[1]}"),
                SecondStart = secondStart,
                SecondEnd = thisYear,
            };
        }

        CMUReviewItem Show(Applicant a)
        {
            return new CMUReviewItem(a.Name, new List<string>
            {
                Loc.GetString("cmu-desk-hiring-line-qualification", ("value", Loc.GetString($"cmu-desk-hiring-qualification-{a.Qualification}"))),
                Loc.GetString("cmu-desk-hiring-line-experience", ("years", a.Years)),
                Loc.GetString("cmu-desk-hiring-line-job", ("employer", a.FirstEmployer), ("start", a.FirstStart), ("end", a.FirstEnd)),
                Loc.GetString("cmu-desk-hiring-line-job", ("employer", a.SecondEmployer), ("start", a.SecondStart), ("end", a.SecondEnd)),
                Loc.GetString(a.Confirmed ? "cmu-desk-hiring-line-reference-ok" : "cmu-desk-hiring-line-reference-none",
                    ("employer", a.SecondEmployer)),
                Loc.GetString(a.Union ? "cmu-desk-hiring-line-notes-union" : "cmu-desk-hiring-line-notes-none"),
            });
        }

        // Option 0 hires, option 1 rejects.
        return Build(tierIndex, tier, rules, Valid, Show, pass: 0, fail: 1);
    }
}
