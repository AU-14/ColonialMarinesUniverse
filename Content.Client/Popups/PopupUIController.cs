using System.Numerics;
using Content.Client.Gameplay;
using Content.Shared.Popups;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.Popups;

/// <summary>
/// Handles screens-space popups. World popups are handled via PopupOverlay.
/// </summary>
public sealed partial class PopupUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [UISystemDependency] private PopupSystem? _popup = default!;

    private Font _smallFont = default!;
    private Font _mediumFont = default!;
    private Font _largeFont = default!;

    private PopupRootControl? _popupControl;

    public override void Initialize()
    {
        base.Initialize();
        var cache = IoCManager.Resolve<IResourceCache>();

        _smallFont = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Italic.ttf"), 10);
        _mediumFont = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Italic.ttf"), 12);
        _largeFont = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-BoldItalic.ttf"), 14);
    }

    public void OnStateEntered(GameplayState state)
    {
        _popupControl = new PopupRootControl(_popup, this);

        UIManager.RootControl.AddChild(_popupControl);
    }

    public void OnStateExited(GameplayState state)
    {
        if (_popupControl == null)
            return;

        UIManager.RootControl.RemoveChild(_popupControl);
        _popupControl = null;
    }

    public void DrawPopup(PopupSystem.PopupLabel popup, DrawingHandleScreen handle, Vector2 position, float scale)
    {
        var lifetime = PopupSystem.GetPopupLifetime(popup);

        // Keep alpha at 1 until TotalTime passes half its lifetime, then gradually decrease to 0.
        var alpha = MathF.Min(1f, 1f - MathF.Max(0f, popup.TotalTime - lifetime / 2) * 2 / lifetime);

        var updatedPosition = position - new Vector2(0f, MathF.Min(8f, 12f * (popup.TotalTime * popup.TotalTime + popup.TotalTime)));
        var font = _smallFont;
        // CMU14: colour lookup shared with popups echoed to chat
        var color = GetPopupColor(popup.Type);

        switch (popup.Type)
        {
            case PopupType.Medium:
            case PopupType.MediumCaution:
                font = _mediumFont;
                break;
            case PopupType.Large:
            case PopupType.LargeCaution:
            case PopupType.MediumXeno:
                font = _largeFont;
                break;
        }

        var dimensions = popup.CMUGetDimensions(handle, font, scale); // CMU14: cache until text/font/scale changes.
        var drawPosition = updatedPosition - dimensions / 2f;
        var outline = TextOutline.Default with { Color = TextOutline.Default.Color.WithAlpha(alpha) };
        handle.DrawString(font, drawPosition, popup.Text, scale, color.WithAlpha(alpha), outline);
    }

    // CMU14 start: the text colour each popup type is drawn in, also used when popups are copied to chat.
    public static Color GetPopupColor(PopupType type)
    {
        return type switch
        {
            PopupType.SmallCaution or PopupType.MediumCaution or PopupType.LargeCaution => Color.Red,
            PopupType.Medium or PopupType.Large => Color.LightGray,
            PopupType.MediumXeno => Color.FromHex("#C400FF"),
            _ => Color.White,
        };
    }
    // CMU14 end

    /// <summary>
    /// Handles drawing all screen popups.
    /// </summary>
    private sealed partial class PopupRootControl : Control
    {
        private readonly PopupSystem? _popup;
        private readonly PopupUIController _controller;

        public PopupRootControl(PopupSystem? system, PopupUIController controller)
        {
            _popup = system;
            _controller = controller;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            if (_popup == null)
                return;

            // Different window
            var windowId = UserInterfaceManager.RootControl.Window.Id;

            foreach (var popup in _popup.CursorLabels)
            {
                if (popup.InitialPos.Window != windowId)
                    continue;

                _controller.DrawPopup(popup, handle, popup.InitialPos.Position, UIScale);
            }
        }
    }
}
