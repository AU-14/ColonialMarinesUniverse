using System.Numerics;
using Content.Client.CMU14.Dropship.TacticalLand;
using Content.Client.Resources;
using Content.Shared.CMU14.Dropship.AttachmentPoint;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Dropship.AttachmentPoint;

public sealed class CMUGunshipCrewHudOverlay : Overlay
{
    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly IEyeManager _eye;
    private readonly IGameTiming _timing;
    private readonly Font _font;
    private readonly Font _smallFont;
    private readonly GunshipHudStatusDisplay _statusDisplay;

    private static readonly Color HudColor = new(0.25f, 0.88f, 1f, 0.95f);
    private static readonly Color HudBackground = new(0.015f, 0.06f, 0.08f, 0.78f);
    private static readonly Color AmmoColor = new(1f, 0.72f, 0.16f, 0.95f);

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CMUGunshipCrewHudOverlay(IEntityManager entities, IPlayerManager player)
    {
        _entities = entities;
        _player = player;
        _eye = IoCManager.Resolve<IEyeManager>();
        _timing = IoCManager.Resolve<IGameTiming>();
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 18);
        _smallFont = cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 12);
        _statusDisplay = new GunshipHudStatusDisplay(_smallFont);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
        => args.ViewportControl == _eye.MainViewport && _player.LocalEntity is { } player
           && _entities.HasComponent<CMUGunshipCrewHudComponent>(player);

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player
            || !_entities.TryGetComponent(player, out CMUGunshipCrewHudComponent? hud))
            return;

        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var text = hud.Dropship == null
            ? Loc.GetString("cmu-gunship-crew-unlinked")
            : Loc.GetString("cmu-gunship-crew-current-ammo",
                ("gun", hud.GunType), ("count", hud.Ammo), ("capacity", hud.Capacity));
        var size = handle.GetDimensions(_font, text, 1);
        var position = new Vector2(bounds.Left + (bounds.Width - size.X) * 0.5f, bounds.Top + 18f);
        handle.DrawString(_font, position + Vector2.One * 2f, text, Color.Black);
        handle.DrawString(_font, position, text, hud.Dropship == null ? HudColor : AmmoColor);
        if (hud.Dropship == null)
            return;

        DrawOtherGuns(handle, bounds, hud);
        _statusDisplay.DrawIntegrityBar(handle, bounds, hud.Integrity, hud.MaxIntegrity);
        _statusDisplay.DrawWarnings(handle, bounds, hud.Malfunctions, hud.Alarms,
            hud.MasterAlarmSilenced, _timing.CurTime);
    }

    private void DrawOtherGuns(DrawingHandleScreen handle, UIBox2i bounds, CMUGunshipCrewHudComponent hud)
    {
        const float margin = 24f;
        const float padding = 10f;
        const float rowHeight = 42f;
        var width = 330f;
        foreach (var gun in hud.OtherGuns)
            width = MathF.Max(width, handle.GetDimensions(_smallFont, GunLabel(gun), 1).X + padding * 2);

        var height = 32f + Math.Max(1, hud.OtherGuns.Count) * rowHeight;
        // Keep the list above the shared hull bar even on narrower viewports.
        const float bottomMargin = 60f;
        var box = new UIBox2(bounds.Left + margin, bounds.Bottom - bottomMargin - height,
            bounds.Left + margin + width, bounds.Bottom - bottomMargin);
        handle.DrawRect(box, HudBackground);
        handle.DrawRect(box, HudColor, false);
        handle.DrawString(_smallFont, new Vector2(box.Left + padding, box.Top + padding),
            Loc.GetString("cmu-gunship-crew-other-guns"), HudColor);

        if (hud.OtherGuns.Count == 0)
        {
            handle.DrawString(_smallFont, new Vector2(box.Left + padding, box.Top + 32f),
                Loc.GetString("cmu-gunship-crew-no-other-guns"), HudColor);
            return;
        }

        for (var i = 0; i < hud.OtherGuns.Count; i++)
        {
            var gun = hud.OtherGuns[i];
            var top = box.Top + 32f + rowHeight * i;
            handle.DrawString(_smallFont, new Vector2(box.Left + padding, top), GunLabel(gun), HudColor);
            var ratio = gun.Capacity > 0 ? Math.Clamp((float) gun.Ammo / gun.Capacity, 0f, 1f) : 0f;
            var bar = new UIBox2(box.Left + padding, top + 19f, box.Right - padding - 48f, top + 27f);
            handle.DrawRect(bar, Color.Black);
            if (ratio > 0)
                handle.DrawRect(new UIBox2(bar.Left, bar.Top, bar.Left + bar.Width * ratio, bar.Bottom), AmmoColor);
            handle.DrawRect(bar, HudColor, false);
            handle.DrawString(_smallFont, new Vector2(bar.Right + 8f, top + 17f),
                Loc.GetString("cmu-gunship-crew-ammo-percent", ("percent", MathF.Round(ratio * 100f))), AmmoColor);
        }
    }

    private static string GunLabel(CMUGunshipCrewGunStatus gun)
    {
        var side = gun.Side switch
        {
            Direction.West => "cmu-gunship-crew-west",
            Direction.East => "cmu-gunship-crew-east",
            Direction.North => "cmu-gunship-crew-north",
            _ => "cmu-gunship-crew-south",
        };
        return Loc.GetString("cmu-gunship-crew-gun", ("side", Loc.GetString(side)), ("gun", gun.GunType));
    }
}
