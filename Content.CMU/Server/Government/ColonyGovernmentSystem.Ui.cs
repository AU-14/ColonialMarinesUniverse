// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;
using Robust.Server.GameObjects;

namespace Content.Server.CMU14.Government;

public sealed partial class ColonyGovernmentSystem
{
    #region What the window asks

    private void OnOpened(Entity<ColonyGovernmentConsoleComponent> console, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is not ColonyGovernmentUi)
            return;

        var government = EnsureGovernment();
        _ui.SetUiState(console.Owner, ColonyGovernmentUi.Key, Build(government.Comp, console.Owner));
    }

    // The state is sent to the windows that are open, at once after a thing is done and every second by the clock of the system.
    private void Refresh()
    {
        SyncTablets();
        if (TryGovernment(out var government))
            RefreshUis(government);
    }

    private void RefreshUis(Entity<ColonyGovernmentComponent> government)
    {
        RefreshCitizenUis(government.Comp);
        RefreshForemanUis(government.Comp);

        // The state is made for each console that is open: who stands near it is for that console alone.
        var consoles = EntityQueryEnumerator<ColonyGovernmentConsoleComponent>();
        while (consoles.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, ColonyGovernmentUi.Key))
                _ui.SetUiState(uid, ColonyGovernmentUi.Key, Build(government.Comp, uid));
        }
    }

    private void OnPropose(Entity<ColonyGovernmentConsoleComponent> console, ref ProposeTreatyMessage msg)
    {
        var government = EnsureGovernment();
        var comp = government.Comp;
        if (!_proto.TryIndex<ColonyTreatyPrototype>(msg.Treaty, out var proto) || !_market.KnownFactions().Contains(msg.Faction))
            return;

        if (comp.Treaties.Count >= TotalsOf(comp).TreatySlots || _budget.GetBudget() < proto.MoneyCost)
            return;

        var attitude = AttitudeOf(comp, msg.Faction);
        if (proto.Kind != TreatyKind.Sanction && attitude < proto.MinAttitude)
            return;

        // A faction is a partner or is under sanction, not both; and the same treaty is not signed twice with it.
        var factionId = msg.Faction;
        if (comp.Treaties.Any(t => t.Faction == factionId && (t.Id == proto.ID || IsSanction(t) != (proto.Kind == TreatyKind.Sanction))))
            return;

        _budget.AddToBudget(-proto.MoneyCost);
        comp.Treaties.Add(new ActiveTreaty(proto.ID, msg.Faction, _timing.CurTime + TimeSpan.FromSeconds(proto.DurationSeconds)));

        ShiftAttitude(comp, msg.Faction, proto.AttitudeShift);
        if (proto.OthersAttitudeShift != 0)
        {
            foreach (var faction in _market.KnownFactions().Where(f => f != factionId))
                ShiftAttitude(comp, faction, proto.OthersAttitudeShift);
        }

        _market.ShiftTrust(msg.Faction, proto.TrustShift);

        Refresh();
    }

    private bool IsSanction(ActiveTreaty treaty)
    {
        return _proto.TryIndex<ColonyTreatyPrototype>(treaty.Id, out var proto) && proto.Kind == TreatyKind.Sanction;
    }

    // A treaty is broken (or a sanction lifted): the faction takes it ill (or well).
    private void OnBreak(Entity<ColonyGovernmentConsoleComponent> console, ref BreakTreatyMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var treatyId = msg.Treaty;
        var factionId = msg.Faction;
        var treaty = comp.Treaties.FirstOrDefault(t => t.Id == treatyId && t.Faction == factionId);
        if (treaty == null)
            return;

        comp.Treaties.Remove(treaty);
        ShiftAttitude(comp, treaty.Faction, IsSanction(treaty) ? 5 : -10);
        Refresh();
    }

    #endregion

    #region The state for the window

    private ColonyGovernmentState Build(ColonyGovernmentComponent comp, EntityUid console)
    {
        var now = _timing.CurTime;
        var budget = _budget.GetBudget();
        var totals = TotalsOf(comp);

        var factions = _market.KnownFactions().Select(f => new FactionEntry(f, AttitudeOf(comp, f))).ToList();

        var treaties = _proto.EnumeratePrototypes<ColonyTreatyPrototype>()
            .OrderBy(p => p.Kind).ThenBy(p => Loc.GetString(p.Name))
            .Select(p => new TreatyEntry(p.ID, p.Kind, Loc.GetString(p.Name), Loc.GetString(p.Description), p.MoneyCost, p.DurationSeconds,
                p.Kind == TreatyKind.Sanction ? -100 : p.MinAttitude, TreatyEffects(p)))
            .ToList();

        var active = new List<ActiveTreatyEntry>();
        foreach (var treaty in comp.Treaties)
        {
            if (_proto.TryIndex<ColonyTreatyPrototype>(treaty.Id, out var proto))
                active.Add(new ActiveTreatyEntry(treaty.Id, Loc.GetString(proto.Name), treaty.Faction, Math.Max(0, (int) Math.Ceiling((treaty.Until - now).TotalSeconds))));
        }

        var petitions = new List<PetitionEntry>();
        foreach (var petition in comp.Petitions)
        {
            if (!_proto.TryIndex<ColonyPetitionPrototype>(petition.Proto, out var proto))
                continue;

            petitions.Add(new PetitionEntry(
                petition.Id, proto.Ultimatum, petition.Faction, Loc.GetString(proto.Text, ("faction", petition.Faction)),
                Math.Max(0, (int) Math.Ceiling((petition.Deadline - now).TotalSeconds)), petition.Cost, petition.Reward,
                !petition.Bargained, budget >= petition.Cost));
        }

        return new ColonyGovernmentState(
            budget, totals.Income, totals.DutyCut, totals.CallDiscount, totals.TreatySlots, comp.Treaties.Count,
            factions, treaties, active, petitions, BuildCredit(comp), BuildCities(comp), BuildLicenses(comp, console),
            BuildLand(comp), BuildPeople(comp, console), BuildAdmin(comp, console), BuildGrowth(comp), BuildQuota(comp), BuildSubsidy(comp));
    }

    private CreditState BuildCredit(ColonyGovernmentComponent comp)
    {
        var now = _timing.CurTime;

        var lenders = _proto.EnumeratePrototypes<ColonyLenderPrototype>()
            .OrderBy(l => l.MinRating).ThenBy(l => Loc.GetString(l.Name))
            .Select(l => new LenderEntry(l.ID, Loc.GetString(l.Name), Loc.GetString(l.Description), LenderAvailable(comp, l), l.InterestPercent, l.Instalments, l.CycleSeconds,
                LoanBlocked(comp, l, now) ?? string.Empty))
            .ToList();

        var loans = comp.Loans
            .Select(l => new LoanEntry(l.Id, LenderName(l.Lender), l.Instalment, l.Left, Math.Max(0, (int) Math.Ceiling((l.NextDue - now).TotalSeconds)), l.Missed))
            .ToList();

        var investments = _proto.EnumeratePrototypes<ColonyInvestmentPrototype>()
            .OrderBy(p => p.Cost)
            .Select(p => new InvestmentEntry(p.ID, Loc.GetString(p.Name), Loc.GetString(p.Description), p.Cost, p.DurationSeconds, (int) MathF.Round(InvestmentChance(comp, p) * 100f),
                (int) (p.Cost * p.PayoutMultiplier), p.IncomePerMinute, InvestmentBlocked(comp, p) ?? string.Empty))
            .ToList();

        var active = new List<ActiveInvestmentEntry>();
        foreach (var investment in comp.Investments)
        {
            if (_proto.TryIndex<ColonyInvestmentPrototype>(investment.Proto, out var proto))
                active.Add(new ActiveInvestmentEntry(investment.Id, Loc.GetString(proto.Name), Math.Max(0, (int) Math.Ceiling((investment.ReadyAt - now).TotalSeconds)), investment.Done, investment.Success));
        }

        return new CreditState(comp.CreditRating, lenders, loans, investments, active);
    }

    private string CapabilitiesText(List<Content.Shared.CMU14.WorldMarket.CityCapability> capabilities)
    {
        return string.Join(", ", capabilities.Select(c => Loc.GetString("rumc-government-cap-" + c.ToString().ToLowerInvariant())));
    }

    private CitiesState BuildCities(ColonyGovernmentComponent comp)
    {
        var cities = _market.Cities()
            .Select(c => new CityEntry(c.Id, c.Name, RelationOf(comp, c.Id), CapabilitiesText(c.Capabilities), c.CrisisName, c.CrisisLeftSeconds))
            .ToList();

        var accords = _proto.EnumeratePrototypes<ColonyAccordPrototype>()
            .OrderBy(p => p.MinRelation).ThenBy(p => Loc.GetString(p.Name))
            .Select(p => new AccordEntry(p.ID, Loc.GetString(p.Name), Loc.GetString(p.Description), p.MoneyCost, p.DurationSeconds, p.MinRelation, AccordEffects(p),
                p.Requires is { } needed ? Loc.GetString("rumc-government-cap-" + needed.ToString().ToLowerInvariant()) : string.Empty))
            .ToList();

        var now = _timing.CurTime;
        var active = new List<ActiveAccordEntry>();
        foreach (var accord in comp.Accords)
        {
            if (_proto.TryIndex<ColonyAccordPrototype>(accord.Id, out var proto))
            {
                var city = _market.Cities().FirstOrDefault(c => c.Id == accord.City)?.Name ?? accord.City;
                active.Add(new ActiveAccordEntry(accord.Id, Loc.GetString(proto.Name), city, Math.Max(0, (int) Math.Ceiling((accord.Until - now).TotalSeconds))));
            }
        }

        return new CitiesState(cities, accords, active, GiftCost, AidCost, BaseAccordSlots);
    }

    private string AccordEffects(ColonyAccordPrototype proto)
    {
        var parts = new List<string>();
        if (proto.IncomePerMinute != 0f)
            parts.Add(Loc.GetString("rumc-government-fx-income", ("value", (int) MathF.Round(proto.IncomePerMinute))));

        if (proto.DutyCut != 0)
            parts.Add(Loc.GetString("rumc-government-fx-duty", ("value", proto.DutyCut)));

        if (proto.CallDiscountPercent != 0)
            parts.Add(Loc.GetString("rumc-government-fx-call", ("value", proto.CallDiscountPercent)));

        return string.Join(", ", parts);
    }

    private LicensesState BuildLicenses(ColonyGovernmentComponent comp, EntityUid console)
    {
        var now = _timing.CurTime;

        var types = _proto.EnumeratePrototypes<ColonyLicensePrototype>()
            .OrderBy(p => Loc.GetString(p.Name))
            .Select(p => new LicenseTypeEntry(p.ID, Loc.GetString(p.Name), Loc.GetString(p.Description), p.Fee, p.DurationSeconds))
            .ToList();

        var issued = comp.Licenses.Values
            .OrderByDescending(r => r.IssuedAt)
            .Take(40)
            .Select(r => new LicenseEntry(r.Serial, _proto.TryIndex<ColonyLicensePrototype>(r.Type, out var proto) ? Loc.GetString(proto.Name) : r.Type, r.Holder,
                (int) Math.Ceiling((r.ExpiresAt - now).TotalSeconds), r.Revoked))
            .ToList();

        return new LicensesState(types, issued, NearbyPeople(console));
    }

    // What a treaty gives, in a line: the parts that it has.
    private string TreatyEffects(ColonyTreatyPrototype proto)
    {
        var parts = new List<string>();
        if (proto.IncomePerMinute != 0f)
            parts.Add(Loc.GetString("rumc-government-fx-income", ("value", (int) MathF.Round(proto.IncomePerMinute))));

        if (proto.DutyCut != 0)
            parts.Add(Loc.GetString("rumc-government-fx-duty", ("value", proto.DutyCut)));

        if (proto.CallDiscountPercent != 0)
            parts.Add(Loc.GetString("rumc-government-fx-call", ("value", proto.CallDiscountPercent)));

        if (proto.PartnerPointsPerMinute > 0f)
            parts.Add(Loc.GetString("rumc-government-fx-points", ("value", proto.PartnerPointsPerMinute.ToString("0.#"))));

        if (proto.Kind == TreatyKind.Sanction)
            parts.Add(Loc.GetString("rumc-government-fx-sanction", ("target", -proto.AttitudeShift), ("others", proto.OthersAttitudeShift), ("trust", -proto.TrustShift)));

        return string.Join(", ", parts);
    }

    #endregion
}
