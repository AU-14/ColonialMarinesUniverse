using System.Numerics;
using Content.Shared.CMU14.Round.Antags.Replicant;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Round.Antags.Replicant;

public sealed class ReplicantPickerWindow : DefaultWindow
{
    public event Action<NetEntity>? OnTargetPicked;

    private readonly BoxContainer _buttonContainer;

    public ReplicantPickerWindow()
    {
        Title = Loc.GetString("replicant-picker-title");
        Resizable = false;
        CloseButton.Visible = false;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8)
        };

        root.AddChild(new Label
        {
            Text = Loc.GetString("replicant-picker-description"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var scroll = new ScrollContainer { MinHeight = 300 };
        _buttonContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical
        };
        scroll.AddChild(_buttonContainer);
        root.AddChild(scroll);

        Contents.AddChild(root);
        SetSize = new Vector2(320, 400);
    }

    public void Populate(List<ReplicantTargetInfo> targets)
    {
        _buttonContainer.RemoveAllChildren();

        foreach (var target in targets)
        {
            var captured = target.Entity;
            var btn = new Button { Text = target.Name };
            btn.OnPressed += _ => OnTargetPicked?.Invoke(captured);
            _buttonContainer.AddChild(btn);
        }
    }
}
