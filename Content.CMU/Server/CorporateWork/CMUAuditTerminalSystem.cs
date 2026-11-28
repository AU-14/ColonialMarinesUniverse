using System.Linq;
using Content.Server.Popups;
using Content.Shared.CMU14.CorporateWork;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.CorporateWork;

public sealed class CMUAuditTerminalSystem : EntitySystem
{
    [Dependency] private CMUCorporateWorkSystem _work = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const string Task = "audit";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUAuditTerminalComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CMUAuditTerminalComponent, CMUAuditStartBuiMsg>(OnStart);
        SubscribeLocalEvent<CMUAuditTerminalComponent, CMUAuditSubmitBuiMsg>(OnSubmit);
    }

    private void OnUiOpened(Entity<CMUAuditTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnStart(Entity<CMUAuditTerminalComponent> ent, ref CMUAuditStartBuiMsg args)
    {
        TryStartCase(ent, args.Tier, args.Actor);
    }

    /// <summary>
    ///     Opens a new case at the given tier, replacing any open one.
    /// </summary>
    public bool TryStartCase(Entity<CMUAuditTerminalComponent> ent, int tier, EntityUid user)
    {
        if (tier < 0 || tier >= ent.Comp.Tiers.Count)
            return false;

        if (_timing.CurTime < ent.Comp.NextCaseAt)
        {
            _popup.PopupEntity(Loc.GetString("cmu-audit-cooldown"), ent, user);
            return false;
        }

        ent.Comp.Case = GenerateCase(ent.Comp, tier);
        ent.Comp.LastResult = null;
        UpdateUi(ent);
        return true;
    }

    private void OnSubmit(Entity<CMUAuditTerminalComponent> ent, ref CMUAuditSubmitBuiMsg args)
    {
        Submit(ent, args.Flagged, args.Actor);
    }

    /// <summary>
    ///     Scores the open case against the flagged report lines and pays the corporate budget.
    /// </summary>
    public CMUAuditResult? Submit(Entity<CMUAuditTerminalComponent> ent, IEnumerable<int> flaggedLines, EntityUid user)
    {
        if (ent.Comp.Case is not { } auditCase)
            return null;

        var flagged = flaggedLines.Where(i => i >= 0 && i < auditCase.Report.Count).ToHashSet();
        var found = flagged.Count(auditCase.Discrepancies.Contains);
        var falseFlags = flagged.Count - found;
        var missed = auditCase.Discrepancies.Count - found;

        // Every false flag cancels out a correct one, so flagging everything earns nothing.
        var tier = ent.Comp.Tiers[auditCase.Tier];
        var accuracy = Math.Max(0, found - falseFlags) / (float) auditCase.Discrepancies.Count;
        var payout = _work.Pay(user, Task, tier.Reward * accuracy);

        var result = new CMUAuditResult(found, missed, falseFlags, payout);
        ent.Comp.LastResult = result;
        ent.Comp.Case = null;
        ent.Comp.NextCaseAt = _timing.CurTime + ent.Comp.Cooldown;
        UpdateUi(ent);
        return result;
    }

    private CMUAuditCase GenerateCase(CMUAuditTerminalComponent comp, int tierIndex)
    {
        var tier = comp.Tiers[tierIndex];
        var items = _prototypes.Index(comp.Items).Values;
        var departments = _prototypes.Index(comp.Departments).Values;

        // Unique items so every report line can be matched to exactly one ledger line.
        var picked = items.ToList();
        _random.Shuffle(picked);
        var rows = Math.Min(tier.Rows, picked.Count);

        var ledger = new List<CMUAuditEntry>(rows);
        for (var i = 0; i < rows; i++)
        {
            var amount = _random.Next(comp.MinAmount, comp.MaxAmount + 1);
            ledger.Add(new CMUAuditEntry(picked[i], _random.Pick(departments), amount));
        }

        var report = new List<CMUAuditEntry>(ledger);
        var errorRows = Enumerable.Range(0, rows).ToList();
        _random.Shuffle(errorRows);
        foreach (var row in errorRows.Take(Math.Min(tier.Errors, rows)))
        {
            report[row] = Falsify(report[row], departments, comp);
        }

        // Shuffle the report so lines have to be matched by item, not by position.
        var order = Enumerable.Range(0, rows).ToList();
        _random.Shuffle(order);
        var shuffled = order.Select(i => report[i]).ToList();
        var discrepancies = new HashSet<int>();
        for (var i = 0; i < order.Count; i++)
        {
            if (shuffled[i] != ledger[order[i]])
                discrepancies.Add(i);
        }

        return new CMUAuditCase
        {
            Tier = tierIndex,
            Ledger = ledger,
            Report = shuffled,
            Discrepancies = discrepancies,
        };
    }

    private CMUAuditEntry Falsify(CMUAuditEntry entry, IReadOnlyList<string> departments, CMUAuditTerminalComponent comp)
    {
        if (departments.Count > 1 && _random.Prob(0.35f))
        {
            var other = departments.Where(d => d != entry.Department).ToList();
            return entry with { Department = _random.Pick(other) };
        }

        // Pad the amount by 5-40%, at least 5 credits, so it never rounds back to the original.
        var change = Math.Max(5, (int) (entry.Amount * _random.NextFloat(0.05f, 0.4f)));
        var amount = _random.Prob(0.75f) ? entry.Amount + change : Math.Max(1, entry.Amount - change);
        return entry with { Amount = amount };
    }

    private void UpdateUi(Entity<CMUAuditTerminalComponent> ent)
    {
        var tiers = ent.Comp.Tiers
            .Select(t => new CMUAuditTierInfo(t.Name, t.Rows, t.Errors, t.Reward))
            .ToList();

        var auditCase = ent.Comp.Case;
        var state = new CMUAuditTerminalBuiState(
            tiers,
            auditCase?.Tier,
            auditCase?.Ledger ?? new List<CMUAuditEntry>(),
            auditCase?.Report ?? new List<CMUAuditEntry>(),
            ent.Comp.NextCaseAt,
            ent.Comp.LastResult);

        _ui.SetUiState(ent.Owner, CMUAuditTerminalUi.Key, state);
    }
}
