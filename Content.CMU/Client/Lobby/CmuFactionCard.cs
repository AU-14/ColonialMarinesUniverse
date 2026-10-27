using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Lobby;

/// <summary>A faction stays selectable while its roster occupies the stage below it.</summary>
public sealed class CmuFactionCard : Button
{
    private readonly CmuFactionArtwork _artwork;
    private readonly Label _caption;
    private readonly Label _availability;
    private readonly Color _accent;

    public CmuFactionCard(string faction)
    {
        HorizontalExpand = true;
        _accent = CmuLobbyPresentation.FactionColor(faction);
        ToolTip = Loc.GetString($"cmu-lobby-join-{faction}-desc");
        Label.Orphan();
        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            MouseFilter = MouseFilterMode.Ignore,
        };
        content.AddChild(new CmuAccentLabel(_accent)
        {
            Text = Loc.GetString($"cmu-workspace-faction-{faction}"),
            ClipText = true,
            Margin = new Thickness(4, 2),
        });
        _artwork = new CmuFactionArtwork
        {
            Faction = faction,
            MinHeight = 180,
            MaxHeight = 180,
            MouseFilter = MouseFilterMode.Ignore,
        };
        content.AddChild(_artwork);
        _caption = new Label
        {
            Text = Loc.GetString($"cmu-workspace-faction-{faction}-caption"),
            FontColorOverride = CmuLobbyPresentation.Text,
            ClipText = true,
            Margin = new Thickness(4, 0),
        };
        content.AddChild(_caption);
        _availability = new CmuAccentLabel(_accent) { ClipText = true, Margin = new Thickness(4, 0, 4, 4) };
        content.AddChild(_availability);
        AddChild(content);
        SetExpanded(false, false);
    }

    public void SetAvailability(int roles) => _availability.Text = Loc.GetString("cmu-workspace-open-roles", ("count", roles));

    public void SetExpanded(bool rosterOpen, bool selected)
    {
        MaxWidth = rosterOpen ? float.PositiveInfinity : 420;
        _artwork.Visible = _caption.Visible = _availability.Visible = !rosterOpen;
        CmuLobbyPresentation.Style(this, _accent, selected);
    }
}
