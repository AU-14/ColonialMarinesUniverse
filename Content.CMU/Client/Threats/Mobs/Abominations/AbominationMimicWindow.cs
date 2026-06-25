using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._CMU14.Threats.Mobs.Abominations;

public sealed class AbominationMimicWindow : DefaultWindow
{
    private readonly BoxContainer _list;

    public AbominationMimicWindow()
    {
        Title = Loc.GetString("abomination-mimic-picker-title");
        SetSize = MinSize = new(260, 320);

        _list = new()
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            HorizontalExpand = true
        };

        var scroll = new ScrollContainer
        {
            HScrollEnabled = false,
            VScrollEnabled = true,
            VerticalExpand = true,
            HorizontalExpand = true
        };
        scroll.AddChild(_list);
        Contents.AddChild(scroll);
    }

    public event Action<int>? OnFormSelected;

    public void Populate(IReadOnlyList<string> profileNames, int? activeIndex)
    {
        _list.RemoveAllChildren();

        for (var i = 0; i < profileNames.Count; i++)
        {
            int index = i;
            string name = profileNames[i];

            var button = new Button
            {
                Text = string.IsNullOrWhiteSpace(name) ? "<unknown>" : name,
                ToggleMode = true,
                Pressed = activeIndex == index,
                HorizontalExpand = true
            };
            button.OnPressed += _ => OnFormSelected?.Invoke(index);
            _list.AddChild(button);
        }
    }
}
