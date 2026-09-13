using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Body.Part;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Medical.Injuries.Wounds;

[UsedImplicitly]
public sealed class BodyPartPickerBui : BoundUserInterface
{
    [ViewVariables]
    private BodyPartPickerWindow? _window;

    public BodyPartPickerBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BodyPartPickerWindow>();
        if (State is BodyPartPickerBuiState s)
            Refresh(s);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is BodyPartPickerBuiState s)
            Refresh(s);
    }

    private void Refresh(BodyPartPickerBuiState state)
    {
        if (_window is null)
            return;

        _window.PartList.DisposeAllChildren();

        if (state.Available.Count == 0)
        {
            var empty = new Label { Text = Loc.GetString("cmu-medical-body-part-picker-empty") };
            _window.PartList.AddChild(empty);
            return;
        }

        foreach (var entry in state.Available)
        {
            var label = Loc.GetString(
                "cmu-medical-body-part-picker-entry",
                ("part", FormatPart(entry.Type, entry.Symmetry)),
                ("count", entry.UntreatedWounds));
            var button = new Button
            {
                Text = label,
                HorizontalExpand = true,
                Margin = new Thickness(0, 0, 0, 4),
            };
            var captured = entry.Part;
            button.OnPressed += _ => SendMessage(new BodyPartPickerSelectMessage(captured));
            _window.PartList.AddChild(button);
        }
    }

    private static string FormatPart(BodyPartType type, BodyPartSymmetry symmetry)
    {
        var typeName = Loc.GetString(type switch
        {
            BodyPartType.Torso => "cmu-medical-body-part-type-torso",
            BodyPartType.Head => "cmu-medical-body-part-type-head",
            BodyPartType.Arm => "cmu-medical-body-part-type-arm",
            BodyPartType.Hand => "cmu-medical-body-part-type-hand",
            BodyPartType.Leg => "cmu-medical-body-part-type-leg",
            BodyPartType.Foot => "cmu-medical-body-part-type-foot",
            BodyPartType.Tail => "cmu-medical-body-part-type-tail",
            _ => "cmu-medical-body-part-type-other",
        });

        var sideKey = symmetry switch
        {
            BodyPartSymmetry.Left => "cmu-medical-body-part-side-left",
            BodyPartSymmetry.Right => "cmu-medical-body-part-side-right",
            _ => null,
        };

        if (sideKey == null)
            return typeName;

        return Loc.GetString(
            "cmu-medical-body-part-sided",
            ("side", Loc.GetString(sideKey)),
            ("type", typeName));
    }
}
