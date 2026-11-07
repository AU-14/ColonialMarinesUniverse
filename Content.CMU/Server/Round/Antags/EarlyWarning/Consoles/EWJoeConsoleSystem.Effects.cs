using Content.Server.Chat.Systems;
using Content.Server.CMU14.Chat;
using Content.Server.CMU14.Chemistry.Research;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.CMU14.Comms;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Robust.Shared.Audio;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Consoles;

/// <summary>
/// Per-console effects of an assimilation (D6/D12, audit §8.1) and the colony-wide announcements.
/// Every randomization is clamped to ±<see cref="EWJoeComponent.ConsoleEffectVariance"/> of the
/// current value so the colony can respond rather than be wiped out.
/// </summary>
public sealed partial class EWJoeConsoleSystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private BudgetConsoleSystem _budgetConsole = default!;
    [Dependency] private ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private ColonyCommsConsoleSystem _comms = default!;
    [Dependency] private CorporateConsoleSystem _corporate = default!;
    [Dependency] private DepartmentConsoleSystem _department = default!;
    [Dependency] private ServerResearchDataTerminalSystem _research = default!;

    private static readonly SoundPathSpecifier AnnounceSound = new("/Audio/Announcements/announce.ogg");

    private static readonly string[] SpoofLines =
    {
        "cmu-ew-joe-spoof-0",
        "cmu-ew-joe-spoof-1",
        "cmu-ew-joe-spoof-2",
        "cmu-ew-joe-spoof-3",
    };

    /// <summary>
    /// Applies the effect matching the console's component. Returns a short description for the
    /// admin log. A console that is several things at once gets every matching effect.
    /// </summary>
    public string ApplyEffect(Entity<EWJoeAssimilatedConsoleComponent> console)
    {
        var applied = new List<string>();
        var variance = console.Comp.AssimilatedBy is { } joe && TryComp(joe, out EWJoeComponent? joeComp)
            ? joeComp.ConsoleEffectVariance
            : 0.4f;

        if (HasComp<BudgetConsoleComponent>(console))
        {
            var before = _colonyBudget.GetBudget();
            var after = MathF.Round(Vary(before, variance));
            _colonyBudget.SetBudget(after);
            _budgetConsole.UpdateAllUi();
            applied.Add($"budget {before:0} -> {after:0}");
        }

        if (TryComp(console, out CorporateConsoleComponent? corporate))
        {
            var before = corporate.CorporateBudget;
            var after = MathF.Round(Vary(before, variance));
            // adds to every corporate console and refreshes them
            _corporate.AddToCorporateBudget(after - before);
            applied.Add($"corporate {before:0} -> {after:0}");
        }

        if (TryComp(console, out ResearchDataTerminalComponent? terminal))
        {
            var faction = terminal.Faction;
            var before = _research.GetSampleData(faction);
            var after = (int) MathF.Round(Vary(before, variance));
            // the terminal's update loop rerolls the contract board as soon as NextReroll has passed,
            // which wipes the picked contracts through the existing code path
            _research.GetResearch(faction).NextReroll = _timing.CurTime;
            // UpdateSampleData → OnResearchBalanceChanged → UpdateFactionUI, so the UI refreshes too
            _research.UpdateSampleData(after, faction);
            applied.Add($"research {faction} samples {before} -> {after}, board rerolled");
        }

        if (TryComp(console, out DepartmentConsoleComponent? department))
        {
            var changed = 0;
            foreach (var card in department.Members)
            {
                if (!Exists(card))
                    continue;

                var current = department.SalaryOverrides.GetValueOrDefault(card, department.DefaultSalary);
                department.SalaryOverrides[card] = Math.Max(0, (int) MathF.Round(Vary(current, variance)));
                changed++;
            }

            // Members and SalaryOverrides are shared by reference across consoles of one department,
            // so one write is enough; every console of the department still needs its UI rebuilt
            var query = EntityQueryEnumerator<DepartmentConsoleComponent>();
            while (query.MoveNext(out var uid, out var other))
            {
                if (other.DepartmentId == department.DepartmentId)
                    _department.UpdateUiState(uid, other);
            }

            applied.Add($"payroll {department.DepartmentId} {changed} salaries");
        }

        if (HasComp<ColonyCommsConsoleComponent>(console))
        {
            console.Comp.NextSpoof = _timing.CurTime + console.Comp.SpoofInterval;
            SpoofAnnouncement(console);
            applied.Add("comms spoofing");
        }

        return applied.Count == 0 ? "no effect" : string.Join("; ", applied);
    }

    /// <summary>Sends one spoofed APOLLO line through the comms console's own broadcast path.</summary>
    public void SpoofAnnouncement(Entity<EWJoeAssimilatedConsoleComponent> console)
    {
        if (!HasComp<ColonyCommsConsoleComponent>(console))
            return;

        var line = Loc.GetString(_random.Pick(SpoofLines));
        _comms.BroadcastColonyAlert(console, line);
    }

    /// <summary>Colony-wide announcement (everyone but Threat/Xeno/Yautja) naming the console and its area.</summary>
    private void Announce(EntityUid console, string locKey)
    {
        var areaName = Loc.GetString("cmu-ew-joe-objective-area-unknown");
        if (_area.TryGetArea(Transform(console).Coordinates, out var area, out _))
            areaName = Name(area.Value);

        _chat.DispatchFilteredAnnouncement(
            ColonyAnnouncements.Recipients(EntityManager),
            // text pass A: APOLLO names the console and its area in capitals
            Loc.GetString(locKey, ("console", Name(console).ToUpperInvariant()), ("area", areaName.ToUpperInvariant())),
            sender: Loc.GetString("cmu-ew-joe-announcement-sender"),
            playSound: true,
            announcementSound: AnnounceSound);
    }

    /// <summary>A value scaled by a random factor in [1 − variance, 1 + variance].</summary>
    private float Vary(float value, float variance)
    {
        return value * _random.NextFloat(1f - variance, 1f + variance);
    }
}
