using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.Abominations;

public sealed class AbominationConstructionWindow : DefaultWindow
{
    public event Action<string>? OnStructurePicked;

    private readonly BoxContainer _list;
    private readonly IPrototypeManager _proto;
    private readonly SpriteSystem _sprite;

    public AbominationConstructionWindow()
    {
        Title = Loc.GetString("abomination-construction-picker-title");
        SetSize = MinSize = new System.Numerics.Vector2(260, 320);

        _proto = IoCManager.Resolve<IPrototypeManager>();
        _sprite = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();

        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        var scroll = new ScrollContainer
        {
            HScrollEnabled = false,
            VScrollEnabled = true,
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        scroll.AddChild(_list);
        Contents.AddChild(scroll);
    }

    public void Populate(IReadOnlyList<string> options, string? selected)
    {
        _list.RemoveAllChildren();

        foreach (var id in options)
        {
            var displayName = id;
            TextureRect? icon = null;
            if (_proto.TryIndex<EntityPrototype>(id, out var proto))
            {
                displayName = proto.Name;
                icon = new TextureRect
                {
                    Texture = _sprite.Frame0(proto),
                    Stretch = TextureRect.StretchMode.KeepAspectCentered,
                    VerticalAlignment = VAlignment.Center,
                    SetSize = new System.Numerics.Vector2(32, 32),
                };
            }

            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
            };

            if (icon != null)
                row.AddChild(icon);

            var button = new Button
            {
                Text = displayName,
                ToggleMode = true,
                Pressed = selected == id,
                HorizontalExpand = true,
            };
            var captured = id;
            button.OnPressed += _ => OnStructurePicked?.Invoke(captured);
            row.AddChild(button);
            _list.AddChild(row);
        }
    }
}
