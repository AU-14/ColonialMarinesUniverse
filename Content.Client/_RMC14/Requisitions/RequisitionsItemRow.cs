using System.Linq;
using Content.Client._CMU14.Interface;
using Content.Shared._RMC14.Requisitions;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Client._RMC14.Requisitions;

public sealed class RequisitionsItemRow : PanelContainer
{
    public readonly Button AddButton;

    public RequisitionsItemRow(
        RequisitionsItemEntry item,
        EntityPrototype prototype,
        SpriteSystem sprites,
        string stockText)
    {
        PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 };
        HorizontalExpand = true;

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = CmuPanelMetrics.GroupSeparation,
            Margin = CmuPanelMetrics.PanelPadding,
        };
        AddChild(row);

        var icon = new LayeredTextureRect { MinSize = new Vector2(42, 42) };
        icon.Textures = sprites.GetPrototypeTextures(prototype).Select(layer => layer.Default).ToList();
        row.AddChild(icon);

        var details = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        details.AddChild(new Label
        {
            Text = item.Name,
            FontColorOverride = CrtTerminalPalette.TextBright,
            ClipText = true,
        });
        details.AddChild(new Label
        {
            Text = $"${item.Cost}  |  {item.Weight} WT  |  {stockText}",
            FontColorOverride = CrtTerminalPalette.TextDim,
        });
        details.AddChild(new Label
        {
            Text = item.Description,
            ClipText = true,
            ToolTip = item.Description,
            FontColorOverride = CrtTerminalPalette.Text,
        });
        row.AddChild(details);

        AddButton = new Button { Text = "+", MinWidth = 44, MinHeight = 42 };
        CmuButtonStyles.Apply(AddButton, CmuButtonStyles.Variant.Affirm);
        row.AddChild(AddButton);
    }
}
