// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Gunsmith;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Damage.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.CMU14.Gunsmith;

/// <summary>
/// The window of the ammunition bench. The player picks the caliber, the material of each part of the bullet and the casing, the powder load and,
/// if they want, a liquid for the core. The window says what that costs and what the inputs have to look like, and gives the tables and the
/// formulas; it never says what the cartridge comes to.
/// </summary>
public sealed class AmmoBenchWindow : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    private readonly PrototypeAmmoData _data;

    private AmmoDesign _design = new();
    private int _count = 10;
    private Dictionary<string, int> _stock = new();
    private Dictionary<string, double> _tank = new();
    private double _tankCapacity;
    private CaliberSpec? _itemCaliber;

    private PresetPicker _preset = default!;
    private NumberField _bore = default!;
    private NumberField _caseDiameter = default!;
    private NumberField _caseLength = default!;
    private NumberField _seatDepth = default!;
    private NumberField _bulletMass = default!;
    private ChoiceField _powder = default!;
    private NumberField _charge = default!;
    private ChoiceField _core = default!;
    private ChoiceField _jacket = default!;
    private ChoiceField _tip = default!;
    private ChoiceField _casing = default!;
    private ChoiceField _powderLevel = default!;
    private ChoiceField _payload = default!;
    private NumberField _payloadUnits = default!;
    private ChoiceField _pellets = default!;
    private ChoiceField _kind = default!;
    private NumberField _countField = default!;
    private readonly Label _caliberKey = new() { HorizontalExpand = true };
    private readonly RichTextLabel _summary = new() { HorizontalExpand = true };
    private readonly RichTextLabel _result = new() { HorizontalExpand = true };
    private readonly Button _build = new() { Text = Loc.GetString("ammo-ui-build") };
    private readonly Button _fromItem = new() { Text = Loc.GetString("ammo-ui-caliber-from-item") };

    private readonly BoxContainer _tankList = new() { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly Label _tankVolume = new() { HorizontalExpand = true };
    private readonly Button _drain = new() { Text = Loc.GetString("ammo-ui-drain") };
    private readonly BoxContainer _stockList = new() { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly Label _itemLabel = new() { HorizontalExpand = true };
    private readonly Button _takeItem = new() { Text = Loc.GetString("ammo-ui-take-item") };

    public event Action<AmmoDesign, int>? OnBuild;
    public event Action<string>? OnEjectStock;
    public event Action? OnDrain;
    public event Action? OnTakeItem;

    public AmmoBenchWindow()
    {
        IoCManager.InjectDependencies(this);
        _data = new PrototypeAmmoData(_prototypes);

        Title = Loc.GetString("ammo-ui-title");
        MinSize = new Vector2(760, 600);

        var tabs = new TabContainer();
        tabs.AddChild(BuildCartridgeTab());
        tabs.AddChild(BuildTankTab());
        tabs.AddChild(BuildStockTab());
        tabs.AddChild(BuildHandbookTab());
        tabs.AddChild(BuildDataTab());
        tabs.SetTabTitle(0, Loc.GetString("ammo-ui-tab-cartridge"));
        tabs.SetTabTitle(1, Loc.GetString("ammo-ui-tab-tank"));
        tabs.SetTabTitle(2, Loc.GetString("ammo-ui-tab-stock"));
        tabs.SetTabTitle(3, Loc.GetString("ammo-ui-tab-handbook"));
        tabs.SetTabTitle(4, Loc.GetString("ammo-ui-tab-data"));
        Contents.AddChild(tabs);

        _build.OnPressed += _ => OnBuild?.Invoke(_design.Clone(), _count);
        _drain.OnPressed += _ => OnDrain?.Invoke();
        _takeItem.OnPressed += _ => OnTakeItem?.Invoke();
        _fromItem.OnPressed += _ =>
        {
            if (_itemCaliber != null)
                LoadCaliber(_itemCaliber);
        };

        LoadDesign();
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
            changed(value);
            Refresh();
        };
        return field;
    }

    private string MaterialName(AmmoMaterialPrototype material) =>
        _data.Material(material.Material) is { } g ? Loc.GetString(g.Name) : material.ID;

    private ChoiceField Parts(string label, AmmoPart part, bool optional, Action<string> chosen)
    {
        var field = new ChoiceField(label);
        if (optional)
            field.Add(string.Empty, Loc.GetString("ammo-ui-none"));

        foreach (var material in _prototypes.EnumeratePrototypes<AmmoMaterialPrototype>().Where(m => m.Parts.ContainsKey(part)).OrderBy(m => m.ID))
            field.Add(material.ID, MaterialName(material));

        field.Chosen += id =>
        {
            chosen(id);
            Refresh();
        };
        return field;
    }

    private string StackName(string stackType) =>
        _prototypes.TryIndex<Content.Shared.Stacks.StackPrototype>(stackType, out var stack) ? Loc.GetString(stack.Name) : stackType;

    private string ReagentName(string id) =>
        _entities.System<RMCReagentSystem>().TryIndex(id, out var reagent) ? reagent.LocalizedName : id;

    // ------------------------------------------------------------------ the cartridge tab

    private Control BuildCartridgeTab()
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
        };

        _bore = Num(Loc.GetString("gunsmith-ui-bore"), "mm", v => _design.Caliber.BoreMm = v);
        _caseDiameter = Num(Loc.GetString("gunsmith-ui-case-diameter"), "mm", v => _design.Caliber.CaseDiameterMm = v);
        _caseLength = Num(Loc.GetString("gunsmith-ui-case-length"), "mm", v => _design.Caliber.CaseLengthMm = v);
        _seatDepth = Num(Loc.GetString("gunsmith-ui-seat-depth"), "mm", v => _design.Caliber.SeatDepthMm = v);
        _bulletMass = Num(Loc.GetString("ammo-ui-nominal-mass"), "g", v => _design.Caliber.BulletMassG = v);
        _powder = new ChoiceField(Loc.GetString("gunsmith-ui-powder"));
        foreach (var powder in _prototypes.EnumeratePrototypes<GunsmithPowderPrototype>().OrderBy(p => p.Force))
            _powder.Add(powder.ID, Loc.GetString(powder.Name));
        _powder.Chosen += id => { _design.Caliber.Powder = id; Refresh(); };
        _charge = Num(Loc.GetString("ammo-ui-nominal-charge"), "g", v => _design.Caliber.ChargeG = v);

        var caliber = Column();
        caliber.AddChild(_preset);
        caliber.AddChild(_bore);
        caliber.AddChild(_caseDiameter);
        caliber.AddChild(_caseLength);
        caliber.AddChild(_seatDepth);
        caliber.AddChild(_bulletMass);
        caliber.AddChild(_powder);
        caliber.AddChild(_charge);
        caliber.AddChild(_caliberKey);
        caliber.AddChild(_fromItem);

        _core = Parts(Loc.GetString("ammo-ui-core"), AmmoPart.Core, false, id => _design.Core = id);
        _jacket = Parts(Loc.GetString("ammo-ui-jacket"), AmmoPart.Jacket, false, id => _design.Jacket = id);
        _tip = Parts(Loc.GetString("ammo-ui-tip"), AmmoPart.Tip, true, id => _design.Tip = id);
        _casing = Parts(Loc.GetString("ammo-ui-casing"), AmmoPart.Casing, false, id => _design.Casing = id);

        _powderLevel = new ChoiceField(Loc.GetString("ammo-ui-powder-level"));
        for (var level = AmmoMechanics.MinPowderLevel; level <= AmmoMechanics.MaxPowderLevel; level++)
            _powderLevel.Add(level.ToString(), level > 0 ? $"+{level}" : level.ToString());
        _powderLevel.Chosen += id =>
        {
            _design.PowderLevel = int.Parse(id);
            Refresh();
        };

        _payload = new ChoiceField(Loc.GetString("ammo-ui-payload"));
        _payload.Chosen += id =>
        {
            _design.Payload = id;
            Refresh();
        };

        _pellets = new ChoiceField(Loc.GetString("ammo-ui-pellets"));
        foreach (var count in AmmoMechanics.ShellPellets)
            _pellets.Add(count.ToString(), count.ToString());
        _pellets.Chosen += id =>
        {
            _design.Pellets = int.Parse(id);
            Refresh();
        };

        // What kind of bullet it is: one made of parts, or buckshot (and heavy buckshot, only for the heavy shotgun caliber). Buckshot has no jacket, no tip, no
        // liquid and no powder load to choose.
        _kind = new ChoiceField(Loc.GetString("ammo-ui-kind"));
        foreach (var kind in Enum.GetValues<AmmoBulletKind>())
            _kind.Add(kind.ToString(), Loc.GetString($"ammo-kind-{kind.ToString().ToLowerInvariant()}"));
        _kind.Chosen += id =>
        {
            _design.Kind = Enum.Parse<AmmoBulletKind>(id);
            RebuildPayload();
            if (_design.Kind is AmmoBulletKind.Buckshot or AmmoBulletKind.HeavyBuckshot && _design.Pellets < 2)
            {
                _design.Pellets = 8;
                _pellets.Selected = "8";
            }

            Refresh();
        };

        _payloadUnits = Num(Loc.GetString("ammo-ui-payload-units"), string.Empty, v => _design.PayloadUnits = v);
        _payloadUnits.Set(_design.PayloadUnits);

        var parts = Column();
        parts.AddChild(_kind);
        parts.AddChild(_pellets);
        parts.AddChild(_core);
        parts.AddChild(_jacket);
        parts.AddChild(_tip);
        parts.AddChild(_casing);
        parts.AddChild(_powderLevel);
        parts.AddChild(_payload);
        parts.AddChild(_payloadUnits);

        _countField = Num(Loc.GetString("ammo-ui-count"), string.Empty, v => _count = (int) Math.Round(v));
        _countField.Set(_count);

        var make = Column();
        make.AddChild(_countField);
        make.AddChild(_summary);
        make.AddChild(_build);

        var result = Column();
        result.AddChild(_result);

        var body = Column(8);
        body.AddChild(new Label { Text = Loc.GetString("ammo-ui-hint"), HorizontalExpand = true });
        body.AddChild(Section(Loc.GetString("ammo-ui-section-caliber"), caliber));
        body.AddChild(Section(Loc.GetString("ammo-ui-section-parts"), parts));
        body.AddChild(Section(Loc.GetString("ammo-ui-section-cost"), make));
        body.AddChild(Section(Loc.GetString("ammo-ui-section-result"), result));
        root.AddChild(Scroll(body));
        return root;
    }

    // ------------------------------------------------------------------ the tank, the stock

    private Control BuildTankTab()
    {
        var root = Column(6);
        root.Margin = new Thickness(6);

        var tank = Column();
        tank.AddChild(_tankVolume);
        tank.AddChild(_tankList);
        tank.AddChild(_drain);

        var body = Column(8);
        body.AddChild(new Label { Text = Loc.GetString("ammo-ui-tank-hint"), HorizontalExpand = true });
        body.AddChild(Section(Loc.GetString("ammo-ui-section-tank"), tank));
        root.AddChild(Scroll(body));
        return root;
    }

    private Control BuildStockTab()
    {
        var root = Column(6);
        root.Margin = new Thickness(6);

        var item = Column();
        item.AddChild(_itemLabel);
        item.AddChild(_takeItem);

        var body = Column(8);
        body.AddChild(new Label { Text = Loc.GetString("ammo-ui-stock-hint"), HorizontalExpand = true });
        body.AddChild(Section(Loc.GetString("gunsmith-ui-section-stock-list"), _stockList));
        body.AddChild(Section(Loc.GetString("ammo-ui-section-item"), item));
        root.AddChild(Scroll(body));
        return root;
    }

    // ------------------------------------------------------------------ the handbook and the tables

    private Control BuildHandbookTab()
    {
        var root = Column(6);
        root.Margin = new Thickness(6);

        var body = Column(10);
        foreach (var section in new[] { "ammo-hb-intro", "ammo-hb-parts", "ammo-hb-powder", "ammo-hb-liquid", "ammo-hb-weapon", "ammo-hb-damage", "ammo-hb-kinds", "ammo-hb-syringe" })
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

        // What each material does in each part.
        var materials = new GridContainer { Columns = 6, HSeparationOverride = 12, VSeparationOverride = 2 };
        foreach (var header in new[] { "material", "part", "damage", "pierce", "seal", "jam" })
            materials.AddChild(new Label { Text = Loc.GetString($"ammo-data-{header}"), FontColorOverride = Color.FromHex("#c9b99a") });

        foreach (var material in _prototypes.EnumeratePrototypes<AmmoMaterialPrototype>().OrderBy(m => m.ID))
        {
            foreach (var (part, effect) in material.Parts.OrderBy(p => p.Key))
            {
                materials.AddChild(new Label { Text = MaterialName(material) });
                materials.AddChild(new Label { Text = Loc.GetString($"ammo-part-name-{part.ToString().ToLowerInvariant()}") });
                materials.AddChild(new Label { Text = NumberField.Format(effect.Damage) });
                materials.AddChild(new Label { Text = NumberField.Format(effect.ArmorPierce) });
                materials.AddChild(new Label { Text = part == AmmoPart.Casing ? NumberField.Format(effect.Seal) : "-" });
                materials.AddChild(new Label { Text = NumberField.Format(effect.Jam) });
            }
        }

        body.AddChild(Section(Loc.GetString("ammo-data-materials"), materials));

        // The powder load.
        var levels = new GridContainer { Columns = 2, HSeparationOverride = 20, VSeparationOverride = 2 };
        levels.AddChild(new Label { Text = Loc.GetString("ammo-data-level"), FontColorOverride = Color.FromHex("#c9b99a") });
        levels.AddChild(new Label { Text = Loc.GetString("ammo-data-factor"), FontColorOverride = Color.FromHex("#c9b99a") });
        for (var level = AmmoMechanics.MinPowderLevel; level <= AmmoMechanics.MaxPowderLevel; level++)
        {
            levels.AddChild(new Label { Text = level > 0 ? $"+{level}" : level.ToString() });
            levels.AddChild(new Label { Text = NumberField.Format(AmmoMechanics.PowderFactor(level)) });
        }

        body.AddChild(Section(Loc.GetString("ammo-data-powder-levels"), levels));

        // The liquids.
        var liquids = new GridContainer { Columns = 6, HSeparationOverride = 12, VSeparationOverride = 2 };
        foreach (var header in new[] { "reagent", "burns", "injected", "inject-share", "damage-type", "damage-per-unit" })
            liquids.AddChild(new Label { Text = Loc.GetString($"ammo-data-{header}"), FontColorOverride = Color.FromHex("#c9b99a") });

        foreach (var payload in _prototypes.EnumeratePrototypes<AmmoPayloadPrototype>().OrderBy(p => p.ID))
        {
            liquids.AddChild(new Label { Text = ReagentName(payload.Reagent) });
            liquids.AddChild(new Label { Text = payload.Ignite ? Loc.GetString("ammo-data-yes") : "-" });
            liquids.AddChild(new Label { Text = payload.Inject ? Loc.GetString("ammo-data-yes") : "-" });
            liquids.AddChild(new Label { Text = NumberField.Format(payload.InjectShare) });
            liquids.AddChild(new Label { Text = payload.DamageType ?? "-" });
            liquids.AddChild(new Label { Text = NumberField.Format(payload.DamagePerUnit) });
        }

        body.AddChild(Section(Loc.GetString("ammo-data-liquids"), liquids));
        body.AddChild(new Label { Text = Loc.GetString("ammo-data-other-liquids"), HorizontalExpand = true });

        // The constants.
        var constants = new GridContainer { Columns = 3, HSeparationOverride = 14, VSeparationOverride = 2 };
        foreach (var (symbol, value, key) in new (string, double, string)[]
                 {
                     ("ρ_ном", AmmoMechanics.NominalBulletDensity, "density"),
                     ("доля рубашки", AmmoMechanics.JacketShare, "jacket-share"),
                     ("доля сердечника", AmmoMechanics.CoreShareNoTip, "core-share"),
                     ("доля сердечника с наконечником", AmmoMechanics.CoreShareTip, "core-share-tip"),
                     ("доля наконечника", AmmoMechanics.TipShare, "tip-share"),
                     ("доля полости", AmmoMechanics.CavityShare, "cavity-share"),
                     ("ед./см³", AmmoMechanics.UnitsPerCm3, "units-per-cm3"),
                     ("L_ref / d", AmmoMechanics.ReferenceBarrelCalibers, "reference-barrel"),
                     ("толщина стенки гильзы, мм", AmmoMechanics.CaseWallMm, "case-wall"),
                     ("толщина донца гильзы, мм", AmmoMechanics.CaseHeadMm, "case-head"),
                 })
        {
            constants.AddChild(new Label { Text = symbol, FontColorOverride = Color.FromHex("#c9b99a") });
            constants.AddChild(new Label { Text = NumberField.Format(value) });
            constants.AddChild(new Label { Text = Loc.GetString($"ammo-const-{key}") });
        }

        body.AddChild(Section(Loc.GetString("gunsmith-data-constants"), constants));
        root.AddChild(Scroll(body));
        return root;
    }

    // ------------------------------------------------------------------ keeping the window and the design in step

    private void LoadDesign()
    {
        _core.Selected = _design.Core;
        _jacket.Selected = _design.Jacket;
        _tip.Selected = _design.Tip;
        _casing.Selected = _design.Casing;
        _powderLevel.Selected = _design.PowderLevel.ToString();
        _pellets.Selected = _design.Pellets.ToString();
        _kind.Selected = _design.Kind.ToString();
        LoadCaliber(_design.Caliber);
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

    public void UpdateState(AmmoBenchBuiState state)
    {
        _stock = state.Stock;
        _tank = state.Tank;
        _tankCapacity = state.TankCapacity;
        _itemCaliber = state.ItemCaliber;

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

        _tankList.RemoveAllChildren();
        var total = _tank.Values.Sum();
        _tankVolume.Text = Loc.GetString("ammo-ui-tank-volume", ("units", NumberField.Format(Math.Round(total, 1))), ("capacity", NumberField.Format(_tankCapacity)));
        if (_tank.Count == 0)
            _tankList.AddChild(new Label { Text = Loc.GetString("ammo-ui-tank-empty") });

        foreach (var (reagent, units) in _tank.OrderByDescending(p => p.Value))
            _tankList.AddChild(new Label { Text = $"{ReagentName(reagent)}: {NumberField.Format(Math.Round(units, 1))}" });

        _drain.Disabled = _tank.Count == 0;

        _itemLabel.Text = _itemCaliber == null
            ? Loc.GetString("ammo-ui-no-item")
            : Loc.GetString("ammo-ui-item-on-bench", ("key", _itemCaliber.Key()));
        _takeItem.Disabled = _itemCaliber == null;
        _fromItem.Disabled = _itemCaliber == null;

        RebuildPayload();
        Refresh();
    }

    /// <summary>
    /// The liquids the bullet can be filled with are the ones in the tank; a plain bullet and buckshot take only the fuels of the flamethrowers, a syringe bullet
    /// takes any.
    /// </summary>
    private void RebuildPayload()
    {
        var chosen = _design.Payload;
        var any = _design.Kind == AmmoBulletKind.Syringe;
        _payload.Clear();
        _payload.Add(string.Empty, Loc.GetString("ammo-ui-none"));
        foreach (var reagent in _tank.Keys.Where(r => any || _data.Payload(r) is { Ignite: true }).OrderBy(ReagentName))
            _payload.Add(reagent, ReagentName(reagent));

        if (string.IsNullOrEmpty(chosen) || !_tank.ContainsKey(chosen) || (!any && _data.Payload(chosen) is not { Ignite: true }))
            _design.Payload = string.Empty;

        _payload.Selected = _design.Payload;
    }

    /// <summary>Redraws what depends on the design: the shape errors, the cost. Never what the cartridge comes to.</summary>
    private void Refresh()
    {
        _caliberKey.Text = Loc.GetString("gunsmith-ui-caliber-key", ("key", _design.Caliber.Key()));

        // A plain bullet has a core, a jacket and a tip; buckshot only the metal of the shot and the pellets; a syringe bullet a body and the liquid.
        var shot = _design.Kind is AmmoBulletKind.Buckshot or AmmoBulletKind.HeavyBuckshot;
        var syringe = _design.Kind == AmmoBulletKind.Syringe;
        var plain = _design.Kind == AmmoBulletKind.Bullet;
        _pellets.Visible = shot;
        _core.Visible = !syringe;
        _jacket.Visible = plain || syringe;
        _tip.Visible = plain;
        _payloadUnits.Visible = syringe;

        var check = AmmoEvaluator.Evaluate(_design, _data);
        if (!check.Valid)
        {
            SetMarkup(_summary, string.Join("\n", check.Errors.Select(e => $"[color=#ff8080]{ErrorName(e)}[/color]")));
            SetMarkup(_result, string.Empty);
            _build.Disabled = true;
            return;
        }

        var count = Math.Clamp(_count, 1, 20);
        var (materials, powder, liquid) = AmmoEvaluator.BatchCost(check.Result, count);
        var lines = new List<string>();

        void Line(string name, int need, int have)
        {
            var color = have >= need ? "#9fdc9f" : "#ff8080";
            lines.Add($"[color={color}]{name}: {need} ({Loc.GetString("gunsmith-ui-in-stock", ("count", have))})[/color]");
        }

        foreach (var (id, amount) in materials)
        {
            var type = _data.Material(id)!.StackType;
            Line(StackName(type), (int) Math.Ceiling(amount), _stock.GetValueOrDefault(type));
        }

        Line(StackName("RUMCSaltpeterPowder"), (int) Math.Ceiling(powder), _stock.GetValueOrDefault("RUMCSaltpeterPowder"));

        if (liquid > 0)
        {
            var have = _tank.GetValueOrDefault(_design.Payload);
            var color = have + 0.001 >= liquid ? "#9fdc9f" : "#ff8080";
            lines.Add($"[color={color}]{ReagentName(_design.Payload)}: {NumberField.Format(liquid)} ({Loc.GetString("ammo-ui-in-tank", ("units", NumberField.Format(Math.Round(have, 1))))})[/color]");
        }

        lines.Add(Loc.GetString("gunsmith-ui-time", ("seconds", (int) Math.Ceiling(4 + count * check.Result.BuildSeconds))));
        SetMarkup(_summary, string.Join("\n", lines));
        SetMarkup(_result, ResultMarkup(check.Result));
        _build.Disabled = false;
    }

    /// <summary>What the cartridge comes to: the damage of the bullet by type, the armor penetration, the fire.</summary>
    private string ResultMarkup(AmmoEvaluation e)
    {
        var damage = string.Join(" + ", e.Damage().Select(p => $"{DamageTypeName(p.Key)} {NumberField.Format(Math.Round(p.Value, 1))}"));
        var lines = new List<string>
        {
            e.Pellets > 1
                ? Loc.GetString("ammo-ui-result-damage-shot", ("pellets", e.Pellets), ("damage", damage))
                : Loc.GetString("ammo-ui-result-damage", ("damage", damage)),
        };

        if (e.Kind == AmmoBulletKind.Bullet && !e.Shell)
            lines.Add(Loc.GetString("ammo-ui-result-armor", ("ap", NumberField.Format(Math.Round(e.ArmorPierce, 1))), ("level", e.ArmorLevel)));

        lines.Add(e.GameDamage > 0
            ? Loc.GetString("ammo-ui-result-fitted")
            : Loc.GetString("ammo-ui-result-custom"));

        if (e.Ignite && _entities.System<RMCReagentSystem>().TryIndex(e.PayloadReagent, out var fuel))
        {
            lines.Add(Loc.GetString("ammo-ui-result-fire",
                ("intensity", fuel.Intensity > 0 ? Math.Clamp(fuel.Intensity, 5, 80) : 30),
                ("duration", fuel.Duration != 0 ? Math.Clamp(Math.Abs(fuel.Duration), 5, 60) : 20)));
        }

        lines.Add(Loc.GetString("ammo-ui-result-note"));
        return string.Join(Environment.NewLine, lines);
    }

    private string DamageTypeName(string type) =>
        _prototypes.TryIndex<DamageTypePrototype>(type, out var proto) ? proto.LocalizedName : type;


    private static string ErrorName(string error)
    {
        var colon = error.IndexOf(':');
        var key = colon < 0 ? error : error[..colon];
        var range = colon < 0 ? string.Empty : error[colon..];
        var id = $"gunsmith-error-{key}";
        return (Loc.TryGetString(id, out var text) ? text : Loc.GetString($"ammo-error-{key}")) + range;
    }
}
