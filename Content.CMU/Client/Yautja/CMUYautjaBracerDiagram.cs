using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Yautja;

/// <summary>The opened item's live sprite, with diagnostic contacts on its lid and control panel.</summary>
public sealed class CMUYautjaBracerDiagram : Control
{
    private static readonly Vector2[] Contacts = [new(8, 13), new(23, 13), new(16, 18), new(7, 25), new(24, 25)];
    private readonly SpriteView _sprite = new()
    {
        Stretch = SpriteView.StretchMode.Fill, OverrideDirection = Direction.South,
        MouseFilter = MouseFilterMode.Ignore,
    };
    private readonly Contact[] _contacts = new Contact[CMUYautjaExperimentComponent.RequiredCount];
    public event Action<int>? OnSealPressed;

    public CMUYautjaBracerDiagram()
    {
        MinSize = new Vector2(320, 240);
        AddChild(_sprite);
        for (var i = 0; i < _contacts.Length; i++)
        {
            var index = i;
            var button = new Contact { Name = $"Contact{i + 1}",
                ToolTip = Loc.GetString("cmu-yautja-experiment-seal-title", ("number", i + 1)) };
            button.OnPressed += _ => OnSealPressed?.Invoke(index);
            _contacts[i] = button;
            AddChild(button);
        }
    }

    public void SetEntity(EntityUid owner) => _sprite.SetEntity(owner);

    public void SetSeals(List<CMUYautjaExperimentSeal> seals)
    {
        for (var i = 0; i < _contacts.Length; i++)
        {
            _contacts[i].Visible = i < seals.Count;
            if (i >= seals.Count)
                continue;
            var seal = seals[i];
            _contacts[i].Released = seal.RecoveredName != null;
        }
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        // Integer sprite pixels keep the actual variant crisp at different window sizes.
        var size = MathF.Floor(MathF.Min(finalSize.X - 32, finalSize.Y) / 32) * 32;
        var origin = (finalSize - new Vector2(size)) / 2;
        _sprite.Arrange(UIBox2.FromDimensions(origin, new Vector2(size)));
        for (var i = 0; i < _contacts.Length; i++)
            _contacts[i].Arrange(UIBox2.FromDimensions(origin + Contacts[i] * (size / 32) - new Vector2(16), new Vector2(32)));
        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var color = Color.FromHex("#38403E");
        // Quiet corner marks frame the physical object without drawing through its art.
        foreach (var side in new[] { -1, 1 })
        {
            var x = side < 0 ? 12 : Size.X - 12;
            foreach (var end in new[] { -1, 1 })
            {
                var y = end < 0 ? 12 : Size.Y - 12;
                handle.DrawLine(new Vector2(x, y), new Vector2(x - side * 16, y), color);
                handle.DrawLine(new Vector2(x, y), new Vector2(x, y - end * 16), color);
            }
        }
    }

    private sealed class Contact : BaseButton
    {
        public bool Released;

        protected override void Draw(DrawingHandleScreen handle)
        {
            var center = Size / 2;
            var color = Color.FromHex(Released ? "#9DC8A8" : "#E66846");
            var active = DrawMode != DrawModeEnum.Normal;
            var radius = active ? 11 : 8;
            handle.DrawCircle(center, radius + 2, color.WithAlpha(active ? .18f : .08f));
            handle.DrawRect(UIBox2.FromDimensions(center - new Vector2(2), new Vector2(4)), color);
            foreach (var side in new[] { -1, 1 })
            {
                var x = center.X + side * radius;
                handle.DrawLine(new Vector2(x, center.Y - 5), new Vector2(x, center.Y + 5), color);
                handle.DrawLine(new Vector2(x, center.Y - 5), new Vector2(x - side * 3, center.Y - 5), color);
                handle.DrawLine(new Vector2(x, center.Y + 5), new Vector2(x - side * 3, center.Y + 5), color);
            }
        }
    }
}
