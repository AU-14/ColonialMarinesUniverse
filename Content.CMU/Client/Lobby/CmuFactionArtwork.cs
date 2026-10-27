using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Lobby;

/// <summary>Faction scenes composed from the game's original directional RSI frames.</summary>
public sealed partial class CmuFactionArtwork : Control
{
    [Dependency] private IEntityManager _entities = default!;
    private readonly Dictionary<(string, int, int), Texture> _frames = new();
    public string Faction { get; set; } = "colonists";

    public CmuFactionArtwork()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
        MinHeight = 210;
        HorizontalExpand = true;
    }

    private Texture Frame(string id, int direction, int phase)
    {
        var key = (id, direction, phase);
        if (_frames.TryGetValue(key, out var texture))
            return texture;
        var source = Assets[id];
        var state = _entities.System<SpriteSystem>().RsiStateLike(new SpriteSpecifier.Rsi(new ResPath(source.Path), source.State));
        var dir = state.RsiDirections == RsiDirectionType.Dir1 ? RsiDirection.South : (RsiDirection) direction;
        texture = state.GetFrame(dir, state.IsAnimated ? Math.Min(phase, state.AnimationFrameCount - 1) : 0);
        _frames.Add(key, texture);
        return texture;
    }

    private void Sprite(DrawingHandleScreen h, string id, float x, float y, int dir = 0,
        float scale = 1, string? tint = null, float alpha = 1, float angle = 0, int phase = 0, float anchor = 0.5f)
    {
        var texture = Frame(id, dir, phase);
        var size = texture.Size * scale;
        var previous = h.GetTransform();
        var pivot = new Vector2(size.X / 2, size.Y * anchor);
        h.SetTransform(Matrix3x2.CreateTranslation(-pivot) * Matrix3x2.CreateRotation(angle * MathF.PI / 180) *
            Matrix3x2.CreateTranslation(new Vector2(x, y) + pivot) * previous);
        h.DrawTextureRect(texture, UIBox2.FromDimensions(Vector2.Zero, size),
            (tint == null ? Color.White : Color.FromHex(tint)).WithAlpha(alpha));
        h.SetTransform(previous);
    }

    private void Actor(DrawingHandleScreen h, float x, float y, string uniform, string hair, float scale,
        string? hat = null, string? armor = null, string? weapon = null, string? pack = null,
        string skin = "#D6B497", int dir = 0)
    {
        foreach (var part in new[] { "torso_m", "head_m", "r_arm", "l_arm", "r_leg", "l_leg" })
            Sprite(h, part, x, y, dir, scale, skin);
        Sprite(h, "eyes", x, y, dir, scale, "#668061");
        Sprite(h, "underwear", x, y, dir, scale);
        Sprite(h, "undershirt", x, y, dir, scale);
        Sprite(h, "uniform:" + uniform, x, y, dir, scale);
        foreach (var part in new[] { "l_foot", "r_foot", "l_hand", "r_hand" })
            Sprite(h, part, x, y, dir, scale, skin);
        Sprite(h, "boots", x, y, dir, scale);
        if (armor != null) Sprite(h, armor, x, y, dir, scale);
        if (pack != null) Sprite(h, pack, x, y, dir, scale);
        Sprite(h, "hair:" + hair, x, y, dir, scale, "#4B3425");
        if (hat != null) Sprite(h, hat, x, y, dir, scale);
        if (weapon != null)
        {
            var texture = Frame(weapon, dir, 0);
            Sprite(h, weapon, x + (32 - texture.Width) * scale / 2, y + (32 - texture.Height) * scale / 2, dir, scale);
        }
    }

    private void Tile(DrawingHandleScreen h, string id, int yStart, int yEnd, string tint, float alpha)
    {
        for (var y = yStart; y < yEnd; y += 32)
        for (var x = 0; x < 320; x += 32)
            Sprite(h, id, x, y, tint: tint, alpha: alpha);
    }

    protected override void Draw(DrawingHandleScreen h)
    {
        var previous = h.GetTransform();
        var unit = Math.Min(Size.X / 320, Size.Y / 180);
        h.SetTransform(Matrix3x2.CreateScale(unit * UIScale) *
            Matrix3x2.CreateTranslation((Size.X - 320 * unit) / 2 * UIScale, (Size.Y - 180 * unit) / 2 * UIScale) * previous);
        h.DrawRect(new UIBox2(0, 0, 320, 180), Color.FromHex("#15151D"));
        var tint = Faction switch { "govfor" => "#87ABA5", "opfor" => "#B79E89", "other" => "#8C809F", _ => "#C9BDA8" };
        Tile(h, Faction == "colonists" ? "street-floor" : Faction == "opfor" || Faction == "other" ? "warehouse-floor" : "hangar-floor", 0, 180, tint, 0.75f);
        Tile(h, Faction == "other" ? "cave-wall" : "wall-panel", -12, 20, tint, 0.85f);
        if (Faction == "colonists")
        {
            // A working street: the cafe opens onto a delivery bay and a mining crew unloading.
            for (var x = 0; x < 160; x += 32)
            for (var y = 20; y < 84; y += 32)
                Sprite(h, "cafe-floor", x, y, tint: "#D0B594", alpha: 0.8f);
            Sprite(h, "colony-window", 36, -4); Sprite(h, "colony-window", 100, -4);
            Sprite(h, "vendor", 4, 20); Sprite(h, "coffee", 116, 25); Sprite(h, "mugs", 116, 3);
            Actor(h, 68, 24, "coffee", "Ponytail", 1);
            for (var x = 48; x < 144; x += 32) Sprite(h, "cafe-table", x, 49);
            Actor(h, 25, 66, "floral", "Afro", 1.25f, skin: "#BC9A88", dir: 2);
            Sprite(h, "truck-wheels", 222, 35, 3, 1.25f); Sprite(h, "truck", 222, 35, 3, 1.25f);
            Sprite(h, "ore", 184, 52); Sprite(h, "tool-crate", 277, 117);
            Actor(h, 203, 99, "engineer", "Buzz cut", 1.5f, "boonie", "vest-miner", pack: "pack", dir: 2);
            Actor(h, 130, 92, "medical", "Long hair", 1.5f, pack: "medpack", dir: 3);
            Actor(h, 73, 111, "frontier", "Side part", 1.75f, pack: "satchel", dir: 2);
            Actor(h, 254, 123, "colonist-rolled", "Short curls", 1.25f, skin: "#8E644C", dir: 3);
            Sprite(h, "medical-crate", 3, 143); Sprite(h, "ore", 180, 146);
        }
        else if (Faction == "govfor")
        {
            // Vehicle bay on the left; briefing and medical/comms staging clear of the APC hull.
            Sprite(h, "uscm-poster", 278, 0); Sprite(h, "flag-ua", 170, 0);
            for (var x = 8; x < 150; x += 12)
                h.DrawLine(new Vector2(x, 119), new Vector2(x + 7, 113), Color.FromHex("#BEAD6666"));
            Sprite(h, "wheels", 14, -8, 2); Sprite(h, "apc", 14, -8, 2);
            Sprite(h, "server", 275, 31); Sprite(h, "ammo", 230, 38);
            Actor(h, 187, 49, "marine", "Crew cut", 1.25f, "helmet", "armor", "rifle", "rto-pack", dir: 3);
            Actor(h, 149, 85, "command", "Side part", 1.75f, "cap", dir: 2);
            Actor(h, 232, 105, "marine", "Buzz cut", 1.5f, "helmet", "armor", "rifle", "rto-pack", skin: "#986D54", dir: 3);
            Actor(h, 43, 125, "marine-rolled", "Undercut", 1.25f, "medhelmet", "armor", pack: "medpack", dir: 2);
            Sprite(h, "medical-crate", 88, 139); Sprite(h, "ammo", 278, 146);
            Sprite(h, "barricade", -10, 157); Sprite(h, "barricade", 22, 157);
        }
        else if (Faction == "opfor")
        {
            // A barricaded loading dock: transport, weapons hand-off, and a lookout.
            Sprite(h, "flag-clf", 145, 0); Sprite(h, "wall-support", 265, 0);
            Sprite(h, "weapon-crate", 216, 31); Sprite(h, "ammo", 248, 43);
            Sprite(h, "van-wheels", 14, 22, 2); Sprite(h, "van", 14, 22, 2);
            Actor(h, 181, 47, "clf-rolled", "Bedhead", 1.25f, pack: "satchel", dir: 2);
            Sprite(h, "weapon-crate", 205, 77);
            Actor(h, 119, 91, "clf", "Mohawk", 1.75f, armor: "vest", weapon: "rifle-clf", pack: "satchel");
            Actor(h, 254, 104, "clf", "Crew cut", 1.5f, "clfhelmet", "vest", "rifle-clf", skin: "#986D54", dir: 3);
            Actor(h, 45, 125, "clf-rolled", "Bedhead", 1.25f, armor: "vest", weapon: "rifle-clf", dir: 2);
            Sprite(h, "ammo", 8, 150);
            for (var x = 191; x < 320; x += 32) Sprite(h, "barricade", x, 150);
        }
        else
        {
            // Converging movement, a blade/claw clash, and a foreground flanker, all using game frames.
            Tile(h, "hive-growth", 20, 180, "#ADA5C3", 0.8f);
            Sprite(h, "mycelium-wall", 240, 14, alpha: 0.6f); Sprite(h, "egg", 272, 52);
            Sprite(h, "rock", 4, 16, alpha: 0.7f); Sprite(h, "egg", 58, 22, alpha: 0.6f);
            Sprite(h, "neomorph", 16, 45, 2, 1.25f, angle: 15);
            Sprite(h, "bull", 63, 89, 2, 1.25f, angle: 8);
            foreach (var id in new[] { "hunter-body", "hunter-mesh", "hunter-greaves", "hunter-battle-armor", "hunter-bracers", "hunter-hair", "hunter-mask", "hunter-glaive" })
                Sprite(h, id, 123, 61, 2, 2.5f, angle: 13, anchor: 0.93f);
            Sprite(h, "warlock", 177, 54, 3, 1.6f, angle: -16, phase: 1);
            Sprite(h, "hunter-glaive", 123, 61, 2, 2.5f, angle: 13, anchor: 0.93f);
            Sprite(h, "battle-claws", 174, 78, scale: 1.3f, tint: "#DFE2CB", alpha: 0.8f, angle: -24, phase: 1);
            Sprite(h, "battle-impact", 181, 82, tint: "#DCF4B3");
            Sprite(h, "sprinter", 233, 112, 3, 1.15f, angle: 15);
            Sprite(h, "battle-claws", 122, 121, tint: "#CBB79D", alpha: 0.65f, angle: 55, phase: 2);
            Sprite(h, "battle-impact", 110, 146, tint: "#A8B783", alpha: 0.6f);
        }
        // Colour belongs to the environment; foreground uniforms and equipment keep their real colours.
        var light = CmuLobbyPresentation.ThemedAccent(CmuLobbyPresentation.FactionColor(Faction));
        for (var y = 0; y < 36; y++)
            h.DrawRect(new UIBox2(0, y, 320, y + 1), light.WithAlpha((36 - y) / 360f));
        for (var y = 156; y < 180; y++)
            h.DrawRect(new UIBox2(0, y, 320, y + 1), Color.Black.WithAlpha((y - 156) / 45f));
        h.SetTransform(previous);
    }
}
