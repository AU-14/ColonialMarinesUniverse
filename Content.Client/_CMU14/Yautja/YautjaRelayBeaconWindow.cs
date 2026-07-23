using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaRelayBeaconWindow : DefaultWindow
{
    private readonly BoxContainer _entries;

    public event Action<YautjaRelayDestinationKind, int, string?>? OnDestination;

    public YautjaRelayBeaconWindow()
    {
        Title = Loc.GetString("cmu-yautja-relay-beacon-title");
        SetSize = new Vector2(320, 190);
        MinSize = new Vector2(280, 160);

        var rootPanel = YautjaBracerUiStyle.Panel(YautjaBracerUiStyle.Surface, YautjaBracerUiStyle.Border, new Thickness(2));
        AddChild(rootPanel);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 7,
            Margin = new Thickness(7),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        rootPanel.AddChild(root);

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 7,
            HorizontalExpand = true,
        };
        root.AddChild(YautjaBracerUiStyle.Wrap(header, YautjaBracerUiStyle.DeepCard, YautjaBracerUiStyle.MutedBorder, new Thickness(7, 5)));

        header.AddChild(YautjaBracerUiStyle.Label(Loc.GetString("cmu-yautja-relay-beacon-header"), YautjaBracerUiStyle.HotRed, "LabelHeading"));

        var close = YautjaBracerUiStyle.CloseButton();
        close.OnPressed += _ => Close();
        header.AddChild(close);

        _entries = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 7,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(_entries);
    }

    public void UpdateState(YautjaRelayBeaconState state)
    {
        _entries.DisposeAllChildren();

        foreach (var entry in state.Destinations)
        {
            var button = new Button
            {
                Text = entry.Available
                    ? entry.Name
                    : Loc.GetString("cmu-yautja-relay-destination-unavailable", ("destination", entry.Name)),
                HorizontalExpand = true,
                MinHeight = 34,
                Disabled = !entry.Available,
                StyleBoxOverride = YautjaBracerUiStyle.Flat(YautjaBracerUiStyle.DeepCard, YautjaBracerUiStyle.MutedBorder),
            };

            var destination = entry.Kind;
            var customIndex = entry.CustomIndex;
            var destinationId = entry.DestinationId;
            button.OnPressed += _ => OnDestination?.Invoke(destination, customIndex, destinationId);
            _entries.AddChild(button);
        }
    }
}
