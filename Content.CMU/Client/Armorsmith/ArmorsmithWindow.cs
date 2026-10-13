// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Gunsmith;
using Content.Shared.CMU14.Armorsmith;
using Content.Shared.CMU14.Gunsmith;
using Content.Shared.Stacks;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.CMU14.Armorsmith;

/// <summary>
/// The window of the armorer's bench. The player designs a piece of armor here: picks the look, the plate, the soft layer behind it and the color. The window says
/// what the inputs have to look like and what the work will cost, and gives the data (materials) and the formulas of the handbook; it never says how well the armor
/// protects. That is for the player to work out. An armor that lies on the bench can be painted anew on the second tab.
/// </summary>
public sealed class ArmorsmithWindow : DefaultWindow
{
    private static readonly string[] HandbookSections =
    {
        "armorsmith-hb-intro",
        "armorsmith-hb-layers",
        "armorsmith-hb-protection",
        "armorsmith-hb-weight",
        "armorsmith-hb-extra",
        "armorsmith-hb-build",
    };

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IResourceCache _resources = default!;

    private readonly PrototypeArmorsmithData _data;

    private ArmorDesign _design = new();
    private Dictionary<string, int> _stock = new();
    private ArmorDesign? _benchItem;
    private bool _loading;

    // The armor tab.
    private readonly BoxContainer _preview = new() { Orientation = LayoutOrientation.Horizontal, HorizontalAlignment = HAlignment.Center };
    private ChoiceField _slot = default!;
    private PresetPicker _look = default!;
    private ChoiceField _plateMaterial = default!;
    private NumberField _thickness = default!;
    private NumberField _coverage = default!;
    private ChoiceField _backing = default!;
    private NumberField _layers = default!;
    private ChoiceField _grade = default!;
    private ToggleField _closed = default!;
    private ColorSelectorSliders _color = default!;
    private readonly LineEdit _itemName = new() { HorizontalExpand = true, PlaceHolder = Loc.GetString("armorsmith-ui-name") };
    private readonly LineEdit _itemDescription = new() { HorizontalExpand = true, PlaceHolder = Loc.GetString("armorsmith-ui-description") };
    private readonly RichTextLabel _summary = new() { HorizontalExpand = true };
    private readonly Button _build = new() { Text = Loc.GetString("armorsmith-ui-build") };

    // The recolor tab.
    private readonly Label _benchLabel = new() { HorizontalExpand = true };
    private readonly BoxContainer _recolorPreview = new() { Orientation = LayoutOrientation.Horizontal, HorizontalAlignment = HAlignment.Center };
    private ColorSelectorSliders _recolor = default!;
    private readonly Button _recolorButton = new() { Text = Loc.GetString("armorsmith-ui-recolor") };
    private readonly Button _takeItem = new() { Text = Loc.GetString("armorsmith-ui-take-item") };

    // The stock tab.
    private readonly BoxContainer _stockList = new() { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4 };

    public event Action<ArmorDesign>? OnBuild;
    public event Action<string>? OnEjectStock;
    public event Action? OnTakeItem;
    public event Action<Color>? OnRecolor;

    public ArmorsmithWindow()
    {
        IoCManager.InjectDependencies(this);
        _data = new PrototypeArmorsmithData(_prototypes);

        Title = Loc.GetString("armorsmith-ui-title");
        MinSize = new Vector2(760, 620);

        var tabs = new TabContainer();
        tabs.AddChild(BuildArmorTab());
        tabs.AddChild(BuildRecolorTab());
        tabs.AddChild(BuildStockTab());
        tabs.AddChild(BuildHandbookTab());
        tabs.AddChild(BuildDataTab());
        tabs.SetTabTitle(0, Loc.GetString("armorsmith-ui-tab-armor"));
        tabs.SetTabTitle(1, Loc.GetString("armorsmith-ui-tab-recolor"));
        tabs.SetTabTitle(2, Loc.GetString("armorsmith-ui-tab-stock"));
        tabs.SetTabTitle(3, Loc.GetString("armorsmith-ui-tab-handbook"));
        tabs.SetTabTitle(4, Loc.GetString("armorsmith-ui-tab-data"));
        Contents.AddChild(tabs);

        _build.OnPressed += _ => OnBuild?.Invoke(_design.Clone());
        _takeItem.OnPressed += _ => OnTakeItem?.Invoke();
        _recolorButton.OnPressed += _ => OnRecolor?.Invoke(_recolor.Color);

        LoadDesign(_design);
        Refresh();
    }

