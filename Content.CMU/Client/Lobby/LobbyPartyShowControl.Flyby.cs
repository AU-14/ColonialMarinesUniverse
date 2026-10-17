using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.CMU14.Lobby;

public sealed partial class LobbyPartyShowControl
{
    private const string JetArt = "/Textures/CMU14/Vehicles/Fighter/jetfighter.rsi";
    private const string TankArt = "/Textures/_RMC14/Structures/Vehicles/tank.rsi";
    private const string ApcArt = "/Textures/_RMC14/Structures/Vehicles/apc.rsi";
    private const string TruckArt = "/Textures/CMU14/Structures/vehicles/militarycargotrucks.rsi";
    private const string XenoArt = "/Textures/CMU14/Mobs/Xenos/Drone/drone.rsi";
    private enum TargetKind : byte { Xeno, Tank, Human }
    private sealed class Target(TargetKind kind, Vector2 position)
    {
        public readonly TargetKind Kind = kind;
        public readonly Vector2 Position = position;
        public int ActorIndex = -1;
        public float HitAt = -100;
        public float DestroyedAt = -100;
    }
    private readonly record struct Impact(int Target, Vector2 Position, Vector2 Origin, float At, bool Missile, int Seed);
    private readonly Target[] _targets = new Target[4];
    private readonly List<Impact> _impacts = new();
    private readonly int[] _missileTargets = new int[2];

    private void InitializeTargets()
    {
        var kinds = new[] { TargetKind.Xeno, TargetKind.Tank, TargetKind.Human, TargetKind.Human };
        for (var i = kinds.Length - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
        }
        for (var i = 0; i < _targets.Length; i++)
            _targets[i] = new Target(kinds[i], new Vector2(0.18f + i * 0.215f, 0.65f + (float) _random.NextDouble() * 0.10f));
        // Shuffled scenery makes each show different while every target gets its own moment.
        _missileTargets[0] = 0;
        _missileTargets[1] = 3;
    }

    private void AdvanceFlyby(float previous)
    {
        for (var pass = 0; pass < LobbyPartyChoreography.PassCount; pass++)
        {
            if (Crossed(previous, LobbyPartyChoreography.PassStart(pass)))
                Sound(pass == 2 ? "jet-pass-high.ogg" : "jet-pass.ogg", pass == 2 ? -17 : -13);
            if (pass == 2)
                continue;
            if (Crossed(previous, LobbyPartyChoreography.ShotTime(pass, 0)))
                Sound("gau-ground.ogg", -16);
            for (var shot = 0; shot < LobbyPartyChoreography.ShotsPerPass; shot++)
            {
                var at = LobbyPartyChoreography.ShotTime(pass, shot);
                if (!Crossed(previous, at))
                    continue;
                var target = pass + 1;
                var offset = new Vector2((shot / (float) (LobbyPartyChoreography.ShotsPerPass - 1) - 0.5f) * 0.075f,
                    MathF.Sin(shot * 7) * 0.012f);
                var jet = JetPose(at, pass);
                var muzzle = Muzzle(jet) / Size;
                _impacts.Add(new Impact(target, TargetPosition(_targets[target]) + offset, muzzle, at + 0.12f, false, shot + pass * 24));
            }
        }
        for (var missile = 0; missile < 2; missile++)
        {
            var at = LobbyPartyChoreography.MissileTime(missile);
            if (!Crossed(previous, at))
                continue;
            Sound("missile-release.ogg", -17);
            var target = _missileTargets[missile];
            var jet = JetPose(at, 2);
            var wing = new Vector2(MathF.Cos(jet.Rotation), MathF.Sin(jet.Rotation)) * (missile == 0 ? -40 : 40) * Unit;
            _impacts.Add(new Impact(target, TargetPosition(_targets[target]), (jet.Position + wing) / Size,
                at + LobbyPartyChoreography.MissileFlight, true, 50 + missile));
        }
        foreach (var impact in _impacts)
        {
            if (!Crossed(previous, impact.At))
                continue;
            _targets[impact.Target].HitAt = impact.At;
            ReactToGroundImpact(impact.Position * Size, impact.At, impact.Missile ? 120 : 70);
            if (impact.Missile)
            {
                _targets[impact.Target].DestroyedAt = impact.At;
                Sound("missile-impact.ogg", -14);
            }
            else if (impact.Seed % 4 == 0)
                Sound("gau-impact.ogg", -23);
        }
        AdvanceCounterBattery(previous);
        AdvanceBattlefield(previous);
        _impacts.RemoveAll(impact => _elapsed - impact.At > 4.5f);
    }

