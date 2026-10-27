using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Lobby;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI.Roles;

public sealed partial class RequirementsSelector
{
    private bool _cmuDeployment;
    private bool _cmuBinary;
    private PanelContainer? _cmuCard;
    private BoxContainer? _cmuActions;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (_cmuDeployment && _options.GetChild(0) is BoxContainer options)
        {
            options.Orientation = availableSize.X < 260 ? LayoutOrientation.Vertical : LayoutOrientation.Horizontal;
            OptionsContainer.Orientation = _cmuActions != null && availableSize.X < 500
                ? LayoutOrientation.Vertical : LayoutOrientation.Horizontal;
        }
        return base.MeasureOverride(availableSize);
    }

    public void UseDeploymentLayout(PanelContainer? card = null, bool binary = false)
    {
        _cmuDeployment = true;
        _cmuCard = card;
        _cmuBinary = binary;
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        TitleContainer.MinWidth = 0;
        TitleLabel.MinSize = Vector2.Zero;
        TitleContainer.HorizontalExpand = true;
        TitleLabel.FontColorOverride = CmuLobbyPresentation.Text;
        TitleLabel.ClipText = true;
        foreach (var label in _lockStripe.Children.OfType<Label>())
            label.FontColorOverride = CmuLobbyPresentation.Text;
        OptionsContainer.MinWidth = 0;
        foreach (var icon in Children.OfType<TextureRect>().ToArray())
        {
            icon.Orphan();
            TitleContainer.AddChild(icon);
            icon.SetPositionFirst();
        }
        foreach (var button in _options.GetChild(0).Children.OfType<Button>())
        {
            button.MinWidth = 54;
            button.MinHeight = 36;
        }
        if (_options.GetChild(0) is BoxContainer options)
            options.SeparationOverride = 6;
        RefreshDeploymentColors();
    }

    public void AddDeploymentActions(params Button[] buttons)
    {
        _cmuActions = new BoxContainer { SeparationOverride = 6, VerticalAlignment = VAlignment.Bottom };
        OptionsContainer.SeparationOverride = 10;
        OptionsContainer.AddChild(_cmuActions);
        foreach (var button in buttons)
        {
            button.Orphan();
            button.Margin = new Thickness(0);
            button.MinHeight = 36;
            button.HorizontalAlignment = HAlignment.Stretch;
            button.VerticalAlignment = VAlignment.Stretch;
            CmuLobbyPresentation.Style(button);
            _cmuActions.AddChild(button);
        }
    }

    private void RefreshDeploymentColors()
    {
        if (!_cmuDeployment) return;
        var colors = _cmuBinary ? new[] { "#5CE09D", "#FF7F8C" } : new[] { "#B0AFBD", "#69AFFF", "#FFB94F", "#5CE09D" };
        if (_cmuCard != null)
            _cmuCard.PanelOverride = CmuLobbyPresentation.Box(Color.FromHex(colors[Math.Clamp(Selected, 0, colors.Length - 1)]), padding: 10);
        var i = 0;
        foreach (var button in _options.GetChild(0).Children.OfType<Button>())
        {
            CmuLobbyPresentation.Style(button, Color.FromHex(colors[Math.Min(i, colors.Length - 1)]), i == Selected);
            i++;
        }
    }
}
