using System.Linq;
using Content.Shared.CMU14.Guidance;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Guidance;

public sealed class CMUGuidanceBui(EntityUid owner, Enum key) : BoundUserInterface(owner, key)
{
    private DefaultWindow? _window;
    private BoxContainer _body = default!;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("cmu-guidance-title");
        _window.MinWidth = 500;
        _body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var scroll = new ScrollContainer { HScrollEnabled = false, MaxHeight = 600 };
        scroll.AddChild(_body);
        _window.Contents.AddChild(scroll);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CMUGuidanceState settings)
            return;
        _body.RemoveAllChildren();
        if (settings.ReceiverCode is { } receiver)
            AddCode("cmu-guidance-receiver-code", receiver, false);
        if (settings.DesignatorCode is { } designator)
            AddCode("cmu-guidance-designator-code", designator, true);
        if (settings.Ammo.Count > 0)
        {
            var help = new RichTextLabel { MaxWidth = 500 };
            help.SetMessage(Loc.GetString("cmu-guidance-gps-help"));
            _body.AddChild(help);
        }
        foreach (var ammo in settings.Ammo)
        {
            _body.AddChild(new Label { Text = ammo.Name, Margin = new Thickness(0, 8, 0, 0) });
            var mode = new OptionButton();
            mode.AddItem(Loc.GetString("cmu-guidance-laser"), (int) CMUGuidanceMode.Laser);
            mode.AddItem(Loc.GetString("cmu-guidance-gps"), (int) CMUGuidanceMode.Gps);
            mode.SetItemDisabled(0, ammo.GpsOnly);
            mode.SelectId((int) ammo.Mode);
            mode.OnItemSelected += args => mode.SelectId(args.Id);
            _body.AddChild(mode);
            var reports = new OptionButton { HorizontalExpand = true, Disabled = settings.Reports.Count == 0 };
            foreach (var report in settings.Reports)
                reports.AddItem(Loc.GetString("cmu-guidance-ground-report",
                    ("observer", report.Observer), ("map", report.Map), ("x", report.X), ("y", report.Y)));
            if (settings.Reports.Count > 0)
                reports.Select(0);
            else
                reports.AddItem(Loc.GetString("cmu-guidance-no-ground-reports"));
            reports.OnItemSelected += args => reports.SelectId(args.Id);
            _body.AddChild(reports);
            if (ammo.Map != null)
                _body.AddChild(new Label
                {
                    Text = Loc.GetString("cmu-guidance-gps-examine", ("x", ammo.X), ("y", ammo.Y)),
                });
            var row = new BoxContainer();
            var apply = new Button { Text = Loc.GetString("cmu-guidance-program") };
            apply.OnPressed += _ =>
            {
                var report = settings.Reports.ElementAtOrDefault(reports.SelectedId);
                SendMessage(new CMUProgramGpsMessage(ammo.Id, (CMUGuidanceMode) mode.SelectedId, report?.Id));
            };
            row.AddChild(apply);
            _body.AddChild(row);
        }
    }

    private void AddCode(string label, string code, bool designator)
    {
        var row = new BoxContainer();
        row.AddChild(new Label { Text = Loc.GetString(label), MinWidth = 190 });
        var edit = new LineEdit { Text = code, HorizontalExpand = true };
        row.AddChild(edit);
        var apply = new Button { Text = Loc.GetString("cmu-guidance-program") };
        apply.OnPressed += _ => SendMessage(new CMUSetLaserCodeMessage(edit.Text, designator));
        row.AddChild(apply);
        _body.AddChild(row);
    }
}
