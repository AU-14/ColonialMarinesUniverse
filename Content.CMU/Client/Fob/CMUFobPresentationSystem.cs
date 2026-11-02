using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Fob;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Fob;

/// <summary>Articulates the original artwork without moving gameplay transforms or fixtures.</summary>
public sealed partial class CMUFobPresentationSystem : VisualizerSystem<CMUFobPresentationComponent>
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<(RSI.State, RsiDirection, CMUFobDeviceAnimation), List<CMUFobSpritePart>> _parts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CMUFobPresentationEvent>(OnCue);
    }

    protected override void OnAppearanceChange(EntityUid uid, CMUFobPresentationComponent component,
        ref AppearanceChangeEvent args)
    {
        // Only the enabled trial publishes status. Prototypes alone do not activate this renderer.
        if (!AppearanceSystem.TryGetData<CMUFobPresentationStatus>(uid, CMUFobPresentationVisuals.Status, out var status, args.Component))
            return;
        var animation = EnsureComp<CMUFobAnimationComponent>(uid);
        animation.Status = status;
        AppearanceSystem.TryGetData(uid, CMUFobPresentationVisuals.StatusSince, out animation.StatusSince, args.Component);
        AppearanceSystem.TryGetData(uid, CMUFobPresentationVisuals.Aim, out animation.Aim, args.Component);
        AppearanceSystem.TryGetData(uid, CMUFobPresentationVisuals.Open, out animation.Open, args.Component);
        if (AppearanceSystem.TryGetData<float>(uid, CMUFobPresentationVisuals.LockDuration, out var duration, args.Component))
            animation.LockDuration = duration;
    }

    private void OnCue(CMUFobPresentationEvent ev)
    {
        if (!TryGetEntity(ev.Entity, out var uid) || !HasComp<CMUFobPresentationComponent>(uid))
            return;
        var animation = EnsureComp<CMUFobAnimationComponent>(uid.Value);
        if (ev.Cue == CMUFobPresentationCue.Shot)
        {
            animation.ShotAt = _timing.CurTime;
            animation.ShotSequence = ev.Sequence;
        }
        else
        {
            animation.Cue = ev.Cue;
            animation.CueAt = _timing.CurTime;
        }
    }

    private static float Ease(float value)
    {
        value = Math.Clamp(value, 0, 1);
        return value * value * (3 - 2 * value);
    }

    private static float Kick(float age, float duration = 0.26f)
        => age < 0 || age >= duration ? 0 : age < 0.035f ? age / 0.035f : 1 - Ease((age - 0.035f) / (duration - 0.035f));

    private void Draw(Entity<CMUFobPresentationComponent> ent, SpriteComponent sprite, CMUFobAnimationComponent animation, float frameTime)
    {
        var device = ent.Comp.Device;
        var now = _timing.CurTime;
        var age = (float) (now - animation.StatusSince).TotalSeconds;
        var cueAge = animation.CueAt is { } cueAt ? (float) (now - cueAt).TotalSeconds : float.PositiveInfinity;
        var shotAge = animation.ShotAt is { } shotAt ? (float) (now - shotAt).TotalSeconds : float.PositiveInfinity;
        var working = animation.Status == CMUFobPresentationStatus.Working;
        var starting = animation.Status == CMUFobPresentationStatus.Starting;
        var active = animation.Status is CMUFobPresentationStatus.Active or CMUFobPresentationStatus.Warning;
        var gun = device is CMUFobDeviceAnimation.HeavyGun or CMUFobDeviceAnimation.Rocket;

        foreach (var layer in animation.Layers)
            SpriteSystem.LayerSetVisible(layer, false);
        if (SpriteSystem.LayerExists(ent.Owner, ent.Comp.EffectLayer))
            SpriteSystem.LayerSetVisible((ent.Owner, sprite), ent.Comp.EffectLayer, false);

        if (device == CMUFobDeviceAnimation.Target)
        {
            var progress = Ease(age / Math.Max(0.1f, animation.LockDuration));
            SpriteSystem.LayerSetScale((ent.Owner, sprite), 0, new Vector2(1.6f - progress * 0.65f));
            SpriteSystem.LayerSetColor((ent.Owner, sprite), 0, new Color(1f, 0.65f - progress * 0.38f, 0f));
            return;
        }

        var stateId = SpriteSystem.LayerGetRsiState((ent.Owner, sprite), ent.Comp.BodyLayer, default);
        if (!SpriteSystem.TryGetLayer((ent.Owner, sprite), ent.Comp.BodyLayer, out var body, false)
            || body.ActualRsi == null || !body.ActualRsi.TryGetState(stateId, out var source))
            return;

        AppearanceSystem.TryGetData<bool>(ent, CMUFobVisuals.Stowed, out var stowed);
        if (gun)
        {
            AppearanceSystem.TryGetData<bool>(ent, CMUFobGunVisuals.Installed, out var installed);
            stowed = !installed;
        }
        if (TryComp<CMUFobEquipmentComponent>(ent, out var equipment))
            stowed = stateId == equipment.StowedState && device != CMUFobDeviceAnimation.Generator;

        SpriteSystem.LayerSetOffset(body, Vector2.Zero);
        SpriteSystem.LayerSetColor(body, Color.White);
        SpriteSystem.LayerSetVisible(body, stowed && !gun && device != CMUFobDeviceAnimation.Terminal);
        if (stowed)
            return;
        // The inherited crate base and door both contain the entire closed crate artwork.
        if (device == CMUFobDeviceAnimation.Crate && SpriteSystem.LayerExists(ent.Owner, "fob-crate-base"))
            SpriteSystem.LayerSetVisible((ent, sprite), "fob-crate-base", false);
        if (device == CMUFobDeviceAnimation.Terminal && SpriteSystem.LayerExists(ent.Owner, "broken"))
            SpriteSystem.LayerSetVisible((ent, sprite), "broken", false);

        // Keep the authored fence animation, including its quiet intervals between arcs.
        if (device == CMUFobDeviceAnimation.Fence)
        {
            SpriteSystem.LayerSetVisible(body, true);
            return;
        }

        var worldFacing = _transform.GetWorldRotation(ent);
        var eyeRotation = _eye.CurrentEye?.Rotation ?? Angle.Zero;
        var facing = (worldFacing + eyeRotation).Reduced().FlipPositive();
        var direction = SpriteComponent.Layer.GetDirection(source.RsiDirections, facing);
        if (device == CMUFobDeviceAnimation.HeavyGun)
        {
            var target = active && animation.Aim.LengthSquared() > 0 ? animation.Aim.ToWorldAngle() : worldFacing;
            var current = animation.TrackingAngle ?? target;
            var delta = Angle.ShortestDistance(current, target).Theta;
            var step = MathHelper.DegreesToRadians(ent.Comp.TurnSpeed) * frameTime;
            animation.TrackingAngle = (current + Math.Clamp(delta, -step, step)).Reduced();
            facing = (animation.TrackingAngle.Value + eyeRotation).Reduced().FlipPositive();
            // Use the tank turret's cardinal sectors and counter-rotation, with a fixed FOB foundation.
            var cardinal = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(facing);
            direction = cardinal.Convert(source.RsiDirections);
            animation.Yaw = (float) Angle.ShortestDistance(cardinal.ToAngle(), facing).Theta;
        }
        if (animation.Source != source || animation.Direction != direction)
        {
            animation.Source = source;
            animation.Direction = direction;
            animation.Parts.Clear();
            animation.Parts.AddRange(GetParts(source, direction, device));
        }

        var forward = direction switch
        {
            RsiDirection.North => Vector2.UnitY,
            RsiDirection.East => Vector2.UnitX,
            RsiDirection.West => -Vector2.UnitX,
            _ => -Vector2.UnitY,
        };
        // The launcher tubes fire vertically in the artwork; only the heavy turret traverses.
        var yaw = new Angle(device == CMUFobDeviceAnimation.HeavyGun ? animation.Yaw : 0);
        var deploy = animation.Cue == CMUFobPresentationCue.Deploy ? 1 - Ease(cueAge / 0.8f) : 0;
        var tint = animation.Status == CMUFobPresentationStatus.Broken ? Color.FromHex("#444444") : Color.White;
        var index = 0;
        if (device == CMUFobDeviceAnimation.Crate)
        {
            // A dark interior behind the hinged lid prevents the floor showing through the open case.
            var interior = GetLayer(ent, sprite, animation, index++);
            SpriteSystem.LayerSetTexture(interior, Texture.White);
            SpriteSystem.LayerSetVisible(interior, true);
            SpriteSystem.LayerSetOffset(interior, new Vector2(0, 3f / 32));
            SpriteSystem.LayerSetScale(interior, new Vector2(22f / Texture.White.Width, 18f / Texture.White.Height));
            SpriteSystem.LayerSetRotation(interior, Angle.Zero);
            SpriteSystem.LayerSetColor(interior, Color.FromHex("#111715"));
        }
        foreach (var part in animation.Parts)
        {
            var center = part.Bounds.Center;
            var offset = new Vector2(center.X - source.Size.X / 2f, source.Size.Y / 2f - center.Y);
            var scale = Vector2.One;
            var rotation = Angle.Zero;
            var move = part.MovingPart;
            switch (device)
            {
                case CMUFobDeviceAnimation.HeavyGun when move >= 0:
                    var stroke = move == animation.ShotSequence % 2 ? Kick(shotAge) : 0;
                    if (starting)
                        stroke += Kick(age - 0.25f - move * 0.45f, 0.35f);
                    offset -= forward * (stroke * 4 + (working ? 3 * Ease(age / 0.3f) : 0));
                    break;
                case CMUFobDeviceAnimation.Rocket when move == 0:
                    offset.Y -= (starting ? 1 - Ease(age / 0.9f) : deploy) * 5;
                    offset.Y -= Kick(shotAge, 0.45f) * 7;
                    rotation = new Angle(working ? Ease(age / 0.4f) * 0.08f : 0);
                    break;
                case CMUFobDeviceAnimation.Platform when move >= 0:
                    offset.X += (move == 0 ? 1 : -1) * deploy * 7;
                    break;
                case CMUFobDeviceAnimation.Light when move == 0:
                    offset.Y -= deploy * 40;
                    break;
                case CMUFobDeviceAnimation.Light when move == 1:
                    scale.Y = 1 - deploy * 0.9f;
                    offset.Y -= deploy * 20;
                    break;
                case CMUFobDeviceAnimation.Sensor when move == 0:
                    rotation = new Angle(active ? MathF.Sin(age * 1.4f) * 0.2f : 0);
                    offset.Y -= deploy * 7;
                    break;
                case CMUFobDeviceAnimation.Relay when move == 0 && working:
                    offset.X -= 3 * Ease(age / 0.3f);
                    break;
                case CMUFobDeviceAnimation.Terminal when move == 1 && working:
                    offset.Y -= 3 * Ease(age / 0.3f);
                    break;
                case CMUFobDeviceAnimation.Crate when move == 0:
                    var open = animation.Open ? Ease(cueAge / 0.45f) : 1 - Ease(cueAge / 0.35f);
                    offset.Y += open * 6;
                    scale.Y = 1 - open * 0.65f;
                    break;
            }
            offset.Y -= gun ? deploy * 5 : 0;
            var layer = GetLayer(ent, sprite, animation, index++);
            SpriteSystem.LayerSetTexture(layer, part.Texture);
            SpriteSystem.LayerSetVisible(layer, true);
            SpriteSystem.LayerSetOffset(layer, yaw.RotateVec(offset / 32));
            SpriteSystem.LayerSetScale(layer, scale);
            SpriteSystem.LayerSetRotation(layer, yaw + rotation);
            SpriteSystem.LayerSetColor(layer, tint);
        }

        DrawIndicator(ent, sprite, animation, source, yaw, age, ref index);
        if (gun && shotAge < (device == CMUFobDeviceAnimation.Rocket ? 0.55f : 0.075f))
        {
            var rocket = device == CMUFobDeviceAnimation.Rocket;
            var flash = SpriteSystem.GetState(new SpriteSpecifier.Rsi(new ResPath(rocket
                ? "_RMC14/Effects/grenade_smoke.rsi" : "_RMC14/Objects/Weapons/Guns/Projectiles/projectiles.rsi"),
                rocket ? "smoke" : "muzzle_bullet_high_RMC"));
            var firstBarrel = animation.ShotSequence % 2 == 0;
            var tip = rocket ? new Vector2(14, -9 - shotAge * 18) : direction switch
            {
                RsiDirection.East => new Vector2(31, firstBarrel ? 5 : -2),
                RsiDirection.West => new Vector2(-31, firstBarrel ? 5 : -2),
                RsiDirection.North => new Vector2(firstBarrel ? -5 : 4, 18),
                _ => new Vector2(firstBarrel ? -5 : 4, -21),
            };
            var layer = GetLayer(ent, sprite, animation, index++);
            SpriteSystem.LayerSetTexture(layer, flash.GetFrame(RsiDirection.South, Math.Min((int) (shotAge / flash.GetDelay(0)), flash.DelayCount - 1)));
            SpriteSystem.LayerSetVisible(layer, true);
            SpriteSystem.LayerSetOffset(layer, yaw.RotateVec(tip / 32));
            SpriteSystem.LayerSetScale(layer, new Vector2(rocket ? 0.45f + shotAge : 0.45f));
            SpriteSystem.LayerSetRotation(layer, rocket ? Angle.Zero : new Angle(MathF.Atan2(forward.Y, forward.X)) + yaw);
            SpriteSystem.LayerSetColor(layer, Color.White.WithAlpha(rocket ? 0.8f * (1 - shotAge / 0.55f) : 1));
            if (rocket && shotAge < 0.12f)
            {
                var ignition = SpriteSystem.GetState(new SpriteSpecifier.Rsi(new ResPath("_RMC14/Effects/welding_effect.rsi"), "welding_sparks"));
                var spark = GetLayer(ent, sprite, animation, index);
                SpriteSystem.LayerSetTexture(spark, ignition.GetFrame(RsiDirection.South, Math.Min((int) (shotAge / ignition.GetDelay(0)), ignition.DelayCount - 1)));
                SpriteSystem.LayerSetVisible(spark, true);
                SpriteSystem.LayerSetOffset(spark, yaw.RotateVec(new Vector2(14, 12) / 32));
                SpriteSystem.LayerSetScale(spark, new Vector2(0.65f));
                SpriteSystem.LayerSetRotation(spark, yaw);
                SpriteSystem.LayerSetColor(spark, Color.White);
            }
        }
    }

    private void DrawIndicator(Entity<CMUFobPresentationComponent> ent, SpriteComponent sprite, CMUFobAnimationComponent animation,
        RSI.State source, Angle yaw, float age, ref int index)
    {
        if (animation.Status is CMUFobPresentationStatus.Off or CMUFobPresentationStatus.Broken or CMUFobPresentationStatus.Spent
            || ent.Comp.Device == CMUFobDeviceAnimation.Crate)
            return;
        var busy = animation.Status is CMUFobPresentationStatus.Starting or CMUFobPresentationStatus.Working;
        var warning = animation.Status == CMUFobPresentationStatus.Warning;
        if (warning ? age % 0.25f >= 0.12f : busy && age % 1 >= 0.5f)
            return;
        var pixel = ent.Comp.Device switch
        {
            CMUFobDeviceAnimation.HeavyGun => new Vector2(32, 26),
            CMUFobDeviceAnimation.Rocket => new Vector2(26, 40),
            CMUFobDeviceAnimation.Relay => new Vector2(13, 27),
            CMUFobDeviceAnimation.Terminal => new Vector2(16, 36),
            CMUFobDeviceAnimation.Light => new Vector2(16, 12),
            CMUFobDeviceAnimation.Sensor => new Vector2(16, 12),
            CMUFobDeviceAnimation.Generator => new Vector2(22, 13),
            _ => new Vector2(source.Size.X / 2f, source.Size.Y * 0.8f),
        };
        var color = animation.Status == CMUFobPresentationStatus.Empty ? Color.OrangeRed
            : busy || warning ? Color.Orange
            : animation.Status == CMUFobPresentationStatus.Ready ? Color.LightSkyBlue : Color.LightGreen;
        var layer = GetLayer(ent, sprite, animation, index++);
        SpriteSystem.LayerSetTexture(layer, Texture.White);
        SpriteSystem.LayerSetVisible(layer, true);
        SpriteSystem.LayerSetScale(layer, new Vector2((ent.Comp.Device == CMUFobDeviceAnimation.Terminal ? 12 : 2) / (float) Texture.White.Width, 1f / Texture.White.Height));
        SpriteSystem.LayerSetOffset(layer, yaw.RotateVec(new Vector2(pixel.X - source.Size.X / 2f, source.Size.Y / 2f - pixel.Y) / 32));
        SpriteSystem.LayerSetRotation(layer, yaw);
        SpriteSystem.LayerSetColor(layer, color);
    }

    private SpriteComponent.Layer GetLayer(Entity<CMUFobPresentationComponent> ent, SpriteComponent sprite, CMUFobAnimationComponent animation, int index)
    {
        while (animation.Layers.Count <= index)
            animation.Layers.Add(SpriteSystem.AddBlankLayer((ent, sprite)));
        return animation.Layers[index];
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUFobPresentationComponent, CMUFobAnimationComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var presentation, out var animation, out var sprite))
        {
            if (!MetaData(uid).EntityPaused)
                Draw((uid, presentation), sprite, animation, frameTime);
        }
    }
}
