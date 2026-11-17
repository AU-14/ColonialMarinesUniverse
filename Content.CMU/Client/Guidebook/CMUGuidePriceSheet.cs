using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Guidebook.Controls;
using Content.Client.Guidebook.Richtext;
using Content.Client.Lobby.UI;
using Content.Shared.CMU14.ColonyEconomy;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Guidebook;

/// <summary>Searchable ASRS sell-price sheet generated from the canonical industry price prototypes.</summary>
[UsedImplicitly]
public sealed class CMUGuidePriceSheet : BoxContainer, IDocumentTag
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public CMUGuidePriceSheet()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 2;
        MouseFilter = MouseFilterMode.Stop;
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        foreach (var price in _prototypes.EnumeratePrototypes<CMUIndustryPricePrototype>()
                     .OrderBy(entry => entry.Product, StringComparer.CurrentCultureIgnoreCase))
        {
            AddChild(new CMUGuidePriceRow(price.Product, price.Price));
        }

        CrtLobbyTheme.Apply(this, useCrtTypography: false);
        control = this;
        return true;
    }
}

/// <summary>One filterable product and per-unit ASRS price row.</summary>
internal sealed class CMUGuidePriceRow : PanelContainer, ISearchableControl
{
    private readonly string _searchText;

    public CMUGuidePriceRow(string product, double price)
    {
        _searchText = $"{product} {price:0.##}";
        HorizontalExpand = true;
        Margin = new Thickness(0, 1);
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#101820"),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 5,
            ContentMarginBottomOverride = 5,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 12,
        };
        row.AddChild(new Label
        {
            Text = product,
            HorizontalExpand = true,
            ClipText = true,
            FontColorOverride = Color.White,
        });
        row.AddChild(new Label
        {
            Text = Loc.GetString("cmu-industry-price-sheet-value", ("price", price.ToString("0.##"))),
            HorizontalAlignment = HAlignment.Right,
            FontColorOverride = Color.FromHex("#65d6a6"),
        });
        AddChild(row);
    }

    public bool CheckMatchesSearch(string query) =>
        _searchText.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    public void SetHiddenState(bool state, string query)
    {
        Visible = CheckMatchesSearch(query) ? state : !state;
    }
}
