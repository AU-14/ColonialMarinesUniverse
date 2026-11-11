using Content.Client._RMC14.UserInterface;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.CMU14.Chemistry.Reagents;
using Content.Client.UserInterface.Controls;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.Chemistry.Reagent;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Content.Client.CMU14.Chemistry.Research;

[UsedImplicitly]
public sealed partial class ResearchDataTerminalBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IGameTiming _time = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IReflectionManager _reflection = default!;
    [Dependency] private IDynamicTypeFactory _typeFactory = default!;
    [Dependency] private IEntitySystemManager _entSys = default!;
    private ResearchDataTerminalWindow? _window;

    /// <summary>Property prototypes default to an effectively unlimited MaxLevel of 999.</summary>
    private const int UncappedPropertyLevel = 999;
    private Dictionary<string, Type>? _effectTypes;
    private readonly Dictionary<string, string?> _effectTextCache = new();

    ResearchDataTerminalAttemptUpgradeBuiMsg UpgradeAttempt = new();
    ResearchDataTerminalPrintLastBuiMsg PrintLast = new();
    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ResearchDataTerminalWindow>();
        _window.ReduceCooldown.OnPressed += _ => SendPredictedMessage(new CMUResearchReduceCooldownBuiMsg());
        _window.Reprint.OnPressed += _ => SendPredictedMessage(PrintLast);
        _window.Upgrade.OnPressed += _ => SendPredictedMessage(UpgradeAttempt);
        if (State is ResearchDataTerminalBuiState s)
        {
            RefreshState(s);
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ResearchDataTerminalBuiState s)
            RefreshState(s);
    }

    private void RefreshState(ResearchDataTerminalBuiState state)
    {
        if (_window is null)
            return;
        string clearance = string.Empty;
        if (state.Clearance == 6)
        {
            clearance = "X";
        }
        else
        {
            clearance = state.Clearance.ToString();
        }
        _window.Clearance.Text = Loc.GetString("research-data-ui-clearance", ("NUM", clearance));
        _window.Credits.Text = Loc.GetString("research-data-ui-credits", ("NUM", state.Credits), ("DATA", state.SampleData));
        _window.Tabs.SetTabTitle(0, Loc.GetString("research-data-ui-manage"));
        _window.Tabs.SetTabTitle(1, Loc.GetString("research-data-ui-view"));
        _window.Tabs.SetTabTitle(2, Loc.GetString("research-data-ui-tab-contracts"));
        _window.Tabs.SetTabTitle(3, Loc.GetString("research-data-ui-tab-properties"));
        _window.NextUpdate = state.NextUpdate;
        _window.ReduceCooldown.Disabled = !state.Picked || state.Credits < 1 || state.NextUpdate <= _time.CurTime;
        _window.TimeLeftBar.MaxValue = (float)(state.NextUpdate - state.LastTime).TotalMilliseconds;
        _window.ChemContainer.RemoveAllChildren();
        var xLocked = state.XLockedUntil is not null && _time.CurTime < state.XLockedUntil.Value;
        if (state.Credits >= state.UpgradeCost && state.Clearance != 6 && !xLocked)
            _window.Upgrade.Disabled = false;
        else _window.Upgrade.Disabled = true;
        _window.UpgradeText.Text = xLocked
            ? Loc.GetString("research-data-ui-improve-locked", ("TIME", Math.Ceiling((state.XLockedUntil!.Value - _time.CurTime).TotalMinutes)))
            : Loc.GetString("research-data-ui-improve", ("NUM", state.UpgradeCost));
        // FLOW-24: a locked faction (GOVFOR during Insurgency) can neither upgrade nor buy cooldown.
        if (state.Locked)
        {
            _window.Upgrade.Disabled = true;
            _window.ReduceCooldown.Disabled = true;
        }
        StyleBoxFlat panel = new();
        panel.BackgroundColor = Color.FromHex("#0f0f00");
        panel.BorderColor = Color.FromHex("#ffbf00");
        panel.BorderThickness = new Thickness(2f);

        StyleBoxFlat panelL = new();
        panelL.BackgroundColor = Color.FromHex("#0f0f00");
        panelL.BorderColor = Color.FromHex("#ffbf00");
        panelL.BorderThickness = new Thickness(2f, 1f, 1f, 1f);

        StyleBoxFlat panelM = new();
        panelM.BackgroundColor = Color.FromHex("#0f0f00");
        panelM.BorderColor = Color.FromHex("#ffbf00");
        panelM.BorderThickness = new Thickness(1f, 1f, 1f, 1f);

        StyleBoxFlat panelR = new();
        panelR.BackgroundColor = Color.FromHex("#0f0f00");
        panelR.BorderColor = Color.FromHex("#ffbf00");
        panelR.BorderThickness = new Thickness(1f, 1f, 2f, 1f);

        StyleBoxFlat panelLB = new();
        panelLB.BackgroundColor = Color.FromHex("#0f0f00");
        panelLB.BorderColor = Color.FromHex("#ffbf00");
        panelLB.BorderThickness = new Thickness(2f, 1f, 1f, 2f);

        StyleBoxFlat panelMB = new();
        panelMB.BackgroundColor = Color.FromHex("#0f0f00");
        panelMB.BorderColor = Color.FromHex("#ffbf00");
        panelMB.BorderThickness = new Thickness(1f, 1f, 1f, 2f);

        StyleBoxFlat panelRB = new();
        panelRB.BackgroundColor = Color.FromHex("#0f0f00");
        panelRB.BorderColor = Color.FromHex("#ffbf00");
        panelRB.BorderThickness = new Thickness(1f, 1f, 2f, 2f);

        StyleBoxFlat but = new();
        but.BackgroundColor = Color.FromHex("#ffbf00");
        _window.DataTable.RemoveChildrenAfter(_window.TableAfter.GetPositionInParent() + 1);
        foreach (var datum in state.Data)
        {
            bool last = false;
            if (datum.Key == state.Data.Last().Key)
            {
                last = true;
            }
            var str = datum.Value.Item2;
            var time = datum.Value.Item3;
            var analysis = datum.Value.Item4;
            var name = datum.Value.Item5.Name;
            var dat = datum.Value.Item5;
            RichTextLabel timel = new();
            timel.Text = Loc.GetString("research-data-ui-scan-time-idx", ("TIME", time.ToString(@"h\:mm\:ss")));
            RichTextLabel analysisl = new();
            if (analysis)
            {
                analysisl.Text = Loc.GetString("research-data-ui-analysis-sim");
            }
            else
            {
                analysisl.Text = Loc.GetString("research-data-ui-analysis-scan");
            }
            RichTextLabel namel = new();
            namel.Text = Loc.GetString("research-data-ui-compound-idx", ("NAME", name));
            BoxContainer con = new();
            con.Orientation = BoxContainer.LayoutOrientation.Horizontal;
            con.SeparationOverride = 20;
            Button read = new Button();
            read.Margin = new(5f);
            read.StyleBoxOverride = but;
            RichTextLabel readl = new();
            readl.Text = Loc.GetString("research-data-ui-read");
            read.AddChild(readl);
            read.Disabled = true;
            Button print = new();
            print.Margin = new(5f);
            print.StyleBoxOverride = but;
            RichTextLabel printl = new();
            printl.Text = Loc.GetString("research-data-ui-print");
            print.AddChild(printl);
            print.OnPressed += _ => SendPredictedMessage(new ResearchDataTerminalPrintChemBuiMsg(datum.Key));
            con.AddChild(read);
            con.AddChild(print);
            PanelContainer A = new();
            PanelContainer B = new();
            PanelContainer C = new();
            PanelContainer D = new();
            A.AddChild(timel);
            B.AddChild(analysisl);
            C.AddChild(namel);
            D.AddChild(con);
            if (last)
            {
                A.PanelOverride = panelLB;
                B.PanelOverride = panelMB;
                C.PanelOverride = panelMB;
                D.PanelOverride = panelRB;
            }
            else
            {
                A.PanelOverride = panelL;
                B.PanelOverride = panelM;
                C.PanelOverride = panelM;
                D.PanelOverride = panelR;
            }
            _window.DataTable.AddChild(A);
            _window.DataTable.AddChild(B);
            _window.DataTable.AddChild(C);
            _window.DataTable.AddChild(D);
            //_window.DataTable.AddChild(timel);
            //_window.DataTable.AddChild(analysisl);
            //_window.DataTable.AddChild(namel);
            //_window.DataTable.AddChild(con);
        }

        if (state.Locked)
        {
            // FLOW-24: the offers are replaced by the lock message; no pick buttons are shown.
            var lockedPanel = new PanelContainer { PanelOverride = panel, HorizontalExpand = true };
            var lockedLabel = new RichTextLabel { Margin = new Thickness(5f, 10f) };
            lockedLabel.Text = state.LockedReason ?? Loc.GetString("research-locked-insurgency");
            lockedPanel.AddChild(lockedLabel);
            _window.ChemContainer.AddChild(lockedPanel);
        }

        foreach (var chem in state.IDs.Take(state.Locked ? 0 : state.IDs.Count))
        {
            BoxContainer box = new BoxContainer();
            PanelContainer panela = new PanelContainer();
            panela.VerticalExpand = true;
            panela.HorizontalExpand = true;
            box.Orientation = BoxContainer.LayoutOrientation.Vertical;
            BoxContainer box2 = new BoxContainer();
            box2.Orientation = BoxContainer.LayoutOrientation.Vertical;
            RichTextLabel name = new RichTextLabel();
            name.Text = Loc.GetString("research-data-ui-chem-name", ("NAME", chem.Name));
            PanelContainer panelb = new PanelContainer();
            HSpacer spacer = new HSpacer();
            spacer.Spacing = 15;
            RichTextLabel diff = new RichTextLabel();
            string difficulty = string.Empty;
            if(chem.GenTier >= 3)
            {
                difficulty = Loc.GetString("research-data-ui-diff-hard");
            }
            else if (chem.GenTier == 2)
            {
                difficulty = Loc.GetString("research-data-ui-diff-inter");
            }
            else
            {
                difficulty = Loc.GetString("research-data-ui-diff-easy");
            }
            diff.Text = Loc.GetString("research-data-ui-chem-difficulty", ("DIFF", difficulty));
            RichTextLabel desc = new RichTextLabel();
            desc.Text = Loc.GetString("research-data-ui-chem-desc",
                ("RECIHINT", ReagentName(chem.RecipeHint)),
                ("PROPHINT", PropertyName(chem.PropertyHint)));
            Button button = new Button();
            button.OnPressed += _ => SendPredictedMessage(new ResearchDataTerminalPickChemBuiMsg(chem.ID));
            button.Margin = new(5, 5, 5, 5);
            RichTextLabel cont = new RichTextLabel();
            cont.Text = Loc.GetString("research-data-ui-chem-take");
            cont.HorizontalAlignment = Control.HAlignment.Left;
            if ((_time.CurTime < state.NextUpdate && state.Picked) || state.Locked)
                button.Disabled = true;
            name.Margin = new Thickness(5f, 10f);
            panela.AddChild(name);
            panela.PanelOverride = panel;
            panelb.PanelOverride = panel;
            box.AddChild(panela);
            box2.AddChild(spacer);
            box2.AddChild(diff);
            box2.AddChild(desc);
            box2.AddChild(button);
            panelb.AddChild(box2);
            button.StyleBoxOverride = but;
            button.AddChild(cont);
            box.AddChild(panelb);
            _window.ChemContainer.AddChild(box);
        }

        RefreshContracts(state, panel);
        RefreshProperties(panel);
    }

    private string ReagentName(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;
        return _proto.GetInstances<ReagentPrototype>().TryGetValue(id, out var reagent) ? reagent.LocalizedName : id;
    }

    private string PropertyName(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;
        return _proto.TryIndex<ReagentPropertyPrototype>(id, out var property) ? property.LocalizedName : id;
    }

    /// <summary>ARCH-03: every contract the faction picked, in the server's contract-paper format.</summary>
    private void RefreshContracts(ResearchDataTerminalBuiState state, StyleBoxFlat panel)
    {
        if (_window is null)
            return;
        _window.ContractsList.RemoveAllChildren();
        if (state.Contracts.Count == 0)
        {
            var empty = new RichTextLabel { Margin = new Thickness(5f) };
            empty.Text = Loc.GetString("research-data-ui-contracts-empty");
            _window.ContractsList.AddChild(empty);
            return;
        }
        foreach (var contract in state.Contracts)
        {
            _window.ContractsList.AddChild(MakeEntry(
                Loc.GetString("research-data-ui-contract-name", ("NAME", contract.Name)),
                contract.Text,
                panel));
        }
    }

    /// <summary>ARCH-03: every discovered property with its description and exact effect text.</summary>
    private void RefreshProperties(StyleBoxFlat panel)
    {
        if (_window is null)
            return;
        _window.PropertiesList.RemoveAllChildren();
        var generator = EntMan.System<ClientReagentGeneratorSystem>();
        var known = new List<ReagentPropertyPrototype>();
        foreach (var id in generator.KnownProperties)
        {
            if (_proto.TryIndex<ReagentPropertyPrototype>(id, out var property))
                known.Add(property);
        }
        if (known.Count == 0)
        {
            var empty = new RichTextLabel { Margin = new Thickness(5f) };
            empty.Text = Loc.GetString("research-data-ui-properties-empty");
            _window.PropertiesList.AddChild(empty);
            return;
        }
        foreach (var property in known.OrderBy(p => p.LocalizedName))
        {
            _window.PropertiesList.AddChild(MakeEntry(
                Loc.GetString("research-data-ui-property-name", ("NAME", property.LocalizedName)),
                DescribeProperty(property),
                panel));
        }
    }

    private static Control MakeEntry(string title, string body, StyleBoxFlat panel)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var titleLabel = new RichTextLabel { Margin = new Thickness(5f, 5f, 5f, 0f) };
        titleLabel.Text = title;
        var bodyLabel = new RichTextLabel { Margin = new Thickness(5f) };
        bodyLabel.Text = body;
        box.AddChild(titleLabel);
        box.AddChild(bodyLabel);
        var container = new PanelContainer
        {
            PanelOverride = panel,
            HorizontalExpand = true,
            Margin = new Thickness(0f, 0f, 0f, 5f),
        };
        container.AddChild(box);
        return container;
    }

    private string DescribeProperty(ReagentPropertyPrototype property)
    {
        var text = Loc.GetString("research-data-ui-property-meta",
            ("CODE", property.Code), ("RARITY", property.Rarity.ToString())) + '\n';
        if (property.MaxLevel < UncappedPropertyLevel)
            text += Loc.GetString("research-data-ui-property-max-level", ("LEVEL", property.MaxLevel)) + '\n';
        text += property.LocalizedDescription + '\n';
        if (property.IntensityPerLevel != 0 || property.DurationPerLevel != 0 || property.RangePerLevel != 0)
        {
            text += Loc.GetString("research-data-ui-property-fire",
                ("INTENSITY", property.IntensityPerLevel.ToString()),
                ("DURATION", property.DurationPerLevel.ToString()),
                ("RADIUS", property.RangePerLevel.ToString())) + '\n';
        }
        var level = Math.Clamp(property.Level, 1, Math.Max(1, property.MaxLevel));
        text += Loc.GetString("research-data-ui-property-effect", ("LEVEL", level)) + '\n';
        text += GetEffectText(property, level) ?? Loc.GetString("research-data-ui-property-no-effect");
        return text;
    }

    /// <summary>
    /// The guidebook text of the property's effect at <paramref name="level"/>. Generated chemicals emit
    /// <c>!type:{EffectName}</c> with <c>potency: level</c>, so the same effect class is built here with that potency.
    /// </summary>
    private string? GetEffectText(ReagentPropertyPrototype property, int level)
    {
        var key = $"{property.ID}:{level}";
        if (_effectTextCache.TryGetValue(key, out var cached))
            return cached;

        string? text = null;
        if (!string.IsNullOrEmpty(property.EffectName))
        {
            if (_effectTypes is null)
            {
                _effectTypes = new Dictionary<string, Type>();
                foreach (var type in _reflection.GetAllChildren<RMCChemicalEffect>())
                {
                    _effectTypes.TryAdd(type.Name, type);
                }
            }
            if (_effectTypes.TryGetValue(property.EffectName, out var effectType))
            {
                try
                {
                    var effect = (RMCChemicalEffect) _typeFactory.CreateInstance(effectType, oneOff: true, inject: false);
                    effect.Potency = level;
                    text = effect.EntityEffectGuidebookText(_proto, _entSys);
                    if (string.IsNullOrWhiteSpace(text))
                        text = null;
                }
                catch (Exception)
                {
                    text = null;
                }
            }
        }
        _effectTextCache[key] = text;
        return text;
    }
}
