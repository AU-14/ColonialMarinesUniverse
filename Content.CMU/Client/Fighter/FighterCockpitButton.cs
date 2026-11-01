using System.Numerics;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Fighter;

/// <summary>A flat cockpit key with a persistent selection mark and optional weapon readout.</summary>
public sealed class FighterCockpitButton : BaseButton
{
    private readonly Font _font = new VectorFont(IoCManager.Resolve<IResourceCache>()
        .GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 9);

    public string Text { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public string? Detail { get; set; }
    public bool VerticalReadout { get; set; }
    public bool Lit { get; set; }
    public bool Segmented { get; set; }
    public bool SegmentDivider { get; set; }
    public Color? StatusColor { get; set; }
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private TimeSpan? _pressedAt;

    public FighterCockpitButton()
    {
        RectClipContent = true;
        OnPressed += _ =>
        {
            _pressedAt = _timing.CurTime;
            IoCManager.Resolve<IEntityManager>().System<AudioSystem>().PlayGlobal(
                new SoundPathSpecifier("/Audio/CMU14/Fighter/control-click.ogg"), Filter.Local(), false,
                AudioParams.Default.WithVolume(-14));
        };
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (PixelSize.X < 4 || PixelSize.Y < 4) return;
        var active = Lit || DrawMode is DrawModeEnum.Pressed or DrawModeEnum.Hover;
        var color = Color.FromHex(Disabled ? "#7E8794" : active ? "#F0F3F8" : "#CBD3DE");
        handle.DrawRect(PixelSizeBox, Color.FromHex(active ? "#263141" : "#11161D"));
        var flash = _pressedAt is { } pressed ? Math.Clamp(1 - (float) (_timing.CurTime - pressed).TotalSeconds / .2f, 0, 1) : 0;
        if (flash > 0) handle.DrawRect(PixelSizeBox, Color.White.WithAlpha(flash * .15f));
        if (SegmentDivider)
            handle.DrawLine(new Vector2(0, 5), new Vector2(0, PixelSize.Y - 5), Color.FromHex("#303741"));
        handle.DrawLine(new Vector2(0, PixelSize.Y - 1), new Vector2(PixelSize.X, PixelSize.Y - 1), Color.FromHex("#303741"));
        if (Lit)
            handle.DrawRect(Tag == null
                ? new UIBox2(2, PixelSize.Y - 3, PixelSize.X - 2, PixelSize.Y - 1)
                : new UIBox2(0, 1, 3, PixelSize.Y - 1), color);
        if (StatusColor is { } indicator) handle.DrawRect(new UIBox2(1, 1, 4, PixelSize.Y - 1), indicator);
        var scale = UIScale;
        var padding = 8 * scale;
        if (Tag is { } tag && Detail is { } detail)
        {
            var detailSize = handle.GetDimensions(_font, detail, scale);
            var lineHeight = _font.GetLineHeight(scale);
            if (VerticalReadout)
            {
                var tagSize = handle.GetDimensions(_font, tag, scale);
                var stackedName = FitText(handle, Text, scale, PixelSize.X - padding);
                var nameSize = handle.GetDimensions(_font, stackedName, scale);
                var centerY = (PixelSize.Y - lineHeight) / 2;
                handle.DrawString(_font, new Vector2((PixelSize.X - tagSize.X) / 2, centerY - lineHeight), tag, scale, Color.FromHex("#929DAC"));
                handle.DrawString(_font, new Vector2((PixelSize.X - nameSize.X) / 2, centerY), stackedName, scale, color);
                handle.DrawString(_font, new Vector2((PixelSize.X - detailSize.X) / 2, centerY + lineHeight), detail, scale, color);
                return;
            }
            var y = Math.Max(0, (PixelSize.Y - lineHeight * 2) / 2);
            handle.DrawString(_font, new Vector2(padding, y), tag, scale, Color.FromHex("#929DAC"));
            handle.DrawString(_font, new Vector2(PixelSize.X - padding - detailSize.X, y), detail, scale, color);
            var name = FitText(handle, Text, scale, PixelSize.X - padding * 2);
            handle.DrawString(_font, new Vector2(padding, y + lineHeight), name, scale, color);
            return;
        }
        var text = FitText(handle, Text, scale, PixelSize.X - padding * 2);
        var textSize = handle.GetDimensions(_font, text, scale);
        handle.DrawString(_font, ((Vector2) PixelSize - textSize) / 2, text, scale, color);
    }

    private string FitText(DrawingHandleScreen handle, string text, float scale, float width)
    {
        if (handle.GetDimensions(_font, text, scale).X <= width) return text;
        var length = text.Length;
        while (length > 0 && handle.GetDimensions(_font, text[..length] + "…", scale).X > width) length--;
        return length == 0 ? string.Empty : text[..length] + "…";
    }
}
