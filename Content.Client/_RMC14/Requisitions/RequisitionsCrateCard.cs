using System.Numerics;
using System.Linq;
using Content.Shared._RMC14.Requisitions;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Requisitions;

public sealed class RequisitionsCrateCard : PanelContainer
{
    public readonly Control LandingAnchor;

    public RequisitionsCrateCard(
        string title,
        string state,
        int weight,
        int weightLimit,
        IEnumerable<(RequisitionsItemEntry Item, int Amount)> lines,
        SpriteSystem sprites,
        IPrototypeManager prototypes,
        RequisitionsTerminalTheme theme,
        Action<EntProtoId, LayeredTextureRect> add,
        Action<EntProtoId, LayeredTextureRect> remove)
    {
        PanelOverride = theme.Panel(theme.SurfaceRaised, corners: true);
        HorizontalExpand = true;
        MinWidth = 310;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 4,
        };
        AddChild(root);

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        header.AddChild(new Label
        {
            Text = title,
            FontColorOverride = theme.TextBright,
            HorizontalExpand = true,
        });
        header.AddChild(new Label { Text = state, FontColorOverride = theme.Accent });
        root.AddChild(header);

        var sealedCrate = weight >= weightLimit;
        root.AddChild(new RequisitionsSealStrip(sealedCrate));
        if (sealedCrate)
            root.AddChild(new RequisitionsSealStamp($"ASRS-{title.Sum(character => character) % 10000:0000}"));

        var barRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };
        var weightBar = new RequisitionsWeightBar { HorizontalExpand = true };
        weightBar.SetLoad(weight, weightLimit, theme, animate: false);
        barRow.AddChild(weightBar);
        barRow.AddChild(new Label
        {
            Text = $"{weight}/{weightLimit} WT",
            FontColorOverride = weight >= weightLimit ? theme.Caution : theme.TextDim,
        });
        root.AddChild(barRow);

        LandingAnchor = weightBar;
        foreach (var (item, amount) in lines.OrderBy(line => line.Item.Name))
        {
            var line = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 4,
                HorizontalExpand = true,
            };

            var icon = new LayeredTextureRect
            {
                MinSize = new Vector2(26, 26),
                SetSize = new Vector2(26, 26),
            };
            if (prototypes.TryIndex<EntityPrototype>(item.Prototype, out var prototype))
                icon.Textures = sprites.GetPrototypeTextures(prototype).Select(layer => layer.Default).ToList();
            line.AddChild(icon);
            line.AddChild(new Label
            {
                Text = item.Name,
                ToolTip = item.Name,
                ClipText = true,
                HorizontalExpand = true,
                FontColorOverride = theme.Text,
            });

            var minus = new Button { Text = "−", MinWidth = 28, ToolTip = Loc.GetString("cmu-asrs-cart-remove") };
            theme.ApplyButton(minus, warning: true);
            minus.OnPressed += _ => remove(item.Prototype, icon);
            line.AddChild(minus);
            line.AddChild(new Label
            {
                Text = amount.ToString("00"),
                MinWidth = 26,
                Align = Label.AlignMode.Center,
                FontColorOverride = theme.TextBright,
            });
            var plus = new Button { Text = "+", MinWidth = 28, ToolTip = Loc.GetString("cmu-asrs-cart-add") };
            theme.ApplyButton(plus, primary: true);
            plus.OnPressed += _ => add(item.Prototype, icon);
            line.AddChild(plus);
            root.AddChild(line);
        }
    }
}