    private bool Crossed(float previous, float at) => previous <= at && _elapsed > at;

    private LobbyPartyChoreography.MarchPose HumanTargetPose(int index)
    {
        return LobbyPartyChoreography.FlybyCrew(_elapsed, index, _actors.Count, Size, _crewHitAt[index], _reduced, _seed);
    }

    private Vector2 TargetPosition(Target target) => target.ActorIndex >= 0 && Size.X > 0 && Size.Y > 0
        ? LobbyPartyChoreography.BattlePosition(target.ActorIndex, _actors.Count, Size, _seed) / Size
        : target.Position;

    private void DrawTargets(DrawingHandleScreen handle)
    {
        for (var i = 0; i < _actors.Count; i++)
        {
            if (_actors[i].Card.Parent == null)
                continue;
            var pose = HumanTargetPose(i);
            var scale = LobbyPartyChoreography.FlybyActorScale(_actors.Count, Size);
            Ellipse(handle, pose.Position + new Vector2(0, pose.Height + 13 * scale),
                new Vector2(12, 4) * scale, Color.Black.WithAlpha(0.35f));
        }
        foreach (var target in _targets)
        {
            if (target.Kind == TargetKind.Human)
                continue;
            var at = target.Position * Size;
            var age = _elapsed - target.HitAt;
            var destruction = _elapsed - target.DestroyedAt;
            var destroyed = target.DestroyedAt > 0;
            var shake = !_reduced && age < 0.35f ? MathF.Sin(age * 75) * (1 - age / 0.35f) * 5 * Unit : 0;
            at.X += shake;
            handle.DrawCircle(at + new Vector2(0, 24 * Unit), 46 * Unit,
                Color.Black.WithAlpha(0.35f));
            var tint = destroyed ? Color.FromHex("#62636A") : Color.White;
            switch (target.Kind)
            {
                case TargetKind.Tank:
                    Sprite(handle, TankArt, destroyed ? "damaged_frame" : "tank_base", at, 176 * Unit, color: tint);
                    Sprite(handle, TankArt, "wheels_1", at, 176 * Unit, color: tint);
                    if (!destroyed)
                    {
                        var aim = BatteryRotation(target);
                        Sprite(handle, TankArt, "tank_turret_0", at, 176 * Unit, aim);
                        Sprite(handle, TankArt, "ltb_cannon_0", at, 176 * Unit, aim);
                    }
                    else if (destruction < 2.5f)
                    {
                        var t = destruction / 2.5f;
                        Sprite(handle, TankArt, "tank_turret_0", at + new Vector2(t * 95, -MathF.Sin(t * MathF.PI) * 175) * Unit,
                            176 * Unit, t * 8, color: tint.WithAlpha(1 - t));
                    }
                    break;
                case TargetKind.Xeno:
                    var tumble = age < 0.8f ? age / 0.8f * MathF.Tau : 0;
                    Sprite(handle, XenoArt, destroyed ? "dead" : "alive", at, 90 * Unit, tumble, color: tint);
                    break;
            }
            if (destroyed && target.Kind == TargetKind.Tank)
            {
                // A persistent wreck smolders after the shockwave and fragments have cleared.
                for (var puff = 0; puff < 5; puff++)
                {
                    var t = (_elapsed * 0.4f + puff * 0.2f) % 1;
                    var p = at + new Vector2(MathF.Sin(puff * 5 + t) * 25, -t * 110) * Unit;
                    handle.DrawCircle(p, (10 + t * 20) * Unit, Color.FromHex("#31373D").WithAlpha((1 - t) * 0.65f));
                }
                handle.DrawCircle(at, (11 + MathF.Sin(_elapsed * 10) * 2) * Unit, Color.Orange.WithAlpha(0.7f));
            }
        }
    }

