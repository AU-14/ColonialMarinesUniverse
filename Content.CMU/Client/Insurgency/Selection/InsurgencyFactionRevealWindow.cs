using System.Numerics;
using Content.Shared._AU14.Insurgency.Selection;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.Insurgency.Selection;

/// <summary>
///     The faction reveal popup. Shown to every cell member once a faction is applied: the faction
///     title, the roleplay style to play it by, the flavour description, and the flag / status-icon
///     sprites when the author set them.
/// </summary>
public sealed class InsurgencyFactionRevealWindow : DefaultWindow
{
    public InsurgencyFactionRevealWindow(InsurgencyFactionRevealEuiState state)
    {
        var prototype = IoCManager.Resolve<IPrototypeManager>();
        var sprites = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();

        Title = Loc.GetString("insfor-reveal-title");
        MinSize = new Vector2(460, 360);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(12) };

        // Header row: flag + icon sprites beside the faction title.
        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };

        if (state.FlagEntity is { } flag && prototype.HasIndex<EntityPrototype>(flag))
        {
            var view = new EntityPrototypeView { MinSize = new Vector2(64, 64) };
            view.SetPrototype(new EntProtoId(flag));
            header.AddChild(view);
        }

        if (state.StatusIcon is { } iconId && prototype.TryIndex<FactionIconPrototype>(iconId, out var iconProto))
        {
            header.AddChild(new TextureRect
            {
                Texture = sprites.Frame0(iconProto.Icon),
                MinSize = new Vector2(32, 32),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                VerticalAlignment = VAlignment.Center,
                Margin = new Thickness(6, 0),
            });
        }

        header.AddChild(new Label
        {
            Text = string.IsNullOrWhiteSpace(state.Title) ? Loc.GetString("insfor-reveal-untitled") : state.Title,
            StyleClasses = { "LabelHeadingBigger" },
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(8, 0),
        });
        root.AddChild(header);

        if (!string.IsNullOrWhiteSpace(state.Roleplay))
        {
            root.AddChild(new Label { Text = Loc.GetString("insfor-reveal-roleplay-header"), StyleClasses = { "LabelHeading" }, Margin = new Thickness(0, 10, 0, 2) });
            root.AddChild(new Label { Text = state.Roleplay, MaxWidth = 420 });
        }

        if (!string.IsNullOrWhiteSpace(state.Description))
        {
            root.AddChild(new Label { Text = Loc.GetString("insfor-reveal-about-header"), StyleClasses = { "LabelHeading" }, Margin = new Thickness(0, 10, 0, 2) });
            root.AddChild(new Label { Text = state.Description, MaxWidth = 420 });
        }

        var close = new Button { Text = Loc.GetString("insfor-reveal-close"), HorizontalAlignment = HAlignment.Center, Margin = new Thickness(0, 14, 0, 0) };
        close.OnPressed += _ => Close();
        root.AddChild(close);

        Contents.AddChild(root);
    }
}
