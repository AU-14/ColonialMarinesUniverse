using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.CMU14.Lobby;

public sealed partial class LobbyPartyShowControl
{
    [Dependency] private IResourceCache _resources = default!;
    private const string RifleArt = "/Textures/CMU14/Weapons/Guns/USCM/m41mk2.rsi";
    private const string CoverArt = "/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi";
    private const string AmmoArt = "/Textures/_RMC14/Structures/Storage/Crates/ammo.rsi";
    private float[] _crewHitAt = Array.Empty<float>();
    private Texture? _ground;

    private void InitializeBattlefield()
    {
        _ground = _resources.GetResource<TextureResource>("/Textures/_RMC14/Tiles/planet/dirt.png").Texture;
        _crewHitAt = new float[_actors.Count];
        Array.Fill(_crewHitAt, -100);
        var group = 1;
        foreach (var target in _targets)
        {
            if (target.Kind == TargetKind.Human && _actors.Count > 0)
                target.ActorIndex = Math.Min(_actors.Count - 1, _actors.Count * group++ / 3);
        }
    }

    private Vector2 BattlePosition(int index) =>
        LobbyPartyChoreography.BattlePosition(index, Math.Max(1, _actors.Count), Size, _seed);

    private void ReactToGroundImpact(Vector2 at, float time, float radius)
    {
        for (var i = 0; i < _crewHitAt.Length; i++)
        {
            if (Vector2.DistanceSquared(BattlePosition(i), at) < radius * radius * Unit * Unit)
                _crewHitAt[i] = time;
        }
    }

    private static float GrenadeTime(int salvo) => 4.8f + salvo * 5.6f;
    private Vector2 GrenadeOrigin(int salvo) => BattlePosition(_actors.Count == 0 ? 0 : salvo * 7 % _actors.Count);
    private Vector2 GrenadeTarget(int salvo) => GrenadeOrigin(salvo) +
        new Vector2(salvo % 2 == 0 ? 100 : -100, 25) * Unit;
    private static float ShellTime(int salvo) => 3.2f + salvo * 5.1f;
    private Vector2 ShellTarget(int salvo) => BattlePosition(_actors.Count == 0 ? 0 : (salvo * 11 + 5) % _actors.Count);

    private Vector2 TankPosition()
    {
        foreach (var target in _targets)
        {
            if (target.Kind == TargetKind.Tank)
                return target.Position * Size;
        }
        return new Vector2(Size.X * 0.8f, Size.Y * 0.7f);
    }

    private Vector2 GroundTarget(int index)
    {
        var xeno = AdvancingXeno(index % 7);
        if (index % 3 == 0 && xeno.Cycle < 5.5f)
            return xeno.Position;
        var partner = index % 2 == 0 ? index + 1 : index - 1;
        return partner < _actors.Count && _actors[partner].Card.Parent != null
            ? HumanTargetPose(partner).Position : TankPosition();
    }

    private void AdvanceBattlefield(float previous)
    {
        // Mix a bounded distant firefight instead of starting an audio stream for every cosmetic bullet.
        for (var volley = 0; volley < 22; volley++)
        {
            if (Crossed(previous, 1.5f + volley * 1.15f))
                Sound("/Audio/CMU14/Weapons/m41/gun_m41a_" + (volley % 5 + 1) + ".ogg", -25 - volley % 3);
        }
        for (var salvo = 0; salvo < 4; salvo++)
        {
            var grenadeHit = GrenadeTime(salvo) + 1.1f;
            if (Crossed(previous, grenadeHit))
            {
                Sound("rocket-impact.ogg", -22);
                ReactToGroundImpact(GrenadeTarget(salvo), grenadeHit, 100);
            }
            if (Crossed(previous, ShellTime(salvo)))
                Sound("rocket-release.ogg", -24);
            if (Crossed(previous, ShellTime(salvo) + 0.65f))
            {
                Sound("missile-impact.ogg", -25);
                ReactToGroundImpact(ShellTarget(salvo), ShellTime(salvo) + 0.65f, 105);
            }
        }
    }

