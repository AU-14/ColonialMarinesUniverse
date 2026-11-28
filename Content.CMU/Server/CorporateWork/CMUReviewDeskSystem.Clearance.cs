using System.Linq;
using Content.Shared.CMU14.CorporateWork;
using Robust.Shared.Random;

namespace Content.Server.CMU14.CorporateWork;

/// <summary>
///     Clearance Desk: access requests checked against the personnel file and the day's rules.
/// </summary>
public sealed partial class CMUReviewDeskSystem
{
    /// <summary>
    ///     The areas, each with the clearance it needs (same index).
    /// </summary>
    private const int ClearanceAreas = 4;

    private sealed class AccessRequest
    {
        public string Name = string.Empty;
        public string RequestId = string.Empty;
        public string FileId = string.Empty;
        public int Area;
        public int Clearance;
        public int Expires;
        public int Hour;
        public bool Countersigned;
    }

    private CMUReviewDeskCase GenerateClearance(int tierIndex, CMUReviewDeskTier tier)
    {
        var today = _random.Next(20, 300);
        var terminated = RandomNames(2);

        var rules = new List<Rule<AccessRequest>>
        {
            new(Loc.GetString("cmu-desk-clearance-rule-expiry", ("today", today)),
                r => r.Expires >= today,
                r => r.Expires = today - _random.Next(1, 30)),
            new(Loc.GetString("cmu-desk-clearance-rule-id"),
                r => r.RequestId == r.FileId,
                r => r.RequestId = AlterId(r.FileId)),
            new(Loc.GetString("cmu-desk-clearance-rule-area"),
                r => r.Clearance == r.Area,
                r => r.Clearance = _random.Pick(Enumerable.Range(1, ClearanceAreas).Where(c => c != r.Area).ToList())),
            new(Loc.GetString("cmu-desk-clearance-rule-terminated", ("names", string.Join(", ", terminated))),
                r => !terminated.Contains(r.Name),
                r => r.Name = _random.Pick(terminated)),
            new(Loc.GetString("cmu-desk-clearance-rule-night"),
                r => !IsNight(r.Hour) || r.Countersigned,
                r =>
                {
                    r.Hour = _random.Pick(new[] { 22, 23, 0, 1, 2, 3, 4, 5 });
                    r.Countersigned = false;
                }),
        };

        AccessRequest Valid()
        {
            var name = RandomName();
            while (terminated.Contains(name))
                name = RandomName();

            var id = $"WY-{_random.Next(10000, 100000)}";
            var area = _random.Next(1, ClearanceAreas + 1);
            var hour = _random.Next(0, 24);
            return new AccessRequest
            {
                Name = name,
                RequestId = id,
                FileId = id,
                Area = area,
                Clearance = area,
                Expires = today + _random.Next(0, 60),
                Hour = hour,
                Countersigned = IsNight(hour) || _random.Prob(0.3f),
            };
        }

        CMUReviewItem Show(AccessRequest r)
        {
            return new CMUReviewItem(r.Name, new List<string>
            {
                Loc.GetString("cmu-desk-clearance-line-request-id", ("id", r.RequestId)),
                Loc.GetString("cmu-desk-clearance-line-file-id", ("id", r.FileId)),
                Loc.GetString("cmu-desk-clearance-line-area", ("area", Loc.GetString($"cmu-desk-clearance-area-{r.Area}"))),
                Loc.GetString("cmu-desk-clearance-line-clearance", ("clearance", Loc.GetString($"cmu-desk-clearance-level-{r.Clearance}"))),
                Loc.GetString("cmu-desk-clearance-line-expires", ("day", r.Expires)),
                Loc.GetString("cmu-desk-clearance-line-time", ("hour", r.Hour.ToString("00"))),
                Loc.GetString("cmu-desk-clearance-line-countersigned", ("value", YesNo(r.Countersigned))),
            });
        }

        // Option 0 approves, option 1 denies.
        return Build(tierIndex, tier, rules, Valid, Show, pass: 0, fail: 1);
    }

    private static bool IsNight(int hour) => hour >= 22 || hour < 6;

    /// <summary>
    ///     The same ID with one digit changed, the way a forged pass usually gets it wrong.
    /// </summary>
    private string AlterId(string id)
    {
        var chars = id.ToCharArray();
        var digits = Enumerable.Range(0, chars.Length).Where(i => char.IsDigit(chars[i])).ToList();
        var at = _random.Pick(digits);
        var old = chars[at];
        char replacement;
        do
        {
            replacement = (char) ('0' + _random.Next(10));
        } while (replacement == old);

        chars[at] = replacement;
        return new string(chars);
    }
}
