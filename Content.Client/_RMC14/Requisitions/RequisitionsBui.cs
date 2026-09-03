using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Content.Client.Stylesheets;
using Content.Client._CMU14.Interface;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Content.Shared._RMC14.Requisitions.Components.RequisitionsElevatorMode;

namespace Content.Client._RMC14.Requisitions;

[UsedImplicitly]
public sealed partial class RequisitionsBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    [ViewVariables]
    private RequisitionsWindow? _window;

    private readonly Dictionary<(int Category, int Order), RequisitionsStockInfo> _stock = new();
    private readonly Dictionary<EntProtoId, RequisitionsItemStockInfo> _itemStock = new();
    private readonly Dictionary<EntProtoId, int> _cart = new();
    private RequisitionsBuiState? _lastState;
    private bool? _raisePlatform;
    private bool _previewOpen;
    private int? _selectedCategory;
    private int? _selectedOrder;
    private string? _selectedItemCategory;
    private int _checkoutRequestId;
    private int? _pendingCheckout;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<RequisitionsWindow>();
        _cart.Clear();

        _window.MainView.OrderItemsButton.OnPressed += _ => ShowView(_window, _window.OrderCategoriesView);
        _window.MainView.PlatformButton.OnPressed += _ => TrySendPlatformMessage();
        _window.MainView.ViewRequestsButton.OnPressed += _ => { };
        _window.MainView.ViewOrdersButton.OnPressed += _ => { };

        _window.OrderCategoriesView.PlatformButton.OnPressed += _ => TrySendPlatformMessage();
        _window.OrderCategoriesView.ItemsModeButton.OnPressed += _ => ShowView(_window, _window.ItemizedView);
        _window.OrderCategoriesView.SearchBar.OnTextChanged += _ => RebuildBrowser();
        _window.OrderCategoriesView.PreviewOrderButton.OnPressed += _ => TryOrderSelected();

        _window.ItemizedView.ItemsTabButton.OnPressed += _ => ShowView(_window, _window.ItemizedView);
        _window.ItemizedView.BundlesTabButton.OnPressed += _ => ShowView(_window, _window.OrderCategoriesView);
        _window.ItemizedView.PlatformButton.OnPressed += _ => TrySendPlatformMessage();
        _window.ItemizedView.SearchBar.OnTextChanged += _ => RebuildItemizedBrowser();
        _window.ItemizedView.CheckoutButton.OnPressed += _ => TryCheckout();
        _window.ItemizedView.ApplyTheme();

        ShowView(_window, _window.ItemizedView);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is RequisitionsBuiState uiState)
            UpdateState(uiState);
    }

    private void UpdateState(RequisitionsBuiState uiState)
    {
        _window ??= this.CreateWindow<RequisitionsWindow>();
        _lastState = uiState;
        _stock.Clear();
        foreach (var stock in uiState.Stock)
        {
            _stock[(stock.Category, stock.Order)] = stock;
        }
        _itemStock.Clear();
        foreach (var stock in uiState.ItemStock)
            _itemStock[stock.Prototype] = stock;

        UpdatePlatform(uiState);
        UpdateBudget(uiState);
        ApplyDisplayMode();
        RebuildBrowser();
        RebuildItemizedBrowser();

        if (!_window.IsOpen)
            _window.OpenCentered();
    }

    private void UpdatePlatform(RequisitionsBuiState uiState)
    {
        var platformLabel = "No platform";
        var platformButtonLabel = "No platform";
        var platformButtonDisabled = false;
        bool? raise = null;
        switch (uiState.PlatformLowered)
        {
            case Lowered or Raised when uiState.Busy:
                platformLabel = $"Platform: {uiState.PlatformLowered}";
                platformButtonLabel = "ASRS busy";
                platformButtonDisabled = true;
                break;
            case Lowered:
                platformButtonLabel = "Raise";
                platformLabel = "Platform: Lowered";
                raise = true;
                break;
            case Raised:
                platformButtonLabel = "Lower";
                platformLabel = "Platform: Raised";
                raise = false;
                break;
            case Lowering:
                platformButtonLabel = "Please wait";
                platformLabel = "Lowering...";
                platformButtonDisabled = true;
                break;
            case Raising:
                platformButtonLabel = "Please wait";
                platformLabel = "Raising...";
                platformButtonDisabled = true;
                break;
            case null:
                platformButtonDisabled = true;
                break;
        }

        _raisePlatform = raise;

        _window!.MainView.PlatformLabel.SetMessage(platformLabel);
        _window.MainView.PlatformButton.Text = platformButtonLabel;
        _window.MainView.PlatformButton.Disabled = platformButtonDisabled;

        _window.OrderCategoriesView.PlatformLabel.SetMessage(platformLabel);
        _window.OrderCategoriesView.PlatformButton.Text = platformButtonLabel;
        _window.OrderCategoriesView.PlatformButton.Disabled = platformButtonDisabled;

        _window.ItemizedView.PlatformLabel.Text = Loc.GetString(
            "cmu-asrs-platform-status",
            ("state", GetPlatformState(uiState)),
            ("slots", uiState.AvailableSlots));
        _window.ItemizedView.PlatformButton.Text = platformButtonLabel;
        _window.ItemizedView.PlatformButton.Disabled = platformButtonDisabled;
    }

    private void UpdateBudget(RequisitionsBuiState uiState)
    {
        var text = $"Supply budget: ${uiState.Balance}";
        var budget = new FormattedMessage();
        budget.AddMarkupOrThrow($"[bold]{text}[/bold]");
        _window!.MainView.BudgetLabel.SetMessage(budget);
        _window.OrderCategoriesView.BudgetLabel.Text = text;
        _window.CategoryView.BudgetLabel.SetMessage(budget);
        _window.OrderSearchView.BudgetLabel.SetMessage(budget);
        _window.ItemizedView.BudgetLabel.Text = Loc.GetString("cmu-asrs-budget", ("balance", uiState.Balance));
    }

    private static string GetPlatformState(RequisitionsBuiState state)
    {
        return Loc.GetString(state.PlatformLowered switch
        {
            null => "cmu-asrs-platform-none",
            Lowered when state.Busy => "cmu-asrs-platform-busy",
            Raised when state.Busy => "cmu-asrs-platform-busy",
            Lowered => "cmu-asrs-platform-lowered",
            Raised => "cmu-asrs-platform-raised",
            Lowering => "cmu-asrs-platform-lowering",
            Raising => "cmu-asrs-platform-raising",
            _ => "cmu-asrs-platform-busy",
        });
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is not RequisitionsCheckoutResultMsg result ||
            _window == null ||
            _pendingCheckout != result.RequestId)
        {
            return;
        }

        _pendingCheckout = null;
        if (result.Result == RequisitionsCheckoutResult.Success)
            _cart.Clear();

        _window.ItemizedView.FeedbackLabel.FontColorOverride = result.Result == RequisitionsCheckoutResult.Success
            ? CrtTerminalPalette.Accent
            : CrtTerminalPalette.Alert;

        _window.ItemizedView.FeedbackLabel.Text = Loc.GetString(result.Result switch
        {
            RequisitionsCheckoutResult.Success => "cmu-asrs-checkout-success",
            RequisitionsCheckoutResult.InvalidOrder => "cmu-asrs-checkout-invalid",
            RequisitionsCheckoutResult.InsufficientFunds => "cmu-asrs-checkout-funds",
            RequisitionsCheckoutResult.InsufficientStock => "cmu-asrs-checkout-stock",
            RequisitionsCheckoutResult.NoPlatform => "cmu-asrs-checkout-platform",
            RequisitionsCheckoutResult.PlatformFull => "cmu-asrs-checkout-full",
            _ => "cmu-asrs-checkout-invalid",
        });
        RebuildItemizedBrowser();
    }

    private void RebuildItemizedBrowser()
    {
        if (_window == null ||
            !_entities.TryGetComponent(Owner, out RequisitionsComputerComponent? computer))
        {
            return;
        }

        var view = _window.ItemizedView;
        var allCategories = computer.ItemCatalog
            .SelectMany(item => item.Categories)
            .Distinct()
            .Order()
            .ToList();
        if (_selectedItemCategory != null && !allCategories.Contains(_selectedItemCategory))
            _selectedItemCategory = null;

        view.CategoriesContainer.RemoveAllChildren();
        AddItemCategoryButton(Loc.GetString("cmu-asrs-category-all"), null);
        foreach (var category in allCategories)
            AddItemCategoryButton(category, category);

        view.ItemsContainer.RemoveAllChildren();
        var filter = view.SearchBar.Text?.Trim();
        var visible = 0;
        for (var catalogIndex = 0; catalogIndex < computer.ItemCatalog.Count; catalogIndex++)
        {
            var item = computer.ItemCatalog[catalogIndex];
            if (_selectedItemCategory != null && !item.Categories.Contains(_selectedItemCategory))
                continue;

            if (!string.IsNullOrWhiteSpace(filter) &&
                !item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !item.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !item.Categories.Any(category => category.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!_prototypes.TryIndex<EntityPrototype>(item.Prototype, out var prototype))
                continue;

            var row = new RequisitionsItemRow(item, prototype, EntMan.System<SpriteSystem>(), GetItemStockText(item.Prototype));
            row.AddButton.Disabled = !CanAddItem(item);
            row.AddButton.OnPressed += _ => AddToCart(item.Prototype);
            view.ItemsContainer.AddChild(row);
            visible++;
        }

        if (visible == 0)
            view.ItemsContainer.AddChild(new Label { Text = Loc.GetString("cmu-asrs-no-results") });
        view.ResultCountLabel.Text = Loc.GetString("cmu-asrs-results", ("count", visible));
        RebuildCart(computer);
    }

    private void AddItemCategoryButton(string label, string? category)
    {
        var selected = _selectedItemCategory == category;
        var button = new Button
        {
            Text = $"{(selected ? "> " : string.Empty)}{label}",
            HorizontalExpand = true,
        };
        button.Label.Align = Label.AlignMode.Left;
        CmuButtonStyles.Apply(button, selected ? CmuButtonStyles.Variant.Affirm : CmuButtonStyles.Variant.Neutral);
        button.OnPressed += _ =>
        {
            _selectedItemCategory = category;
            RebuildItemizedBrowser();
        };
        _window!.ItemizedView.CategoriesContainer.AddChild(button);
    }

    private bool CanAddItem(RequisitionsItemEntry item)
    {
        _cart.TryGetValue(item.Prototype, out var amount);
        if (amount >= 99)
            return false;

        return !_itemStock.TryGetValue(item.Prototype, out var stock) || amount < stock.Current;
    }

    private void AddToCart(EntProtoId prototype)
    {
        if (!_entities.TryGetComponent(Owner, out RequisitionsComputerComponent? computer) ||
            computer.ItemCatalog.FirstOrDefault(item => item.Prototype == prototype) is not { } item ||
            !CanAddItem(item))
        {
            return;
        }

        _cart.TryGetValue(prototype, out var amount);
        _cart[prototype] = amount + 1;
        _window!.ItemizedView.FeedbackLabel.Text = string.Empty;
        RebuildItemizedBrowser();
    }

    private void RemoveFromCart(EntProtoId prototype)
    {
        if (!_cart.TryGetValue(prototype, out var amount))
            return;

        if (amount <= 1)
            _cart.Remove(prototype);
        else
            _cart[prototype] = amount - 1;
        RebuildItemizedBrowser();
    }

    private void RebuildCart(RequisitionsComputerComponent computer)
    {
        var view = _window!.ItemizedView;
        view.CartContainer.RemoveAllChildren();
        var cost = 0;
        var weight = 0;
        foreach (var (prototype, amount) in _cart.OrderBy(pair => pair.Key.Id).ToArray())
        {
            if (computer.ItemCatalog.FirstOrDefault(item => item.Prototype == prototype) is not { } item)
            {
                _cart.Remove(prototype);
                continue;
            }

            cost += item.Cost * amount;
            weight += item.Weight * amount;

            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 4,
            };
            row.AddChild(new Label
            {
                Text = $"{amount}x {item.Name}",
                HorizontalExpand = true,
                ClipText = true,
                ToolTip = item.Name,
            });
            var remove = new Button { Text = "−", MinWidth = 34, ToolTip = Loc.GetString("cmu-asrs-cart-remove") };
            CmuButtonStyles.Apply(remove, CmuButtonStyles.Variant.Caution);
            var captured = prototype;
            remove.OnPressed += _ => RemoveFromCart(captured);
            row.AddChild(remove);
            view.CartContainer.AddChild(row);
        }

        var slots = EstimateShipmentSlots(computer);
        view.CartEmptyLabel.Visible = _cart.Count == 0;
        view.CartCostLabel.Text = Loc.GetString("cmu-asrs-cart-cost", ("cost", cost));
        view.CartWeightLabel.Text = Loc.GetString("cmu-asrs-cart-weight", ("weight", weight));
        view.CartCratesLabel.Text = Loc.GetString("cmu-asrs-cart-crates", ("crates", slots));
        view.CheckoutButton.Disabled = _cart.Count == 0 ||
                                       _pendingCheckout != null ||
                                       _lastState == null ||
                                       cost > _lastState.Balance ||
                                       slots > _lastState.AvailableSlots ||
                                       !CartStockAvailable(computer);
    }

    private bool CartStockAvailable(RequisitionsComputerComponent computer)
    {
        foreach (var (prototype, amount) in _cart)
        {
            if (computer.ItemCatalog.FirstOrDefault(item => item.Prototype == prototype) is not { } item)
                return false;

            if (_itemStock.TryGetValue(item.Prototype, out var stock) && amount > stock.Current)
                return false;
        }

        return true;
    }

    private int EstimateShipmentSlots(RequisitionsComputerComponent computer)
    {
        var bins = new List<int>();
        var loose = 0;
        var weights = new List<int>();
        foreach (var (prototype, amount) in _cart)
        {
            if (computer.ItemCatalog.FirstOrDefault(item => item.Prototype == prototype) is not { } item)
                continue;

            for (var i = 0; i < amount; i++)
            {
                if (item.Packable && item.Weight <= computer.ItemShipmentWeightLimit)
                    weights.Add(item.Weight);
                else
                    loose++;
            }
        }

        foreach (var itemWeight in weights.OrderByDescending(weight => weight))
        {
            var bin = bins.FindIndex(weight => weight + itemWeight <= computer.ItemShipmentWeightLimit);
            if (bin < 0)
                bins.Add(itemWeight);
            else
                bins[bin] += itemWeight;
        }

        return loose + bins.Count;
    }

    private string GetItemStockText(EntProtoId prototype)
    {
        if (!_itemStock.TryGetValue(prototype, out var stock))
            return Loc.GetString("cmu-asrs-stock-unlimited");

        if (stock.Current < stock.Max)
        {
            return Loc.GetString(
                "cmu-asrs-stock-count-refill",
                ("current", stock.Current),
                ("max", stock.Max),
                ("time", FormatTime(stock.SecondsUntilNextReplenish)));
        }

        return Loc.GetString("cmu-asrs-stock-count", ("current", stock.Current), ("max", stock.Max));
    }

    private void TryCheckout()
    {
        if (_pendingCheckout != null ||
            _cart.Count == 0 ||
            !_entities.TryGetComponent(Owner, out RequisitionsComputerComponent? computer) ||
            !CartStockAvailable(computer))
        {
            return;
        }

        var lines = _cart.Select(pair => new RequisitionsCheckoutLine(pair.Key, pair.Value)).ToList();
        var requestId = ++_checkoutRequestId;
        _pendingCheckout = requestId;
        _window!.ItemizedView.FeedbackLabel.FontColorOverride = CrtTerminalPalette.Caution;
        _window.ItemizedView.FeedbackLabel.Text = Loc.GetString("cmu-asrs-checkout-pending");
        _window.ItemizedView.CheckoutButton.Disabled = true;
        SendMessage(new RequisitionsCheckoutMsg(requestId, lines));
    }

    private void RebuildBrowser()
    {
        if (_window == null ||
            !_entities.TryGetComponent(Owner, out RequisitionsComputerComponent? computer))
        {
            return;
        }

        if (_selectedCategory != null &&
            _selectedCategory.Value >= computer.Categories.Count)
        {
            _selectedCategory = null;
            _selectedOrder = null;
        }

        if (_selectedCategory == null &&
            _selectedOrder == null &&
            computer.Categories.Count > 0)
        {
            _selectedCategory = 0;
            _selectedOrder = 0;
        }

        RebuildCategories(computer);
        RebuildOrders(computer);
        UpdatePreview(computer);
    }

    private void RebuildCategories(RequisitionsComputerComponent computer)
    {
        var categoryHeader = new FormattedMessage();
        categoryHeader.AddMarkupOrThrow("[bold]CATEGORIES[/bold]");
        _window!.OrderCategoriesView.CategoryHeaderLabel.SetMessage(categoryHeader);
        _window.OrderCategoriesView.CategoriesContainer.DisposeAllChildren();

        for (var categoryIndex = 0; categoryIndex < computer.Categories.Count; categoryIndex++)
        {
            var category = computer.Categories[categoryIndex];
            var selected = _selectedCategory == categoryIndex;
            var categoryButton = new Button
            {
                Text = $"{(selected ? "> " : string.Empty)}{GetCategoryLabel(category.Name)}",
                HorizontalExpand = true,
                StyleClasses = { "ButtonSquare" },
            };
            categoryButton.Label.AddStyleClass(CMStyleClasses.CMLabelAlignLeft);
            categoryButton.Label.ClipText = true;
            SetButtonCrtMode(categoryButton, IsCrtMode());

            var index = categoryIndex;
            categoryButton.OnPressed += _ => SelectCategory(index);
            _window.OrderCategoriesView.CategoriesContainer.AddChild(categoryButton);
        }
    }

    private void RebuildOrders(RequisitionsComputerComponent computer)
    {
        _window!.OrderCategoriesView.OrdersContainer.DisposeAllChildren();

        var filter = _window.OrderCategoriesView.SearchBar.Text?.Trim();
        var searching = !string.IsNullOrWhiteSpace(filter);
        var header = "ALL CATEGORIES";
        if (searching)
            header = "SEARCH RESULTS";

        (int Category, int Order)? firstVisible = null;
        var selectedVisible = false;
        for (var categoryIndex = 0; categoryIndex < computer.Categories.Count; categoryIndex++)
        {
            if (!searching &&
                _selectedCategory != null &&
                _selectedCategory.Value != categoryIndex)
            {
                continue;
            }

            var category = computer.Categories[categoryIndex];
            if (!searching)
                header = category.Name.ToUpperInvariant();

            for (var orderIndex = 0; orderIndex < category.Entries.Count; orderIndex++)
            {
                var entry = category.Entries[orderIndex];
                if (searching && !MatchesFilter(category.Name, entry, filter!))
                    continue;

                firstVisible ??= (categoryIndex, orderIndex);
                if (_selectedCategory == categoryIndex &&
                    _selectedOrder == orderIndex)
                {
                    selectedVisible = true;
                }

                _window.OrderCategoriesView.OrdersContainer.AddChild(CreateOrderControl(
                    categoryIndex,
                    orderIndex,
                    entry));
            }
        }

        if (!selectedVisible && firstVisible != null)
        {
            _selectedCategory = firstVisible.Value.Category;
            _selectedOrder = firstVisible.Value.Order;
        }

        if (firstVisible == null)
            _window.OrderCategoriesView.OrdersContainer.AddChild(new Label { Text = "No matching orders." });

        var catalogHeader = new FormattedMessage();
        catalogHeader.AddMarkupOrThrow($"[bold]{header}[/bold]");
        _window.OrderCategoriesView.CatalogHeaderLabel.SetMessage(catalogHeader);
    }

    private RequisitionsOrderButton CreateOrderControl(int categoryIndex, int orderIndex, RequisitionsEntry entry)
    {
        var order = new RequisitionsOrderButton();
        order.SetEntry(categoryIndex, orderIndex, GetEntryName(entry), GetEntryDescription(entry), entry.Cost);
        order.SetStock(GetStockText(categoryIndex, orderIndex));
        order.SetCrtMode(IsCrtMode());
        SetPrototypeIcon(order.Texture, entry);

        var category = categoryIndex;
        var entryIndex = orderIndex;
        order.DetailsButton.OnPressed += _ => SelectOrder(category, entryIndex, true);
        order.OrderButton.OnPressed += _ => TryOrder(category, entryIndex);

        UpdateOrderButton(order);
        return order;
    }

    private void UpdatePreview(RequisitionsComputerComponent computer)
    {
        var previewHeader = new FormattedMessage();
        previewHeader.AddMarkupOrThrow("[bold]ORDER PREVIEW[/bold]");
        _window!.OrderCategoriesView.PreviewHeaderLabel.SetMessage(previewHeader);
        _window.OrderCategoriesView.PreviewPanel.Visible = _previewOpen;

        if (_selectedCategory == null ||
            _selectedOrder == null ||
            !TryGetEntry(computer, _selectedCategory.Value, _selectedOrder.Value, out var entry))
        {
            _window.OrderCategoriesView.PreviewPanel.Visible = false;
            _window.OrderCategoriesView.PreviewTexture.Textures.Clear();
            _window.OrderCategoriesView.PreviewNameLabel.Text = "No order selected";
            _window.OrderCategoriesView.PreviewCostLabel.Text = string.Empty;
            _window.OrderCategoriesView.PreviewStockLabel.Text = string.Empty;
            _window.OrderCategoriesView.PreviewDescriptionLabel.SetMessage(string.Empty);
            _window.OrderCategoriesView.PreviewContentsLabel.SetMessage(string.Empty);
            _window.OrderCategoriesView.PreviewOrderButton.Disabled = true;
            return;
        }

        SetPrototypeIcon(_window.OrderCategoriesView.PreviewTexture, entry);
        _window.OrderCategoriesView.PreviewNameLabel.Text = GetEntryName(entry);
        _window.OrderCategoriesView.PreviewCostLabel.Text = $"Cost: ${entry.Cost}";
        _window.OrderCategoriesView.PreviewStockLabel.Text = GetStockText(_selectedCategory.Value, _selectedOrder.Value);
        _window.OrderCategoriesView.PreviewDescriptionLabel.SetMessage(GetEntryDescription(entry));
        _window.OrderCategoriesView.PreviewContentsLabel.SetMessage(GetContentsText(entry));
        _window.OrderCategoriesView.PreviewOrderButton.Disabled = !CanOrder(_selectedCategory.Value, _selectedOrder.Value, entry);
    }

    private bool MatchesFilter(string categoryName, RequisitionsEntry entry, string filter)
    {
        return categoryName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               GetEntryName(entry).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               GetEntryDescription(entry).Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void SelectCategory(int? category)
    {
        _selectedCategory = category;
        _selectedOrder = category == null ? null : 0;
        _previewOpen = false;
        RebuildBrowser();
    }

    private void SelectOrder(int category, int order, bool openPreview = false)
    {
        _selectedCategory = category;
        _selectedOrder = order;
        _previewOpen |= openPreview;

        if (_window != null &&
            _entities.TryGetComponent(Owner, out RequisitionsComputerComponent? computer))
        {
            UpdatePreview(computer);
        }
    }

    private void TryOrderSelected()
    {
        if (_selectedCategory == null ||
            _selectedOrder == null)
        {
            return;
        }

        TryOrder(_selectedCategory.Value, _selectedOrder.Value);
    }

    private void TryOrder(int category, int order)
    {
        if (!_entities.TryGetComponent(Owner, out RequisitionsComputerComponent? computer) ||
            !TryGetEntry(computer, category, order, out var entry) ||
            !CanOrder(category, order, entry))
        {
            return;
        }

        _selectedCategory = category;
        _selectedOrder = order;
        SendMessage(new RequisitionsBuyMsg(category, order));
    }

    private bool CanOrder(int category, int order, RequisitionsEntry entry)
    {
        if (_lastState == null ||
            _lastState.Balance < entry.Cost ||
            _lastState.Full)
        {
            return false;
        }

        return !_stock.TryGetValue((category, order), out var stock) || stock.Current > 0;
    }

    private void UpdateOrderButton(RequisitionsOrderButton order)
    {
        if (!_entities.TryGetComponent(Owner, out RequisitionsComputerComponent? computer) ||
            !TryGetEntry(computer, order.Category, order.Order, out var entry))
        {
            order.OrderButton.Disabled = true;
            return;
        }

        var canOrder = CanOrder(order.Category, order.Order, entry);
        order.OrderButton.Disabled = !canOrder;
        order.CostLabel.Modulate = _lastState != null && _lastState.Balance < order.Cost ? Color.Red : Color.White;
        order.StockLabel.Modulate = _stock.TryGetValue((order.Category, order.Order), out var stock) && stock.Current <= 0
            ? Color.Red
            : Color.White;
    }

    private bool TryGetEntry(
        RequisitionsComputerComponent computer,
        int category,
        int order,
        [NotNullWhen(true)] out RequisitionsEntry? entry)
    {
        entry = null;
        if (category < 0 ||
            category >= computer.Categories.Count)
        {
            return false;
        }

        var categoryEntry = computer.Categories[category];
        if (order < 0 ||
            order >= categoryEntry.Entries.Count)
        {
            return false;
        }

        entry = categoryEntry.Entries[order];
        return true;
    }

    private string GetEntryName(RequisitionsEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Name))
            return entry.Name;

        return _prototypes.TryIndex<EntityPrototype>(entry.Crate, out var prototype)
            ? prototype.Name
            : entry.Crate;
    }

    private string GetEntryDescription(RequisitionsEntry entry)
    {
        return _prototypes.TryIndex<EntityPrototype>(entry.Crate, out var prototype) &&
               !string.IsNullOrWhiteSpace(prototype.Description)
            ? prototype.Description
            : "No manifest description.";
    }

    private string GetContentsText(RequisitionsEntry entry)
    {
        if (entry.Entities.Count == 0)
            return "Delivered as a sealed crate.";

        var contents = "MANIFEST";
        foreach (var entity in entry.Entities)
        {
            contents += _prototypes.TryIndex<EntityPrototype>(entity, out var prototype)
                ? $"\n- {prototype.Name}"
                : $"\n- {entity}";
        }

        return contents;
    }

    private string GetStockText(int category, int order)
    {
        if (!_stock.TryGetValue((category, order), out var stock))
            return "Stock: unlimited";

        var refill = stock.Current < stock.Max
            ? $"  +{FormatTime(stock.SecondsUntilNextReplenish)}"
            : string.Empty;

        return $"Stock: {stock.Current}/{stock.Max}{refill}";
    }

    private static string FormatTime(int seconds)
    {
        if (seconds <= 0)
            return "now";

        var time = TimeSpan.FromSeconds(seconds);
        return $"{(int) time.TotalMinutes:00}:{time.Seconds:00}";
    }

    private void SetPrototypeIcon(LayeredTextureRect texture, RequisitionsEntry entry)
    {
        texture.Textures.Clear();
        texture.Modulate = Color.White;

        if (!_prototypes.TryIndex<EntityPrototype>(entry.Crate, out var prototype))
            return;

        texture.Textures = EntMan.System<SpriteSystem>().GetPrototypeTextures(prototype)
            .Select(layer => layer.Default)
            .ToList();

        if (prototype.TryComp<SpriteComponent>(CompName.Get<SpriteComponent>(EntMan.ComponentFactory), out var sprite) &&
            sprite.AllLayers.FirstOrDefault() is { } firstLayer)
        {
            texture.Modulate = firstLayer.Color;
        }
    }

    private void TrySendPlatformMessage()
    {
        if (_raisePlatform == null)
            return;

        SendMessage(new RequisitionsPlatformMsg(_raisePlatform.Value));
    }

    private void ApplyDisplayMode()
    {
        if (_window == null)
            return;

        var crt = IsCrtMode();
        var view = _window.OrderCategoriesView;

        _window.ItemizedView.ApplyTheme();

        SetClass(view.RootPanel, StyleNano.StyleClassCrtPanel, crt);
        SetClass(view.CategoryPanel, StyleNano.StyleClassCrtInsetPanel, crt);
        SetClass(view.OrdersPanel, StyleNano.StyleClassCrtInsetPanel, crt);
        SetClass(view.PreviewPanel, StyleNano.StyleClassCrtInsetPanel, crt);
        SetClass(view.SearchBar, StyleNano.StyleClassCrtLineEdit, crt);

        SetClass(view.BudgetLabel, StyleNano.StyleClassCrtHeadingBig, crt);
        SetClass(view.PlatformLabel, StyleNano.StyleClassCrtRichText, crt);
        SetClass(view.CategoryHeaderLabel, StyleNano.StyleClassCrtRichText, crt);
        SetClass(view.CatalogHeaderLabel, StyleNano.StyleClassCrtRichText, crt);
        SetClass(view.PreviewHeaderLabel, StyleNano.StyleClassCrtRichText, crt);
        SetClass(view.PreviewDescriptionLabel, StyleNano.StyleClassCrtRichText, crt);
        SetClass(view.PreviewContentsLabel, StyleNano.StyleClassCrtRichText, crt);

        SetClass(view.PreviewNameLabel, StyleNano.StyleClassCrtText, crt);
        SetClass(view.PreviewCostLabel, StyleNano.StyleClassCrtText, crt);
        SetClass(view.PreviewStockLabel, StyleNano.StyleClassCrtDimText, crt);

        SetButtonCrtMode(view.PlatformButton, crt);
        SetButtonCrtMode(view.ItemsModeButton, crt);
        SetButtonCrtMode(view.PreviewOrderButton, crt);
    }

    private static bool IsCrtMode()
    {
        return StyleNano.CrtUiEnabled;
    }

    private static void SetButtonCrtMode(Button button, bool enabled)
    {
        SetClass(button, StyleNano.StyleClassCrtButton, enabled);
        SetClass(button.Label, StyleNano.StyleClassCrtButtonLabel, enabled);
    }

    private static void SetClass(Control control, string styleClass, bool enabled)
    {
        if (enabled)
        {
            if (!control.HasStyleClass(styleClass))
                control.AddStyleClass(styleClass);
            return;
        }

        control.RemoveStyleClass(styleClass);
    }

    private void ShowView(RequisitionsWindow window, Control view)
    {
        foreach (var child in window.Contents.Children)
        {
            child.Visible = child == view;
        }
    }

    private static string Truncate(string value, int length)
    {
        if (value.Length <= length)
            return value;

        return $"{value[..Math.Max(0, length - 3)]}...";
    }

    private static string GetCategoryLabel(string name)
    {
        var label = name.Replace(" and ", "/");
        var parenIndex = label.IndexOf(" (", StringComparison.Ordinal);
        if (parenIndex >= 0)
            label = label[..parenIndex];

        return Truncate(label, 21);
    }
}
