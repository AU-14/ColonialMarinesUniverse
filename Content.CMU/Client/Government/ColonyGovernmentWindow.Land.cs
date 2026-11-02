// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Client.CMU14.WorldMarket;
using Content.Shared.CMU14.Government;
using Robust.Client.UserInterface.Controls;
using static Content.Client.CMU14.WorldMarket.TermKit;

namespace Content.Client.CMU14.Government;

// The two last tabs of the window: the land (the zones, the requests for a plot, the plots, the tax) and the symbols (the flag, the arms, the motto, the holidays).
public sealed partial class ColonyGovernmentWindow
{

    // ---------------------------------------------------------------- the land

    private static string LandSignature(ColonyGovernmentState state)
    {
        var land = state.Land;
        return "z|" + land.Tax + ":" + land.SalesTax + ":" + land.Enforce
               + "|" + string.Join(";", land.Zones.Select(z => $"{z.Id}:{z.Plots}"))
               + "|" + string.Join(";", land.Plots.Select(p => $"{p.Id}:{p.Missed}:{p.Rent}"))
               + "|" + string.Join(";", land.Requests.Select(r => $"{r.Id}:{r.CanAllot}"));
    }

    private void BuildLand(ColonyGovernmentState state)
    {
        var land = state.Land;

        // The rules: the tax, the enforcement, the tablet.
        var rules = Column(6);
        rules.AddChild(Gold(Loc.GetString("rumc-land-title")));
        rules.AddChild(Wrapped(Loc.GetString("rumc-land-hint"), TermPalette.Muted));
        rules.AddChild(Rule());

        var tax = Row(8);
        tax.AddChild(Text(Loc.GetString("rumc-land-tax", ("value", land.Tax)), TermPalette.Text, expand: true));
        tax.AddChild(Action("−", () => OnSetTax?.Invoke(land.Tax - 1), land.Tax > 0, 36));
        tax.AddChild(Action("+", () => OnSetTax?.Invoke(land.Tax + 1), land.Tax < 20, 36));
        rules.AddChild(tax);
        rules.AddChild(Wrapped(Loc.GetString("rumc-land-tax-hint"), TermPalette.Muted));

        var salesTax = Row(8);
        salesTax.AddChild(Text(Loc.GetString("rumc-land-sales-tax", ("value", land.SalesTax)), TermPalette.Text, expand: true));
        salesTax.AddChild(Action("−", () => OnSetSalesTax?.Invoke(land.SalesTax - 1), land.SalesTax > 0, 36));
        salesTax.AddChild(Action("+", () => OnSetSalesTax?.Invoke(land.SalesTax + 1), land.SalesTax < 15, 36));
        rules.AddChild(salesTax);
        rules.AddChild(Wrapped(Loc.GetString("rumc-land-sales-tax-hint"), TermPalette.Muted));

        var enforce = new TermButton { Text = Loc.GetString(land.Enforce ? "rumc-land-enforce-on" : "rumc-land-enforce-off"), MinWidth = 260, Pressed = land.Enforce };
        enforce.OnPressed += _ => OnSetEnforce?.Invoke(!land.Enforce);
        rules.AddChild(enforce);
        rules.AddChild(Wrapped(Loc.GetString("rumc-land-enforce-hint", ("permit", land.PermitName), ("fee", Money(land.PermitFee))), TermPalette.Muted));

        rules.AddChild(Rule());
        rules.AddChild(Action(Loc.GetString("rumc-land-tablet"), () => OnIssueSurveyor?.Invoke(), true, 260));
        rules.AddChild(Wrapped(Loc.GetString("rumc-land-tablet-hint", ("cost", Money(land.SurveyCost)), ("max", land.MaxZones)), TermPalette.Muted));
        rules.AddChild(Rule());
        rules.AddChild(Text(Loc.GetString("rumc-land-income", ("sum", Money(land.RentPerCycle)), ("time", Time(land.CycleSeconds))), TermPalette.Good));
        _page.AddChild(Card(rules));

        // What the zones are for.
        var kinds = Column(4);
        kinds.AddChild(Gold(Loc.GetString("rumc-land-kinds")));
        foreach (var kind in land.Kinds)
        {
            kinds.AddChild(Rule());
            kinds.AddChild(Text(Loc.GetString("rumc-land-kind-line", ("name", kind.Name), ("rent", Money(kind.Rent))), TermPalette.Text));
            kinds.AddChild(Wrapped(kind.Description, TermPalette.Muted));
        }

        _page.AddChild(Card(kinds));

        // The zones that are marked.
        var zones = Column(6);
        zones.AddChild(Gold(Loc.GetString("rumc-land-zones", ("used", land.Zones.Count), ("max", land.MaxZones))));
        if (land.Zones.Count == 0)
            zones.AddChild(Muted(Loc.GetString("rumc-land-zones-none")));

        foreach (var zone in land.Zones)
        {
            var id = zone.Id;
            zones.AddChild(Rule());
            zones.AddChild(Text(zone.Name, TermPalette.Text));
            zones.AddChild(Muted(Loc.GetString("rumc-land-zone-line", ("size", zone.Size), ("x", zone.X), ("y", zone.Y), ("plots", zone.Plots), ("capacity", zone.Capacity))));
            zones.AddChild(Danger(Loc.GetString("rumc-land-zone-remove"), () => OnRemoveZone?.Invoke(id), true, 150));
        }

        _page.AddChild(Card(zones));

        // The people who ask for a plot.
        var requests = Column(6);
        requests.AddChild(Gold(Loc.GetString("rumc-land-requests")));
        requests.AddChild(Wrapped(Loc.GetString("rumc-land-requests-hint"), TermPalette.Muted));
        if (land.Requests.Count == 0)
            requests.AddChild(Muted(Loc.GetString("rumc-land-requests-none")));

        foreach (var request in land.Requests)
        {
            var id = request.Id;
            requests.AddChild(Rule());
            requests.AddChild(Text(Loc.GetString("rumc-land-request-line", ("name", request.Name), ("kind", request.Kind)), TermPalette.Text));
            if (!request.CanAllot)
                requests.AddChild(Text(Loc.GetString("rumc-land-request-nofree"), TermPalette.Bad));

            var buttons = Row(8);
            buttons.AddChild(Action(Loc.GetString("rumc-land-allot"), () => OnAllot?.Invoke(id), request.CanAllot, 150));
            buttons.AddChild(Danger(Loc.GetString("rumc-land-decline"), () => OnDeclineRequest?.Invoke(id), true, 130));
            requests.AddChild(buttons);
        }

        _page.AddChild(Card(requests));

        // The plots that are given.
        var plots = Column(6);
        plots.AddChild(Gold(Loc.GetString("rumc-land-plots")));
        if (land.Plots.Count == 0)
            plots.AddChild(Muted(Loc.GetString("rumc-land-plots-none")));

        foreach (var plot in land.Plots)
        {
            var id = plot.Id;
            plots.AddChild(Rule());
            plots.AddChild(Text(Loc.GetString("rumc-land-plot-line", ("id", plot.Id), ("holder", plot.Holder), ("zone", plot.Zone)), TermPalette.Text));
            plots.AddChild(Muted(Loc.GetString("rumc-land-plot-place", ("place", plot.Place), ("rent", Money(plot.Rent)))));
            if (plot.Missed > 0)
                plots.AddChild(Text(Loc.GetString("rumc-land-plot-missed", ("count", plot.Missed)), TermPalette.Bad));

            plots.AddChild(Danger(Loc.GetString("rumc-land-plot-revoke"), () => OnRevokePlot?.Invoke(id), true, 150));
        }

        _page.AddChild(Card(plots));
    }

    // ---------------------------------------------------------------- the symbols

}
