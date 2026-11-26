using Content.Client.CMU14.Construction;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.IntegrationTests.Tests.Interaction;
using Robust.Client.Placement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared;
using Content.Shared.Construction.Prototypes;

namespace Content.IntegrationTests.CMU14.Construction;

public sealed class CMUConstructionMenuTest : InteractionTest
{
    [Test]
    public async Task BrowseFavoriteAndReopen()
    {
        await ClickWidgetControl<GameTopMenuBar>("CraftingButton");
        var menu = GetWindow<GmodConstructionMenu>();
        var favorites = menu.FindControl<Button>("FavoritesFilter");
        var details = menu.FindControl<PanelContainer>("RecipeDetails");
        Assert.That(favorites.Visible, Is.True);
        Assert.That(details.Visible, Is.False);

        var name = Client.ResolveDependency<ILocalizationManager>().GetString("construction-category-structures");
        var groups = menu.GroupedRecipesContainer;
        var heading = groups.Children.OfType<Label>().Single(label => label.Text == name);
        var count = groups.GetChild(heading.GetPositionInParent() + 1).ChildCount;
        var another = groups.Children.OfType<Label>().First(label => label.Text != name);
        var otherRecipe = groups.GetChild(another.GetPositionInParent() + 1).GetChild(0).GetChild(0).Name!;
        var categories = menu.FindControl<BoxContainer>("CategoryButtons");
        var category = categories.Children.OfType<ContainerButton>().Single(button => button.ToolTip == name);
        var categoryRow = category.Children.OfType<BoxContainer>().Single();
        Assert.That(categoryRow.Children.OfType<TextureRect>().Single().Texture, Is.Not.Null);
        Assert.That(categoryRow.Children.OfType<RichTextLabel>().Single().HorizontalExpand, Is.True);
        await ClickControl(category);
        var search = menu.FindControl<LineEdit>("SearchBar");
        await Client.WaitPost(() => search.SetText(otherRecipe, true));
        Assert.That(groups.Children.OfType<Label>().Select(label => label.Text), Does.Contain(another.Text));
        await Client.WaitPost(() => search.SetText(string.Empty, true));
        await RunTicks(1);
        await Client.WaitPost(() =>
        {
            menu.Measure(menu.SetSize);
            menu.Arrange(UIBox2.FromDimensions(menu.Position, menu.SetSize));
        });
        var grid = groups.Children.OfType<WrapContainer>().Single();
        Assert.That(grid.ChildCount, Is.EqualTo(count));

        var barricadeName = CProtoMan.Index<ConstructionPrototype>("AU14BarricadeMetalRecipe").Name!;
        var recipe = grid.Children.Select(cell => (ContainerButton)cell.GetChild(0)).First(button => button.Name == barricadeName);
        var favoriteName = recipe.Name;
        var delay = Client.CfgMan.GetCVar(CVars.DoubleClickDelay);
        try
        {
            // Allow headless ticks between the two pointer releases without depending on workstation load.
            await Client.WaitPost(() => Client.CfgMan.SetCVar(CVars.DoubleClickDelay, 10000));
            await ClickControl(recipe);
            Assert.That(details.Visible, Is.True);
            var build = menu.FindControl<Button>("BuildButton");
            Assert.That(build.Pressed, Is.False);
            await ClickControl(recipe);
            Assert.That(build.Pressed, Is.True);
            Assert.That(Client.ResolveDependency<IPlacementManager>().IsActive, Is.True);
        }
        finally
        {
            await Client.WaitPost(() => Client.CfgMan.SetCVar(CVars.DoubleClickDelay, delay));
        }
        await ClickControl(recipe);
        Assert.That(details.Visible, Is.False);
        // Choosing the recipe again should expose its favorite action.
        await ClickControl(recipe);
        await ClickControl(menu.FindControl<Button>("FavoriteButton"));
        await ClickControl(favorites);
        Assert.That(groups.Children.OfType<WrapContainer>().SelectMany(group => group.Children)
            .Select(cell => cell.GetChild(0).Name), Does.Contain(favoriteName));
        await ClickControl(menu.FindControl<Button>("FavoriteButton"));
        Assert.That(groups.Children.OfType<WrapContainer>(), Is.Empty);
        Assert.That(favorites.Pressed, Is.True);
        await Client.WaitPost(() => search.SetText(otherRecipe, true));
        Assert.That(groups.Children.OfType<Label>().Select(label => label.Text), Does.Contain(another.Text));
        await Client.WaitPost(() => search.SetText(string.Empty, true));
        Assert.That(groups.Children.OfType<WrapContainer>(), Is.Empty);
        Assert.That(details.Visible, Is.False);

        await Client.WaitPost(() => LayoutContainer.SetPosition(menu, new Vector2(32, 24)));
        await RunTicks(1);
        var position = menu.Position;
        await Client.WaitPost(menu.Close);
        await ClickWidgetControl<GameTopMenuBar>("CraftingButton");
        Assert.That(GetWindow<GmodConstructionMenu>().Position, Is.EqualTo(position));
        await Client.WaitPost(menu.Close);
    }
}
