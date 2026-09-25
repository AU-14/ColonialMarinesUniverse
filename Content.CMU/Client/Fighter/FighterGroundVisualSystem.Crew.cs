using System.Numerics;
using Content.Client.Graphics;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Fighter;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterGroundVisualSystem
{
    private const string CrewMaskId = "fighter-cockpit";
    private static readonly ProtoId<ShaderPrototype> CrewMaskShader = "CMUFighterCockpitCrew";

    private sealed class CrewSpriteState(SpriteComponent sprite, ShaderInstance mask)
    {
        public readonly Vector2 Scale = sprite.Scale;
        public readonly Vector2 Offset = sprite.Offset;
        public readonly Color Color = sprite.Color;
        public readonly Angle Rotation = sprite.Rotation;
        public readonly bool NoRotation = sprite.NoRotation;
        public readonly bool EnableDirectionOverride = sprite.EnableDirectionOverride;
        public readonly Direction DirectionOverride = sprite.DirectionOverride;
        public readonly ShaderInstance Mask = mask;
    }

    private void InitializeCrewMask()
    {
        SubscribeLocalEvent<BuckleComponent, BeforePostShaderRenderEvent>(OnCrewShaderRender);
    }

    private ShaderInstance AddCrewMask(SpriteComponent sprite)
    {
        var shader = ProtoMan.Index(CrewMaskShader).InstanceUnique();
        _sprites.SetPostShader(sprite, new SpriteComponent.PostShaderArgs(CrewMaskId, shader)
        {
            RaiseShaderEvent = true,
            Before = ContentPostShaderIds.BeforeOutlines,
        });
        return shader;
    }

    private void OnCrewShaderRender(Entity<BuckleComponent> crew, ref BeforePostShaderRenderEvent args)
    {
        if (args.Id != CrewMaskId) return;
        args.Shader.SetParameter("clip_enabled", false);
        if (crew.Comp.BuckledTo is not { } seatUid || !TryComp(seatUid, out FighterSeatComponent? seat)) return;
        var canopyUid = TryComp(Transform(seatUid).ParentUid, out FighterGroundComponent? ground)
            ? ground.Canopy : CompOrNull<FighterAircraftComponent>(seat.Aircraft)?.Canopy;
        if (canopyUid is not { } canopy || !TryComp(canopy, out SpriteComponent? sprite)) return;

        // Apertures in the 266 x 335 airframe art, also used by cockpit-frame.swsl.
        // Clip the actual character, including equipment, rather than shrinking it.
        var center = seat.Pilot ? new Vector2(133, 228.5f) : new Vector2(133, 193.5f);
        var halfSize = seat.Pilot ? new Vector2(8.5f, 12.5f) : new Vector2(9.5f, 16.5f);
        var pixelScale = sprite.Scale / EyeManager.PixelsPerMeter * new Vector2(1, -1);
        var rotation = _transform.GetWorldRotation(canopy) + sprite.Rotation;
        var origin = _transform.GetWorldPosition(canopy) +
            _transform.GetWorldRotation(canopy).RotateVec(sprite.Offset) +
            rotation.RotateVec((center - new Vector2(133, 167.5f)) * pixelScale);
        var viewport = args.Viewport;
        var screenOrigin = ScreenUv(viewport, origin);
        var x = ScreenUv(viewport, origin + rotation.RotateVec(new Vector2(halfSize.X * pixelScale.X, 0))) - screenOrigin;
        var y = ScreenUv(viewport, origin + rotation.RotateVec(new Vector2(0, halfSize.Y * pixelScale.Y))) - screenOrigin;
        var determinant = x.X * y.Y - y.X * x.Y;
        if (MathF.Abs(determinant) < 1e-12f) return;

        // Reproject for each viewport, so turning, zooming and VTOL lift share
        // exactly the same moving opening as the canopy, without moving the eye.
        args.Shader.SetParameter("window_origin", screenOrigin);
        args.Shader.SetParameter("window_x", new Vector2(y.Y, -y.X) / determinant);
        args.Shader.SetParameter("window_y", new Vector2(-x.Y, x.X) / determinant);
        args.Shader.SetParameter("clip_enabled", true);
    }

    private static Vector2 ScreenUv(IClydeViewport viewport, Vector2 world)
    {
        var screen = viewport.WorldToLocal(world) / viewport.Size;
        return new Vector2(screen.X, 1 - screen.Y);
    }
}
