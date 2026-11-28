using System.Linq;
using Content.Server.Popups;
using Content.Shared.CMU14.CorporateWork;
using Content.Shared.Dataset;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.CorporateWork;

/// <summary>
///     The corporate review desks (claims, comms, hiring, clearance). Each case picks a few of the
///     desk's rules, builds every document to pass all of them, then breaks one rule on about half the
///     documents. The right decision is worked out from the rules in force, so it always matches what
///     the player can see. Each desk's documents and rules are in its own partial file.
/// </summary>
public sealed partial class CMUReviewDeskSystem : EntitySystem
{
    [Dependency] private CMUCorporateWorkSystem _work = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    /// <summary>
    ///     Chance a document breaks one of the rules in force.
    /// </summary>
    private const float BreakChance = 0.5f;

    /// <summary>
    ///     Chance a document also breaks a rule that isn't in force today, which mustn't count against it.
    /// </summary>
    private const float NoiseChance = 0.35f;

    private static readonly ProtoId<LocalizedDatasetPrototype> FirstNames = "NamesFirst";
    private static readonly ProtoId<LocalizedDatasetPrototype> LastNames = "NamesLast";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUReviewDeskComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CMUReviewDeskComponent, CMUReviewDeskStartBuiMsg>(OnStart);
        SubscribeLocalEvent<CMUReviewDeskComponent, CMUReviewDeskSubmitBuiMsg>(OnSubmit);
    }

    private void OnUiOpened(Entity<CMUReviewDeskComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnStart(Entity<CMUReviewDeskComponent> ent, ref CMUReviewDeskStartBuiMsg args)
    {
        TryStartCase(ent, args.Tier, args.Actor);
    }

    private void OnSubmit(Entity<CMUReviewDeskComponent> ent, ref CMUReviewDeskSubmitBuiMsg args)
    {
        Submit(ent, args.Decisions, args.Actor);
    }

    private static string Task(CMUReviewDeskKind kind) => $"desk-{kind}";

    /// <summary>
    ///     Opens a new case at the given tier, replacing any open one.
    /// </summary>
    public bool TryStartCase(Entity<CMUReviewDeskComponent> ent, int tier, EntityUid user)
    {
        if (tier < 0 || tier >= ent.Comp.Tiers.Count)
            return false;

        if (_timing.CurTime < ent.Comp.NextCaseAt)
        {
            _popup.PopupEntity(Loc.GetString("cmu-desk-cooldown"), ent, user);
            return false;
        }

        var info = ent.Comp.Tiers[tier];
        ent.Comp.Case = ent.Comp.Kind switch
        {
            CMUReviewDeskKind.Claims => GenerateClaims(tier, info),
            CMUReviewDeskKind.Comms => GenerateComms(tier, info),
            CMUReviewDeskKind.Hiring => GenerateHiring(tier, info),
            CMUReviewDeskKind.Clearance => GenerateClearance(tier, info),
            _ => null,
        };

        ent.Comp.LastResult = null;
        UpdateUi(ent);
        return ent.Comp.Case != null;
    }

    /// <summary>
    ///     Scores the decisions and pays the corporate budget. Each wrong decision cancels a right one;
    ///     undecided documents earn nothing.
    /// </summary>
    public CMUReviewDeskResult? Submit(Entity<CMUReviewDeskComponent> ent, int[] decisions, EntityUid user)
    {
        if (ent.Comp.Case is not { } deskCase)
            return null;

        int correct = 0, wrong = 0, undecided = 0;
        for (var i = 0; i < deskCase.Answers.Count; i++)
        {
            var decision = i < decisions.Length ? decisions[i] : -1;
            if (decision < 0 || decision >= ent.Comp.Options.Count)
                undecided++;
            else if (decision == deskCase.Answers[i])
                correct++;
            else
                wrong++;
        }

        var tier = ent.Comp.Tiers[deskCase.Tier];
        var accuracy = Math.Max(0, correct - wrong) / (float) Math.Max(1, deskCase.Answers.Count);
        var payout = _work.Pay(user, Task(ent.Comp.Kind), tier.Reward * accuracy);

        var result = new CMUReviewDeskResult(correct, wrong, undecided, payout);
        ent.Comp.LastResult = result;
        ent.Comp.Case = null;
        ent.Comp.NextCaseAt = _timing.CurTime + ent.Comp.Cooldown;
        UpdateUi(ent);
        return result;
    }

    /// <summary>
    ///     A rule a document can pass or break.
    /// </summary>
    private sealed class Rule<T>(string text, Func<T, bool> passes, Action<T> breakIt)
    {
        public readonly string Text = text;
        public readonly Func<T, bool> Passes = passes;
        public readonly Action<T> Break = breakIt;
    }

    /// <summary>
    ///     Builds a case from a desk's rules. <paramref name="makeValid"/> must make a document that
    ///     passes every rule in <paramref name="pool"/>; <paramref name="pass"/> and
    ///     <paramref name="fail"/> are the option indices for a document that passes or breaks the rules.
    /// </summary>
    private CMUReviewDeskCase Build<T>(
        int tierIndex,
        CMUReviewDeskTier tier,
        List<Rule<T>> pool,
        Func<T> makeValid,
        Func<T, CMUReviewItem> show,
        int pass,
        int fail)
    {
        var shuffled = pool.ToList();
        _random.Shuffle(shuffled);
        var active = shuffled.Take(Math.Clamp(tier.Rules, 1, pool.Count)).ToList();
        var inactive = shuffled.Skip(active.Count).ToList();

        var items = new List<CMUReviewItem>();
        var answers = new List<int>();
        for (var i = 0; i < tier.Items; i++)
        {
            var document = makeValid();

            // Something that would matter on another day, to keep the player reading the rules.
            if (inactive.Count > 0 && _random.Prob(NoiseChance))
                _random.Pick(inactive).Break(document);

            // The last document makes sure the queue isn't all one answer.
            bool breakOne;
            var last = i == tier.Items - 1 && tier.Items > 1;
            if (last && answers.All(a => a == pass))
                breakOne = true;
            else if (last && answers.All(a => a == fail))
                breakOne = false;
            else
                breakOne = _random.Prob(BreakChance);

            if (breakOne)
                _random.Pick(active).Break(document);

            answers.Add(active.All(r => r.Passes(document)) ? pass : fail);
            items.Add(show(document));
        }

        return new CMUReviewDeskCase
        {
            Tier = tierIndex,
            Rules = active.Select(r => r.Text).ToList(),
            Items = items,
            Answers = answers,
        };
    }

    private string RandomName()
    {
        var first = _prototypes.Index(FirstNames).Values;
        var last = _prototypes.Index(LastNames).Values;
        return $"{Loc.GetString(_random.Pick(first))} {Loc.GetString(_random.Pick(last))}";
    }

    /// <summary>
    ///     <paramref name="count"/> different names.
    /// </summary>
    private List<string> RandomNames(int count)
    {
        var names = new HashSet<string>();
        for (var tries = 0; names.Count < count && tries < count * 20; tries++)
            names.Add(RandomName());

        return names.ToList();
    }

    /// <summary>
    ///     A localized entry from a numbered set: <c>{prefix}1</c> to <c>{prefix}{count}</c>.
    /// </summary>
    private string PickLoc(string prefix, int count)
    {
        return Loc.GetString($"{prefix}{_random.Next(1, count + 1)}");
    }

    private string YesNo(bool value)
    {
        return Loc.GetString(value ? "cmu-desk-yes" : "cmu-desk-no");
    }

    private void UpdateUi(Entity<CMUReviewDeskComponent> ent)
    {
        var comp = ent.Comp;
        var tiers = comp.Tiers.Select(t => new CMUReviewDeskTierInfo(t.Name, t.Items, t.Rules, t.Reward)).ToList();
        var state = new CMUReviewDeskBuiState(
            Loc.GetString(comp.Title),
            Loc.GetString(comp.Heading),
            Loc.GetString(comp.Instructions),
            comp.Options.Select(o => Loc.GetString(o)).ToList(),
            tiers,
            comp.Case?.Tier,
            comp.Case?.Rules.ToList() ?? new List<string>(),
            comp.Case?.Items.ToList() ?? new List<CMUReviewItem>(),
            comp.NextCaseAt,
            comp.LastResult);

        _ui.SetUiState(ent.Owner, CMUReviewDeskUi.Key, state);
    }
}
