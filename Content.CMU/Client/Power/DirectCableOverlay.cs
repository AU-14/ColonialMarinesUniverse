// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using Content.Shared.CMU14.Power;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Power;

// The cables run by hand are drawn as the tube of an IV drip is: a line from one device to the other under everything, but thick, and in
// the colour of the network it carries. The line is the sprite of the cable under the floor, laid along the wire.
public sealed class DirectCableOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    // The piece of cable that lies under the floor, stretched along the wire: the straight piece of the cable of the network.
    private readonly Dictionary<string, Texture?> _textures = new();

    // How thick the wire is drawn, in tiles (the piece of the sprite is a tile high).
    private const float Thickness = 1.5f;

    // Half of the width of the wire, and the distance of its dark edge, in tiles (for the plain line drawn when there is no sprite).
    private const float Half = 0.035f;
    private const float Edge = 0.065f;

    public DirectCableOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    // The colour of the cable by what it carries: the high voltage orange, the medium one yellow, the low voltage green.
    public static Color ColorOf(string group) => group switch
    {
        "HVPower" => Color.FromHex("#f08a1e"),
        "MVPower" => Color.FromHex("#f2d21e"),
        "Apc" => Color.FromHex("#3fcf5a"),
        _ => Color.FromHex("#cfd4da"),
    };

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var transform = _entMan.System<SharedTransformSystem>();
        var handle = args.WorldHandle;

        var query = _entMan.EntityQueryEnumerator<DirectCableVisualComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            for (var i = 0; i < comp.Others.Count; i++)
            {
                if (!_entMan.TryGetEntity(comp.Others[i], out var other) || !_entMan.EntityExists(other))
                    continue;

                // Each cable is on the list of both its ends: it is drawn from the lower one.
                if (uid.Id > other.Value.Id)
                    continue;

                var from = transform.GetMapCoordinates(uid);
                var to = transform.GetMapCoordinates(other.Value);
                if (from.MapId != args.MapId || to.MapId != args.MapId)
                    continue;

                DrawCable(handle, from.Position, to.Position, comp.Groups[i]);
            }
        }
    }

    private Texture? TextureOf(string group)
    {
        if (_textures.TryGetValue(group, out var texture))
            return texture;

        var (path, state) = group switch
        {
            "HVPower" => ("/Textures/Structures/Power/Cables/hv_cable.rsi", "hvcable_12"),
            "MVPower" => ("/Textures/Structures/Power/Cables/mv_cable.rsi", "mvcable_12"),
            "Apc" => ("/Textures/Structures/Power/Cables/lv_cable.rsi", "lvcable_12"),
            _ => (string.Empty, string.Empty),
        };

        texture = null;
        if (path != string.Empty && _cache.TryGetResource<RSIResource>(new ResPath(path), out var rsi) && rsi.RSI.TryGetState(state, out var found))
            texture = found.Frame0;

        _textures[group] = texture;
        return texture;
    }

    // The wire is the sprite of the cable under the floor, laid along the line in pieces of about a tile and tinted in the colour of the network (the high voltage
    // sprite is orange itself). Without the sprite it is drawn as lines.
    private void DrawCable(DrawingHandleWorld handle, Vector2 from, Vector2 to, string group)
    {
        var direction = to - from;
        var length = direction.Length();
        if (length < 0.01f)
            return;

        var color = ColorOf(group);
        if (TextureOf(group) is not { } texture)
        {
            DrawLines(handle, from, to, color);
            return;
        }

        var tint = group == "HVPower" ? Color.White : color;
        var angle = MathF.Atan2(direction.Y, direction.X);
        var pieces = Math.Max(1, (int) MathF.Ceiling(length));
        var piece = length / pieces;
        var unit = direction / length;
        var box = Box2.CenteredAround(Vector2.Zero, new Vector2(piece, Thickness));

        for (var i = 0; i < pieces; i++)
        {
            var centre = from + unit * (piece * (i + 0.5f));
            handle.SetTransform(Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(centre));
            handle.DrawTextureRect(texture, box, tint);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private static void DrawLines(DrawingHandleWorld handle, Vector2 from, Vector2 to, Color color)
    {
        var direction = to - from;
        var normal = Vector2.Normalize(new Vector2(-direction.Y, direction.X));
        var edge = new Color(color.R * 0.35f, color.G * 0.35f, color.B * 0.35f, 1f);

        handle.DrawLine(from + normal * Edge, to + normal * Edge, edge);
        handle.DrawLine(from - normal * Edge, to - normal * Edge, edge);
        for (var offset = -Half; offset <= Half + 0.001f; offset += Half / 2f)
            handle.DrawLine(from + normal * offset, to + normal * offset, color);
    }
}