    private void DrawFlyby(DrawingHandleScreen handle)
    {
        DrawGroundCombat(handle);
        if (_reduced)
        {
            Sprite(handle, JetArt, "jetfighter", new Vector2(Size.X * 0.5f, Size.Y * 0.38f), 175 * Unit, -MathF.PI / 2);
            return;
        }
        foreach (var impact in _impacts)
        {
            var age = _elapsed - impact.At;
            var at = impact.Position * Size;
            if (age < 0)
            {
                var duration = impact.Missile ? LobbyPartyChoreography.MissileFlight : 0.12f;
                var t = Math.Clamp(1 + age / duration, 0, 1);
                var from = impact.Origin * Size;
                Vector2 Flight(float progress) => Vector2.Lerp(from, at, progress) +
                    new Vector2(0, impact.Missile ? -MathF.Sin(progress * MathF.PI) * 40 * Unit : 0);
                var tip = Flight(t);
                var tail = Flight(Math.Max(0, t - 0.11f));
                if (impact.Missile)
                {
                    for (var segment = 1; segment <= 15; segment++)
                    {
                        var sample = t - segment * 0.018f;
                        if (sample < 0)
                            break;
                        handle.DrawCircle(Flight(sample), (2 + segment * 0.5f) * Unit,
                            Color.FromHex("#B5BEC1").WithAlpha((1 - segment / 16f) * 0.55f));
                    }
                    handle.DrawLine(tail, tip, Color.White);
                    handle.DrawCircle(tail, 4 * Unit, Color.Orange);
                }
                else
                {
                    handle.DrawLine(tail, tip, Color.FromHex("#FFDA82"));
                    handle.DrawCircle(tip, 2 * Unit, Color.White);
                }
                continue;
            }
            DrawImpact(handle, impact, at, age);
        }
        DrawCounterBattery(handle);
        for (var pass = 0; pass < LobbyPartyChoreography.PassCount; pass++)
        {
            var t = _elapsed - LobbyPartyChoreography.PassStart(pass);
            if (t < 0 || t > LobbyPartyChoreography.PassDuration)
                continue;
            var jet = JetPose(_elapsed, pass);
            var heading = jet.Heading;
            // The imported airframe is painted nose-down; flight headings use a nose-up basis.
            var spriteRotation = jet.Rotation + MathF.PI;
            var stretch = JetStretch(jet);
            Sprite(handle, JetArt, "jetfighter", jet.Position + new Vector2(18 + jet.Altitude * 50, 24 + jet.Altitude * 95) * Unit,
                174 * Unit, spriteRotation, color: Color.Black.WithAlpha(0.36f - jet.Altitude * 0.18f), stretch: stretch);
            for (var trail = 24; trail >= 1; trail--)
            {
                var past = JetPose(_elapsed - trail * 0.022f, pass);
                var p = past.Position - past.Heading * 85 * Unit;
                var wing = new Vector2(-past.Heading.Y, past.Heading.X) * 32 * Unit;
                var alpha = (1 - trail / 25f) * 0.22f;
                handle.DrawCircle(p + wing, (2 + trail * 0.23f) * Unit, Color.FromHex("#DAE1E4").WithAlpha(alpha));
                handle.DrawCircle(p - wing, (2 + trail * 0.23f) * Unit, Color.FromHex("#DAE1E4").WithAlpha(alpha));
            }
            Sprite(handle, JetArt, "jetfighter", jet.Position, 174 * Unit, spriteRotation, stretch: stretch);
            Sprite(handle, JetArt, "canopyglass", jet.Position, 174 * Unit, spriteRotation, stretch: stretch);
            if (pass < 2 && _elapsed >= LobbyPartyChoreography.ShotTime(pass, 0) &&
                _elapsed <= LobbyPartyChoreography.ShotTime(pass, LobbyPartyChoreography.ShotsPerPass - 1) + 0.06f)
            {
                var muzzle = Muzzle(jet);
                var pulse = 0.6f + MathF.Abs(MathF.Sin(t * 160)) * 0.4f;
                handle.DrawCircle(muzzle, 17 * Unit * pulse, Color.Orange.WithAlpha(0.22f));
                handle.DrawCircle(muzzle, 6 * Unit * pulse, Color.FromHex("#FFF5CE"));
                handle.DrawLine(muzzle, muzzle + heading * 30 * Unit * pulse, Color.White);
                for (var casing = 0; casing < 8; casing++)
                {
                    var age = (t * 5 + casing / 8f) % 1;
                    var p = muzzle + new Vector2(-heading.Y, heading.X) * age * 40 * Unit -
                        heading * age * 75 * Unit;
                    handle.DrawCircle(p, 1.4f * Unit, Color.FromHex("#C6AB6C").WithAlpha(1 - age));
                }
            }
        }
    }

