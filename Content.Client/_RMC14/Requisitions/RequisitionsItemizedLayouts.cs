using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._RMC14.Requisitions;

public sealed partial class RequisitionsLoadBayLayout : Control
{
    public RequisitionsLoadBayLayout() => RobustXamlLoader.Load(this);
}

public sealed class RequisitionsLayoutRefs
{
    public readonly Control Layout;
    public readonly PanelContainer RootPanel;
    public readonly PanelContainer StatusPanel;
    public readonly PanelContainer FilterPanel;
    public readonly PanelContainer CatalogPanel;
    public readonly PanelContainer CartPanel;
    public readonly PanelContainer CartSummaryPanel;
    public readonly PanelContainer PackingHintPanel;
    public readonly PanelContainer CheckoutStagePanel;
    public readonly PanelContainer ReceiptPanel;
    public readonly PanelContainer ItemPreviewPanel;
    public readonly Label BudgetLabel;
    public readonly Label ProjectedBudgetLabel;
    public readonly Label PlatformLabel;
    public readonly Label ResultCountLabel;
    public readonly Label PackingHintLabel;
    public readonly Label CartStateLabel;
    public readonly Label CartEmptyLabel;
    public readonly Label CartCostLabel;
    public readonly Label CartWeightLabel;
    public readonly Label CartCratesLabel;
    public readonly Label CartCapacityLabel;
    public readonly Label FeedbackLabel;
    public readonly Label CheckoutPhaseLabel;
    public readonly Label ReceiptLabel;
    public readonly Label StyleSubtitleLabel;
    public readonly Label PreviewNameLabel;
    public readonly Label PreviewMetaLabel;
    public readonly Button ItemsTabButton;
    public readonly Button BundlesTabButton;
    public readonly Button PlatformButton;
    public readonly Button CheckoutButton;
    public readonly Button ReceiptDismissButton;
    public readonly LineEdit SearchBar;
    public readonly BoxContainer CategoriesContainer;
    public readonly BoxContainer ItemsContainer;
    public readonly BoxContainer CartContainer;
    public readonly BoxContainer PlatformSlotsContainer;
    public readonly ProgressBar CheckoutProgress;
    public readonly RequisitionsTurntablePreview PreviewIcon;
    public readonly RequisitionsScanLine PreviewScanLine;

    public RequisitionsLayoutRefs(Control layout)
    {
        Layout = layout;
        RootPanel = Find<PanelContainer>("RootPanel");
        StatusPanel = Find<PanelContainer>("StatusPanel");
        FilterPanel = Find<PanelContainer>("FilterPanel");
        CatalogPanel = Find<PanelContainer>("CatalogPanel");
        CartPanel = Find<PanelContainer>("CartPanel");
        CartSummaryPanel = Find<PanelContainer>("CartSummaryPanel");
        PackingHintPanel = Find<PanelContainer>("PackingHintPanel");
        CheckoutStagePanel = Find<PanelContainer>("CheckoutStagePanel");
        ReceiptPanel = Find<PanelContainer>("ReceiptPanel");
        ItemPreviewPanel = Find<PanelContainer>("ItemPreviewPanel");
        BudgetLabel = Find<Label>("BudgetLabel");
        ProjectedBudgetLabel = Find<Label>("ProjectedBudgetLabel");
        PlatformLabel = Find<Label>("PlatformLabel");
        ResultCountLabel = Find<Label>("ResultCountLabel");
        PackingHintLabel = Find<Label>("PackingHintLabel");
        CartStateLabel = Find<Label>("CartStateLabel");
        CartEmptyLabel = Find<Label>("CartEmptyLabel");
        CartCostLabel = Find<Label>("CartCostLabel");
        CartWeightLabel = Find<Label>("CartWeightLabel");
        CartCratesLabel = Find<Label>("CartCratesLabel");
        CartCapacityLabel = Find<Label>("CartCapacityLabel");
        FeedbackLabel = Find<Label>("FeedbackLabel");
        CheckoutPhaseLabel = Find<Label>("CheckoutPhaseLabel");
        ReceiptLabel = Find<Label>("ReceiptLabel");
        StyleSubtitleLabel = Find<Label>("StyleSubtitleLabel");
        PreviewNameLabel = Find<Label>("PreviewNameLabel");
        PreviewMetaLabel = Find<Label>("PreviewMetaLabel");
        ItemsTabButton = Find<Button>("ItemsTabButton");
        BundlesTabButton = Find<Button>("BundlesTabButton");
        PlatformButton = Find<Button>("PlatformButton");
        CheckoutButton = Find<Button>("CheckoutButton");
        ReceiptDismissButton = Find<Button>("ReceiptDismissButton");
        SearchBar = Find<LineEdit>("SearchBar");
        CategoriesContainer = Find<BoxContainer>("CategoriesContainer");
        ItemsContainer = Find<BoxContainer>("ItemsContainer");
        CartContainer = Find<BoxContainer>("CartContainer");
        PlatformSlotsContainer = Find<BoxContainer>("PlatformSlotsContainer");
        CheckoutProgress = Find<ProgressBar>("CheckoutProgress");
        PreviewIcon = Find<RequisitionsTurntablePreview>("PreviewIcon");
        PreviewScanLine = Find<RequisitionsScanLine>("PreviewScanLine");
    }

    private T Find<T>(string name) where T : Control
    {
        return Layout.FindControl<T>(name);
    }
}
