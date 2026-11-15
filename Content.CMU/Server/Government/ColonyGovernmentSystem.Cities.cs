// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Server.CMU14.WorldMarket;
using Content.Shared.CMU14.Government;

namespace Content.Server.CMU14.Government;

public sealed partial class ColonyGovernmentSystem
{
    // What a gift to a city costs and what an aid in a trouble costs, and how many agreements with the cities can be in force at once.
    public const int GiftCost = 800;
    public const int AidCost = 1500;
    public const int BaseAccordSlots = 2;

    #region The cities of the map

    private static int RelationOf(ColonyGovernmentComponent comp, string city)
    {
        return comp.CityRelation.GetValueOrDefault(city);
    }

    private static void ShiftRelation(ColonyGovernmentComponent comp, string city, int amount)
    {
        comp.CityRelation[city] = Math.Clamp(RelationOf(comp, city) + amount, 0, 100);
    }

    // A gift: money for the good will of a city.
    private void OnCityGift(Entity<ColonyGovernmentConsoleComponent> console, ref CityGiftMessage msg)
    {
        var government = EnsureGovernment();
        var cityId = msg.City;
        var city = _market.Cities().FirstOrDefault(c => c.Id == cityId);
        if (city == null || _budget.GetBudget() < GiftCost)
            return;

        _budget.AddToBudget(-GiftCost);
        ShiftRelation(government.Comp, city.Id, 10);

        Refresh();
    }

    // Help for a city that is in a trouble: it costs more, the trouble goes sooner, and the city is much obliged.
    private void OnCityAid(Entity<ColonyGovernmentConsoleComponent> console, ref CityAidMessage msg)
    {
        var government = EnsureGovernment();
        var comp = government.Comp;
        var cityId = msg.City;
        var city = _market.Cities().FirstOrDefault(c => c.Id == cityId);
        if (city == null || city.CrisisLeftSeconds <= 0 || _budget.GetBudget() < AidCost)
            return;

        _budget.AddToBudget(-AidCost);
        ShiftRelation(comp, city.Id, 20);
        _market.ShortenCrisis(city.Id, 0.4f);

        Refresh();
    }

    // Why an agreement cannot be made with a city (null if it can).
    private string? AccordBlocked(ColonyGovernmentComponent comp, ColonyAccordPrototype proto, WorldMarketCityInfo city)
    {
        if (comp.Accords.Any(a => a.Id == proto.ID && a.City == city.Id))
            return Loc.GetString("rumc-government-accord-in-force");

        if (comp.Accords.Count >= BaseAccordSlots)
            return Loc.GetString("rumc-government-accord-slots-full");

        if (RelationOf(comp, city.Id) < proto.MinRelation)
            return Loc.GetString("rumc-government-accord-need", ("relation", proto.MinRelation));

        if (proto.Requires is { } needed && !city.Capabilities.Contains(needed))
            return Loc.GetString("rumc-government-accord-city-lacks");

        if (_budget.GetBudget() < proto.MoneyCost)
            return Loc.GetString("rumc-government-blocked-money");

        return null;
    }

    private void OnSignAccord(Entity<ColonyGovernmentConsoleComponent> console, ref SignAccordMessage msg)
    {
        var government = EnsureGovernment();
        var comp = government.Comp;
        var cityId = msg.City;
        var city = _market.Cities().FirstOrDefault(c => c.Id == cityId);
        if (city == null || !_proto.TryIndex<ColonyAccordPrototype>(msg.Accord, out var proto) || AccordBlocked(comp, proto, city) != null)
            return;

        _budget.AddToBudget(-proto.MoneyCost);
        comp.Accords.Add(new ActiveAccord(proto.ID, city.Id, _timing.CurTime + TimeSpan.FromSeconds(proto.DurationSeconds)));
        ShiftRelation(comp, city.Id, proto.RelationShift);

        Refresh();
    }

    // Breaking an agreement: the other city takes it ill.
    private void OnBreakAccord(Entity<ColonyGovernmentConsoleComponent> console, ref BreakAccordMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var accordId = msg.Accord;
        var cityId = msg.City;
        var accord = comp.Accords.FirstOrDefault(a => a.Id == accordId && a.City == cityId);
        if (accord == null)
            return;

        comp.Accords.Remove(accord);
        ShiftRelation(comp, accord.City, -10);
        Refresh();
    }

    private void UpdateAccords(ColonyGovernmentComponent comp, TimeSpan now)
    {
        foreach (var accord in comp.Accords.ToList())
        {
            if (accord.Until > now)
                continue;

            comp.Accords.Remove(accord);
            var name = _proto.TryIndex<ColonyAccordPrototype>(accord.Id, out var proto) ? Loc.GetString(proto.Name) : accord.Id;
            var city = _market.Cities().FirstOrDefault(c => c.Id == accord.City)?.Name ?? accord.City;
        }
    }

    #endregion
}