    private LobbyPartyChoreography.FlightPose JetPose(float elapsed, int pass) =>
        LobbyPartyChoreography.Jet(elapsed, pass, Size, pass < 2 ? TargetPosition(_targets[pass + 1]) :
            (TargetPosition(_targets[_missileTargets[0]]) + TargetPosition(_targets[_missileTargets[1]])) / 2);

    private static Vector2 JetStretch(LobbyPartyChoreography.FlightPose jet) =>
        new(1 - MathF.Abs(jet.Bank) * 0.30f, 1 - jet.Pitch * 0.19f);

    private Vector2 Muzzle(LobbyPartyChoreography.FlightPose jet) =>
        jet.Position + jet.Heading * 102 * JetStretch(jet).Y * Unit;

    private void DrawImpact(DrawingHandleScreen handle, Impact impact, Vector2 at, float age)
    {
        var kind = _targets[impact.Target].Kind;
        var lifetime = impact.Missile ? 3.5f : 1.2f;
        if (age > lifetime)
            return;
        var fade = Math.Clamp(1 - age / lifetime, 0, 1);
        var radius = (impact.Missile ? 65 : 15) * Unit;
        if (impact.Missile && age < 0.75f)
        {
            handle.DrawCircle(at, (18 + age * 135) * Unit, Color.FromHex("#F3DDB2").WithAlpha((1 - age / 0.75f) * 0.5f), false);
            handle.DrawCircle(at, radius * MathF.Sin(Math.Clamp(age / 0.5f, 0, 1) * MathF.PI), Color.Orange.WithAlpha(fade));
        }
        var material = kind switch
        {
            TargetKind.Xeno => Color.FromHex("#A7D33E"),
            TargetKind.Human => Color.FromHex("#C3736C"),
            TargetKind.Tank => Color.FromHex("#FFE7A0"),
            _ => Color.FromHex("#B1B8A4"),
        };
        var count = impact.Missile ? 22 : 7;
        for (var i = 0; i < count; i++)
        {
            var angle = i * 2.39996f + impact.Seed;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var velocity = (35 + i * 9 % 95) * (impact.Missile ? 1.7f : 0.5f) * Unit;
            var p = at + direction * velocity * age + new Vector2(0, age * age * 48 - age * 48) * Unit;
            var half = new Vector2(kind == TargetKind.Tank && impact.Missile ? 4 : 2) * Unit * fade;
            if (kind == TargetKind.Tank)
                handle.DrawRect(new UIBox2(p - half, p + half), material.WithAlpha(fade));
            else
                handle.DrawCircle(p, half.X * 1.6f, material.WithAlpha(fade));
        }
        if (kind == TargetKind.Tank)
        {
            for (var i = 0; i < (impact.Missile ? 7 : 2); i++)
            {
                var p = at + new Vector2(MathF.Sin(i * 13) * radius * age * 0.6f, -age * (18 + i * 4) * Unit);
                handle.DrawCircle(p, (4 + age * (impact.Missile ? 18 : 6)) * Unit,
                    Color.FromHex("#45494E").WithAlpha(fade * 0.35f));
            }
        }
    }
}
