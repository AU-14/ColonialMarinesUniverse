using System.Linq;
using System.Numerics;
using Content.Client.Lobby;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared._AU14.Construction;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Prototypes;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.Construction.UI
{
    /// <summary>
    /// This class presents the Construction/Crafting UI to the client, linking the <see cref="ConstructionSystem" /> with the
    /// model. This is where the bulk of UI work is done, either calling functions in the model to change state, or collecting
    /// data out of the model to *present* to the screen though the UI framework.
    /// </summary>
    internal sealed partial class ConstructionMenuPresenter : IDisposable
    {
        [Dependency] private EntityManager _entManager = default!;
        [Dependency] private IEntitySystemManager _systemManager = default!;
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IPlacementManager _placementManager = default!;
        [Dependency] private IUserInterfaceManager _uiManager = default!;
        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private IClientPreferencesManager _preferencesManager = default!;
        private readonly SpriteSystem _spriteSystem;
        private readonly SkillsSystem _skillsSystem;

        private readonly IConstructionMenuView _constructionView;
        private readonly EntityWhitelistSystem _whitelistSystem;

        private ConstructionSystem? _constructionSystem;
        private ConstructionPrototype? _selected;
        private List<ConstructionPrototype> _favoritedRecipes = [];
        private readonly Dictionary<string, ContainerButton> _recipeButtons = new();
        private string _selectedCategory = string.Empty;

        private const string FavoriteCatName = "construction-category-favorites";
        private const string ForAllCategoryName = "construction-category-all";

        // Default spawnlist that ungrouped (empty-spawnlist) recipes (i.e. all vanilla items) belong to.
        private const string DefaultSpawnlistName = "AU14";

        // Uniform grid cell sizing, shared by both the classic grid view and the improved menu.
        private const float GridCellSize = 64f;
        private const float GridCellMargin = 2f;
        private const int GridColumns = 5;

        private bool CraftingAvailable
        {
            get => _uiManager.GetActiveUIWidget<GameTopMenuBar>().CraftingButton.Visible;
            set
            {
                _uiManager.GetActiveUIWidget<GameTopMenuBar>().CraftingButton.Visible = value;
                if (!value)
                    _constructionView.Close();
            }
        }

        /// <summary>
        /// Does the window have focus? If the window is closed, this will always return false.
        /// </summary>
        private bool IsAtFront => _constructionView.IsOpen && _constructionView.IsAtFront();

        private bool WindowOpen
        {
            get => _constructionView.IsOpen;
            set
            {
                if (value && CraftingAvailable)
                {
                    if (_constructionView.IsOpen)
                        _constructionView.MoveToFront();
                    else
                        _constructionView.OpenCentered();

                    if (_selected != null)
                        PopulateInfo(_selected);
                }
                else
                    _constructionView.Close();
            }
        }

        /// <summary>
        /// Constructs a new instance of <see cref="ConstructionMenuPresenter" />.
        /// </summary>
        /// <param name="view">
        /// The view to use. Pass <see langword="null"/> to use the default classic menu.
        /// </param>
        public ConstructionMenuPresenter(IConstructionMenuView? view = null)
        {
            // This is a lot easier than a factory
            IoCManager.InjectDependencies(this);
            _constructionView = view ?? new ConstructionMenu();
            _whitelistSystem = _entManager.System<EntityWhitelistSystem>();
            _spriteSystem = _entManager.System<SpriteSystem>();
            _skillsSystem = _entManager.System<SkillsSystem>();

            // This is required so that if we load after the system is initialized, we can bind to it immediately
            if (_systemManager.TryGetEntitySystem<ConstructionSystem>(out var constructionSystem))
                SystemBindingChanged(constructionSystem);

            _systemManager.SystemLoaded += OnSystemLoaded;
            _systemManager.SystemUnloaded += OnSystemUnloaded;

            _placementManager.PlacementChanged += OnPlacementChanged;

            _constructionView.OnClose +=
                () => _uiManager.GetActiveUIWidget<GameTopMenuBar>().CraftingButton.Pressed = false;
            _constructionView.ClearAllGhosts += (_, _) => _constructionSystem?.ClearAllGhosts();
            _constructionView.PopulateRecipes += OnViewPopulateRecipes;
            _constructionView.RecipeSelected += OnViewRecipeSelected;
            _constructionView.BuildButtonToggled += (_, b) => BuildButtonToggled(b);
            _constructionView.EraseButtonToggled += (_, b) =>
            {
                if (_constructionSystem is null)
                    return;
                if (b)
                    _placementManager.Clear();
                _placementManager.ToggleEraserHijacked(new ConstructionPlacementHijack(_constructionSystem, null));
                _constructionView.EraseButtonPressed = b;
            };

            _constructionView.RecipeFavorited += (_, _) => OnViewFavoriteRecipe();

            SetFavorites(_preferencesManager.Preferences?.ConstructionFavorites ?? []);
            OnViewPopulateRecipes(_constructionView, (string.Empty, string.Empty));
        }

        public void OnHudCraftingButtonToggled(BaseButton.ButtonToggledEventArgs args)
        {
            WindowOpen = args.Pressed;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _constructionView.Close();

            SystemBindingChanged(null);
            _systemManager.SystemLoaded -= OnSystemLoaded;
            _systemManager.SystemUnloaded -= OnSystemUnloaded;

            _placementManager.PlacementChanged -= OnPlacementChanged;
        }

        private void OnPlacementChanged(object? sender, EventArgs e)
        {
            _constructionView.ResetPlacement();
        }

        private void OnViewRecipeSelected(object? sender, ConstructionMenu.ConstructionMenuListData? item)
        {
            if (item is null)
            {
                _selected = null;
                _constructionView.ClearRecipeInfo();
                return;
            }

            _selected = item.Prototype;

            if (_placementManager is { IsActive: true, Eraser: false })
                UpdateGhostPlacement();

            PopulateInfo(_selected);
        }

        private void OnGridViewRecipeSelected(object? _, ConstructionPrototype? recipe)
        {
            if (recipe is null)
            {
                _selected = null;
                _constructionView.ClearRecipeInfo();
                return;
            }

            _selected = recipe;

            if (_placementManager is { IsActive: true, Eraser: false })
                UpdateGhostPlacement();

            PopulateInfo(_selected);
        }

        private void OnViewPopulateRecipes(object? sender, (string search, string catagory) args)
        {
            if (_constructionSystem is null)
                return;

            var actualRecipes = GetAndSortRecipes(args);

            var recipesList = _constructionView.Recipes;
            var recipesGrid = _constructionView.RecipesGrid;
            recipesGrid.RemoveAllChildren();

            _constructionView.RecipesGridScrollContainer.Visible = _constructionView.GridViewButtonPressed;
            _constructionView.Recipes.Visible = !_constructionView.GridViewButtonPressed;

            // Grouped view (improved menu): per-category sections inside a vertical container.
            var groupedContainer = _constructionView.GroupedRecipesContainer;
            groupedContainer.RemoveAllChildren();

            if (_constructionView.GridViewButtonPressed)
            {
                recipesList.PopulateList([]);

                if (_constructionView.UseGroupedView)
                {
                    recipesGrid.Visible = false;
                    groupedContainer.Visible = true;
                    PopulateGroupedGrid(groupedContainer, actualRecipes);
                }
                else
                {
                    PopulateGrid(recipesGrid, actualRecipes);
                }
            }
            else
            {
                recipesList.PopulateList(actualRecipes);
            }
        }

        private void PopulateGrid(GridContainer recipesGrid,
            IEnumerable<ConstructionMenu.ConstructionMenuListData> actualRecipes)
        {
            foreach (var recipe in actualRecipes)
            {
                recipesGrid.AddChild(CreateRecipeCell(recipe));
            }
        }

        /// <summary>
        /// Builds the grouped, sub-categorized grid for the improved menu: each construction category
        /// becomes a header label followed by a sub-grid of that category's items (gmod-style).
        /// </summary>
        private void PopulateGroupedGrid(BoxContainer container,
            IEnumerable<ConstructionMenu.ConstructionMenuListData> actualRecipes)
        {
            var groups = new Dictionary<string, List<ConstructionMenu.ConstructionMenuListData>>();

            foreach (var recipe in actualRecipes)
            {
                var rawCategory = recipe.Prototype.Category;
                var categoryName = string.IsNullOrEmpty(rawCategory)
                    ? Loc.GetString(ForAllCategoryName)
                    : Loc.GetString(rawCategory);

                if (!groups.TryGetValue(categoryName, out var list))
                {
                    list = new List<ConstructionMenu.ConstructionMenuListData>();
                    groups[categoryName] = list;
                }

                list.Add(recipe);
            }

            foreach (var categoryName in groups.Keys.OrderBy(c => c, StringComparer.InvariantCulture))
            {
                container.AddChild(new Label
                {
                    Text = categoryName,
                    // Use the same key-text style AND blue accent as the left-tree / tools category headers
                    // ("Your Spawnlists", "Construction", etc.) so the center-grid titles match the other grids.
                    StyleClasses = { "LabelKeyText" },
                    FontColorOverride = Color.FromHex("#4C8DFF"),
                    Margin = new Thickness(4, 8, 0, 2),
                });

                var sectionGrid = new GridContainer { Columns = GridColumns };
                foreach (var recipe in groups[categoryName])
                {
                    sectionGrid.AddChild(CreateRecipeCell(recipe));
                }

                container.AddChild(sectionGrid);
            }
        }

        /// <summary>
        /// Creates a single standardized, equal-sized recipe cell and wires its selection toggle.
        /// Shared by both the flat grid and the grouped grid.
        /// </summary>
        private Control CreateRecipeCell(ConstructionMenu.ConstructionMenuListData recipe)
        {
            var protoView = new EntityPrototypeView()
            {
                Scale = new Vector2(1.2f),
                Modulate = recipe.Prototype.IconColor,
                HorizontalAlignment = Control.HAlignment.Center,
                VerticalAlignment = Control.VAlignment.Center,
            };
            protoView.SetPrototype(recipe.TargetPrototype);

            var itemButton = new ContainerButton()
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                Name = recipe.Prototype.Name,
                ToolTip = recipe.Prototype.Name,
                ToggleMode = true,
                Children = { protoView },
            };

            var itemButtonPanelContainer = new PanelContainer
            {
                MinSize = new Vector2(GridCellSize, GridCellSize),
                Margin = new Thickness(GridCellMargin),
                PanelOverride = new StyleBoxFlat { BackgroundColor = StyleNano.ButtonColorDefault },
                Children = { itemButton },
            };

            itemButton.OnToggled += buttonToggledEventArgs =>
            {
                SelectGridButton(itemButton, buttonToggledEventArgs.Pressed);

                if (buttonToggledEventArgs.Pressed &&
                    _selected != null &&
                    _recipeButtons.TryGetValue(_selected.ID, out var oldButton))
                {
                    oldButton.Pressed = false;
                    SelectGridButton(oldButton, false);
                }

                OnGridViewRecipeSelected(this, buttonToggledEventArgs.Pressed ? recipe.Prototype : null);
            };

            _recipeButtons[recipe.Prototype.ID] = itemButton;
            var isCurrentButtonSelected = _selected == recipe.Prototype;
            itemButton.Pressed = isCurrentButtonSelected;
            SelectGridButton(itemButton, isCurrentButtonSelected);

            return itemButtonPanelContainer;
        }

        /// <summary>
        /// Construction ids hidden from the menu: the persisted set (the generated <c>au14MenuOverrides</c>
        /// prototype, applies after restart for everyone) plus the acting admin's this-session runtime set
        /// (so "Remove Item" hides immediately without a restart).
        /// </summary>
        private HashSet<string> BuildHiddenRecipeSet()
        {
            var query = new ConstructionMenuFilterEvent(new HashSet<string>(), new HashSet<string>());
            _constructionSystem?.QueryMenuExtensions(ref query);
            return query.HiddenRecipes;
        }

        private List<ConstructionMenu.ConstructionMenuListData> GetAndSortRecipes((string, string) args)
        {
            var recipes = new List<ConstructionMenu.ConstructionMenuListData>();

            var (search, category) = args;
            var isEmptyCategory = string.IsNullOrEmpty(category) || category == ForAllCategoryName;
            _selectedCategory = isEmptyCategory ? string.Empty : category;

            // Spawnlist filter (ignored in the Favorites view, which spans all spawnlists). The default "AU14"
            // spawnlist is the "All" view - it shows recipes from every spawnlist, while every other spawnlist
            // shows only its own. Selecting AU14 therefore disables the per-spawnlist filter below.
            var isFavorites = category == FavoriteCatName;
            var rawSelectedSpawnlist = _constructionView.SelectedSpawnlist;
            var selectedSpawnlist = rawSelectedSpawnlist == DefaultSpawnlistName ? string.Empty : rawSelectedSpawnlist;

            // Recipes an admin removed from the menu via "Remove Item" (works for vanilla recipes too).
            var hidden = BuildHiddenRecipeSet();
            var filter = new ConstructionMenuFilterEvent(hidden, new HashSet<string>());
            _constructionSystem?.QueryMenuExtensions(ref filter);

            foreach (var recipe in _prototypeManager.EnumerateCM<ConstructionPrototype>())
            {
                if (recipe.Hide || hidden.Contains(recipe.ID))
                    continue;

                if (_playerManager.LocalSession == null
                    || _playerManager.LocalEntity == null
                    || _whitelistSystem.IsWhitelistFail(recipe.EntityWhitelist, _playerManager.LocalEntity.Value))
                    continue;

                if (!isFavorites)
                {
                    var recipeSpawnlist = string.IsNullOrEmpty(recipe.Spawnlist) ? DefaultSpawnlistName : recipe.Spawnlist;
                    if (!string.IsNullOrEmpty(selectedSpawnlist))
                    {
                        // A specific spawnlist is selected: show only that spawnlist's recipes.
                        if (recipeSpawnlist != selectedSpawnlist)
                            continue;
                    }
                    // AU14 / "All" view (the spawnlist filter is off). The "Z-Level (Experimental)" page is a
                    // SEPARATE main category, so its spawnlists (Tiles, ZLevel) must not bleed into the All list -
                    // those constructs live only on their own page. (AU14 building overhaul - z-level separation.)
                    else if (filter.ExcludedSpawnlists.Contains(recipeSpawnlist))
                    {
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(search) && (recipe.Name is { } name &&
                                                      !name.Contains(search.Trim(),
                                                          StringComparison.InvariantCultureIgnoreCase)))
                    continue;

                if (!isEmptyCategory)
                {
                    if ((category != FavoriteCatName || !_favoritedRecipes.Contains(recipe)) &&
                        recipe.Category != category)
                        continue;
                }

                if (!_constructionSystem!.TryGetRecipePrototype(recipe.ID, out var targetProtoId))
                {
                    Logger.GetSawmill("content").Error("Cannot find the target prototype in the recipe cache with the id \"{0}\" of {1}.",
                        recipe.ID,
                        nameof(ConstructionPrototype));
                    continue;
                }

                if (!_prototypeManager.TryIndex(targetProtoId, out EntityPrototype? proto))
                    continue;

                recipes.Add(new(recipe, proto));
            }

            recipes.Sort(
                (a, b) => string.Compare(a.Prototype.Name, b.Prototype.Name, StringComparison.InvariantCulture));

            return recipes;
        }

        private void SelectGridButton(BaseButton button, bool select)
        {
            if (button.Parent is not PanelContainer buttonPanel)
                return;

            // RMC14
            //button.Children.Single().Modulate = select ? Color.Green : Color.White;
            var buttonColor = select ? StyleNano.ButtonColorDefault : Color.Transparent;
            buttonPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = buttonColor };
        }

        /// <summary>
        /// Discovers the distinct spawnlists across all recipes (empty → <see cref="DefaultSpawnlistName"/>)
        /// and hands them to the view to build its left-tree entries. AU14 is always first.
        /// </summary>
        private void PopulateSpawnlists()
        {
            var set = new HashSet<string> { DefaultSpawnlistName };
            var hidden = BuildHiddenRecipeSet();

            foreach (var recipe in _prototypeManager.EnumerateCM<ConstructionPrototype>())
            {
                if (recipe.Hide || hidden.Contains(recipe.ID))
                    continue;

                set.Add(string.IsNullOrEmpty(recipe.Spawnlist) ? DefaultSpawnlistName : recipe.Spawnlist);
            }

            var spawnlists = set.Where(s => s != DefaultSpawnlistName)
                .OrderBy(s => s, StringComparer.InvariantCulture)
                .ToList();
            spawnlists.Insert(0, DefaultSpawnlistName);

            _constructionView.SetSpawnlists(spawnlists);
        }

        private void PopulateCategories(string? selectCategory = null)
        {
            var uniqueCategories = new HashSet<string>();

            foreach (var prototype in _prototypeManager.EnumerateCM<ConstructionPrototype>())
            {
                var category = prototype.Category;

                if (!string.IsNullOrEmpty(category))
                    uniqueCategories.Add(category);
            }

            var isFavorites = _favoritedRecipes.Count > 0;
            var categoriesArray = new string[isFavorites ? uniqueCategories.Count + 2 : uniqueCategories.Count + 1];

            // hard-coded to show all recipes
            var idx = 0;
            categoriesArray[idx++] = ForAllCategoryName;

            // hard-coded to show favorites if it need
            if (isFavorites)
            {
                categoriesArray[idx++] = FavoriteCatName;
            }

            var sortedProtoCategories = uniqueCategories.OrderBy(Loc.GetString);

            foreach (var cat in sortedProtoCategories)
            {
                categoriesArray[idx++] = cat;
            }

            _constructionView.OptionCategories.Clear();

            for (var i = 0; i < categoriesArray.Length; i++)
            {
                _constructionView.OptionCategories.AddItem(Loc.GetString(categoriesArray[i]), i);

                if (!string.IsNullOrEmpty(selectCategory) && selectCategory == categoriesArray[i])
                    _constructionView.OptionCategories.SelectId(i);
            }

            _constructionView.Categories = categoriesArray;
        }

        private void PopulateInfo(ConstructionPrototype? prototype)
        {
            if (_constructionSystem is null)
                return;

            _constructionView.ClearRecipeInfo();

            if (prototype is null)
                return;

            if (!_constructionSystem.TryGetRecipePrototype(prototype.ID, out var targetProtoId))
                return;

            if (!_prototypeManager.TryIndex(targetProtoId, out EntityPrototype? proto))
                return;

            _constructionView.SetRecipeInfo(
                prototype.Name!,
                prototype.Description!,
                proto,
                prototype.IconColor,
                prototype.Type != ConstructionType.Item,
                !_favoritedRecipes.Contains(prototype),
                prototype);

            var stepList = _constructionView.RecipeStepList;
            var skillLevel = _playerManager.LocalEntity is { } player
                ? _skillsSystem.GetSkill(player, "RMCSkillConstruction")
                : 0;
            _constructionView.SetConstructionSkillInfo(skillLevel,
                AU14ConstructionSkillSystem.GetDiscountPercent(skillLevel));
            GenerateStepList(prototype, stepList, skillLevel);
        }

        private void GenerateStepList(ConstructionPrototype prototype, ItemList stepList, int skillLevel)
        {
            if (_constructionSystem?.GetGuide(prototype) is not { } guide)
                return;

            var materialSteps = GetMaterialSteps(prototype);
            foreach (var entry in guide.Entries)
            {
                // Defensive: a single malformed entry (e.g. a bad localization argument) must not throw and
                // blank the entire steps list. Skip the offending entry instead of losing them all.
                string text;
                try
                {
                    var arguments = entry.Arguments;
                    if (entry.Localization == "construction-presenter-material-step" &&
                        materialSteps.TryDequeue(out var materialStep) &&
                        _prototypeManager.TryIndex(materialStep.MaterialPrototypeId, out StackPrototype? material))
                    {
                        var amount = AU14ConstructionSkillSystem.GetMaterialCost(
                            materialStep.MaterialPrototypeId,
                            materialStep.Amount,
                            skillLevel);
                        var materialName = Loc.GetString(material.Name, ("amount", amount));
                        arguments = [("amount", amount), ("material", materialName)];
                    }

                    text = arguments != null
                        ? Loc.GetString(entry.Localization, arguments)
                        : Loc.GetString(entry.Localization);

                    if (entry.EntryNumber is { } number)
                    {
                        text = Loc.GetString("construction-presenter-step-wrapper",
                            ("step-number", number),
                            ("text", text));
                    }
                }
                catch (Exception e)
                {
                    Logger.GetSawmill("construction").Warning($"Skipped a bad construction guide entry '{entry.Localization}': {e.Message}");
                    continue;
                }

                // The padding needs to be applied regardless of text length... (See PadLeft documentation)
                text = text.PadLeft(text.Length + entry.Padding);

                var icon = entry.Icon != null ? _spriteSystem.Frame0(entry.Icon) : Texture.Transparent;
                stepList.AddItem(text, icon, false);
            }
        }

        private Queue<MaterialConstructionGraphStep> GetMaterialSteps(ConstructionPrototype prototype)
        {
            var result = new Queue<MaterialConstructionGraphStep>();
            if (!_prototypeManager.TryIndex(prototype.Graph, out ConstructionGraphPrototype? graph) ||
                !graph.Nodes.TryGetValue(prototype.StartNode, out var node) ||
                !graph.Nodes.TryGetValue(prototype.TargetNode, out var target) ||
                graph.Path(prototype.StartNode, prototype.TargetNode) is not { } path)
                return result;

            var index = 0;
            while (node != target && index < path.Length)
            {
                if (!node.TryGetEdge(path[index].Name, out var edge))
                    break;

                foreach (var step in edge.Steps)
                {
                    if (step is MaterialConstructionGraphStep material)
                        result.Enqueue(material);
                }

                node = path[index++];
            }

            return result;
        }

        private void BuildButtonToggled(bool pressed)
        {
            if (pressed)
            {
                if (_selected == null)
                    return;

                // not bound to a construction system
                if (_constructionSystem is null)
                {
                    _constructionView.BuildButtonPressed = false;
                    return;
                }

                if (_selected.Type == ConstructionType.Item || _selected.RMCPrototype != null)
                {
                    _constructionSystem.TryStartItemConstruction(_selected.ID);
                    _constructionView.BuildButtonPressed = false;
                    if (_constructionView.CloseOnConstruct)
                        WindowOpen = false;
                    return;
                }

                _placementManager.BeginPlacing(new PlacementInformation
                    {
                        IsTile = false,
                        PlacementOption = _selected.PlacementMode
                    },
                    new ConstructionPlacementHijack(_constructionSystem, _selected));

                UpdateGhostPlacement();

                // Close the menu once placement begins so the player can place the ghost unobstructed.
                if (_constructionView.CloseOnConstruct)
                    WindowOpen = false;
            }
            else
                _placementManager.Clear();

            _constructionView.BuildButtonPressed = pressed;
        }

        private void UpdateGhostPlacement()
        {
            if (_selected == null)
                return;

            if (_selected.Type != ConstructionType.Structure)
            {
                _placementManager.Clear();
                return;
            }

            var constructSystem = _systemManager.GetEntitySystem<ConstructionSystem>();

            _placementManager.BeginPlacing(new PlacementInformation()
                {
                    IsTile = false,
                    PlacementOption = _selected.PlacementMode,
                },
                new ConstructionPlacementHijack(constructSystem, _selected));

            _constructionView.BuildButtonPressed = true;
        }

        private void OnSystemLoaded(object? sender, SystemChangedArgs args)
        {
            if (args.System is ConstructionSystem system)
                SystemBindingChanged(system);
        }

        private void OnSystemUnloaded(object? sender, SystemChangedArgs args)
        {
            if (args.System is ConstructionSystem)
                SystemBindingChanged(null);
        }

        private void OnViewFavoriteRecipe()
        {
            if (_selected is null)
                return;

            if (!_favoritedRecipes.Remove(_selected))
                _favoritedRecipes.Add(_selected);

            if (_selectedCategory == FavoriteCatName)
            {
                OnViewPopulateRecipes(_constructionView,
                    _favoritedRecipes.Count > 0 ? (string.Empty, FavoriteCatName) : (string.Empty, string.Empty));
            }

            var newFavorites = new List<ProtoId<ConstructionPrototype>>(_favoritedRecipes.Count);
            foreach (var recipe in _favoritedRecipes)
                newFavorites.Add(recipe.ID);

            _preferencesManager.UpdateConstructionFavorites(newFavorites);
            PopulateInfo(_selected);
            PopulateCategories(_selectedCategory);
        }

        public void SetFavorites(IReadOnlyList<ProtoId<ConstructionPrototype>> favorites)
        {
            _favoritedRecipes.Clear();

            foreach (var id in favorites)
            {
                if (_prototypeManager.TryIndex(id, out ConstructionPrototype? recipe))
                    _favoritedRecipes.Add(recipe);
            }

            if (_selectedCategory == FavoriteCatName)
            {
                OnViewPopulateRecipes(_constructionView,
                    _favoritedRecipes.Count > 0 ? (string.Empty, FavoriteCatName) : (string.Empty, string.Empty));
            }

            PopulateCategories(_selectedCategory);
        }

        private void SystemBindingChanged(ConstructionSystem? newSystem)
        {
            if (newSystem is null)
            {
                if (_constructionSystem is null)
                    return;

                UnbindFromSystem();
            }
            else
            {
                if (_constructionSystem is null)
                {
                    BindToSystem(newSystem);
                    return;
                }

                UnbindFromSystem();
                BindToSystem(newSystem);
            }
        }

        private void BindToSystem(ConstructionSystem system)
        {
            _constructionSystem = system;

            PopulateSpawnlists();
            OnViewPopulateRecipes(_constructionView, (string.Empty, string.Empty));

            system.ToggleCraftingWindow += SystemOnToggleMenu;
            system.FlipConstructionPrototype += SystemFlipConstructionPrototype;
            system.CraftingAvailabilityChanged += SystemCraftingAvailabilityChanged;
            system.ConstructionGuideAvailable += SystemGuideAvailable;
            system.ConstructionRecipesChanged += SystemRecipesChanged;
            if (_uiManager.GetActiveUIWidgetOrNull<GameTopMenuBar>() != null)
            {
                CraftingAvailable = system.CraftingEnabled;
            }
        }

        private void UnbindFromSystem()
        {
            var system = _constructionSystem;

            if (system is null)
                throw new InvalidOperationException();

            system.ToggleCraftingWindow -= SystemOnToggleMenu;
            system.FlipConstructionPrototype -= SystemFlipConstructionPrototype;
            system.CraftingAvailabilityChanged -= SystemCraftingAvailabilityChanged;
            system.ConstructionGuideAvailable -= SystemGuideAvailable;
            system.ConstructionRecipesChanged -= SystemRecipesChanged;
            _constructionSystem = null;
        }

        private void SystemRecipesChanged(object? sender, EventArgs args)
        {
            var selectedId = _selected?.ID;
            var favoriteIds = _favoritedRecipes.Select(recipe => (ProtoId<ConstructionPrototype>) recipe.ID).ToArray();
            SetFavorites(favoriteIds);
            PopulateSpawnlists();
            PopulateCategories(_selectedCategory);
            OnViewPopulateRecipes(_constructionView, (string.Empty, _selectedCategory));

            if (selectedId != null && _prototypeManager.TryIndex(selectedId, out ConstructionPrototype? selected))
            {
                _selected = selected;
                PopulateInfo(selected);
            }
            else
            {
                _selected = null;
                _constructionView.ClearRecipeInfo();
            }
        }

        private void SystemCraftingAvailabilityChanged(object? sender, CraftingAvailabilityChangedArgs e)
        {
            if (_uiManager.ActiveScreen == null)
                return;
            CraftingAvailable = e.Available;
        }

        private void SystemOnToggleMenu(object? sender, EventArgs eventArgs)
        {
            if (!CraftingAvailable)
                return;

            if (WindowOpen)
            {
                if (IsAtFront)
                {
                    WindowOpen = false;
                    _uiManager.GetActiveUIWidget<GameTopMenuBar>()
                        .CraftingButton.SetClickPressed(false); // This does not call CraftingButtonToggled
                }
                else
                    _constructionView.MoveToFront();
            }
            else
            {
                WindowOpen = true;
                _uiManager.GetActiveUIWidget<GameTopMenuBar>()
                    .CraftingButton.SetClickPressed(true); // This does not call CraftingButtonToggled
            }
        }

        private void SystemFlipConstructionPrototype(object? sender, EventArgs eventArgs)
        {
            if (!_placementManager.IsActive || _placementManager.Eraser)
            {
                return;
            }

            if (_selected == null || _selected.Mirror == null)
            {
                return;
            }

            _selected = _prototypeManager.Index<ConstructionPrototype>(_selected.Mirror);
            UpdateGhostPlacement();
        }

        private void SystemGuideAvailable(object? sender, string e)
        {
            if (!CraftingAvailable)
                return;

            if (!WindowOpen)
                return;

            if (_selected == null)
                return;

            PopulateInfo(_selected);
        }
    }
}
