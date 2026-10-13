// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Gunsmith;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.CMU14.Gunsmith;

/// <summary>
/// The window of the gunsmith's bench. The player designs a weapon here: picks the parts, the materials and the sizes. The window says what the
/// inputs have to look like and what the work will cost, and gives the data (materials, powders, constants) and the formulas of the handbook;
/// it never says whether the design holds. That is for the player to work out.
/// </summary>
public sealed class GunsmithWindow : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IResourceCache _resources = default!;

    private readonly PrototypeGunsmithData _data;

    private FirearmDesign _design = new();
    private MagazineDesign _magazine = new();
    private Dictionary<string, int> _stock = new();
    private FirearmDesign? _benchWeapon;

    // The weapon tab.
    private ChoiceField _action = default!;
    private ChoiceField _barrelMaterial = default!;
    private NumberField _barrelLength = default!;
    private NumberField _barrelOuter = default!;
    private ChoiceField _boltMaterial = default!;
    private NumberField _boltDiameter = default!;
    private NumberField _pinHole = default!;
    private NumberField _lugs = default!;
    private NumberField _lugWidth = default!;
    private NumberField _lugLength = default!;
    private NumberField _lugDepth = default!;
    private ChoiceField _receiverMaterial = default!;
    private ChoiceField _stockKind = default!;
    private ChoiceField _stockMaterial = default!;
    private NumberField _stockWidth = default!;
    private NumberField _stockHeight = default!;
    private NumberField _stockLength = default!;
    private NumberField _hingePin = default!;
    private NumberField _fireRate = default!;
    private ChoiceField _grade = default!;
    private ChoiceField _look = default!;
    private ChoiceField _class = default!;
    private ChoiceField _feed = default!;
    private NumberField _feedCapacity = default!;
    private readonly LineEdit _weaponName = new() { HorizontalExpand = true, PlaceHolder = Loc.GetString("gunsmith-ui-weapon-name-hint") };
    private readonly LineEdit _weaponDescription = new() { HorizontalExpand = true, PlaceHolder = Loc.GetString("gunsmith-ui-weapon-description-hint") };
    private CaliberSpec? _benchCaliber;
    private readonly Label _weaponCaliber = new() { HorizontalExpand = true };
    private readonly RichTextLabel _weaponSummary = new() { HorizontalExpand = true };
    private readonly Button _buildWeapon = new() { Text = Loc.GetString("gunsmith-ui-build-weapon") };
    private readonly Control _preview = new() { MinSize = new Vector2(160, 64), HorizontalAlignment = HAlignment.Center };

    // The caliber tab.
    private PresetPicker _preset = default!;
    private NumberField _bore = default!;
    private NumberField _caseDiameter = default!;
    private NumberField _caseLength = default!;
    private NumberField _seatDepth = default!;
    private NumberField _bulletMass = default!;
    private ChoiceField _powder = default!;
    private NumberField _charge = default!;
    private readonly Label _caliberKey = new() { HorizontalExpand = true };
    private readonly RichTextLabel _caliberSummary = new() { HorizontalExpand = true };
    private readonly Button _fromWeapon = new() { Text = Loc.GetString("gunsmith-ui-caliber-from-weapon") };

    // The magazine tab.
    private ChoiceField _capacity = default!;
    private ChoiceField _bodyMaterial = default!;
    private NumberField _wall = default!;
    private ChoiceField _springMaterial = default!;
    private NumberField _wire = default!;
    private NumberField _coil = default!;
    private NumberField _turns = default!;
    private NumberField _freeLength = default!;
    private ChoiceField _magazineGrade = default!;
    private readonly Label _magazineCaliber = new() { HorizontalExpand = true };
    private readonly RichTextLabel _magazineSummary = new() { HorizontalExpand = true };
    private readonly Button _buildMagazine = new() { Text = Loc.GetString("gunsmith-ui-build-magazine") };

    // The stock tab.
    private readonly BoxContainer _stockList = new() { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly Label _benchWeaponLabel = new() { HorizontalExpand = true };
    private readonly Button _takeWeapon = new() { Text = Loc.GetString("gunsmith-ui-take-weapon") };

    public event Action<FirearmDesign>? OnBuildFirearm;
    public event Action<MagazineDesign>? OnBuildMagazine;
    public event Action<string>? OnEjectStock;
    public event Action? OnTakeWeapon;

    public GunsmithWindow()
    {
        IoCManager.InjectDependencies(this);
        _data = new PrototypeGunsmithData(_prototypes);

        Title = Loc.GetString("gunsmith-ui-title");
        MinSize = new Vector2(780, 600);

        var tabs = new TabContainer();
        tabs.AddChild(BuildWeaponTab());
        tabs.AddChild(BuildCaliberTab());
        tabs.AddChild(BuildMagazineTab());
        tabs.AddChild(BuildStockTab());
        tabs.AddChild(BuildHandbookTab());
        tabs.AddChild(BuildDataTab());
        tabs.SetTabTitle(0, Loc.GetString("gunsmith-ui-tab-weapon"));
        tabs.SetTabTitle(1, Loc.GetString("gunsmith-ui-tab-caliber"));
        tabs.SetTabTitle(2, Loc.GetString("gunsmith-ui-tab-magazine"));
        tabs.SetTabTitle(3, Loc.GetString("gunsmith-ui-tab-stock"));
        tabs.SetTabTitle(4, Loc.GetString("gunsmith-ui-tab-handbook"));
        tabs.SetTabTitle(5, Loc.GetString("gunsmith-ui-tab-data"));
        Contents.AddChild(tabs);

        _buildWeapon.OnPressed += _ => OnBuildFirearm?.Invoke(_design.Clone());
        _buildMagazine.OnPressed += _ => OnBuildMagazine?.Invoke(_magazine.Clone());
        _takeWeapon.OnPressed += _ => OnTakeWeapon?.Invoke();
        _fromWeapon.OnPressed += _ =>
        {
            if (_benchCaliber != null)
                LoadCaliber(_benchCaliber);
        };

        LoadDesign(_design);
        LoadCaliber(_design.Caliber);
        LoadMagazine(_magazine);
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

    private IEnumerable<GunsmithMaterialPrototype> StructuralMaterials() =>
        _prototypes.EnumeratePrototypes<GunsmithMaterialPrototype>().Where(m => m.Structural).OrderBy(m => m.ID);

    private ChoiceField Materials(string label, Action<string> chosen)
    {
        var field = new ChoiceField(label);
        foreach (var material in StructuralMaterials())
            field.Add(material.ID, Loc.GetString(material.Name));

        field.Chosen += chosen;
        return field;
    }

    private ChoiceField Grades(Action<ToleranceGrade> chosen)
    {
        var field = new ChoiceField(Loc.GetString("gunsmith-ui-grade"));
        foreach (var grade in Enum.GetValues<ToleranceGrade>())
            field.Add(grade.ToString(), Loc.GetString($"gunsmith-grade-{grade.ToString().ToLowerInvariant()}"));

        field.Chosen += id => chosen(Enum.Parse<ToleranceGrade>(id));
        return field;
    }

    private NumberField Num(string label, string unit, Action<double> changed)
    {
        var field = new NumberField(label, 0, unit);
        field.Changed += value =>
        {
            changed(value);
            Refresh();
        };
        return field;
    }

    private string StackName(string stackType) =>
        _prototypes.TryIndex<Content.Shared.Stacks.StackPrototype>(stackType, out var stack) ? Loc.GetString(stack.Name) : stackType;

    private string StackOfMaterial(string materialId) => _data.Material(materialId)?.StackType ?? materialId;

    /// <summary>The cost as lines: what is needed, what the bench holds (red where it is not enough).</summary>
    private string CostMarkup(Dictionary<string, double> units)
    {
        var lines = new List<string>();
        foreach (var (key, amount) in units)
        {
            var type = StackOfMaterial(key);
            var need = (int) Math.Ceiling(amount);
            var have = _stock.GetValueOrDefault(type);
            var color = have >= need ? "#9fdc9f" : "#ff8080";
            lines.Add($"[color={color}]{StackName(type)}: {need} ({Loc.GetString("gunsmith-ui-in-stock", ("count", have))})[/color]");
        }

        return string.Join("\n", lines);
    }

    private static void SetMarkup(RichTextLabel label, string markup) => label.SetMessage(FormattedMessage.FromMarkupPermissive(markup));

    // ------------------------------------------------------------------ the weapon tab

    private Control BuildWeaponTab()
    {
        var root = Column(6);
        root.Margin = new Thickness(6);

        root.AddChild(_preview);

        _action = new ChoiceField(Loc.GetString("gunsmith-ui-action"));
        foreach (var action in _prototypes.EnumeratePrototypes<GunsmithActionPrototype>().OrderBy(a => a.Complexity))
            _action.Add(action.ID, Loc.GetString(action.Name));
        _action.Chosen += id => { _design.Action = id; Refresh(); };

        // The look is chosen apart from the caliber: it is only a picture of a weapon of the game (or the weapon made of parts, as it was).
        _look = new ChoiceField(Loc.GetString("gunsmith-ui-look"));
        RebuildLooks();
        _look.Chosen += id =>
        {
            _design.Look = id;

            // A look is of a class of weapons, and a weapon with it is of that class.
            if (!string.IsNullOrEmpty(id) && _data.Look(id) is { } chosen)
            {
                _design.Class = chosen.Class;
                _class.Selected = chosen.Class;
            }

            Refresh();
        };

        // The class of the weapon: what caliber and what barrel it can have. The looks are the looks of that class.
        _class = new ChoiceField(Loc.GetString("gunsmith-ui-class"));
        foreach (var weaponClass in _prototypes.EnumeratePrototypes<GunsmithClassPrototype>().OrderBy(c => c.MaxBoreMm).ThenBy(c => c.MaxChargeG))
            _class.Add(weaponClass.ID, Loc.GetString(weaponClass.Name));

        _class.Chosen += id =>
        {
            _design.Class = id;
            if (!string.IsNullOrEmpty(_design.Look) && _data.Look(_design.Look)?.Class != id)
                _design.Look = string.Empty;

            RebuildLooks();
            Refresh();
        };

        // How the weapon is fed.
        _feed = new ChoiceField(Loc.GetString("gunsmith-ui-feed"));
        foreach (var feed in Enum.GetValues<FeedKind>())
            _feed.Add(feed.ToString(), Loc.GetString($"gunsmith-feed-{feed.ToString().ToLowerInvariant()}"));

        _feed.Chosen += id => { _design.Feed = Enum.Parse<FeedKind>(id); Refresh(); };
        _feedCapacity = Num(Loc.GetString("gunsmith-ui-feed-capacity"), string.Empty, v => _design.FeedCapacity = (int) Math.Round(v));

        _weaponName.IsValid = text => text.Length <= FirearmDesign.MaxNameLength;
        _weaponName.OnTextChanged += args => { _design.WeaponName = args.Text; Refresh(); };
        _weaponDescription.IsValid = text => text.Length <= FirearmDesign.MaxDescriptionLength;
        _weaponDescription.OnTextChanged += args => { _design.WeaponDescription = args.Text; Refresh(); };

        var naming = Column();
        naming.AddChild(new Label { Text = Loc.GetString("gunsmith-ui-weapon-name") });
        naming.AddChild(_weaponName);
        naming.AddChild(new Label { Text = Loc.GetString("gunsmith-ui-weapon-description") });
        naming.AddChild(_weaponDescription);

        var general = Column();
        general.AddChild(new Label { Text = Loc.GetString("gunsmith-ui-class-hint"), HorizontalExpand = true });
        general.AddChild(_class);
        general.AddChild(_look);
        general.AddChild(_feed);
        general.AddChild(_feedCapacity);
        general.AddChild(new Label { Text = Loc.GetString("gunsmith-ui-attachments"), HorizontalExpand = true });
        general.AddChild(_action);
        general.AddChild(_weaponCaliber);
        _fireRate = Num(Loc.GetString("gunsmith-ui-fire-rate"), Loc.GetString("gunsmith-ui-fire-rate-unit"), v => _design.FireRate = v);
        general.AddChild(new Label { Text = Loc.GetString("gunsmith-ui-fire-rate-hint"), HorizontalExpand = true });
        general.AddChild(_fireRate);
        _grade = Grades(g => { _design.Grade = g; Refresh(); });
        general.AddChild(_grade);

        var barrel = Column();
        _barrelMaterial = Materials(Loc.GetString("gunsmith-ui-material"), id => { _design.BarrelMaterial = id; Refresh(); });
        _barrelLength = Num(Loc.GetString("gunsmith-ui-barrel-length"), "mm", v => _design.BarrelLengthMm = v);
        _barrelOuter = Num(Loc.GetString("gunsmith-ui-barrel-outer"), "mm", v => _design.BarrelOuterMm = v);
        barrel.AddChild(_barrelMaterial);
        barrel.AddChild(_barrelLength);
        barrel.AddChild(_barrelOuter);

        var bolt = Column();
        _boltMaterial = Materials(Loc.GetString("gunsmith-ui-material"), id => { _design.BoltMaterial = id; Refresh(); });
        _boltDiameter = Num(Loc.GetString("gunsmith-ui-bolt-diameter"), "mm", v => _design.BoltDiameterMm = v);
        _pinHole = Num(Loc.GetString("gunsmith-ui-pin-hole"), "mm", v => _design.PinHoleMm = v);
        _lugs = Num(Loc.GetString("gunsmith-ui-lugs"), string.Empty, v => _design.Lugs = (int) Math.Round(v));
        _lugWidth = Num(Loc.GetString("gunsmith-ui-lug-width"), "mm", v => _design.LugWidthMm = v);
        _lugLength = Num(Loc.GetString("gunsmith-ui-lug-length"), "mm", v => _design.LugLengthMm = v);
        _lugDepth = Num(Loc.GetString("gunsmith-ui-lug-depth"), "mm", v => _design.LugDepthMm = v);
        bolt.AddChild(_boltMaterial);
        bolt.AddChild(_boltDiameter);
        bolt.AddChild(_pinHole);
        bolt.AddChild(_lugs);
        bolt.AddChild(_lugWidth);
        bolt.AddChild(_lugLength);
        bolt.AddChild(_lugDepth);

        var receiver = Column();
        _receiverMaterial = Materials(Loc.GetString("gunsmith-ui-material"), id => { _design.ReceiverMaterial = id; Refresh(); });
        receiver.AddChild(_receiverMaterial);

        var stock = Column();
        _stockKind = new ChoiceField(Loc.GetString("gunsmith-ui-stock-kind"));
        foreach (var kind in Enum.GetValues<StockKind>())
            _stockKind.Add(kind.ToString(), Loc.GetString($"gunsmith-stock-{kind.ToString().ToLowerInvariant()}"));
        _stockKind.Chosen += id => { _design.Stock = Enum.Parse<StockKind>(id); Refresh(); };
        _stockMaterial = Materials(Loc.GetString("gunsmith-ui-material"), id => { _design.StockMaterial = id; Refresh(); });
        _stockWidth = Num(Loc.GetString("gunsmith-ui-stock-width"), "mm", v => _design.StockWidthMm = v);
        _stockHeight = Num(Loc.GetString("gunsmith-ui-stock-height"), "mm", v => _design.StockHeightMm = v);
        _stockLength = Num(Loc.GetString("gunsmith-ui-stock-length"), "mm", v => _design.StockLengthMm = v);
        _hingePin = Num(Loc.GetString("gunsmith-ui-hinge-pin"), "mm", v => _design.HingePinMm = v);
        stock.AddChild(_stockKind);
        stock.AddChild(_stockMaterial);
        stock.AddChild(_stockWidth);
        stock.AddChild(_stockHeight);
        stock.AddChild(_stockLength);
        stock.AddChild(_hingePin);

        var body = Column(8);
        body.AddChild(Section(Loc.GetString("gunsmith-ui-section-general"), general));
        body.AddChild(Section(Loc.GetString("gunsmith-ui-section-naming"), naming));
        body.AddChild(Section(Loc.GetString("gunsmith-ui-section-barrel"), barrel));
        body.AddChild(Section(Loc.GetString("gunsmith-ui-section-bolt"), bolt));
        body.AddChild(Section(Loc.GetString("gunsmith-ui-section-receiver"), receiver));
        body.AddChild(Section(Loc.GetString("gunsmith-ui-section-stock"), stock));
        body.AddChild(Section(Loc.GetString("gunsmith-ui-section-cost"), _weaponSummary));
        body.AddChild(_buildWeapon);

        root.AddChild(Scroll(body));
        return root;
    }

    // ------------------------------------------------------------------ the caliber tab

    private Control BuildCaliberTab()
    {
        var root = Column(6);
        root.Margin = new Thickness(6);

        _preset = new PresetPicker(Loc.GetString("gunsmith-ui-preset"));
        foreach (var preset in _prototypes.EnumeratePrototypes<GunsmithCaliberPrototype>().OrderBy(c => c.BoreMm).ThenBy(c => c.CaseLengthMm))
            _preset.Add(preset.ID, Loc.GetString(preset.Name));
        _preset.Chosen += id =>
        {
            if (!_prototypes.TryIndex<GunsmithCaliberPrototype>(id, out var p))
                return;

            LoadCaliber(new CaliberSpec
            {
                BoreMm = p.BoreMm,
                CaseDiameterMm = p.CaseDiameterMm,
                CaseLengthMm = p.CaseLengthMm,
                SeatDepthMm = p.SeatDepthMm,
                BulletMassG = p.BulletMassG,
                Powder = p.Powder,
                ChargeG = p.ChargeG,
            });
            Refresh();
        };

        _bore = Num(Loc.GetString("gunsmith-ui-bore"), "mm", v => _design.Caliber.BoreMm = v);
        _caseDiameter = Num(Loc.GetString("gunsmith-ui-case-diameter"), "mm", v => _design.Caliber.CaseDiameterMm = v);
        _caseLength = Num(Loc.GetString("gunsmith-ui-case-length"), "mm", v => _design.Caliber.CaseLengthMm = v);
        _seatDepth = Num(Loc.GetString("gunsmith-ui-seat-depth"), "mm", v => _design.Caliber.SeatDepthMm = v);
        _bulletMass = Num(Loc.GetString("gunsmith-ui-bullet-mass"), "g", v => _design.Caliber.BulletMassG = v);
        _powder = new ChoiceField(Loc.GetString("gunsmith-ui-powder"));
        foreach (var powder in _prototypes.EnumeratePrototypes<GunsmithPowderPrototype>().OrderBy(p => p.Force))
            _powder.Add(powder.ID, Loc.GetString(powder.Name));
        _powder.Chosen += id => { _design.Caliber.Powder = id; Refresh(); };
        _charge = Num(Loc.GetString("gunsmith-ui-charge"), "g", v => _design.Caliber.ChargeG = v);

        var fields = Column();
        fields.AddChild(_preset);
        fields.AddChild(_bore);
        fields.AddChild(_caseDiameter);
        fields.AddChild(_caseLength);
        fields.AddChild(_seatDepth);
        fields.AddChild(_bulletMass);
        fields.AddChild(_powder);
        fields.AddChild(_charge);
        fields.AddChild(_caliberKey);
        fields.AddChild(_fromWeapon);

        var body = Column(8);
        body.AddChild(new Label { Text = Loc.GetString("gunsmith-ui-caliber-hint"), HorizontalExpand = true });
        body.AddChild(Section(Loc.GetString("gunsmith-ui-section-caliber"), fields));
        body.AddChild(_caliberSummary);
        root.AddChild(Scroll(body));
        return root;
    }

    // ------------------------------------------------------------------ the magazine tab

    private Control BuildMagazineTab()
    {
        var root = Column(6);
        root.Margin = new Thickness(6);

        _capacity = new ChoiceField(Loc.GetString("gunsmith-ui-capacity"));
        foreach (var capacity in Content.Shared.CMU14.Gunsmith.GunsmithCapacities.All)
            _capacity.Add(capacity.ToString(), capacity.ToString());
        _capacity.Chosen += id => { _magazine.Capacity = int.Parse(id); Refresh(); };

        _bodyMaterial = Materials(Loc.GetString("gunsmith-ui-body-material"), id => { _magazine.BodyMaterial = id; Refresh(); });
        _wall = Num(Loc.GetString("gunsmith-ui-wall"), "mm", v => _magazine.WallMm = v);
        _springMaterial = Materials(Loc.GetString("gunsmith-ui-spring-material"), id => { _magazine.SpringMaterial = id; Refresh(); });
        _wire = Num(Loc.GetString("gunsmith-ui-wire"), "mm", v => _magazine.WireMm = v);
        _coil = Num(Loc.GetString("gunsmith-ui-coil"), "mm", v => _magazine.CoilMm = v);
        _turns = Num(Loc.GetString("gunsmith-ui-turns"), string.Empty, v => _magazine.Turns = v);
        _freeLength = Num(Loc.GetString("gunsmith-ui-free-length"), "mm", v => _magazine.FreeLengthMm = v);
        _magazineGrade = Grades(g => { _magazine.Grade = g; Refresh(); });

        var fields = Column();
        fields.AddChild(_magazineCaliber);
        fields.AddChild(_capacity);
        fields.AddChild(_bodyMaterial);
        fields.AddChild(_wall);
        fields.AddChild(_springMaterial);
        fields.AddChild(_wire);
        fields.AddChild(_coil);
        fields.AddChild(_turns);
        fields.AddChild(_freeLength);
        fields.AddChild(_magazineGrade);

        var body = Column(8);
        body.AddChild(new Label { Text = Loc.GetString("gunsmith-ui-magazine-hint"), HorizontalExpand = true });
        body.AddChild(Section(Loc.GetString("gunsmith-ui-section-magazine"), fields));
        body.AddChild(Section(Loc.GetString("gunsmith-ui-section-cost"), _magazineSummary));
        body.AddChild(_buildMagazine);
        root.AddChild(Scroll(body));
        return root;
    }

    // ------------------------------------------------------------------ the stock tab

    private Control BuildStockTab()
    {
        var root = Column(6);
        root.Margin = new Thickness(6);

        var weapon = Column();
        weapon.AddChild(_benchWeaponLabel);
        weapon.AddChild(_takeWeapon);

        var body = Column(8);
        body.AddChild(new Label { Text = Loc.GetString("gunsmith-ui-stock-hint"), HorizontalExpand = true });
        body.AddChild(Section(Loc.GetString("gunsmith-ui-section-stock-list"), _stockList));
        body.AddChild(Section(Loc.GetString("gunsmith-ui-section-bench-weapon"), weapon));
        root.AddChild(Scroll(body));
        return root;
    }

    // ------------------------------------------------------------------ the handbook and the data

    private Control BuildHandbookTab()
    {
        var root = Column(6);
        root.Margin = new Thickness(6);

        var body = Column(10);
        foreach (var section in GunsmithHandbook.Sections)
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

        // The materials.
        var materials = new GridContainer { Columns = 11, HSeparationOverride = 10, VSeparationOverride = 2 };
        foreach (var header in new[] { "name", "yield", "ultimate", "endurance", "young", "shear", "density", "heat", "limit", "machin", "volume" })
            materials.AddChild(new Label { Text = Loc.GetString($"gunsmith-data-{header}"), FontColorOverride = Color.FromHex("#c9b99a") });

        foreach (var m in _prototypes.EnumeratePrototypes<GunsmithMaterialPrototype>().OrderBy(m => m.ID))
        {
            materials.AddChild(new Label { Text = Loc.GetString(m.Name) });
            foreach (var value in new[] { m.YieldMpa, m.UltimateMpa, m.EnduranceMpa, m.YoungGpa, m.ShearGpa, m.DensityGcm3, m.SpecificHeat, m.LimitTempC, m.Machinability, m.VolumeCm3 })
                materials.AddChild(new Label { Text = NumberField.Format(value) });
        }

        body.AddChild(Section(Loc.GetString("gunsmith-data-materials"), materials));

        // The powders.
        var powders = new GridContainer { Columns = 6, HSeparationOverride = 12, VSeparationOverride = 2 };
        foreach (var header in new[] { "name", "force", "covolume", "energy", "efficiency", "cost" })
            powders.AddChild(new Label { Text = Loc.GetString($"gunsmith-data-{header}"), FontColorOverride = Color.FromHex("#c9b99a") });

        foreach (var p in _prototypes.EnumeratePrototypes<GunsmithPowderPrototype>().OrderBy(p => p.Force))
        {
            powders.AddChild(new Label { Text = Loc.GetString(p.Name) });
            foreach (var value in new[] { p.Force, p.Covolume, p.Energy, p.Efficiency, p.CostPerGram })
                powders.AddChild(new Label { Text = NumberField.Format(value) });
        }

        body.AddChild(Section(Loc.GetString("gunsmith-data-powders"), powders));

        // The actions.
        var actions = new GridContainer { Columns = 4, HSeparationOverride = 12, VSeparationOverride = 2 };
        foreach (var header in new[] { "name", "complexity", "cycle", "bolt-length" })
            actions.AddChild(new Label { Text = Loc.GetString($"gunsmith-data-{header}"), FontColorOverride = Color.FromHex("#c9b99a") });

        foreach (var a in _prototypes.EnumeratePrototypes<GunsmithActionPrototype>().OrderBy(a => a.Complexity))
        {
            actions.AddChild(new Label { Text = Loc.GetString(a.Name) });
            foreach (var value in new[] { a.Complexity, a.CycleSeconds, a.BoltLengthMm })
                actions.AddChild(new Label { Text = NumberField.Format(value) });
        }

        body.AddChild(Section(Loc.GetString("gunsmith-data-actions"), actions));

        // The constants.
        var constants = new GridContainer { Columns = 3, HSeparationOverride = 14, VSeparationOverride = 2 };
        foreach (var (symbol, value, key) in new (string, double, string)[]
                 {
                     ("η_p", GunsmithMechanics.PeakFactor, "peak"),
                     ("k_g", GunsmithMechanics.GasVelocityFactor, "gas"),
                     ("L0 / d", GunsmithMechanics.BoreLengthsToBurn, "burn"),
                     ("χ", GunsmithMechanics.BarrelHeatShare, "heat-share"),
                     ("τт / σт", GunsmithMechanics.ShearYieldRatio, "shear-ratio"),
                     ("t_пр, с", GunsmithMechanics.StockPulseSeconds, "stock-pulse"),
                     ("k_э", GunsmithMechanics.StockEccentricity, "stock-ecc"),
                     ("T_окр, °C", GunsmithMechanics.AmbientC, "ambient"),
                     ("T½, с", Content.Shared.CMU14.Gunsmith.GunsmithConstants.CoolingHalfLifeSeconds, "half-life"),
                     ("l_г, мм", GunsmithMechanics.LipLengthMm, "lip-length"),
                     ("b_г, мм", GunsmithMechanics.LipWidthMm, "lip-width"),
                     ("μ", GunsmithMechanics.FeedFriction, "friction"),
                     ("a_п, g", GunsmithMechanics.FeedAccelerationG, "feed-accel"),
                     ("[τ]/σв", GunsmithMechanics.SpringAllowableShare, "spring-allow"),
                     ("k_пр", GunsmithMechanics.SpringWireFactor, "spring-wire"),
                     ("g, м/с²", GunsmithMechanics.Gravity, "gravity"),
                 })
        {
            constants.AddChild(new Label { Text = symbol, FontColorOverride = Color.FromHex("#c9b99a") });
            constants.AddChild(new Label { Text = NumberField.Format(value) });
            constants.AddChild(new Label { Text = Loc.GetString($"gunsmith-const-{key}") });
        }

        body.AddChild(Section(Loc.GetString("gunsmith-data-constants"), constants));

        root.AddChild(Scroll(body));
        return root;
    }

    // ------------------------------------------------------------------ keeping the window and the design in step

    private void LoadDesign(FirearmDesign design)
    {
        _design = design;
        _action.Selected = design.Action;
        _barrelMaterial.Selected = design.BarrelMaterial;
        _barrelLength.Set(design.BarrelLengthMm);
        _barrelOuter.Set(design.BarrelOuterMm);
        _boltMaterial.Selected = design.BoltMaterial;
        _boltDiameter.Set(design.BoltDiameterMm);
        _pinHole.Set(design.PinHoleMm);
        _lugs.Set(design.Lugs);
        _lugWidth.Set(design.LugWidthMm);
        _lugLength.Set(design.LugLengthMm);
        _lugDepth.Set(design.LugDepthMm);
        _receiverMaterial.Selected = design.ReceiverMaterial;
        _stockKind.Selected = design.Stock.ToString();
        _stockMaterial.Selected = design.StockMaterial;
        _stockWidth.Set(design.StockWidthMm);
        _stockHeight.Set(design.StockHeightMm);
        _stockLength.Set(design.StockLengthMm);
        _hingePin.Set(design.HingePinMm);
        _fireRate.Set(design.FireRate);
        _grade.Selected = design.Grade.ToString();
        _class.Selected = design.Class;
        RebuildLooks();
        _look.Selected = design.Look;
        _feed.Selected = design.Feed.ToString();
        _feedCapacity.Set(design.FeedCapacity);
        _weaponName.Text = design.WeaponName;
        _weaponDescription.Text = design.WeaponDescription;
    }

    private void LoadCaliber(CaliberSpec caliber)
    {
        _design.Caliber = caliber.Clone();
        _bore.Set(caliber.BoreMm);
        _caseDiameter.Set(caliber.CaseDiameterMm);
        _caseLength.Set(caliber.CaseLengthMm);
        _seatDepth.Set(caliber.SeatDepthMm);
        _bulletMass.Set(caliber.BulletMassG);
        _powder.Selected = caliber.Powder;
        _charge.Set(caliber.ChargeG);
        Refresh();
    }

    private void LoadMagazine(MagazineDesign magazine)
    {
        _magazine = magazine;
        _capacity.Selected = magazine.Capacity.ToString();
        _bodyMaterial.Selected = magazine.BodyMaterial;
        _wall.Set(magazine.WallMm);
        _springMaterial.Selected = magazine.SpringMaterial;
        _wire.Set(magazine.WireMm);
        _coil.Set(magazine.CoilMm);
        _turns.Set(magazine.Turns);
        _freeLength.Set(magazine.FreeLengthMm);
        _magazineGrade.Selected = magazine.Grade.ToString();
    }

    public void UpdateState(GunsmithBuiState state)
    {
        _stock = state.Stock;
        _benchWeapon = state.Weapon;
        _benchCaliber = state.ItemCaliber;

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

        _benchWeaponLabel.Text = _benchCaliber == null
            ? Loc.GetString("gunsmith-ui-no-weapon")
            : Loc.GetString("gunsmith-ui-weapon-on-bench", ("key", _benchCaliber.Key()));
        _takeWeapon.Disabled = _benchCaliber == null;
        _fromWeapon.Disabled = _benchCaliber == null;

        Refresh();
    }

    /// <summary>The looks to choose from are the looks of weapons of the class chosen (and the weapon made of parts).</summary>
    private void RebuildLooks()
    {
        var chosen = _design.Look;
        _look.Clear();
        _look.Add(string.Empty, Loc.GetString("gunsmith-ui-look-default"));
        foreach (var look in _prototypes.EnumeratePrototypes<GunsmithLookPrototype>().Where(l => l.Class == _design.Class).OrderBy(l => Loc.GetString(l.Name)))
            _look.Add(look.ID, Loc.GetString(look.Name));

        _look.Selected = chosen;
    }

    /// <summary>Redraws what depends on the design: the preview, the shape errors, the cost. Never the strength.</summary>
    private void Refresh()
    {
        if (_weaponSummary == null)
            return;

        _feedCapacity.Visible = _design.Feed != FeedKind.Magazine;

        _magazine.Caliber = _design.Caliber.Clone();
        _caliberKey.Text = Loc.GetString("gunsmith-ui-caliber-key", ("key", _design.Caliber.Key()));
        _weaponCaliber.Text = Loc.GetString("gunsmith-ui-weapon-caliber", ("key", _design.Caliber.Key()));
        _magazineCaliber.Text = Loc.GetString("gunsmith-ui-weapon-caliber", ("key", _magazine.Caliber.Key()));

        // The weapon.
        var check = GunsmithEvaluator.Evaluate(_design, _data);
        if (check.Valid)
        {
            SetMarkup(_weaponSummary, CostMarkup(check.Result.MaterialUnits) + "\n" +
                Loc.GetString("gunsmith-ui-complexity", ("points", NumberField.Format(Math.Round(check.Result.Complexity, 1)))) + "\n" +
                Loc.GetString("gunsmith-ui-time", ("seconds", (int) Math.Ceiling(check.Result.BuildSeconds))));
        }
        else
        {
            SetMarkup(_weaponSummary, ErrorMarkup(check.Errors));
        }

        _buildWeapon.Disabled = !check.Valid;
        DrawPreview();

        // The caliber: only whether it can exist.
        var caliberCheck = GunsmithEvaluator.CheckCaliber(_design.Caliber, _data);
        SetMarkup(_caliberSummary, caliberCheck.Valid ? string.Empty : ErrorMarkup(caliberCheck.Errors));

        // The magazine.
        var magazineCheck = GunsmithEvaluator.EvaluateMagazine(_magazine, _data);
        if (!Content.Shared.CMU14.Gunsmith.GunsmithCapacities.All.Contains(_magazine.Capacity))
            magazineCheck.Errors.Add("capacity");

        if (magazineCheck.Valid)
        {
            SetMarkup(_magazineSummary, CostMarkup(magazineCheck.Result.MaterialUnits) + "\n" +
                Loc.GetString("gunsmith-ui-complexity", ("points", NumberField.Format(Math.Round(magazineCheck.Result.Complexity, 1)))) + "\n" +
                Loc.GetString("gunsmith-ui-time", ("seconds", (int) Math.Ceiling(magazineCheck.Result.BuildSeconds))));
        }
        else
        {
            SetMarkup(_magazineSummary, ErrorMarkup(magazineCheck.Errors));
        }

        _buildMagazine.Disabled = !magazineCheck.Valid;
    }

    private static string ErrorMarkup(List<string> errors) =>
        string.Join("\n", errors.Select(e => $"[color=#ff8080]{Loc.GetString("gunsmith-ui-error", ("what", ErrorName(e)))}[/color]"));

    private static string ErrorName(string error)
    {
        var colon = error.IndexOf(':');
        var key = colon < 0 ? error : error[..colon];
        var range = colon < 0 ? string.Empty : error[colon..];
        return Loc.GetString($"gunsmith-error-{key}") + range;
    }

    /// <summary>The picture of the weapon the design will give, made of the same parts the bench uses.</summary>
    private void DrawPreview()
    {
        _preview.RemoveAllChildren();

        // A look is a picture of a weapon of the game; without one the weapon is put together of parts.
        if (!string.IsNullOrEmpty(_design.Look) && _data.Look(_design.Look) is { } look
            && _resources.TryGetResource<RSIResource>("/Textures/" + look.Rsi, out var lookRsi) && lookRsi.RSI.TryGetState(look.State, out var lookState))
        {
            _preview.AddChild(new TextureRect
            {
                Texture = lookState.Frame0,
                Stretch = TextureRect.StretchMode.KeepCentered,
                TextureScale = new Vector2(3, 3),
                MinSize = new Vector2(160, 64),
            });
            return;
        }

        if (_data.Action(_design.Action) is not { } action)
            return;

        var rsi = _resources.GetResource<RSIResource>(GunsmithSprites.Rsi).RSI;
        foreach (var state in new[] { GunsmithSprites.Rear(_design, action), GunsmithSprites.Mid(action), GunsmithSprites.Front(_design) })
        {
            if (!rsi.TryGetState(state, out var layer))
                continue;

            _preview.AddChild(new TextureRect
            {
                Texture = layer.Frame0,
                Stretch = TextureRect.StretchMode.KeepCentered,
                TextureScale = new Vector2(4, 4),
                MinSize = new Vector2(160, 64),
            });
        }
    }
}
