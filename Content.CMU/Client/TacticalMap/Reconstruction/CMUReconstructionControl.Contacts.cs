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
    public CMUReconContact[] TrackedContacts = [];
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
            var point = Project(new Vector3((Vector2) (blip.Indices - scene.Origin) + new Vector2(0.5f), _selectedLevel * 3 + 0.4f));
            if (!PixelSizeBox.Contains(new Vector2i((int) point.X, (int) point.Y))) continue;
            var rect = UIBox2.FromDimensions(point - new Vector2(10 * UIScale), new Vector2(20 * UIScale));
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
        }
    }
}
