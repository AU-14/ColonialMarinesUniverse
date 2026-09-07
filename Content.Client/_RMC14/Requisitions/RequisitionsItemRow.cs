using System.Linq;
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
    public readonly Button FavoriteButton;
    public readonly LayeredTextureRect ItemIcon;

    public RequisitionsItemRow(
        RequisitionsItemEntry item,
        EntityPrototype prototype,
        SpriteSystem sprites,
        string stockText,
        int cartAmount,
        bool favorite,
        RequisitionsTerminalTheme theme)
    {
        PanelOverride = theme.Panel(theme.SurfaceRaised);
        HorizontalExpand = true;

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Margin = new Thickness(8),
        };
        AddChild(row);

        ItemIcon = new LayeredTextureRect { MinSize = new Vector2(42, 42) };
        ItemIcon.Textures = sprites.GetPrototypeTextures(prototype).Select(layer => layer.Default).ToList();
        row.AddChild(ItemIcon);

        var details = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        details.AddChild(new Label
        {
            Text = item.Name,
            FontColorOverride = theme.TextBright,
            ClipText = true,
            ToolTip = item.Name,
        });
        details.AddChild(new Label
        {
            Text = item.Units > 1
                ? $"${item.Cost} / {item.Units} UNITS  |  {item.Weight} WT  |  {stockText}"
                : $"${item.Cost}  |  {item.Weight} WT  |  {stockText}",
            FontColorOverride = theme.TextDim,
        });
        details.AddChild(new Label
        {
            Text = item.Description,
            ClipText = true,
            ToolTip = item.Description,
            FontColorOverride = theme.Text,
        });
        row.AddChild(details);

        FavoriteButton = new Button
        {
            Text = favorite ? "★" : "☆",
            MinWidth = 34,
            MinHeight = 42,
            ToolTip = Loc.GetString("cmu-asrs-favorite-toggle"),
        };
        theme.ApplyButton(FavoriteButton, primary: favorite);
        row.AddChild(FavoriteButton);

        AddButton = new Button
        {
            Text = cartAmount > 0 ? $"+  [{cartAmount}]" : "+",
            MinWidth = 54,
            MinHeight = 42,
            ToolTip = Loc.GetString("cmu-asrs-cart-add"),
        };
        theme.ApplyButton(AddButton, primary: true);
        row.AddChild(AddButton);
    }

    public void SetCartAmount(int amount, bool canAdd)
    {
        AddButton.Text = amount > 0 ? $"+  [{amount}]" : "+";
        AddButton.Disabled = !canAdd;
    }
}
