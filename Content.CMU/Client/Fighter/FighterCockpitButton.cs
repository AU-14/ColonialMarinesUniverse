using System.Numerics;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Fighter;

/// <summary>A small, backlit console key without the normal window-button chrome.</summary>
public sealed class FighterCockpitButton : BaseButton
{
    private readonly Font _font = new VectorFont(IoCManager.Resolve<IResourceCache>()
        .GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 12);

    public string Text { get; set; } = string.Empty;
    public bool Lit { get; set; }
    public bool Segmented { get; set; }
    public bool SegmentDivider { get; set; }
    public Color? StatusColor { get; set; }
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private TimeSpan? _pressedAt;

    public FighterCockpitButton()
    {
        OnPressed += _ =>
        {
            _pressedAt = _timing.CurTime;
            IoCManager.Resolve<IEntityManager>().System<AudioSystem>().PlayGlobal(
                new SoundPathSpecifier("/Audio/CMU14/Blackfoot/buttonpress.ogg"), Filter.Local(), false,
                AudioParams.Default.WithVolume(-14));
        };
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var active = Lit || DrawMode is DrawModeEnum.Pressed or DrawModeEnum.Hover;
        var color = Disabled ? Color.FromHex("#626B61") : active ? Color.FromHex("#D3EBA2") : Color.FromHex("#AAB798");
        handle.DrawRect(PixelSizeBox, Color.FromHex(active ? "#28382D" : "#111916"));
        var flash = _pressedAt is { } pressed ? Math.Clamp(1 - (float) (_timing.CurTime - pressed).TotalSeconds / .2f, 0, 1) : 0;
        if (flash > 0) handle.DrawRect(PixelSizeBox, Color.FromHex("#BCD997").WithAlpha(flash * .25f));
        if (!Segmented)
            handle.DrawRect(PixelSizeBox, Color.FromHex(active ? "#829366" : "#454E42"), false);
        else
        {
            if (SegmentDivider)
                handle.DrawLine(new Vector2(0, 5), new Vector2(0, PixelSize.Y - 5), Color.FromHex("#454E42"));
            if (Lit)
                handle.DrawRect(new UIBox2(3, PixelSize.Y - 3, PixelSize.X - 3, PixelSize.Y - 1), color);
        }
        if (StatusColor is { } indicator) handle.DrawRect(new UIBox2(1, 1, 4, PixelSize.Y - 1), indicator);
        if (!Segmented)
            handle.DrawLine(new Vector2(2, PixelSize.Y - 2), new Vector2(PixelSize.X - 2, PixelSize.Y - 2), Color.Black);
        var scale = Math.Clamp(PixelSize.Y / 32f, .8f, 2.5f);
        var textSize = handle.GetDimensions(_font, Text, scale);
        scale = Math.Min(scale, (PixelSize.X - 8) / Math.Max(1, textSize.X) * scale);
        textSize = handle.GetDimensions(_font, Text, scale);
        handle.DrawString(_font, ((Vector2) PixelSize - textSize) / 2, Text, scale, color);
    }
}
