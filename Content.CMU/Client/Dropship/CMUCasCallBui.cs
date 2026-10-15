using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Dropship.Cas;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Dropship;

/// <summary>The RTO's CAS uplink: call a mark, nearest hostile or offset position, then transmit the 9-line.</summary>
public sealed class CMUCasCallBui : BoundUserInterface
{
    private static readonly Color Amber = Color.FromHex("#FFB000");
    private static readonly Color Dim = Color.FromHex("#8A8A8A");

    private DefaultWindow? _window;
    private BoxContainer _targets = default!;
    private BoxContainer _preview = default!;
    private BoxContainer _requests = default!;
    private Label _header = default!;
    private OptionButton _munition = default!;
    private OptionButton _effect = default!;
    private LineEdit _remarks = default!;
    private Button _send = default!;
    private SpinBox _offsetX = default!;
    private SpinBox _offsetY = default!;

    private CMUCasCallBuiState? _state;
    // Null target = own position or nearest hostile. _selected false = nothing picked.
    private NetEntity? _target;
    private bool _selected;
    private bool _ownPosition;

    public CMUCasCallBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("cmu-cas-call-title");
        _window.MinSize = new Vector2(720, 520);
        _window.SetSize = new Vector2(760, 560);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true };
        _header = new Label { FontColorOverride = Amber };
        root.AddChild(_header);

        var columns = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, VerticalExpand = true };
        var left = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 260, VerticalExpand = true };
        left.AddChild(new Label { Text = Loc.GetString("cmu-cas-call-targets"), FontColorOverride = Amber });
        var targetScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _targets = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        targetScroll.AddChild(_targets);
        left.AddChild(targetScroll);
        columns.AddChild(left);

        var right = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true, Margin = new Thickness(8, 0, 0, 0) };
        right.AddChild(new Label { Text = Loc.GetString("cmu-cas-call-preview"), FontColorOverride = Amber });
        _preview = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        right.AddChild(_preview);

        _offsetX = CreateOffset();
        _offsetY = CreateOffset();
        right.AddChild(Row("cmu-cas-call-offset-x", _offsetX));
        right.AddChild(Row("cmu-cas-call-offset-y", _offsetY));

        _munition = new OptionButton { HorizontalExpand = true };
        foreach (var munition in Enum.GetValues<CMUCasMunition>())
            _munition.AddItem(CMUCasRequestSystem.MunitionName(munition), (int) munition);
        _munition.OnItemSelected += args => _munition.SelectId(args.Id);
        right.AddChild(Row("cmu-cas-call-munition", _munition));

        _effect = new OptionButton { HorizontalExpand = true };
        foreach (var effect in Enum.GetValues<CMUCasEffect>())
            _effect.AddItem(CMUCasRequestSystem.EffectName(effect), (int) effect);
        _effect.OnItemSelected += args => _effect.SelectId(args.Id);
        right.AddChild(Row("cmu-cas-call-effect", _effect));

        _remarks = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-cas-call-remarks-placeholder") };
        _remarks.OnTextChanged += args =>
        {
            if (args.Text.Length > CMUCasRequestSystem.MaxRemarksLength)
                _remarks.Text = args.Text[..CMUCasRequestSystem.MaxRemarksLength];
        };
        right.AddChild(Row("cmu-cas-call-remarks", _remarks));

        _send = new Button { Text = Loc.GetString("cmu-cas-call-send"), Disabled = true, Margin = new Thickness(0, 6, 0, 0) };
        _send.OnPressed += _ =>
        {
            if (!_selected)
                return;
            SendMessage(new CMUCasCallSendMsg(_target, (CMUCasMunition) _munition.SelectedId,
                (CMUCasEffect) _effect.SelectedId, _remarks.Text, _ownPosition, _offsetX.Value, _offsetY.Value));
            _remarks.Text = string.Empty;
        };
        right.AddChild(_send);
        columns.AddChild(right);
        root.AddChild(columns);

        root.AddChild(new Label { Text = Loc.GetString("cmu-cas-call-requests"), FontColorOverride = Amber, Margin = new Thickness(0, 6, 0, 0) });
        var requestScroll = new ScrollContainer { MinHeight = 140, HScrollEnabled = false };
        _requests = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        requestScroll.AddChild(_requests);
        root.AddChild(requestScroll);

        _window.Contents.AddChild(root);
    }

    private static BoxContainer Row(string label, Control control)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, Margin = new Thickness(0, 4, 0, 0) };
        row.AddChild(new Label { Text = Loc.GetString(label), MinWidth = 120 });
        row.AddChild(control);
        return row;
    }

    private static SpinBox CreateOffset()
    {
        var offset = new SpinBox
        {
            HorizontalExpand = true,
            IsValid = value => value >= -CMUCasRequestSystem.PositionOffsetLimit &&
                value <= CMUCasRequestSystem.PositionOffsetLimit,
        };
        offset.OverrideValue(0);
        offset.InitDefaultButtons();
        return offset;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CMUCasCallBuiState call)
            return;
        _state = call;
        _header.Text = Loc.GetString("cmu-cas-call-header", ("callsign", call.Callsign), ("range", call.Range));

        // Drop a selection whose mark burned out.
        if (!_ownPosition && (_selected && _target is { } picked && call.Marks.All(mark => mark.Target != picked) ||
            _selected && _target == null && call.NearestHostile == null))
            _selected = false;

        RebuildTargets();
        RebuildPreview();
        RebuildRequests();
    }

    private void RebuildTargets()
    {
        if (_state is not { } state)
            return;
        _targets.RemoveAllChildren();
        AddTarget(state.OwnPosition, null, Loc.GetString("cmu-cas-call-own-position"), ownPosition: true);
        AddTarget(state.NearestHostile, null, Loc.GetString("cmu-cas-call-no-flare"));
        foreach (var mark in state.Marks)
            AddTarget(mark, mark.Target, mark.Name);
        if (state.Marks.Count == 0)
            _targets.AddChild(new Label { Text = Loc.GetString("cmu-cas-call-no-marks"), FontColorOverride = Dim });
    }

    private void AddTarget(CMUCasCallTarget? target, NetEntity? net, string name, bool ownPosition = false)
    {
        var text = target == null
            ? Loc.GetString("cmu-cas-call-target-none", ("name", name))
            : Loc.GetString("cmu-cas-call-target-entry", ("name", name), ("distance", target.Distance),
                ("danger", target.DangerClose ? Loc.GetString("cmu-cas-call-danger-tag") : ""),
                ("laser", target.Designated ? Loc.GetString("cmu-cas-call-laser-tag") : ""));
        var button = new Button
        {
            Text = text,
            ToggleMode = true,
            ClipText = true,
            HorizontalExpand = true,
            Disabled = target == null,
            Pressed = _selected && _target == net && _ownPosition == ownPosition,
        };
        if (target?.DangerClose == true)
            button.ModulateSelfOverride = Color.FromHex("#FFC8A0");
        button.OnPressed += _ =>
        {
            _selected = true;
            _target = net;
            _ownPosition = ownPosition;
            RebuildTargets();
            RebuildPreview();
        };
        _targets.AddChild(button);
    }

    private void RebuildPreview()
    {
        _preview.RemoveAllChildren();
        var target = !_selected || _state == null ? null
            : _ownPosition ? _state.OwnPosition
            : _target == null ? _state.NearestHostile
            : _state.Marks.FirstOrDefault(mark => mark.Target == _target);
        _offsetX.LineEditDisabled = _offsetY.LineEditDisabled = !_ownPosition;
        _offsetX.SetButtonDisabled(!_ownPosition);
        _offsetY.SetButtonDisabled(!_ownPosition);
        _send.Disabled = target == null;
        if (target == null)
        {
            _preview.AddChild(new Label { Text = Loc.GetString("cmu-cas-call-pick"), FontColorOverride = Dim });
            return;
        }

        void Line(string loc, string value, Color? color = null)
        {
            _preview.AddChild(new Label
            {
                Text = Loc.GetString(loc, ("value", value)),
                ClipText = true,
                HorizontalExpand = true,
                FontColorOverride = color,
                ToolTip = value,
            });
        }

        Line("cmu-cas-call-line-mark", _target == null ? Loc.GetString("cmu-cas-mark-radio") :
            target.Designated ? Loc.GetString("cmu-cas-mark-laser") : Loc.GetString("cmu-cas-mark-flare"));
        Line("cmu-cas-call-line-target", target.Name);
        Line("cmu-cas-call-line-location", target.Location);
        Line("cmu-cas-call-line-description", target.Description);
        Line("cmu-cas-call-line-friendlies", target.Friendlies);
        Line("cmu-cas-call-line-distance", target.Distance.ToString());
        if (_ownPosition)
            Line("cmu-cas-call-line-danger", Loc.GetString("cmu-cas-call-position-deviation",
                ("deviation", CMUCasRequestSystem.PositionDeviation)), Color.OrangeRed);
        if (target.DangerClose)
            Line("cmu-cas-call-line-danger", Loc.GetString("cmu-cas-request-card-danger"), Color.OrangeRed);
        if (target.Designated)
            Line("cmu-cas-call-line-laser", Loc.GetString("cmu-cas-call-laser-bonus"), Color.LightGreen);
        _send.ModulateSelfOverride = target.DangerClose ? Color.FromHex("#FF9070") : null;
    }

    private void RebuildRequests()
    {
        if (_state is not { } state)
            return;
        _requests.RemoveAllChildren();
        if (state.Requests.Count == 0)
        {
            _requests.AddChild(new Label { Text = Loc.GetString("cmu-cas-requests-none"), FontColorOverride = Dim });
            return;
        }

        foreach (var request in state.Requests)
        {
            var id = request.Id;
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
            var carry = request.Status is CMUCasRequestStatus.Accepted or CMUCasRequestStatus.ClearedHot
                ? Loc.GetString($"cmu-cas-carry-{request.CarryMode.ToString().ToLowerInvariant()}",
                    ("seconds", request.CarryCountdown), ("volleys", request.CarryVolleys))
                : string.Empty;
            row.AddChild(new Label
            {
                Text = Loc.GetString("cmu-cas-call-request-entry", ("id", id), ("target", request.TargetName),
                    ("munition", CMUCasRequestSystem.MunitionName(request.Munition)),
                    ("effect", CMUCasRequestSystem.EffectName(request.Effect)),
                    ("status", Loc.GetString($"cmu-cas-status-{request.Status.ToString().ToLowerInvariant()}")),
                    ("aircraft", request.Aircraft ?? "-"), ("carry", carry)),
                HorizontalExpand = true,
                ClipText = true,
                FontColorOverride = request.Status switch
                {
                    CMUCasRequestStatus.ClearedHot => Color.Red,
                    CMUCasRequestStatus.Accepted => Color.LightGreen,
                    CMUCasRequestStatus.Pending => Color.Yellow,
                    _ => Dim,
                },
            });

            var clear = new Button
            {
                Text = Loc.GetString("cmu-cas-call-clear-hot"),
                Disabled = request.Status != CMUCasRequestStatus.Accepted,
                MinWidth = 90,
            };
            clear.OnPressed += _ => SendMessage(new CMUCasCallClearHotMsg(id));
            row.AddChild(clear);

            var abort = new Button
            {
                Text = Loc.GetString("cmu-cas-call-abort"),
                Disabled = request.Status is CMUCasRequestStatus.Denied or CMUCasRequestStatus.Complete,
                MinWidth = 70,
            };
            abort.OnPressed += _ => SendMessage(new CMUCasCallAbortMsg(id));
            row.AddChild(abort);
            _requests.AddChild(row);
        }
    }
}
