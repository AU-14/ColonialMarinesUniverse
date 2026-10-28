using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.UserInterface.Controls;
using Content.Shared.CMU14.Round.Objectives;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Round.Objectives;

/// <summary>
///     Intel on one objective, in the CRT theme: the tiers already unlocked, then the next tier and the button that
///     pays for it. Titles and descriptions wrap, so long ones stay inside the window.
/// </summary>
public sealed class ObjectiveIntelWindow : DefaultWindow
{
    private readonly CrtConsoleKit _kit = new();

    private readonly BoxContainer _titleBox;
    private readonly Label _subheading;
    private readonly PanelContainer _pointsTag;
    private readonly Label _pointsText;
    private readonly BoxContainer _body;
    private readonly Label _footer;

    public ObjectiveIntelWindow()
    {
        Title = Loc.GetString("objective-intel-window-title");
        MinSize = new Vector2(440, 380);
        SetSize = new Vector2(520, 560);

        // Own header so a long objective title wraps instead of being clipped.
        var header = CrtConsoleKit.Strip(CrtTerminalPalette.Surface3);
        var headerRow = CrtConsoleKit.Row();
        var titles = CrtConsoleKit.Column(2);
        titles.HorizontalExpand = true;
        _titleBox = CrtConsoleKit.Column(0);
        titles.AddChild(_titleBox);
        _subheading = CrtConsoleKit.Text(string.Empty, _kit.Small, CrtTerminalPalette.TextDim);
        _subheading.ClipText = true;
        titles.AddChild(_subheading);
        headerRow.AddChild(titles);
        _pointsTag = _kit.Tag(out _pointsText);
        headerRow.AddChild(_pointsTag);
        header.AddChild(headerRow);
        _kit.Root.AddChild(header);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _body = CrtConsoleKit.Column(8);
        _body.HorizontalExpand = true;
        _body.Margin = new Thickness(0, 0, 6, 0);
        scroll.AddChild(_body);
        _kit.Root.AddChild(scroll);

        _kit.Root.AddChild(_kit.Footer(out _footer));
        _footer.Text = Loc.GetString("cmu-ui-objintel-footer");

        Contents.AddChild(_kit.Tube);
    }

    public void Populate(
        string objectiveId,
        string defaultTitle,
        List<ObjectiveIntelTierEntry>? tiers,
        int unlockedTier,
        float factionPoints,
        Action<int>? unlockCallback = null)
    {
        tiers ??= new List<ObjectiveIntelTierEntry>();
        unlockedTier = Math.Clamp(unlockedTier, 0, tiers.Count);

        _titleBox.RemoveAllChildren();
        _titleBox.AddChild(CrtConsoleKit.BoldWrapping(defaultTitle.ToUpperInvariant(), CrtTerminalPalette.TextBright));
        _subheading.Text = Loc.GetString("cmu-ui-objintel-subheading", ("unlocked", unlockedTier), ("total", tiers.Count));
        CrtConsoleKit.SetTag(_pointsTag, _pointsText,
            Loc.GetString("cmu-ui-objintel-points", ("points", factionPoints.ToString("0.##"))), CrtTerminalPalette.Accent);

        _body.RemoveAllChildren();

        // What's been unlocked so far.
        var unlocked = _kit.Section(Loc.GetString("cmu-ui-objintel-unlocked"), out var unlockedColumn, null,
            _kit.Chip(unlockedTier.ToString(), CrtTerminalPalette.TextDim));
        if (unlockedTier == 0)
            unlockedColumn.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-ui-objintel-none"), CrtTerminalPalette.TextDim));

        for (var i = 0; i < unlockedTier; i++)
            unlockedColumn.AddChild(TierCard(tiers[i], i, CrtTerminalPalette.Accent, true));
        _body.AddChild(unlocked);

        // The next tier and the button to buy it.
        var next = _kit.Section(Loc.GetString("cmu-ui-objintel-next"), out var nextColumn, CrtTerminalPalette.Caution);
        if (unlockedTier >= tiers.Count)
        {
            nextColumn.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("objective-intel-all-unlocked"), CrtTerminalPalette.TextDim));
        }
        else
        {
            var tier = tiers[unlockedTier];
            nextColumn.AddChild(TierCard(tier, unlockedTier, CrtTerminalPalette.Caution, false));

            var cost = tier.CostToUnlock;
            var affordable = factionPoints >= cost;
            if (!affordable)
            {
                nextColumn.AddChild(CrtConsoleKit.Wrapping(
                    Loc.GetString("cmu-ui-objintel-short", ("points", (cost - factionPoints).ToString("0.##"))),
                    CrtTerminalPalette.Alert));
            }

            var unlock = new ConfirmButton
            {
                Text = Loc.GetString("cmu-ui-objintel-unlock", ("cost", cost.ToString("0.##"))),
                ConfirmationText = Loc.GetString("cmu-ui-objintel-confirm"),
                HorizontalExpand = true,
                Disabled = unlockCallback == null || !affordable,
            };
            CmuButtonStyles.Apply(unlock, CmuButtonStyles.Variant.Affirm);
            unlock.Label.FontOverride = _kit.Bold;
            if (unlockCallback != null)
            {
                var index = unlockedTier;
                unlock.OnPressed += _ => unlockCallback(index);
            }

            nextColumn.AddChild(unlock);
        }

        _body.AddChild(next);
    }

    private Control TierCard(ObjectiveIntelTierEntry tier, int index, Color accent, bool unlocked)
    {
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 },
        };
        var row = CrtConsoleKit.Row(8);
        row.AddChild(new PanelContainer { MinWidth = 4, PanelOverride = new StyleBoxFlat { BackgroundColor = accent } });

        var column = CrtConsoleKit.Column(3);
        column.HorizontalExpand = true;
        column.Margin = new Thickness(0, 6, 8, 6);
        column.AddChild(CrtConsoleKit.Text(Loc.GetString("cmu-ui-objintel-tier", ("tier", index + 1)), _kit.Small, accent));
        column.AddChild(CrtConsoleKit.BoldWrapping(tier.Title, CrtTerminalPalette.TextBright));
        if (!string.IsNullOrEmpty(tier.Description))
        {
            column.AddChild(CrtConsoleKit.WrappingMarkup(tier.Description,
                unlocked ? CrtTerminalPalette.Text : CrtTerminalPalette.TextDim));
        }

        row.AddChild(column);
        panel.AddChild(row);
        return panel;
    }
}
