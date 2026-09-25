using System.Numerics;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUReconstructionControl
{
    [Dependency] private IGameTiming _timing = default!;
    public bool ShowContacts = true;
    public bool ShowNames;
    public CMUReconContact[] TrackedContacts = [];
    public Action<NetEntity>? OnCameraRequested;
    public Action<NetEntity>? OnXenoWatchRequested;
    private NetEntity? _pressedXeno;
    private NetEntity? _pressedContact;
    private Vector2 _contactPress;
    private static readonly ResPath BlipRsi = new("/Textures/_RMC14/Interface/map_blips.rsi");
    private static readonly SpriteSpecifier.Rsi DefaultBlip = new(BlipRsi, "background");
    private static readonly SpriteSpecifier.Rsi HiveLeaderBlip = new(BlipRsi, "xenoleader");
    private static readonly SpriteSpecifier.Rsi[] MedicalBlips =
    [new(BlipRsi, "defibbable"), new(BlipRsi, "defibbable2"), new(BlipRsi, "defibbable3"), new(BlipRsi, "defibbable4"), new(BlipRsi, "undefibbable")];

    private void DrawContacts(DrawingHandleScreen handle)
    {
        if (!ShowContacts || Scene is not { } scene) return;
        var sprites = _entities.System<SpriteSystem>();
        foreach (var contact in TrackedContacts)
        {
            if (contact.Depth != scene.MinDepth + _selectedLevel) continue;
            var blip = contact.Blip;
            var point = ContactPosition(contact, scene);
            if (!PixelSizeBox.Contains(new Vector2i((int) point.X, (int) point.Y))) continue;
            var rect = UIBox2.FromDimensions(point - new Vector2(10 * UIScale), new Vector2(20 * UIScale));
            if (contact.CameraTarget != null || contact.XenoWatchTarget != null)
                handle.DrawRect(UIBox2.FromDimensions(point - new Vector2(12 * UIScale), new Vector2(24 * UIScale)),
                    Color.FromHex("#75C8BA"), false);
            if (blip.Background is { } background) handle.DrawTextureRect(sprites.GetFrame(background, _timing.CurTime), rect, blip.Color);
            else handle.DrawTextureRect(sprites.GetFrame(DefaultBlip, _timing.CurTime), rect, blip.Color);
            if (blip.Image is { } icon) handle.DrawTextureRect(sprites.GetFrame(icon, _timing.CurTime), rect);
            var status = blip.Status switch
            {
                TacticalMapBlipStatus.Defibabble => MedicalBlips[0],
                TacticalMapBlipStatus.Defibabble2 => MedicalBlips[1],
                TacticalMapBlipStatus.Defibabble3 => MedicalBlips[2],
                TacticalMapBlipStatus.Defibabble4 => MedicalBlips[3],
                TacticalMapBlipStatus.Undefibabble => MedicalBlips[4],
                _ => null,
            };
            if (status != null) handle.DrawTextureRect(sprites.GetFrame(status, _timing.CurTime), rect);
            if (blip.HiveLeader) handle.DrawTextureRect(sprites.GetFrame(HiveLeaderBlip, _timing.CurTime), rect);
            if (blip.OccupantCount > 0) handle.DrawString(_font, point + new Vector2(6, 6), blip.OccupantCount.ToString(), Color.White);
            if (blip.FireteamNumber > 0) handle.DrawString(_font, point - new Vector2(12, 12), blip.FireteamNumber.ToString(), Color.White);
            if (ShowNames && contact.Name is { Length: > 0 } name)
            {
                var label = point + new Vector2(14, -7) * UIScale;
                var size = handle.GetDimensions(_font, name, UIScale);
                if (label.X + size.X > PixelWidth)
                    label.X = Math.Max(0, point.X - 14 * UIScale - size.X);
                handle.DrawRect(UIBox2.FromDimensions(label - new Vector2(3, 2) * UIScale,
                    size + new Vector2(6, 4) * UIScale), Color.FromHex("#0B141DE8"));
                handle.DrawString(_font, label, name, UIScale, Color.White);
            }
        }
    }

    private Vector2 ContactPosition(CMUReconContact contact, CMUReconSnapshotMessage scene) =>
        Project(new Vector3((Vector2) (contact.Blip.Indices - scene.Origin) + new Vector2(0.5f),
            _selectedLevel * CMUReconGeometry.LevelHeight + 0.4f));

    public NetEntity? CameraAt(Vector2 relativePosition)
        => ContactAt(relativePosition)?.CameraTarget;

    public NetEntity? XenoAt(Vector2 relativePosition)
        => ContactAt(relativePosition)?.XenoWatchTarget;

    private CMUReconContact? ContactAt(Vector2 relativePosition)
    {
        if (!ShowContacts || Scene is not { } scene) return null;
        var pixel = relativePosition * UIScale;
        if (!PixelSizeBox.Contains(new Vector2i((int) pixel.X, (int) pixel.Y))) return null;
        // The last icon drawn is on top when contacts overlap.
        for (var i = TrackedContacts.Length - 1; i >= 0; i--)
        {
            var contact = TrackedContacts[i];
            if (contact.Depth != scene.MinDepth + _selectedLevel) continue;
            var point = ContactPosition(contact, scene);
            if (UIBox2.FromDimensions(point - new Vector2(10 * UIScale), new Vector2(20 * UIScale)).Contains(pixel))
                return contact;
        }
        return null;
    }
}
