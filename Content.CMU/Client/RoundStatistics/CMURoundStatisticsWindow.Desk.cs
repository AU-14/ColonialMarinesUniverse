using System;
using System.Numerics;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Shared.CMU14.RoundStatistics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.RoundStatistics;

public sealed partial class CMURoundStatisticsWindow
{
    private sealed class MatrixScroll : ScrollContainer
    {
        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            var size = base.MeasureOverride(availableSize);
            // Report the rows' height without making a wide matrix expand the entire window.
            return new Vector2(Math.Min(size.X, availableSize.X), size.Y);
        }
    }

    private static readonly Color DeskLine = Color.FromHex("#2c4938");
    private static readonly Color DeskRaised = Color.FromHex("#183225");
    private static readonly Color DeskPurple = Color.FromHex("#352338");
    private static readonly Color DeskAccent = Color.FromHex("#d5a3d0");

    private static StyleBoxFlat DeskStyle(Color background, Color border, Thickness? thickness = null) => new()
    {
        BackgroundColor = background,
        BorderColor = border,
        BorderThickness = thickness ?? new Thickness(0, 0, 0, 1),
    };

    private static PanelContainer Surface(Color background, Color border) => new()
    {
        HorizontalExpand = true,
        PanelOverride = DeskStyle(background, border),
    };

    private static Button DeskButton(string text, Action action)
    {
        var button = new Button { Text = text, MinHeight = 32 };
        button.OnPressed += _ => action();
        CrtLobbyTheme.Apply(button, useCrtTypography: true);
        button.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = DeskRaised,
            BorderColor = DeskLine,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 10,
            ContentMarginRightOverride = 10,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6,
        };
        button.OnMouseEntered += _ => button.ModulateSelfOverride = Color.FromHex("#cce6d4");
        button.OnMouseExited += _ => button.ModulateSelfOverride = Color.White;
        return button;
    }

    private static void StyleNavigation(Button button, bool selected, bool mode)
    {
        button.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = selected ? DeskRaised : Card,
            BorderColor = selected ? mode ? DeskAccent : ColonistGreen : Color.Transparent,
            BorderThickness = mode ? new Thickness(0, 0, 0, 3) : new Thickness(2, 0, 0, 0),
            ContentMarginLeftOverride = 10,
            ContentMarginRightOverride = 10,
            ContentMarginTopOverride = 8,
            ContentMarginBottomOverride = 8,
        };
        button.Label.FontColorOverride = selected ? Text : Muted;
    }

    private void AddPageHeading(string title, int total)
    {
        var row = new BoxContainer { HorizontalExpand = true, SeparationOverride = 16, Margin = new Thickness(0, 4, 0, 14) };
        var label = MakeSectionLabel(title);
        label.StyleClasses.Add(StyleNano.StyleClassLabelBig);
        row.AddChild(label);
        row.AddChild(new Label
        {
            Text = Loc.GetString("cmu-round-stats-ending-count", ("count", total)),
            FontColorOverride = Muted,
        });
        _body.AddChild(row);
    }

    private static Label Cell(string text, float width = 0, bool right = false)
    {
        var label = new Label
        {
            Text = text,
            ToolTip = text,
            FontColorOverride = Text,
            ClipText = true,
            HorizontalExpand = width == 0,
            Align = right ? Label.AlignMode.Right : Label.AlignMode.Left,
        };
        if (width > 0)
            label.MinWidth = label.MaxWidth = width;
        return label;
    }

    private static RichTextLabel WrappedCell(string text, float width = 0, Color? color = null)
    {
        var label = new RichTextLabel { HorizontalExpand = width == 0, ToolTip = text };
        if (width > 0)
            label.MinWidth = label.MaxWidth = width;
        // Prototype names and recorded sources are plain text, never rich-text markup.
        label.SetMessage(text, color ?? Text);
        return label;
    }

    private static BoxContainer DataRow() => new()
    {
        HorizontalExpand = true,
        SeparationOverride = 14,
        Margin = new Thickness(8, 10),
    };

    private void AddDataRow(Control content, Action? action = null)
    {
        if (action == null)
        {
            var panel = Surface(Background, DeskLine);
            panel.AddChild(content);
            _body.AddChild(panel);
            return;
        }
        var button = new ContainerButton
        {
            HorizontalExpand = true,
            StyleBoxOverride = DeskStyle(Background, DeskLine),
        };
        button.AddChild(content);
        button.OnMouseEntered += _ => button.StyleBoxOverride = DeskStyle(DeskRaised, DeskLine);
        button.OnMouseExited += _ => button.StyleBoxOverride = DeskStyle(Background, DeskLine);
        button.OnPressed += _ => { if (!_loading) action(); };
        _body.AddChild(button);
    }

    private static Control CompactBar(CMURoundStatisticsPreset preset, int a, int b, int draws, int unknown)
    {
        var bar = new BoxContainer { SeparationOverride = 1, HorizontalExpand = true, MinHeight = 7, MaxHeight = 7, VerticalAlignment = VAlignment.Center };
        void Segment(int count, Color color)
        {
            if (count <= 0)
                return;
            bar.AddChild(new PanelContainer
            {
                HorizontalExpand = true,
                SizeFlagsStretchRatio = count,
                PanelOverride = new StyleBoxFlat { BackgroundColor = color },
            });
        }
        Segment(a, GetSideAColor(preset));
        Segment(b, GetSideBColor(preset));
        Segment(draws, DrawGray);
        Segment(unknown, UnknownGray);
        if (a + b + draws + unknown == 0)
            Segment(1, DeskRaised);
        bar.ToolTip = Loc.GetString("cmu-round-stats-counts", ("total", a + b + draws + unknown),
            ("decided", a + b), ("draws", draws), ("unknown", unknown));
        return bar;
    }

    private static Control RateCell(CMURoundModeStatistics mode, int a, int b, int draws, int unknown)
    {
        var box = Column();
        box.SeparationOverride = 5;
        var rates = new BoxContainer { HorizontalExpand = true };
        rates.AddChild(Cell(FormatRate(a, a + b)));
        rates.AddChild(Cell(FormatRate(b, a + b), right: true));
        box.AddChild(rates);
        box.AddChild(CompactBar(mode.Preset, a, b, draws, unknown));
        return box;
    }

    private static Control Frequency(int count, int max)
    {
        var row = new BoxContainer { HorizontalExpand = true, MinHeight = 18, MaxHeight = 18, SeparationOverride = 0, VerticalAlignment = VAlignment.Center };
        if (count > 0)
            row.AddChild(new PanelContainer { HorizontalExpand = true, SizeFlagsStretchRatio = count, PanelOverride = new StyleBoxFlat { BackgroundColor = Muted.WithAlpha(0.7f) } });
        if (max > count || count == 0)
            row.AddChild(new PanelContainer { HorizontalExpand = true, SizeFlagsStretchRatio = Math.Max(1, max - count), PanelOverride = new StyleBoxFlat { BackgroundColor = DeskRaised } });
        return row;
    }

    private void AddLegend(CMURoundModeStatistics mode)
    {
        var row = new BoxContainer { SeparationOverride = 18, Margin = new Thickness(8, 12), HorizontalExpand = true };
        foreach (var (label, color) in new[]
                 {
                     (mode.SideA, GetSideAColor(mode.Preset)), (mode.SideB, GetSideBColor(mode.Preset)),
                     (Loc.GetString("cmu-round-stats-draw"), DrawGray), (Loc.GetString("cmu-round-stats-unknown"), UnknownGray),
                 })
        {
            var item = new BoxContainer { SeparationOverride = 5 };
            item.AddChild(new PanelContainer { MinWidth = 7, MinHeight = 7, MaxHeight = 7, VerticalAlignment = VAlignment.Center, PanelOverride = new StyleBoxFlat { BackgroundColor = color } });
            item.AddChild(new Label { Text = label, FontColorOverride = Muted });
            row.AddChild(item);
        }
        _body.AddChild(row);
    }
}
