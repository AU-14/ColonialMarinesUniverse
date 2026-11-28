using Content.Shared.CMU14.CorporateWork;
using Robust.Shared.Random;

namespace Content.Server.CMU14.CorporateWork;

/// <summary>
///     Claims Adjuster: insurance and compensation claims, paid out or denied under the policy's fine print.
/// </summary>
public sealed partial class CMUReviewDeskSystem
{
    private const int ClaimIncidents = 10;
    private const int ClaimCauses = 6;

    private sealed class Claim
    {
        public string Name = string.Empty;
        public string Status = string.Empty;
        public bool Employed = true;
        public string Incident = string.Empty;
        public string Cause = string.Empty;
        public bool Fauna;
        public bool OnShift = true;
        public int DaysLate;
        public int Amount;
        public bool Signed;
    }

    private CMUReviewDeskCase GenerateClaims(int tierIndex, CMUReviewDeskTier tier)
    {
        var deadline = _random.Pick(new[] { 7, 14, 30 });
        var limit = _random.Pick(new[] { 2000, 5000, 10000 });

        var rules = new List<Rule<Claim>>
        {
            new(Loc.GetString("cmu-desk-claims-rule-deadline", ("days", deadline)),
                c => c.DaysLate <= deadline,
                c => c.DaysLate = _random.Next(deadline + 1, deadline + 40)),
            new(Loc.GetString("cmu-desk-claims-rule-shift"),
                c => c.OnShift,
                c => c.OnShift = false),
            new(Loc.GetString("cmu-desk-claims-rule-fauna"),
                c => !c.Fauna,
                c =>
                {
                    c.Fauna = true;
                    c.Cause = Loc.GetString("cmu-desk-claims-cause-fauna");
                }),
            new(Loc.GetString("cmu-desk-claims-rule-signature", ("limit", limit)),
                c => c.Amount <= limit || c.Signed,
                c =>
                {
                    c.Amount = _random.Next(limit + 1, limit * 3);
                    c.Signed = false;
                }),
            new(Loc.GetString("cmu-desk-claims-rule-employee"),
                c => c.Employed,
                c =>
                {
                    c.Employed = false;
                    c.Status = Loc.GetString(_random.Prob(0.5f) ? "cmu-desk-claims-status-contractor" : "cmu-desk-claims-status-terminated");
                }),
        };

        Claim Valid()
        {
            var claim = new Claim
            {
                Name = RandomName(),
                Status = Loc.GetString("cmu-desk-claims-status-employee"),
                Incident = PickLoc("cmu-desk-claims-incident-", ClaimIncidents),
                Cause = PickLoc("cmu-desk-claims-cause-", ClaimCauses),
                DaysLate = _random.Next(1, deadline + 1),
                Amount = _random.Next(3, limit * 2 / 100) * 100,
            };

            // Over the limit always carries a signature; under it, a signature is just paperwork.
            claim.Signed = claim.Amount > limit || _random.Prob(0.5f);
            return claim;
        }

        CMUReviewItem Show(Claim c)
        {
            return new CMUReviewItem(c.Name, new List<string>
            {
                Loc.GetString("cmu-desk-claims-line-status", ("value", c.Status)),
                Loc.GetString("cmu-desk-claims-line-incident", ("value", c.Incident)),
                Loc.GetString("cmu-desk-claims-line-cause", ("value", c.Cause)),
                Loc.GetString("cmu-desk-claims-line-shift", ("value", YesNo(c.OnShift))),
                Loc.GetString("cmu-desk-claims-line-filed", ("days", c.DaysLate)),
                Loc.GetString("cmu-desk-claims-line-amount", ("amount", c.Amount)),
                Loc.GetString("cmu-desk-claims-line-signed", ("value", YesNo(c.Signed))),
            });
        }

        // Option 0 pays out, option 1 denies.
        return Build(tierIndex, tier, rules, Valid, Show, pass: 0, fail: 1);
    }
}
