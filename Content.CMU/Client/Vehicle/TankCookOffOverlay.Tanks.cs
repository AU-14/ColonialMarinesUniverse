using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Vehicle;

public sealed partial class TankCookOffOverlay
{
    private void AddTanks(in OverlayDrawArgs args)
    {
        var query = entities.EntityQueryEnumerator<ActiveTankCookOffComponent, TankCookOffComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var active, out var tank, out var xform))
        {
            if (xform.MapID != args.MapId) continue;
            var point = _transform.GetWorldPosition(uid);
            if (!args.WorldAABB.Enlarged(18).Contains(point)) continue;
            var age = Math.Max(0, (float)(_timing.CurTime - _metadata.GetPauseTime(uid) - active.StartedAt).TotalSeconds);
            if (!active.Ruptured) age = Math.Min(age, tank.RuptureDelay - .001f);
            _visible.Add(uid);
            if (!_playback.TryGetValue(uid, out var playback) || playback.Epoch != active.StartedAt)
            {
                var seed = unchecked((uint)entities.GetNetEntity(uid).GetHashCode());
                _playback[uid] = playback = new(active.StartedAt,
                    TankCookOffParticleScene.Tank(tank.RuptureDelay, seed));
            }
            _batch.Add(playback.Scene, age, point);
        }
    }

    private void DrawTurrets(in OverlayDrawArgs args)
    {
        var sprites = entities.System<SpriteSystem>();
        var query = entities.EntityQueryEnumerator<ActiveTankCookOffComponent, TankCookOffComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var active, out var tank, out var xform))
        {
            if (!active.Ruptured || xform.MapID != args.MapId || !_visible.Contains(uid)) continue;
            var rupture = (float)(_timing.CurTime - _metadata.GetPauseTime(uid) - active.StartedAt).TotalSeconds - tank.RuptureDelay;
            if (rupture < 0) continue;
            var point = _transform.GetWorldPosition(uid);
            var seed = unchecked((uint)entities.GetNetEntity(uid).GetHashCode());
            var pose = TankCookOffParticleScene.TankTurret(rupture, seed);
            var ground = point + pose.Ground;
            // Turret specs use texture-relative paths. Frame0 resolves /Textures and handles missing states.
            var texture = sprites.Frame0(tank.TurretSprite);
            var size = (Vector2)texture.Size / 32;
            var spin = (float)_transform.GetWorldRotation(uid).Theta + pose.Rotation;
            var rotation = Matrix3x2.CreateRotation(spin);
            args.WorldHandle.SetTransform(Matrix3x2.CreateScale(1, .5f) * rotation * Matrix3x2.CreateTranslation(ground));
            args.WorldHandle.DrawTextureRect(texture, Box2.CenteredAround(Vector2.Zero, size), new Color(0, 0, 0, .28f));
            args.WorldHandle.SetTransform(rotation * Matrix3x2.CreateTranslation(point + pose.Position));
            args.WorldHandle.DrawTextureRect(texture, Box2.CenteredAround(Vector2.Zero, size), new Color(.64f, .58f, .52f));
            args.WorldHandle.SetTransform(Matrix3x2.Identity);
        }
    }
}
