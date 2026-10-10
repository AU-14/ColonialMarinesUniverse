using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Lobby;

public sealed partial class LobbyPartyShowControl
{
    private float BatteryRotation(Target target)
    {
        if (_reduced)
            return 0;
        for (var salvo = 0; salvo < 4; salvo++)
        {
            if (_elapsed >= ShellTime(salvo) - 0.2f && _elapsed < ShellTime(salvo) + 0.65f)
            {
                var groundAim = ShellTarget(salvo) - target.Position * Size;
                return MathF.Atan2(groundAim.Y, groundAim.X);
            }
        }
        var pass = _elapsed < LobbyPartyChoreography.PassStart(1) ? 0 : 1;
        var aim = JetPose(Math.Clamp(_elapsed, LobbyPartyChoreography.PassStart(pass),
            LobbyPartyChoreography.PassStart(pass) + LobbyPartyChoreography.PassDuration), pass).Position -
            target.Position * Size;
        return MathF.Atan2(aim.Y, aim.X);
    }

    private void AdvanceCounterBattery(float previous)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            if (Crossed(previous, LobbyPartyChoreography.PassStart(pass) + 2.8f))
                Sound("gau-cockpit.ogg", -26);
            if (Crossed(previous, LobbyPartyChoreography.PassStart(pass) + 3.25f))
                Sound("airburst.ogg", -22);
        }
        if (Crossed(previous, LobbyPartyChoreography.ThreatTime))
            Sound("rocket-release.ogg", -20);
        if (Crossed(previous, LobbyPartyChoreography.FlareTime))
            Sound("countermeasures.ogg", -17);
        if (Crossed(previous, LobbyPartyChoreography.FlareTime + 0.75f))
            Sound("airburst.ogg", -19);
        foreach (var impact in _impacts)
        {
            if (impact.Missile && Crossed(previous, impact.At + 0.6f) &&
                _targets[impact.Target].Kind == TargetKind.Tank)
                Sound("rocket-impact.ogg", -23);
        }
    }

    private void DrawCounterBattery(DrawingHandleScreen handle)
    {
        for (var pass = 0; pass < 2; pass++)
        for (var shot = 0; shot < 8; shot++)
        {
            var launched = LobbyPartyChoreography.PassStart(pass) + 2.8f + shot * 0.13f;
            var age = _elapsed - launched;
            if (age is < 0 or > 1.7f)
                continue;
            // The tank answers the run with tracers and flak.
            foreach (var target in _targets)
            {
                if (target.Kind != TargetKind.Tank)
                    continue;
                var origin = target.Position * Size;
                var intercept = JetPose(launched + 0.33f, pass).Position +
                    new Vector2(MathF.Sin(shot * 8) * 75, 40 + MathF.Cos(shot * 3) * 45) * Unit;
                var direction = Vector2.Normalize(intercept - origin);
                origin += direction * 60 * Unit;
                if (age < 0.13f)
                {
                    handle.DrawCircle(origin, (12 - age * 55) * Unit, Color.Orange.WithAlpha(0.65f));
                    handle.DrawCircle(origin, 3 * Unit, Color.White);
                }
                if (age < 0.33f)
                {
                    var t = age / 0.33f;
                    var tip = Vector2.Lerp(origin, intercept, t);
                    var tail = Vector2.Lerp(origin, intercept, Math.Max(0, t - 0.13f));
                    handle.DrawLine(tail, tip, Color.FromHex("#FF9A71"));
                    handle.DrawCircle(tip, 2 * Unit, Color.FromHex("#FFF5D4"));
                }
                else if (shot % 2 == 0)
                    DrawAirburst(handle, intercept, age - 0.33f, 0.6f, shot);
            }
        }
        DrawMissileChase(handle);
        foreach (var impact in _impacts)
        {
            if (!impact.Missile || _targets[impact.Target].Kind != TargetKind.Tank)
                continue;
            var age = _elapsed - impact.At;
            for (var secondary = 0; secondary < 3; secondary++)
            {
                var at = impact.Position * Size + new Vector2(MathF.Sin(secondary * 5 + impact.Seed) * 35,
                    MathF.Cos(secondary * 7) * 17) * Unit;
                DrawAirburst(handle, at, age - 0.6f - secondary * 0.35f, 0.7f, secondary + impact.Seed);
            }
        }
    }

    private Vector2 Decoy(float elapsed, int index)
    {
        var pose = JetPose(LobbyPartyChoreography.FlareTime, 1);
        var age = Math.Max(0, elapsed - LobbyPartyChoreography.FlareTime);
        var side = new Vector2(-pose.Heading.Y, pose.Heading.X);
        var spread = (index / 2 + 1) * (index % 2 == 0 ? -1 : 1);
        return pose.Position - pose.Heading * (45 + age * 105) * Unit +
            side * spread * age * 47 * Unit + new Vector2(0, age * age * 55) * Unit;
    }

    private Vector2 Pursuer(float t)
    {
        var start = _targets[0].Position * Size;
        foreach (var target in _targets)
        {
            if (target.Kind == TargetKind.Tank)
                start = target.Position * Size;
        }
        var chase = JetPose(LobbyPartyChoreography.FlareTime, 1).Position;
        if (t < 0.6f)
            return LobbyPartyChoreography.Curve(start, start + new Vector2(0, -100 * Unit),
                chase + new Vector2(-30, 100) * Unit, chase, t / 0.6f);
        return Vector2.Lerp(chase, Decoy(LobbyPartyChoreography.FlareTime + 0.75f, 5),
            LobbyLineupChoreography.Smooth((t - 0.6f) / 0.4f));
    }

    private void DrawMissileChase(DrawingHandleScreen handle)
    {
        var age = _elapsed - LobbyPartyChoreography.ThreatTime;
        const float flight = 1.85f;
        if (age is >= 0 and <= flight)
        {
            var t = age / flight;
            for (var i = 23; i >= 1; i--)
            {
                var past = t - i * 0.014f;
                if (past < 0)
                    continue;
                handle.DrawCircle(Pursuer(past), (2 + i * 0.42f) * Unit,
                    Color.FromHex("#C4C1B7").WithAlpha((1 - i / 24f) * 0.50f));
            }
            var tip = Pursuer(t);
            var tail = Pursuer(Math.Max(0, t - 0.025f));
            handle.DrawLine(tail, tip, Color.White);
            handle.DrawCircle(tail, 5 * Unit, Color.Orange);
            handle.DrawCircle(tail, 10 * Unit, Color.Orange.WithAlpha(0.2f));
        }
        var flareAge = _elapsed - LobbyPartyChoreography.FlareTime;
        if (flareAge is >= 0 and <= 2.8f)
        {
            var fade = 1 - flareAge / 2.8f;
            for (var i = 0; i < 8; i++)
            {
                var at = Decoy(_elapsed, i);
                for (var trail = 1; trail <= 9; trail++)
                {
                    if (flareAge < trail * 0.04f)
                        break;
                    handle.DrawCircle(Decoy(_elapsed - trail * 0.04f, i), (2 + trail * 0.55f) * Unit,
                        Color.FromHex("#B3B5B2").WithAlpha((1 - trail / 10f) * fade * 0.3f));
                }
                handle.DrawCircle(at, 15 * Unit, Color.Orange.WithAlpha(fade * 0.15f));
                handle.DrawCircle(at, (3 + MathF.Abs(MathF.Sin(_elapsed * 35 + i))) * Unit,
                    Color.FromHex("#FFF3B7").WithAlpha(fade));
            }
        }
        DrawAirburst(handle, Pursuer(1), age - flight, 1.4f, 17);
    }

    private void DrawAirburst(DrawingHandleScreen handle, Vector2 at, float age, float scale, int seed)
    {
        if (age is < 0 or > 1.5f)
            return;
        var fade = 1 - age / 1.5f;
        var size = scale * Unit;
        if (age < 0.35f)
        {
            handle.DrawCircle(at, (8 + age * 80) * size, Color.FromHex("#FFF1C6").WithAlpha(1 - age / 0.35f));
            handle.DrawCircle(at, (15 + age * 155) * size, Color.Orange.WithAlpha(fade * 0.5f), false);
        }
        for (var i = 0; i < 9; i++)
        {
            var direction = new Vector2(MathF.Cos(i * 2.4f + seed), MathF.Sin(i * 2.4f + seed));
            var p = at + direction * (10 + age * 45) * size;
            handle.DrawCircle(p, (4 + age * 13) * size, Color.FromHex("#3B3B3D").WithAlpha(fade * 0.55f));
            handle.DrawLine(p, p + direction * 10 * size * fade, Color.FromHex("#FFBB65").WithAlpha(fade));
        }
    }
}
