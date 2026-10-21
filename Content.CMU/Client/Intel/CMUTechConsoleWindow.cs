using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.UserInterface.Controls;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared.FixedPoint;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.Utility;

namespace Content.Client.CMU14.Intel;

/// <summary>
///     The intel tech console in the CRT theme. Tiers down the left, the picked tier's options as cards grouped by
///     what they do (call-ins, fire missions, budgets), and the picked option's details with the buy button on the
///     right, all in one window so a long list of options never runs off the screen.
/// </summary>
public sealed class CMUTechConsoleWindow : DefaultWindow
{
    /// <summary>Raised with the tier and index of the option to buy.</summary>
    public event Action<int, int>? OnPurchase;

    [Dependency] private IEntityManager _entities = default!;

    private readonly CrtConsoleKit _kit = new();
    private readonly SpriteSystem _sprite;

    private readonly Label _subheading;
    private readonly PanelContainer _pointsTag;
    private readonly Label _pointsText;
    private readonly BoxContainer _tiers;
    private readonly BoxContainer _cards;
    private readonly BoxContainer _detail;
    private readonly Label _footer;

    private TechState? _state;
    private int _selectedTier = -1;
    private int _selectedOption = -1;

    private sealed record TechState(List<List<TechOption>> Options, int CurrentTier, FixedPoint2 Points, TimeSpan RoundTime);

    private enum Category
    {
        CallIn,
        FireMission,
        Budget,
        Escalation,
        Other,
    }

    private enum Status
    {
        Available,
        Owned,
        Maxed,
        TierLocked,
        TimeLocked,
        TooExpensive,
    }

    public CMUTechConsoleWindow()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entities.System<SpriteSystem>();

        Title = Loc.GetString("cmu-ui-techcon-title");
        MinSize = new Vector2(780, 520);
        SetSize = new Vector2(940, 640);

        _pointsTag = _kit.Tag(out _pointsText);
        _kit.Root.AddChild(_kit.Header(Loc.GetString("cmu-ui-techcon-heading"), out _subheading, _pointsTag));

        var body = CrtConsoleKit.Row(8);
        body.VerticalExpand = true;

        // Tiers.
        var tierSection = _kit.Section(Loc.GetString("cmu-ui-techcon-tiers"), out var tierColumn);
        tierSection.MinWidth = 180;
        tierSection.HorizontalExpand = false;
        tierSection.VerticalExpand = true;
        _tiers = CrtConsoleKit.Column(4);
        tierColumn.AddChild(_tiers);
        body.AddChild(tierSection);

