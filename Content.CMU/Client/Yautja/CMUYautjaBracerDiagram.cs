using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Yautja;

/// <summary>Contacts surrounding the real world sprite. The center passes input through to the item.</summary>
public sealed class CMUYautjaBracerDiagram : Control
{
    public static readonly Vector2 Anchor = new(160, 65);
    private static readonly Vector2[] Positions = [new(0, 0), new(230, 0), new(0, 80), new(230, 80), new(115, 130)];
    private readonly Button[] _contacts = new Button[CMUYautjaExperimentComponent.RequiredCount];
    private readonly bool[] _released = new bool[CMUYautjaExperimentComponent.RequiredCount];
    private int _selected;
    public event Action<int>? OnSealPressed;

    public CMUYautjaBracerDiagram()
    {
        MouseFilter = MouseFilterMode.Ignore;
        MinSize = new Vector2(320, 160);
        for (var i = 0; i < _contacts.Length; i++)
        {
            var index = i;
            var button = new Button { Name = $"Contact{i + 1}", ClipText = true };
            button.OnPressed += _ => OnSealPressed?.Invoke(index);
            _contacts[i] = button;
            AddChild(button);
        }
    }

    public void SetSeals(List<CMUYautjaExperimentSeal> seals, int selected)
    {
        _selected = selected;
        for (var i = 0; i < _contacts.Length; i++)
        {
            var button = _contacts[i];
            button.Visible = i < seals.Count;
            if (i >= seals.Count)
                continue;
            _released[i] = seals[i].RecoveredName != null;
            button.Text = Loc.GetString(_released[i] ? "cmu-yautja-experiment-contact-open" :
                "cmu-yautja-experiment-contact", ("number", i + 1));
            button.ToolTip = seals[i].RecoveredName;
            button.StyleBoxOverride = YautjaBracerUiStyle.Flat(Color.FromHex(i == selected ? "#38372EF2" : "#20272AF2"),
                Color.FromHex(_released[i] ? "#91BA9B" : i == selected ? "#CEAE78" : "#68726E"));
        }
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        for (var i = 0; i < _contacts.Length; i++)
            _contacts[i].Arrange(UIBox2.FromDimensions(Positions[i], new Vector2(90, 30)));
        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        for (var i = 0; i < _contacts.Length; i++)
        {
            if (!_contacts[i].Visible)
                continue;
            var end = Positions[i] + new Vector2(i == 4 ? 45 : i % 2 == 0 ? 90 : 0, i == 4 ? 0 : 15);
            var direction = Vector2.Normalize(end - Anchor);
            var color = Color.FromHex(_released[i] ? "#91BA9B" : "#CEAE78").WithAlpha(i == _selected ? .85f : .35f);
            handle.DrawLine((Anchor + direction * 26) * UIScale, end * UIScale, color);
        }
    }
}