    private void DrawBattlefield(DrawingHandleScreen handle)
    {
        var top = Size.Y * 0.20f;
        var bottom = Size.Y * 0.94f;
        handle.DrawRect(new UIBox2(0, top, Size.X, bottom), Color.FromHex("#161C18"));
        if (_ground != null)
        {
            var tile = Math.Max(72 * Unit, Size.X / 40);
            for (var y = top; y < bottom; y += tile)
            for (var x = 0f; x < Size.X; x += tile)
            {
                handle.DrawTextureRect(_ground, new UIBox2(x, y, Math.Min(Size.X, x + tile), Math.Min(bottom, y + tile)),
                    Color.FromHex("#8B9387"));
            }
        }
        handle.DrawLine(new Vector2(0, top), new Vector2(Size.X, top), Color.FromHex("#9B9975"));
        handle.DrawLine(new Vector2(0, bottom), new Vector2(Size.X, bottom), Color.FromHex("#5F6753"));
        // Trenches and shell-scarred lanes break up the terrain without adding buildings.
        for (var lane = 0; lane < 3; lane++)
        {
            var y = Size.Y * (0.34f + lane * 0.21f);
            for (var mark = 0; mark < 24; mark++)
            {
                var x = mark / 23f * Size.X;
                var at = new Vector2(x, y + MathF.Sin(mark * 0.7f + lane) * 18 * Unit);
                Ellipse(handle, at, new Vector2(Size.X / 35, 11 * Unit), Color.FromHex("#292B22").WithAlpha(0.32f));
            }
        }
        var scale = LobbyPartyChoreography.FlybyActorScale(_actors.Count, Size);
        for (var i = 0; i < _actors.Count; i++)
        {
            if (i % Math.Max(2, _actors.Count / 20) != 0)
                continue;
            var at = BattlePosition(i);
            var side = i % 4 == 0 ? 1 : -1;
            for (var bag = -1; bag <= 1; bag++)
                Sprite(handle, CoverArt, "sandbag", at + new Vector2(side * 25, bag * 17 + 14) * scale,
                    35 * scale, direction: RsiDirection.East);
            if (i % 4 == 0)
                Sprite(handle, AmmoArt, "icon", at + new Vector2(-25, 24) * scale, 30 * scale);
        }
        if (_reduced)
            return;
        for (var salvo = 0; salvo < 4; salvo++)
        {
            if (_elapsed >= GrenadeTime(salvo) + 1.1f)
                DrawCrater(handle, GrenadeTarget(salvo), 26 * Unit);
            if (_elapsed >= ShellTime(salvo) + 0.65f)
                DrawCrater(handle, ShellTarget(salvo), 34 * Unit);
        }
        foreach (var impact in _impacts)
        {
            if (_elapsed > impact.At)
                Ellipse(handle, impact.Position * Size, new Vector2(impact.Missile ? 35 : 5, impact.Missile ? 19 : 3) * Unit,
                    Color.Black.WithAlpha(0.35f));
        }
    }

    private static void DrawCrater(DrawingHandleScreen handle, Vector2 at, float radius)
    {
        Ellipse(handle, at, new Vector2(radius, radius * 0.6f), Color.FromHex("#1C211C").WithAlpha(0.75f));
        Ellipse(handle, at + new Vector2(0, radius * 0.12f), new Vector2(radius * 0.65f, radius * 0.28f),
            Color.Black.WithAlpha(0.45f));
    }

    private void DrawGroundCombat(DrawingHandleScreen handle)
    {
        if (_reduced || _elapsed < 1.3f)
            return;
        var scale = LobbyPartyChoreography.FlybyActorScale(_actors.Count, Size);
        for (var i = 0; i < _actors.Count; i++)
        {
            if (_actors[i].Card.Parent == null || _actors[i].Card.StageEntity == null)
                continue;
            var pose = HumanTargetPose(i);
            if (pose.Height > 15 * scale)
                continue;
            var target = GroundTarget(i);
            var hand = pose.Position + new Vector2(0, 3 * scale);
            var direction = target - hand;
            if (direction.LengthSquared() < 1)
                continue;
            direction = Vector2.Normalize(direction);
            var muzzle = hand + direction * 21 * scale;
            var angle = MathF.Atan2(direction.Y, direction.X);
            Sprite(handle, RifleArt, "base", hand + direction * 8 * scale, 32 * scale, angle);
            // Large casts rotate through firing groups; every person still moves and takes cover.
            if ((i + (int) (_elapsed / 3) * 31) % _actors.Count >= 48)
                continue;
            var cycle = (_elapsed - 1.3f + i * 0.173f) % (1.35f + i % 5 * 0.12f);
            for (var shot = 0; shot < 3; shot++)
            {
                var age = cycle - shot * 0.09f;
                if (age is < 0 or > 0.55f)
                    continue;
                var end = target + new Vector2(MathF.Sin(i * 7 + shot) * 9, MathF.Cos(i * 3 + shot) * 7) * scale;
                if (age < 0.06f)
                {
                    handle.DrawCircle(muzzle, 9 * scale, Color.Orange.WithAlpha(0.32f));
                    handle.DrawCircle(muzzle, 3 * scale, Color.FromHex("#FFF4C4"));
                }
                if (age < 0.18f)
                {
                    var t = age / 0.18f;
                    var tip = Vector2.Lerp(muzzle, end, t);
                    handle.DrawLine(Vector2.Lerp(muzzle, end, Math.Max(0, t - 0.14f)), tip,
                        i % 2 == 0 ? Color.FromHex("#FFE2A2") : Color.FromHex("#FFA489"));
                    handle.DrawCircle(tip, 1.3f * scale, Color.White);
                }
                else
                {
                    var dust = (age - 0.18f) / 0.37f;
                    handle.DrawCircle(end + new Vector2(0, -dust * 12 * scale),
                        (2 + dust * 6) * scale, Color.FromHex("#A39C7C").WithAlpha((1 - dust) * 0.45f));
                }
            }
        }
        DrawGroundOrdnance(handle);
        DrawAdvancingXenos(handle);
    }

