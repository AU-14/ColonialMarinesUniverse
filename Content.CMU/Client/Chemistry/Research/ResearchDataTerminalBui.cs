using Content.Client._RMC14.UserInterface;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.UserInterface.Controls;
using Content.Shared._AU14.Chemistry.Research;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Content.Client._AU14.Chemistry.Research;

[UsedImplicitly]
public sealed partial class ResearchDataTerminalBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IGameTiming _time = default!;
    private ResearchDataTerminalWindow? _window;

    ResearchDataTerminalAttemptUpgradeBuiMsg UpgradeAttempt = new();
    ResearchDataTerminalPrintLastBuiMsg PrintLast = new();
    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ResearchDataTerminalWindow>();
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
        _window.Credits.Text = Loc.GetString("research-data-ui-credits", ("NUM", state.Credits));
        _window.Tabs.SetTabTitle(0, Loc.GetString("research-data-ui-manage"));
        _window.Tabs.SetTabTitle(1, Loc.GetString("research-data-ui-view"));
        _window.NextUpdate = state.NextUpdate;
        _window.TimeLeftBar.MaxValue = (float)(state.NextUpdate - state.LastTime).TotalMilliseconds;
        _window.ChemContainer.RemoveAllChildren();
        if (state.Credits >= state.UpgradeCost && state.Clearance != 6)
            _window.Upgrade.Disabled = false;
        else _window.Upgrade.Disabled = true;
        _window.UpgradeText.Text = Loc.GetString("research-data-ui-improve", ("NUM", state.UpgradeCost));
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

        foreach (var chem in state.IDs)
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
            desc.Text = Loc.GetString("research-data-ui-chem-desc", ("RECIHINT", chem.RecipeHint), ("PROPHINT", chem.PropertyHint));
            Button button = new Button();
            button.OnPressed += _ => SendPredictedMessage(new ResearchDataTerminalPickChemBuiMsg(chem.ID));
            button.Margin = new(5, 5, 5, 5);
            RichTextLabel cont = new RichTextLabel();
            cont.Text = Loc.GetString("research-data-ui-chem-take");
            cont.HorizontalAlignment = Control.HAlignment.Left;
            if (_time.CurTime < state.NextUpdate && state.Picked)
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
    }
}
