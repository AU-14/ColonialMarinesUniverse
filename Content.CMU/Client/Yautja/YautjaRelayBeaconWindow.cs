using System.Numerics;
using Content.Client.CMU14.Yautja.ClanHall;
using Content.Shared.CMU14.Yautja;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Yautja;

// pick where the beacon takes you. destinations that can't be reached stay listed, dim, so the list doesn't jump
public sealed class YautjaRelayBeaconWindow : YautjaHudWindow
{
    private readonly BoxContainer _entries;

    public event Action<YautjaRelayDestinationKind, int, string?>? OnDestination;

    public YautjaRelayBeaconWindow()
        : base(Loc.GetString("cmu-yautja-relay-beacon-title"), new Vector2(360, 280), new Vector2(300, 200))
    {
        AddClose();

        var hint = Hud.Write(Loc.GetString("cmu-yautja-relay-beacon-header").ToUpperInvariant(), SmallFont, Hud.Muted);
        Body.AddChild(hint);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        _entries = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        scroll.AddChild(_entries);
        Body.AddChild(scroll);
    }

    public void UpdateState(YautjaRelayBeaconState state)
    {
        _entries.RemoveAllChildren();

        foreach (var entry in state.Destinations)
        {
            var text = entry.Available
                ? entry.Name
                : Loc.GetString("cmu-yautja-relay-destination-unavailable", ("destination", entry.Name));
            var button = new HudButton(text.ToUpperInvariant(), LabelFont)
            {
                HorizontalExpand = true,
                MinHeight = 30,
                Disabled = !entry.Available,
            };

            var destination = entry.Kind;
            var customIndex = entry.CustomIndex;
            var destinationId = entry.DestinationId;
            button.Activated += () => OnDestination?.Invoke(destination, customIndex, destinationId);
            _entries.AddChild(button);
        }
    }
}