    private void DrawGroundOrdnance(DrawingHandleScreen handle)
    {
        for (var salvo = 0; salvo < 4; salvo++)
        {
            var age = _elapsed - GrenadeTime(salvo);
            var from = GrenadeOrigin(salvo);
            var to = GrenadeTarget(salvo);
            if (age is >= 0 and < 1.1f)
            {
                var t = age / 1.1f;
                var p = Vector2.Lerp(from, to, t) - new Vector2(0, MathF.Sin(t * MathF.PI) * 70 * Unit);
                Ellipse(handle, Vector2.Lerp(from, to, t), new Vector2(5, 2) * Unit, Color.Black.WithAlpha(0.4f));
                handle.DrawCircle(p, 4 * Unit, Color.FromHex("#778150"));
                handle.DrawCircle(p + new Vector2(1, -2) * Unit, 1.5f * Unit, Color.Orange);
            }
            DrawAirburst(handle, to, age - 1.1f, 1.2f, salvo);
            var shellAge = _elapsed - ShellTime(salvo);
            var target = ShellTarget(salvo);
            var origin = TankPosition();
            if (shellAge is >= 0 and <= 0.65f)
            {
                var t = shellAge / 0.65f;
                var direction = Vector2.Normalize(target - origin);
                origin += direction * 68 * Unit;
                if (shellAge < 0.12f)
                {
                    handle.DrawCircle(origin, 22 * Unit, Color.Orange.WithAlpha(0.35f));
                    handle.DrawCircle(origin, 8 * Unit, Color.FromHex("#FFF3C2"));
                }
                var tip = Vector2.Lerp(origin, target, t);
                var tail = Vector2.Lerp(origin, target, Math.Max(0, t - 0.13f));
                handle.DrawLine(tail, tip, Color.FromHex("#FFC986"));
                handle.DrawCircle(tip, 3 * Unit, Color.White);
            }
            DrawAirburst(handle, target, shellAge - 0.65f, 1.5f, salvo + 41);
            var smokeAge = shellAge - 0.65f;
            if (smokeAge is > 0 and < 5)
            {
                for (var puff = 0; puff < 5; puff++)
                {
                    var drift = (smokeAge * 0.35f + puff * 0.2f) % 1;
                    var p = target + new Vector2(MathF.Sin(puff * 4) * 15 + drift * 20, -drift * 95) * Unit;
                    handle.DrawCircle(p, (7 + drift * 20) * Unit,
                        Color.FromHex("#32352F").WithAlpha((1 - drift) * (1 - smokeAge / 5) * 0.65f));
                }
            }
        }
    }

    private (Vector2 Position, float Cycle) AdvancingXeno(int index)
    {
        var cycle = (_elapsed + index * 0.93f) % 8.5f;
        var target = BattlePosition((index * 7 + 3) % Math.Max(1, _actors.Count));
        var from = new Vector2(index % 2 == 0 ? -80 * Unit : Size.X + 80 * Unit,
            Size.Y * (0.24f + (index * 0.618f) % 1 * 0.59f));
        var progress = Math.Clamp((cycle - 0.5f) / 4, 0, 1);
        return (Vector2.Lerp(from, target + new Vector2(index % 2 == 0 ? -24 : 24, 0) * Unit, progress), cycle);
    }

    private void DrawAdvancingXenos(DrawingHandleScreen handle)
    {
        if (_actors.Count == 0)
            return;
        for (var i = 0; i < 7; i++)
        {
            var (at, cycle) = AdvancingXeno(i);
            var target = BattlePosition((i * 7 + 3) % _actors.Count);
            var dead = cycle > 5.5f;
            var fade = dead ? Math.Clamp((8.5f - cycle) / 3, 0, 1) : 1;
            at.Y -= dead ? 0 : MathF.Abs(MathF.Sin(_elapsed * 13 + i)) * 3 * Unit;
            Ellipse(handle, at + new Vector2(0, 18) * Unit, new Vector2(21, 7) * Unit, Color.Black.WithAlpha(fade * 0.3f));
            Sprite(handle, XenoArt, dead ? "dead" : "alive", at, 68 * Unit, direction:
                i % 2 == 0 ? RsiDirection.East : RsiDirection.West, color: Color.White.WithAlpha(fade));
            if (cycle is > 4.8f and < 5.35f)
            {
                var claw = (cycle - 4.8f) / 0.55f;
                for (var scratch = 0; scratch < 3; scratch++)
                {
                    var start = target + new Vector2(-16 + scratch * 7, -18 + claw * 18) * Unit;
                    handle.DrawLine(start, start + new Vector2(18, 20) * Unit, Color.FromHex("#B9564B").WithAlpha(1 - claw));
                }
            }
        }
    }
}
