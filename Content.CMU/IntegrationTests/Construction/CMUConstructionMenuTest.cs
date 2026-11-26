using Content.Client.CMU14.Construction;
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
        var favorites = menu.FindControl<Button>("FavoritesFilter");
        var details = menu.FindControl<BoxContainer>("RecipeDetails");
        Assert.That(favorites.Visible, Is.True);
        Assert.That(details.Visible, Is.False);

        var name = Client.ResolveDependency<ILocalizationManager>().GetString("construction-category-structures");
        var groups = menu.GroupedRecipesContainer;
        var heading = groups.Children.OfType<Label>().Single(label => label.Text == name);
        var count = groups.GetChild(heading.GetPositionInParent() + 1).ChildCount;
        await ClickControl(menu.OptionCategories);
        var options = menu.OptionCategories.OptionsScroll.Children.OfType<BoxContainer>().Single().Children.OfType<BoxContainer>().Single();
        await ClickControl(options.Children.OfType<Button>().Single(button => button.Text == name));
        var grid = groups.Children.OfType<WrapContainer>().Single();
        Assert.That(grid.ChildCount, Is.EqualTo(count));

        var recipe = (ContainerButton)grid.GetChild(0).GetChild(0);
        var favoriteName = recipe.Name;
        await ClickControl(recipe);
        Assert.That(details.Visible, Is.True);
        Assert.That(menu.FindControl<Button>("BuildButton").Visible, Is.True);
        await ClickControl(recipe);
        Assert.That(details.Visible, Is.False);
        await ClickControl(recipe);
        await ClickControl(menu.FindControl<Button>("FavoriteButton"));
        await ClickControl(favorites);
        Assert.That(groups.Children.OfType<WrapContainer>().SelectMany(group => group.Children)
            .Select(cell => cell.GetChild(0).Name), Does.Contain(favoriteName));
        await ClickControl(menu.FindControl<Button>("FavoriteButton"));
        Assert.That(groups.Children.OfType<WrapContainer>(), Is.Empty);
        Assert.That(favorites.Pressed, Is.True);
        Assert.That(details.Visible, Is.False);

        await Client.WaitPost(() => LayoutContainer.SetPosition(menu, new Vector2(32, 24)));
        await RunTicks(1);
        var position = menu.Position;
        await Client.WaitPost(menu.Close);
        await ClickWidgetControl<GameTopMenuBar>("CraftingButton");
        Assert.That(GetWindow<GmodConstructionMenu>().Position, Is.EqualTo(position));
    }
}