    // ------------------------------------------------------------------ helpers

    private static BoxContainer Column(int separation = 4) =>
        new() { Orientation = LayoutOrientation.Vertical, SeparationOverride = separation, HorizontalExpand = true };

    private static Control Section(string title, Control content)
    {
        var box = Column(2);
        box.AddChild(new Label { Text = title, FontColorOverride = Color.FromHex("#c9b99a") });
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#00000040") },
            HorizontalExpand = true,
        };
        var inner = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(6), HorizontalExpand = true };
        inner.AddChild(content);
        panel.AddChild(inner);
        box.AddChild(panel);
        return box;
    }

    private static Control Scroll(Control content)
    {
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        scroll.AddChild(content);
        return scroll;
    }

    private static void SetMarkup(RichTextLabel label, string markup) => label.SetMessage(FormattedMessage.FromMarkupPermissive(markup));

    private NumberField Num(string label, string unit, Action<double> changed)
    {
        var field = new NumberField(label, 0, unit);
        field.Changed += value =>
        {
            if (_loading)
                return;

            changed(value);
            Refresh();
        };
        return field;
    }

    private string StackName(string stackType) =>
        _prototypes.TryIndex<StackPrototype>(stackType, out var stack) ? Loc.GetString(stack.Name) : stackType;

    private string StackOf(string key) => _data.Material(key)?.StackType ?? key;

    /// <summary>The cost as lines: what is needed, what the bench holds (red where it is not enough).</summary>
    private string CostMarkup(Dictionary<string, double> units)
    {
        var lines = new List<string>();
        foreach (var (key, amount) in units)
        {
            var type = StackOf(key);
            var need = (int) Math.Ceiling(amount);
            var have = _stock.GetValueOrDefault(type);
            var color = have >= need ? "#9fdc9f" : "#ff8080";
            lines.Add($"[color={color}]{StackName(type)}: {need} ({Loc.GetString("gunsmith-ui-in-stock", ("count", have))})[/color]");
        }

        return string.Join("\n", lines);
    }

    private static string ErrorMarkup(List<string> errors) =>
        string.Join("\n", errors.Select(e => $"[color=#ff8080]{Loc.GetString("gunsmith-ui-error", ("what", ErrorName(e)))}[/color]"));

    private static string ErrorName(string error)
    {
        var colon = error.IndexOf(':');
        var key = colon < 0 ? error : error[..colon];
        var range = colon < 0 ? string.Empty : error[colon..];
        return Loc.GetString($"armorsmith-error-{key}") + range;
    }

    // ------------------------------------------------------------------ the armor tab

    private Control BuildArmorTab()
    {
        var root = Column(6);
        root.Margin = new Thickness(6);
        root.AddChild(_preview);

        _slot = new ChoiceField(Loc.GetString("armorsmith-ui-slot"));
        foreach (var kind in Enum.GetValues<ArmorSlotKind>())
            _slot.Add(kind.ToString(), Loc.GetString($"armorsmith-slot-{kind.ToString().ToLowerInvariant()}"));
        _slot.Chosen += id =>
        {
            if (_loading)
                return;

            _design.Look = string.Empty;
            RebuildLooks(Enum.Parse<ArmorSlotKind>(id));
            Refresh();
        };

        _look = new PresetPicker(Loc.GetString("armorsmith-ui-look"));
        _look.Chosen += id =>
        {
            if (_loading)
                return;

            _design.Look = id;
            Refresh();
        };

        _plateMaterial = new ChoiceField(Loc.GetString("armorsmith-ui-plate-material"));
        foreach (var plate in _prototypes.EnumeratePrototypes<ArmorsmithPlatePrototype>().OrderBy(p => p.Material))
        {
            if (_data.Material(plate.Material) is { } material)
                _plateMaterial.Add(material.ID, Loc.GetString(material.Name));
        }

        _plateMaterial.Chosen += id => { if (!_loading) { _design.PlateMaterial = id; Refresh(); } };

        _thickness = Num(Loc.GetString("armorsmith-ui-thickness"), "mm", v => _design.PlateThicknessMm = v);
        _coverage = Num(Loc.GetString("armorsmith-ui-coverage"), "%", v => _design.CoveragePercent = v);

        _backing = new ChoiceField(Loc.GetString("armorsmith-ui-backing"));
        _backing.Add(string.Empty, Loc.GetString("armorsmith-ui-backing-none"));
        foreach (var backing in _prototypes.EnumeratePrototypes<ArmorsmithBackingPrototype>().OrderBy(b => b.ID))
            _backing.Add(backing.ID, Loc.GetString(backing.Name));
        _backing.Chosen += id => { if (!_loading) { _design.BackingMaterial = id; Refresh(); } };

        _layers = Num(Loc.GetString("armorsmith-ui-layers"), string.Empty, v => _design.BackingLayers = (int) Math.Round(v));

        _grade = new ChoiceField(Loc.GetString("gunsmith-ui-grade"));
        foreach (var grade in Enum.GetValues<ToleranceGrade>())
            _grade.Add(grade.ToString(), Loc.GetString($"gunsmith-grade-{grade.ToString().ToLowerInvariant()}"));
        _grade.Chosen += id => { if (!_loading) { _design.Grade = Enum.Parse<ToleranceGrade>(id); Refresh(); } };

        _closed = new ToggleField(Loc.GetString("armorsmith-ui-closed"));
        _closed.Toggled += on => { if (!_loading) { _design.ClosedHelmet = on; Refresh(); } };

        _color = new ColorSelectorSliders { SelectorType = ColorSelectorSliders.ColorSelectorType.Hsv, HorizontalExpand = true };
        _color.OnColorChanged += color =>
        {
            if (_loading)
                return;

            _design.Color = color;
            Refresh();
        };

        _itemName.IsValid = text => text.Length <= ArmorDesign.MaxNameLength;
        _itemName.OnTextChanged += args => { if (!_loading) { _design.ItemName = args.Text; Refresh(); } };
        _itemDescription.IsValid = text => text.Length <= ArmorDesign.MaxDescriptionLength;
        _itemDescription.OnTextChanged += args => { if (!_loading) { _design.ItemDescription = args.Text; Refresh(); } };

        var general = Column();
        general.AddChild(_slot);
        general.AddChild(_look);

        var layers = Column();
        layers.AddChild(_plateMaterial);
        layers.AddChild(_thickness);
        layers.AddChild(_coverage);
        layers.AddChild(_backing);
        layers.AddChild(_layers);
        layers.AddChild(_grade);
        layers.AddChild(_closed);

        var paint = Column();
        paint.AddChild(new Label { Text = Loc.GetString("armorsmith-ui-color-hint"), HorizontalExpand = true });
        paint.AddChild(_color);

        var naming = Column();
        naming.AddChild(new Label { Text = Loc.GetString("armorsmith-ui-name") });
        naming.AddChild(_itemName);
        naming.AddChild(new Label { Text = Loc.GetString("armorsmith-ui-description") });
        naming.AddChild(_itemDescription);

        var body = Column(8);
        body.AddChild(new Label { Text = Loc.GetString("armorsmith-ui-hint"), HorizontalExpand = true });
        body.AddChild(Section(Loc.GetString("armorsmith-ui-section-look"), general));
        body.AddChild(Section(Loc.GetString("armorsmith-ui-section-layers"), layers));
        body.AddChild(Section(Loc.GetString("armorsmith-ui-section-color"), paint));
        body.AddChild(Section(Loc.GetString("armorsmith-ui-section-naming"), naming));
        body.AddChild(Section(Loc.GetString("armorsmith-ui-section-cost"), _summary));
        body.AddChild(_build);
        root.AddChild(Scroll(body));
        return root;
    }

    // ------------------------------------------------------------------ the recolor tab

    private Control BuildRecolorTab()
    {
        var root = Column(6);
        root.Margin = new Thickness(6);

        _recolor = new ColorSelectorSliders { SelectorType = ColorSelectorSliders.ColorSelectorType.Hsv, HorizontalExpand = true };
        _recolor.OnColorChanged += _ => DrawRecolorPreview();

        var body = Column(8);
        body.AddChild(new Label { Text = Loc.GetString("armorsmith-ui-recolor-hint"), HorizontalExpand = true });
        body.AddChild(_benchLabel);
        body.AddChild(_recolorPreview);
        body.AddChild(Section(Loc.GetString("armorsmith-ui-section-color"), _recolor));

        var buttons = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8 };
        buttons.AddChild(_recolorButton);
        buttons.AddChild(_takeItem);
        body.AddChild(buttons);

        root.AddChild(Scroll(body));
        return root;
    }

    // ------------------------------------------------------------------ the stock, the handbook and the data

    private Control BuildStockTab()
    {
        var root = Column(6);
        root.Margin = new Thickness(6);

        var body = Column(8);
        body.AddChild(new Label { Text = Loc.GetString("armorsmith-ui-stock-hint"), HorizontalExpand = true });
        body.AddChild(Section(Loc.GetString("armorsmith-ui-section-stock-list"), _stockList));
        root.AddChild(Scroll(body));
        return root;
    }

    private Control BuildHandbookTab()
    {
        var root = Column(6);
        root.Margin = new Thickness(6);

        var body = Column(10);
        foreach (var section in HandbookSections)
        {
            var text = new RichTextLabel { HorizontalExpand = true };
            SetMarkup(text, Loc.GetString(section));
            body.AddChild(text);
        }

        root.AddChild(Scroll(body));
        return root;
    }

    private Control BuildDataTab()
    {
        var root = Column(6);
        root.Margin = new Thickness(6);

        var body = Column(10);

        var plates = new GridContainer { Columns = 8, HSeparationOverride = 12, VSeparationOverride = 2 };
        foreach (var header in new[] { "name", "yield", "ultimate", "density", "young", "strain", "machin", "volume" })
            plates.AddChild(new Label { Text = Loc.GetString($"armorsmith-data-{header}"), FontColorOverride = Color.FromHex("#c9b99a") });

        foreach (var plate in _prototypes.EnumeratePrototypes<ArmorsmithPlatePrototype>().OrderBy(p => p.Material))
        {
            if (_data.Material(plate.Material) is not { } m)
                continue;

            plates.AddChild(new Label { Text = Loc.GetString(m.Name) });
            foreach (var value in new[] { m.YieldMpa, m.UltimateMpa, m.DensityGcm3, m.YoungGpa, plate.FailureStrain, m.Machinability, m.VolumeCm3 })
                plates.AddChild(new Label { Text = NumberField.Format(value) });
        }

        body.AddChild(Section(Loc.GetString("armorsmith-data-plates"), plates));

        var backings = new GridContainer { Columns = 7, HSeparationOverride = 12, VSeparationOverride = 2 };
        foreach (var header in new[] { "name", "ultimate", "strain", "density", "young", "layer", "area" })
            backings.AddChild(new Label { Text = Loc.GetString($"armorsmith-data-{header}"), FontColorOverride = Color.FromHex("#c9b99a") });

        foreach (var b in _prototypes.EnumeratePrototypes<ArmorsmithBackingPrototype>().OrderBy(b => b.ID))
        {
            backings.AddChild(new Label { Text = Loc.GetString(b.Name) });
            foreach (var value in new[] { b.UltimateMpa, b.FailureStrain, b.DensityGcm3, b.YoungGpa, b.LayerKgM2, b.AreaPerItemM2 })
                backings.AddChild(new Label { Text = NumberField.Format(value) });
        }

        body.AddChild(Section(Loc.GetString("armorsmith-data-backings"), backings));

        var constants = new GridContainer { Columns = 3, HSeparationOverride = 14, VSeparationOverride = 2 };
        foreach (var (symbol, value, key) in new (string, double, string)[]
                 {
                     ("S_тело", ArmorsmithMechanics.BodyAreaM2, "body-area"),
                     ("S_голова", ArmorsmithMechanics.HeadAreaM2, "head-area"),
                     ("m_каркас тело", ArmorsmithMechanics.BodyFrameKg, "body-frame"),
                     ("m_каркас голова", ArmorsmithMechanics.HeadFrameKg, "head-frame"),
                     ("m_пули", ArmorsmithMechanics.ReferenceBulletMassKg, "bullet-mass"),
                     ("S_пули", ArmorsmithMechanics.ReferenceBulletAreaM2, "bullet-area"),
                     ("k", ArmorsmithMechanics.ShapeFactor, "shape"),
                     ("V₀", ArmorsmithMechanics.BulletScaleVelocity, "velocity-scale"),
                     ("пул. макс.", ArmorsmithMechanics.BulletCap, "bullet-cap"),
                     ("ближ. макс.", ArmorsmithMechanics.MeleeCap, "melee-cap"),
                     ("взр. макс.", ArmorsmithMechanics.ExplosionCap, "explosion-cap"),
                     ("био макс.", ArmorsmithMechanics.BioCap, "bio-cap"),
                     ("S₀", ArmorsmithMechanics.MeleeScale, "melee-scale"),
                     ("A₀", ArmorsmithMechanics.ExplosionScale, "explosion-scale"),
                     ("k_т", ArmorsmithMechanics.SlowdownPerMm, "slowdown-mm"),
                     ("k_с", ArmorsmithMechanics.SlowdownPerLayer, "slowdown-layer"),
                     ("v_мин", ArmorsmithMechanics.MinSpeedShare, "speed-min"),
                     ("закр. шлем ×", ArmorsmithMechanics.ClosedBonus, "closed-bonus"),
                     ("закр. шлем S ×", ArmorsmithMechanics.ClosedAreaFactor, "closed-area"),
                     ("гилли, ед/м²", ArmorsmithMechanics.GhillieFiberPerM2, "ghillie-fiber"),
                 })
        {
            constants.AddChild(new Label { Text = symbol });
            constants.AddChild(new Label { Text = value >= 1e-3 ? NumberField.Format(value) : value.ToString("0.###E+0", System.Globalization.CultureInfo.InvariantCulture) });
            constants.AddChild(new Label { Text = Loc.GetString($"armorsmith-const-{key}") });
        }

        body.AddChild(Section(Loc.GetString("armorsmith-data-constants"), constants));
        root.AddChild(Scroll(body));
        return root;
    }

    // ------------------------------------------------------------------ state

    private void LoadDesign(ArmorDesign design)
    {
        _loading = true;
        _design = design;
        var slot = _data.Look(design.Look)?.Slot ?? ArmorSlotKind.Body;
        _slot.Selected = slot.ToString();
        RebuildLooks(slot);
        _plateMaterial.Selected = design.PlateMaterial;
        _thickness.Set(design.PlateThicknessMm);
        _coverage.Set(design.CoveragePercent);
        _backing.Selected = design.BackingMaterial;
        _layers.Set(design.BackingLayers);
        _grade.Selected = design.Grade.ToString();
        _closed.Value = design.ClosedHelmet;
        _color.Color = design.Color;
        _itemName.Text = design.ItemName;
        _itemDescription.Text = design.ItemDescription;
        _loading = false;
    }

    private void RebuildLooks(ArmorSlotKind slot)
    {
        _look.Clear();
        foreach (var look in _prototypes.EnumeratePrototypes<ArmorsmithLookPrototype>().Where(l => l.Slot == slot).OrderBy(l => Loc.GetString(l.Name)))
            _look.Add(look.ID, Loc.GetString(look.Name));
    }

    public void UpdateState(ArmorsmithBuiState state)
    {
        _stock = state.Stock;
        _benchItem = state.Item;

        _stockList.RemoveAllChildren();
        if (_stock.Count == 0)
            _stockList.AddChild(new Label { Text = Loc.GetString("gunsmith-ui-stock-empty") });

        foreach (var (type, count) in _stock.OrderBy(p => p.Key))
        {
            var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8, HorizontalExpand = true };
            row.AddChild(new Label { Text = StackName(type), HorizontalExpand = true });
            row.AddChild(new Label { Text = count.ToString(), MinWidth = 50 });
            var eject = new Button { Text = Loc.GetString("gunsmith-ui-eject") };
            var captured = type;
            eject.OnPressed += _ => OnEjectStock?.Invoke(captured);
            row.AddChild(eject);
            _stockList.AddChild(row);
        }

        _benchLabel.Text = _benchItem == null ? Loc.GetString("armorsmith-ui-no-item") : Loc.GetString("armorsmith-ui-item-on-bench");
        _takeItem.Disabled = _benchItem == null;
        _recolorButton.Disabled = _benchItem == null;
        if (_benchItem != null)
        {
            _loading = true;
            _recolor.Color = _benchItem.Color;
            _loading = false;
        }

        DrawRecolorPreview();
        Refresh();
    }

    /// <summary>Redraws what depends on the design: the preview, the shape errors, the cost. Never the protection.</summary>
    private void Refresh()
    {
        var check = ArmorsmithEvaluator.Evaluate(_design, _data);
        if (check.Valid)
        {
            SetMarkup(_summary, CostMarkup(check.Result.MaterialUnits) + "\n" +
                Loc.GetString("gunsmith-ui-complexity", ("points", NumberField.Format(Math.Round(check.Result.Complexity, 1)))) + "\n" +
                Loc.GetString("gunsmith-ui-time", ("seconds", (int) Math.Ceiling(check.Result.BuildSeconds))));
        }
        else
        {
            SetMarkup(_summary, ErrorMarkup(check.Errors));
        }

        _build.Disabled = !check.Valid;

        // Only a helmet can be closed or open.
        _closed.Visible = _slot.Selected == ArmorSlotKind.Head.ToString();
        DrawPreview(_preview, _design.Look, _design.Color);
    }

    private void DrawRecolorPreview() => DrawPreview(_recolorPreview, _benchItem?.Look ?? string.Empty, _recolor.Color);

    /// <summary>The picture of the armor in the color it will have: the grey sprite of the look, tinted.</summary>
    private void DrawPreview(BoxContainer into, string lookId, Color color)
    {
        into.RemoveAllChildren();
        if (string.IsNullOrEmpty(lookId) || _data.Look(lookId) is not { } look
            || !_resources.TryGetResource<RSIResource>("/Textures/" + look.Rsi, out var rsi) || !rsi.RSI.TryGetState("icon", out var state))
        {
            return;
        }

        into.AddChild(new TextureRect
        {
            Texture = state.Frame0,
            Stretch = TextureRect.StretchMode.KeepCentered,
            TextureScale = new Vector2(4, 4),
            MinSize = new Vector2(160, 128),
            ModulateSelfOverride = color,
        });
    }
}
