using System;
using System.Numerics;
using Content.Client._AU14.Insurgency.CustomFactions;
using Content.Shared._AU14.Insurgency;
using Content.Shared._AU14.Insurgency.Selection;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.Insurgency.Selection;

/// <summary>
///     The CLF-leader faction selection popup. Two columns: Default factions from the round (List A),
///     with non-matching ones greyed out, and the player's own Custom factions (List B), selectable
///     only when they hold the authorization flag. The server has the final say on every pick.
/// </summary>
public sealed class InsurgencyFactionSelectWindow : DefaultWindow
{
    private readonly IPrototypeManager _prototype;
    private readonly InsurgencyCustomFactionStore _customStore = new();

    private readonly Label _govforLabel;
    private readonly BoxContainer _defaultList;
    private readonly BoxContainer _customList;

    private bool _canUseCustom;

    public event Action<int>? OnSelectDefault;
    public event Action<FactionDefinition>? OnSelectCustom;

    public InsurgencyFactionSelectWindow()
    {
        _prototype = IoCManager.Resolve<IPrototypeManager>();

        Title = Loc.GetString("insfor-select-title");
        MinSize = new Vector2(900, 620);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true };

        _govforLabel = new Label { Text = string.Empty, Margin = new Thickness(4) };
        root.AddChild(_govforLabel);

        var columns = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, VerticalExpand = true };

        // Left column: Default factions.
        var left = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        left.AddChild(new Label { Text = Loc.GetString("insfor-select-default-header"), StyleClasses = { "LabelHeading" } });
        _defaultList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        left.AddChild(new ScrollContainer { Children = { _defaultList }, VerticalExpand = true, HorizontalExpand = true });

        // Right column: Custom factions.
        var right = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var customHeader = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        customHeader.AddChild(new Label { Text = Loc.GetString("insfor-select-custom-header"), StyleClasses = { "LabelHeading" }, HorizontalExpand = true });
        var refresh = new Button { Text = Loc.GetString("insfor-select-custom-refresh") };
        refresh.OnPressed += _ => RebuildCustom();
        customHeader.AddChild(refresh);
        right.AddChild(customHeader);
        _customList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        right.AddChild(new ScrollContainer { Children = { _customList }, VerticalExpand = true, HorizontalExpand = true });

        columns.AddChild(left);
        columns.AddChild(right);
        root.AddChild(columns);
        Contents.AddChild(root);
    }

    public void SetState(InsurgencyFactionSelectEuiState state)
    {
        _canUseCustom = state.CanUseCustom;

        _govforLabel.Text = state.GovforPlatoonName is { } name
            ? Loc.GetString("insfor-select-govfor", ("name", name))
            : Loc.GetString("insfor-select-govfor-unknown");

        RebuildDefault(state);
        RebuildCustom();
    }

    private void RebuildDefault(InsurgencyFactionSelectEuiState state)
    {
        _defaultList.RemoveAllChildren();

        if (state.Defaults.Count == 0)
        {
            _defaultList.AddChild(new Label { Text = Loc.GetString("insfor-select-empty") });
            return;
        }

        foreach (var option in state.Defaults)
        {
            // Only factions that oppose the round's GOVFOR platoon may be picked; others are shown
            // greyed so the leader can see they exist but understands why they are unavailable.
            var id = option.Id;
            var row = BuildRow(
                option.Title,
                option.Description,
                option.FlagEntity,
                enabled: option.Opposes,
                disabledReason: Loc.GetString("insfor-select-not-opposed"),
                onPressed: () => OnSelectDefault?.Invoke(id));
            _defaultList.AddChild(row);
        }
    }

    private void RebuildCustom()
    {
        _customList.RemoveAllChildren();

        if (!_canUseCustom)
        {
            _customList.AddChild(new Label { Text = Loc.GetString("insfor-select-custom-locked") });
            return;
        }

        var customs = _customStore.List();
        if (customs.Count == 0)
        {
            _customList.AddChild(new Label { Text = Loc.GetString("insfor-select-custom-empty") });
            return;
        }

        foreach (var custom in customs)
        {
            var def = custom.Definition;
            var title = string.IsNullOrWhiteSpace(def.Metadata.Title) ? custom.Name : def.Metadata.Title;
            var row = BuildRow(
                title,
                def.Metadata.Description,
                def.Metadata.FlagEntity?.Id,
                enabled: true,
                disabledReason: null,
                onPressed: () => OnSelectCustom?.Invoke(def));
            _customList.AddChild(row);
        }
    }

    // One selectable faction row: flag sprite (if any), title, a short description, and a Choose button.
    private Control BuildRow(string title, string description, string? flagEntity, bool enabled, string? disabledReason, Action onPressed)
    {
        var panel = new PanelContainer { Margin = new Thickness(0, 0, 0, 6) };
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(6) };

        if (flagEntity != null && _prototype.HasIndex<EntityPrototype>(flagEntity))
        {
            var view = new EntityPrototypeView { MinSize = new Vector2(48, 48) };
            view.SetPrototype(new EntProtoId(flagEntity));
            row.AddChild(view);
        }

        var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, Margin = new Thickness(6, 0) };
        text.AddChild(new Label { Text = title, StyleClasses = { "LabelHeading" } });
        if (!string.IsNullOrWhiteSpace(description))
            text.AddChild(new Label { Text = Truncate(description, 160), MaxWidth = 460 });
        if (!enabled && disabledReason != null)
            text.AddChild(new Label { Text = disabledReason, StyleClasses = { "LabelSubText" } });
        row.AddChild(text);

        var choose = new Button
        {
            Text = Loc.GetString("insfor-select-choose"),
            Disabled = !enabled,
            VerticalAlignment = VAlignment.Center,
        };
        choose.OnPressed += _ => onPressed();
        row.AddChild(choose);

        panel.AddChild(row);
        return panel;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";
}
