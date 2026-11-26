using Content.Client.Construction.UI;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.IntegrationTests.Tests.Interaction;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.CMU14.Construction;

public sealed class CMUConstructionMenuTest : InteractionTest
{
    [Test]
    public async Task BrowseFavoriteAndReopen()
    {
        await ClickWidgetControl<GameTopMenuBar>("CraftingButton");
        var menu = GetWindow<GmodConstructionMenu>();
        var tools = menu.FindControl<Button>("TabTools");
        var panel = menu.FindControl<PanelContainer>("ToolsPanel");
        var favorites = menu.FindControl<Button>("TreeFavorites");
        Assert.That(favorites.Visible, Is.True);
        Assert.That(panel.Visible, Is.False);
        var compactWidth = menu.SetWidth;
        await ClickControl(tools);
        Assert.That(panel.Visible, Is.True);
        Assert.That(menu.SetWidth, Is.GreaterThan(compactWidth));
        await ClickControl(tools);
        Assert.That(panel.Visible, Is.False);

        var name = Client.ResolveDependency<ILocalizationManager>().GetString("construction-category-structures");
        var tree = menu.FindControl<BoxContainer>("SpawnlistContainer");
        var categories = tree.Children.OfType<BoxContainer>().Single(box => box.Visible);
        var structures = categories.Children.OfType<Button>().Single(button => button.Text == name);
        var groups = menu.GroupedRecipesContainer;
        var heading = groups.Children.OfType<Label>().Single(label => label.Text == name);
        var count = groups.GetChild(heading.GetPositionInParent() + 1).ChildCount;
        await ClickControl(structures);
        var grid = groups.Children.OfType<GridContainer>().Single();
        Assert.That(grid.ChildCount, Is.EqualTo(count));

        var recipe = (ContainerButton)grid.GetChild(0).GetChild(0);
        var favoriteName = recipe.Name;
        await ClickControl(recipe);
        await ClickControl(menu.FindControl<Button>("FavoriteButton"));
        await ClickControl(favorites);
        Assert.That(groups.Children.OfType<GridContainer>().SelectMany(group => group.Children)
            .Select(cell => cell.GetChild(0).Name), Does.Contain(favoriteName));
        await ClickControl(menu.FindControl<Button>("FavoriteButton"));
        Assert.That(groups.Children.OfType<GridContainer>(), Is.Empty);
        Assert.That(favorites.Pressed, Is.True);

        await Client.WaitPost(() => LayoutContainer.SetPosition(menu, new Vector2(32, 24)));
        await RunTicks(1);
        var position = menu.Position;
        await ClickControl(menu.FindControl<Button>("HeaderCloseButton"));
        await ClickWidgetControl<GameTopMenuBar>("CraftingButton");
        Assert.That(GetWindow<GmodConstructionMenu>().Position, Is.EqualTo(position));
    }
}
