using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared.Body.Part;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._CMU14.Medical.Wounds;

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
            var empty = new Label { Text = "No wounds to bandage." };
            _window.PartList.AddChild(empty);
            return;
        }

        foreach (var entry in state.Available)
        {
            var label = $"{FormatPart(entry.Type, entry.Symmetry)} — {entry.UntreatedWounds} wound(s)";
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
        var side = symmetry switch
        {
            BodyPartSymmetry.Left => "Left ",
            BodyPartSymmetry.Right => "Right ",
            _ => string.Empty,
        };
        return side + type;
    }
}
