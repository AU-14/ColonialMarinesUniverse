using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Lobby;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Lobby;

public sealed partial class LobbyLineupCard
{
    // Reuse the game's directional/animated artwork without spawning gameplay entities.
    private readonly Dictionary<(string Path, string State), IRsiStateLike> _partyTextures = new();
    private readonly Vector2[] _flashVertices = new Vector2[6];

    private sealed class PartyEffectsOverlay(LobbyLineupCard card) : Control
    {
        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            card.DrawCombatEffects(handle);
        }
    }

    private Texture PartyTexture(string path, string state, RsiDirection direction, float time)
    {
        var key = (path, state);
        if (!_partyTextures.TryGetValue(key, out var animation))
        {
            animation = _entities.System<SpriteSystem>().RsiStateLike(new SpriteSpecifier.Rsi(new ResPath(path), state));
            _partyTextures.Add(key, animation);
        }
        var frame = 0;
        if (animation.IsAnimated)
        {
            var length = 0f;
            for (var i = 0; i < animation.AnimationFrameCount; i++)
                length += animation.GetDelay(i);
            var remaining = length > 0 ? Math.Max(0, time) % length : 0;
            while (frame < animation.AnimationFrameCount - 1 && remaining >= animation.GetDelay(frame))
                remaining -= animation.GetDelay(frame++);
        }
        return animation.GetFrame(animation.RsiDirections == RsiDirectionType.Dir1 ? RsiDirection.South : direction, frame);
    }

    private void DrawCombatEffects(DrawingHandleScreen handle)
    {
        if (StageMoving || _gestureDelay > 0 || !LobbyLineupChoreography.IsCombat(_gesture) || _previewBounds.Height <= 0)
            return;

        var reduced = _configuration.GetCVar(CCVars.ReducedMotion);
        var elapsed = _gestureDuration - _gestureRemaining;
        var phase = reduced ? 2.3f : elapsed;
        var pose = LobbyLineupChoreography.Sample(_gesture, elapsed, reduced);
        var center = (_previewBounds.Center + pose.Offset * _previewBounds.Size) * UIScale;
        var unit = Preview.Scale.X * UIScale / 2;
        if (unit <= 0)
            return;
        var fade = LobbyLineupChoreography.Envelope(elapsed, _gestureDuration, 0.3f, 0.45f);

        void Sprite(string path, string state, Vector2 at, float size, RsiDirection direction = RsiDirection.South, float alpha = 1)
        {
            var half = new Vector2(size * unit / 2);
            handle.DrawTextureRect(PartyTexture(path, state, direction, reduced ? 0 : phase),
                new UIBox2(at - half, at + half), Color.White.WithAlpha(alpha * fade));
        }

        void Heart(Vector2 at, float alpha)
        {
            var color = Color.FromHex("#FF719C").WithAlpha(alpha * fade);
            Vector2 Point(float x, float y) => at + new Vector2(x, y) * unit;
            handle.DrawLine(Point(-3, 0), Point(0, 3), color);
            handle.DrawLine(Point(0, 3), Point(3, 0), color);
            handle.DrawLine(Point(-3, 0), Point(-1.5f, -1.5f), color);
            handle.DrawLine(Point(-1.5f, -1.5f), at, color);
            handle.DrawLine(at, Point(1.5f, -1.5f), color);
            handle.DrawLine(Point(1.5f, -1.5f), Point(3, 0), color);
        }

        const string drone = "/Textures/CMU14/Mobs/Xenos/Drone/drone.rsi";
        const string hugger = "/Textures/CMU14/Mobs/Xenos/Parasite/parasite.rsi";
        switch (_gesture)
        {
            case LobbyLineupEmote.BurstFire:
            case LobbyLineupEmote.SprayAndPray:
                // The east-facing wielded layer has the same 32px canvas as the humanoid.
                // Its barrel, recoil, flash, tracer and brass all use the same shot clock.
                Sprite("/Textures/CMU14/Weapons/Guns/USCM/m41mk2.rsi", "wielded-inhand-right", center, 64, RsiDirection.East);
                var muzzle = center + new Vector2(27, 2) * unit;
                if (reduced)
                    break;
                var flash = _flashVertices;
                flash[0] = muzzle + new Vector2(-1, 0) * unit;
                flash[1] = muzzle + new Vector2(5, -3) * unit;
                flash[2] = muzzle + new Vector2(3, 0) * unit;
                flash[3] = muzzle + new Vector2(7, 1) * unit;
                flash[4] = muzzle + new Vector2(3, 2) * unit;
                flash[5] = muzzle + new Vector2(4, 4) * unit;
                for (var shot = 0; shot < LobbyLineupChoreography.ShotCount(_gesture.Value); shot++)
                {
                    var age = phase - LobbyLineupChoreography.ShotTime(_gesture.Value, shot);
                    if (age is < 0 or > 0.7f)
                        continue;
                    if (age < 0.055f)
                    {
                        var glow = 1 - age / 0.055f;
                        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, flash, Color.FromHex("#FFCC67").WithAlpha(glow));
                        handle.DrawCircle(muzzle, 1.2f * unit, Color.FromHex("#FFF2CE").WithAlpha(glow));
                    }
                    if (age < 0.10f)
                    {
                        var tracer = muzzle + new Vector2(2 + age * 100, 0) * unit;
                        handle.DrawLine(tracer, tracer + new Vector2(5, 0) * unit,
                            Color.FromHex("#FFD993").WithAlpha(1 - age / 0.1f));
                    }
                    var life = age / 0.7f;
                    var brass = center + new Vector2(6 - life * 17, 2 - life * 26 + life * life * 43) * unit;
                    var angle = age * 17 + shot;
                    handle.DrawLine(brass, brass + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (2.5f * unit),
                        Color.FromHex("#D8B56C").WithAlpha(1 - life));
                }
                break;
            case LobbyLineupEmote.XenoHug:
                var arrive = LobbyLineupChoreography.Smooth(phase / 1.1f);
                var leave = LobbyLineupChoreography.Smooth((phase - 4.1f) / 1.1f);
                var walking = phase < 1.1f || phase > 4.1f;
                var droneState = walking ? "drone_walk_" + (int) (phase * 9) % 4 : "alive";
                var hugAt = center + new Vector2(40 - arrive * 29 + leave * 32, walking ? -MathF.Abs(MathF.Sin(phase * 12)) * 2 : 0) * unit;
                Sprite(drone, droneState, hugAt, 57, leave > 0 ? RsiDirection.East : RsiDirection.West);
                if (phase is > 1.1f and < 4.1f)
                    for (var i = 0; i < 3; i++)
                    {
                        var life = (phase - 1.1f + i * 0.35f) % 1;
                        Heart(center + new Vector2(-11 + i * 11, -14 - life * 18) * unit, 1 - life);
                    }
                break;
            case LobbyLineupEmote.Facehugger:
                if (phase < 1)
                {
                    var crawl = LobbyLineupChoreography.Smooth(phase);
                    Sprite(hugger, "running", center + new Vector2(35 - crawl * 12, 24) * unit, 33, RsiDirection.West);
                }
                else if (phase < 1.45f)
                {
                    var leap = Math.Clamp((phase - 1) / 0.45f, 0, 1);
                    var at = center + new Vector2(23 * (1 - leap), 24 - leap * 40 - MathF.Sin(leap * MathF.PI) * 18) * unit;
                    Sprite(hugger, "thrown", at, 32);
                }
                else if (phase < 3.7f)
                {
                    Sprite(hugger, "equipped-MASK", center, 64);
                    Heart(center + new Vector2(16, -25) * unit, 0.8f);
                }
                else
                {
                    var flee = Math.Clamp((phase - 3.7f) / 1.2f, 0, 1);
                    var at = center + new Vector2(-flee * 40, -16 + LobbyLineupChoreography.Smooth(flee * 2) * 40) * unit;
                    Sprite(hugger, flee < 0.5f ? "thrown" : "running", at, 32, RsiDirection.West);
                }
                break;
            case LobbyLineupEmote.Chestburst:
            case LobbyLineupEmote.XenoMorph:
                var xenoBlend = LobbyLineupChoreography.XenoBlend(elapsed);
                if (xenoBlend <= 0)
                    break;
                var morph = _gesture == LobbyLineupEmote.XenoMorph;
                var pop = Math.Clamp((phase - 1.75f) / 0.65f, 0, 1);
                var xenoAt = center + new Vector2(morph ? 0 : MathF.Sin(Math.Max(0, phase - 2.4f) * 3) * 11,
                    morph ? 0 : 14 * pop - MathF.Sin(pop * MathF.PI) * 21) * unit;
                Sprite(morph ? drone : "/Textures/CMU14/Mobs/Xenos/Larva/larva.rsi",
                    morph ? "alive" : "running", xenoAt, morph ? 64 : 36,
                    morph ? RsiDirection.South : RsiDirection.East, xenoBlend);
                var ring = Math.Clamp((phase - 1.75f) / 0.65f, 0, 1);
                for (var i = 0; i < 10; i++)
                {
                    var ray = new Vector2(MathF.Cos(i * MathF.Tau / 10), MathF.Sin(i * MathF.Tau / 10));
                    handle.DrawLine(center + ray * (5 + ring * 17) * unit,
                        center + ray * (9 + ring * 19) * unit,
                        (i % 2 == 0 ? Color.Lime : Color.FromHex("#FFE9A6")).WithAlpha((1 - ring) * xenoBlend));
                }
                break;
            case LobbyLineupEmote.DodgeRoll:
                var dust = Math.Clamp((phase - 0.6f) / 1.4f, 0, 1);
                for (var i = 0; i < 4; i++)
                {
                    var at = center + new Vector2(-17 - dust * i * 4, 22 - dust * i * 2) * unit;
                    handle.DrawCircle(at, (1 + dust * 3) * unit, Color.FromHex("#B6C0B6").WithAlpha(MathF.Sin(dust * MathF.PI) * 0.35f));
                }
                break;
            case LobbyLineupEmote.GrenadeOops:
                var resting = _previewBounds.Center * UIScale;
                var toss = Math.Clamp((phase - 0.45f) / 1.0f, 0, 1);
                var grenade = resting + new Vector2(6 + toss * 17, 6 + toss * 18 - MathF.Sin(toss * MathF.PI) * 18) * unit;
                if (phase < 2.15f)
                    Sprite("/Textures/_RMC14/Objects/Weapons/Grenades/m40hedp.rsi", "primed", grenade, 24);
                else
                {
                    var puff = Math.Clamp((phase - 2.15f) / 1.65f, 0, 1);
                    if (puff < 0.25f)
                        handle.DrawCircle(grenade, (3 + puff * 25) * unit,
                            Color.FromHex("#FFC777").WithAlpha((1 - puff * 4) * 0.8f));
                    for (var i = 0; i < 6; i++)
                    {
                        var smoke = grenade + new Vector2(MathF.Sin(i * 2.4f) * puff * 20, -puff * (8 + i * 5)) * unit;
                        handle.DrawCircle(smoke, (3 + puff * 6) * unit, Color.FromHex("#C1C6B7").WithAlpha((1 - puff) * 0.5f));
                    }
                }
                break;
        }
    }
}