        // The picked tier's options.
        var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, HScrollEnabled = false };
        _cards = CrtConsoleKit.Column(6);
        _cards.HorizontalExpand = true;
        _cards.Margin = new Thickness(0, 0, 6, 0);
        scroll.AddChild(_cards);
        body.AddChild(scroll);

        // The picked option.
        var detailScroll = new ScrollContainer { MinWidth = 280, VerticalExpand = true, HScrollEnabled = false };
        _detail = CrtConsoleKit.Column(8);
        _detail.HorizontalExpand = true;
        detailScroll.AddChild(_detail);
        body.AddChild(detailScroll);

        _kit.Root.AddChild(body);
        _kit.Root.AddChild(_kit.Footer(out _footer));
        _footer.Text = Loc.GetString("cmu-ui-techcon-footer");

        Contents.AddChild(_kit.Tube);
    }

    public void UpdateState(List<List<TechOption>> options, int currentTier, FixedPoint2 points, TimeSpan roundTime)
    {
        _state = new TechState(options, currentTier, points, roundTime);

        if (_selectedTier < 0 || _selectedTier >= options.Count)
            _selectedTier = Math.Clamp(currentTier, 0, Math.Max(0, options.Count - 1));

        if (_selectedOption >= TierOptions(_selectedTier).Count)
            _selectedOption = -1;

        Rebuild();
    }

    private List<TechOption> TierOptions(int tier)
    {
        return _state is { } s && tier >= 0 && tier < s.Options.Count ? s.Options[tier] : new List<TechOption>();
    }

    private void Rebuild()
    {
        if (_state is not { } s)
            return;

        CrtConsoleKit.SetTag(_pointsTag, _pointsText,
            Loc.GetString("cmu-ui-techcon-points", ("points", s.Points.Double().ToString("0.#"))),
            CrtTerminalPalette.Accent);

        var available = s.Options.SelectMany((tier, i) => tier.Select(o => GetStatus(o, i, s)))
            .Count(status => status == Status.Available);
        _subheading.Text = Loc.GetString("cmu-ui-techcon-subheading",
            ("tier", s.CurrentTier), ("max", Math.Max(0, s.Options.Count - 1)), ("available", available));

        BuildTiers(s);
        BuildCards(s);
        BuildDetail(s);
    }

    private void BuildTiers(TechState s)
    {
        _tiers.RemoveAllChildren();
        for (var tier = 0; tier < s.Options.Count; tier++)
        {
            var unlocked = tier <= s.CurrentTier;
            var selected = tier == _selectedTier;
            var affordable = s.Options[tier].Count(o => GetStatus(o, tier, s) == Status.Available);

            var button = new ContainerButton { HorizontalExpand = true };
            var panel = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = selected ? CrtTerminalPalette.Surface4 : CrtTerminalPalette.Surface2,
                },
            };
            var row = CrtConsoleKit.Row(6);
            row.AddChild(new PanelContainer
            {
                MinWidth = 4,
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = unlocked ? CrtTerminalPalette.Accent : CrtTerminalPalette.Line,
                },
            });

            var column = CrtConsoleKit.Column(0);
            column.HorizontalExpand = true;
            column.Margin = new Thickness(0, 4);
            column.AddChild(CrtConsoleKit.Text(Loc.GetString("cmu-ui-techcon-tier", ("tier", tier)), _kit.Bold,
                unlocked ? CrtTerminalPalette.TextBright : CrtTerminalPalette.TextDim));
            column.AddChild(CrtConsoleKit.Text(
                unlocked
                    ? Loc.GetString("cmu-ui-techcon-tier-available", ("count", affordable), ("total", s.Options[tier].Count))
                    : Loc.GetString("cmu-ui-techcon-tier-options", ("total", s.Options[tier].Count)),
                _kit.Small, unlocked && affordable > 0 ? CrtTerminalPalette.Accent : CrtTerminalPalette.TextDim));
            row.AddChild(column);

            var chip = tier == s.CurrentTier
                ? _kit.Chip(Loc.GetString("cmu-ui-techcon-current"), CrtTerminalPalette.Accent)
                : unlocked
                    ? _kit.Chip(Loc.GetString("cmu-ui-techcon-open"), CrtTerminalPalette.TextDim)
                    : _kit.Chip(Loc.GetString("cmu-ui-techcon-locked"), CrtTerminalPalette.Line);
            chip.Margin = new Thickness(0, 0, 6, 0);
            row.AddChild(chip);

            panel.AddChild(row);
            button.AddChild(panel);
            Hover(button, panel, selected);

            var picked = tier;
            button.OnPressed += _ =>
            {
                _selectedTier = picked;
                _selectedOption = -1;
                Rebuild();
            };
            _tiers.AddChild(button);
        }
    }

    private void BuildCards(TechState s)
    {
        _cards.RemoveAllChildren();
        var options = TierOptions(_selectedTier);
        if (options.Count == 0)
        {
            var none = CrtConsoleKit.Text(Loc.GetString("cmu-ui-techcon-empty"), _kit.Bold, CrtTerminalPalette.TextDim);
            none.HorizontalAlignment = HAlignment.Center;
            none.Margin = new Thickness(0, 24, 0, 0);
            _cards.AddChild(none);
            return;
        }

        var groups = options
            .Select((option, index) => (option, index))
            .GroupBy(entry => CategoryOf(entry.option))
            .OrderBy(group => group.Key);

        foreach (var group in groups)
        {
            var section = _kit.Section(Loc.GetString(CategoryLoc(group.Key)), out var column, CategoryColor(group.Key),
                _kit.Chip(group.Count().ToString(), CrtTerminalPalette.TextDim));
            // One card per row: a grid measures every cell at the full width, so long names can't wrap in one.
            var list = CrtConsoleKit.Column(6);
            list.HorizontalExpand = true;
            foreach (var (option, index) in group)
                list.AddChild(Card(option, index, s));
            column.AddChild(list);
            _cards.AddChild(section);
        }
    }

    private Control Card(TechOption option, int index, TechState s)
    {
        var status = GetStatus(option, _selectedTier, s);
        var selected = index == _selectedOption;
        var muted = status is Status.Owned or Status.Maxed or Status.TierLocked;

        var button = new ContainerButton { HorizontalExpand = true };
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = selected ? CrtTerminalPalette.Surface4 : CrtTerminalPalette.Surface2 },
        };
        var row = CrtConsoleKit.Row(6);
        row.AddChild(new PanelContainer
        {
            MinWidth = 4,
            PanelOverride = new StyleBoxFlat { BackgroundColor = StatusColor(status).WithAlpha(selected ? 1f : 0.6f) },
        });

        row.AddChild(Icon(option, 40, muted));

        var column = CrtConsoleKit.Column(1);
        column.HorizontalExpand = true;
        column.Margin = new Thickness(0, 4, 6, 4);
        column.AddChild(CrtConsoleKit.BoldWrapping(LocOrRaw(option.Name).ToUpperInvariant(),
            muted ? CrtTerminalPalette.TextDim : CrtTerminalPalette.TextBright));

        var meta = CrtConsoleKit.Row(6);
        meta.AddChild(CrtConsoleKit.Text(Loc.GetString("cmu-ui-techcon-cost", ("cost", option.CurrentCost)), _kit.Small,
            status == Status.TooExpensive ? CrtTerminalPalette.Alert : CrtTerminalPalette.Caution));
        meta.AddChild(new Control { HorizontalExpand = true });
        meta.AddChild(_kit.Chip(StatusText(option, status, s), StatusColor(status)));
        column.AddChild(meta);
        row.AddChild(column);

        panel.AddChild(row);
        button.AddChild(panel);
        Hover(button, panel, selected);

        button.OnPressed += _ =>
        {
            _selectedOption = index;
            Rebuild();
        };
        return button;
    }

    private void BuildDetail(TechState s)
    {
        _detail.RemoveAllChildren();
        var options = TierOptions(_selectedTier);
        if (_selectedOption < 0 || _selectedOption >= options.Count)
        {
            var section = _kit.Section(Loc.GetString("cmu-ui-techcon-details"), out var column);
            section.VerticalExpand = true;
            column.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-ui-techcon-pick"), CrtTerminalPalette.TextDim));
            _detail.AddChild(section);
            return;
        }

        var option = options[_selectedOption];
        var status = GetStatus(option, _selectedTier, s);
        var category = CategoryOf(option);

        var detail = _kit.Section(Loc.GetString(CategoryLoc(category)), out var col, CategoryColor(category),
            _kit.Chip(StatusText(option, status, s), StatusColor(status)));
        detail.VerticalExpand = true;

        var top = CrtConsoleKit.Row(8);
        top.AddChild(Icon(option, 64, false));
        var name = CrtConsoleKit.BoldWrapping(LocOrRaw(option.Name).ToUpperInvariant(), CrtTerminalPalette.TextBright);
        name.VerticalAlignment = VAlignment.Center;
        top.AddChild(name);
        col.AddChild(top);

        col.AddChild(CrtConsoleKit.Wrapping(LocOrRaw(option.Description), CrtTerminalPalette.Text));

        col.AddChild(_kit.Field(Loc.GetString("cmu-ui-techcon-field-cost"),
            Loc.GetString("cmu-ui-techcon-cost", ("cost", option.CurrentCost)),
            status == Status.TooExpensive ? CrtTerminalPalette.Alert : CrtTerminalPalette.Caution, 110));
        col.AddChild(_kit.Field(Loc.GetString("cmu-ui-techcon-field-tier"),
            Loc.GetString("cmu-ui-techcon-tier", ("tier", _selectedTier)), null, 110));
        if (option.Repurchasable)
        {
            col.AddChild(_kit.Field(Loc.GetString("cmu-ui-techcon-field-purchases"),
                option.MaxPurchases > 0
                    ? Loc.GetString("cmu-ui-techcon-purchases-capped", ("bought", option.TimesPurchased), ("max", option.MaxPurchases))
                    : Loc.GetString("cmu-ui-techcon-purchases-unlimited", ("bought", option.TimesPurchased)),
                null, 110));
        }

        if (option.Increase != 0)
        {
            col.AddChild(_kit.Field(Loc.GetString("cmu-ui-techcon-field-step"),
                Loc.GetString("cmu-ui-techcon-step", ("increase", option.Increase)), CrtTerminalPalette.TextDim, 110));
        }

        if (option.TimeLock > TimeSpan.Zero)
        {
            col.AddChild(_kit.Field(Loc.GetString("cmu-ui-techcon-field-timelock"),
                Loc.GetString("cmu-ui-techcon-timelock", ("minutes", (int) Math.Ceiling(option.TimeLock.TotalMinutes))),
                status == Status.TimeLocked ? CrtTerminalPalette.Caution : CrtTerminalPalette.TextDim, 110));
        }

        col.AddChild(new Control { VerticalExpand = true });

        var reason = StatusReason(option, status, s);
        if (reason != null)
            col.AddChild(CrtConsoleKit.Wrapping(reason, StatusColor(status)));

        var buy = new ConfirmButton
        {
            Text = Loc.GetString("cmu-ui-techcon-purchase", ("cost", option.CurrentCost)),
            ConfirmationText = Loc.GetString("cmu-ui-techcon-confirm"),
            HorizontalExpand = true,
            Disabled = status != Status.Available,
        };
        CmuButtonStyles.Apply(buy, CmuButtonStyles.Variant.Affirm);
        buy.Label.FontOverride = _kit.Bold;
        var tier = _selectedTier;
        var index = _selectedOption;
        buy.OnPressed += _ =>
        {
            if (_state is { } current && GetStatus(TierOptions(tier)[index], tier, current) == Status.Available)
                OnPurchase?.Invoke(tier, index);
        };
        col.AddChild(buy);

        _detail.AddChild(detail);
    }

    private Control Icon(TechOption option, int size, bool muted)
    {
        var frame = new PanelContainer
        {
            MinSize = new Vector2(size + 4, size + 4),
            VerticalAlignment = VAlignment.Center,
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface1 },
        };
        frame.AddChild(new TextureRect
        {
            Texture = _sprite.RsiStateLike(option.Icon).TextureFor(Direction.South),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(size, size),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Modulate = muted ? Color.White.WithAlpha(0.45f) : Color.White,
        });
        return frame;
    }

    private static void Hover(ContainerButton button, PanelContainer panel, bool selected)
    {
        if (selected)
            return;

        button.OnMouseEntered += _ => panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface3 };
        button.OnMouseExited += _ => panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 };
    }

    private static Status GetStatus(TechOption option, int tier, TechState s)
    {
        if (option.Purchased && !option.Repurchasable)
            return Status.Owned;

        if (option.MaxPurchases > 0 && option.TimesPurchased >= option.MaxPurchases)
            return Status.Maxed;

        if (tier > s.CurrentTier)
            return Status.TierLocked;

        if (option.TimeLock >= s.RoundTime)
            return Status.TimeLocked;

        if (s.Points < option.CurrentCost)
            return Status.TooExpensive;

        return Status.Available;
    }

    private static Color StatusColor(Status status)
    {
        return status switch
        {
            Status.Available => CrtTerminalPalette.Accent,
            Status.TooExpensive => CrtTerminalPalette.Alert,
            Status.TimeLocked => CrtTerminalPalette.Caution,
            Status.TierLocked => CrtTerminalPalette.Line,
            _ => CrtTerminalPalette.TextDim,
        };
    }

    private static string StatusText(TechOption option, Status status, TechState s)
    {
        return status switch
        {
            Status.Available => Loc.GetString("cmu-ui-techcon-status-available"),
            Status.Owned => Loc.GetString("cmu-ui-techcon-status-owned"),
            Status.Maxed => Loc.GetString("cmu-ui-techcon-status-maxed"),
            Status.TierLocked => Loc.GetString("cmu-ui-techcon-status-locked"),
            Status.TimeLocked => Loc.GetString("cmu-ui-techcon-status-timed",
                ("minutes", (int) Math.Ceiling((option.TimeLock - s.RoundTime).TotalMinutes))),
            Status.TooExpensive => Loc.GetString("cmu-ui-techcon-status-short",
                ("points", (option.CurrentCost - s.Points).Double().ToString("0.#"))),
            _ => string.Empty,
        };
    }

    private static string? StatusReason(TechOption option, Status status, TechState s)
    {
        return status switch
        {
            Status.Owned => Loc.GetString("cmu-ui-techcon-reason-owned"),
            Status.Maxed => Loc.GetString("cmu-ui-techcon-reason-maxed"),
            Status.TierLocked => Loc.GetString("cmu-ui-techcon-reason-locked"),
            Status.TimeLocked => Loc.GetString("cmu-ui-techcon-reason-timed",
                ("minutes", (int) Math.Ceiling((option.TimeLock - s.RoundTime).TotalMinutes))),
            Status.TooExpensive => Loc.GetString("cmu-ui-techcon-reason-short",
                ("points", (option.CurrentCost - s.Points).Double().ToString("0.#"))),
            _ => null,
        };
    }

    /// <summary>What an option does, judged by the events it fires.</summary>
    private static Category CategoryOf(TechOption option)
    {
        var names = option.Events.Select(e => e.GetType().Name).ToList();
        if (names.Contains("TechUnlockTierEvent"))
            return Category.Escalation;
        if (names.Any(n => n.Contains("PartySpawn") || n.Contains("Cryo")))
            return Category.CallIn;
        if (names.Any(n => n.Contains("ShipWeapon") || n.Contains("LogisticsDelivery")))
            return Category.FireMission;
        if (names.Any(n => n.Contains("Budget")))
            return Category.Budget;
        return Category.Other;
    }

    private static string CategoryLoc(Category category)
    {
        return category switch
        {
            Category.CallIn => "cmu-ui-techcon-cat-callins",
            Category.FireMission => "cmu-ui-techcon-cat-fire",
            Category.Budget => "cmu-ui-techcon-cat-budget",
            Category.Escalation => "cmu-ui-techcon-cat-escalation",
            _ => "cmu-ui-techcon-cat-other",
        };
    }

    private static Color CategoryColor(Category category)
    {
        return category switch
        {
            Category.FireMission => CrtTerminalPalette.Alert,
            Category.Budget => CrtTerminalPalette.Caution,
            Category.Escalation => CrtTerminalPalette.TextBright,
            _ => CrtTerminalPalette.Accent,
        };
    }

    private static string LocOrRaw(string value)
    {
        return Loc.TryGetString(value, out var localized) ? localized : value;
    }
}
