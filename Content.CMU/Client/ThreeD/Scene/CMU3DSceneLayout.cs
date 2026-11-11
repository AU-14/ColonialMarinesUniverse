using System.Numerics;
using System.Linq;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Source sprite facing and grid-connected panel geometry, independent of the review camera.</summary>
public static class CMU3DSceneLayout
{
    // Same bit assignments as IconSmoothSystem.CardinalConnectDirs.
    public static readonly (int Flag, Vector2i Offset, int Turn)[] Cardinals =
    [
        (1, new Vector2i(0, 1), 1), (2, new Vector2i(0, -1), 3),
        (4, new Vector2i(1, 0), 0), (8, new Vector2i(-1, 0), 2),
    ];

    // Cardinal flags are unchanged; diagonal flags extend that mask.
    public static readonly (int Flag, Vector2i Offset)[] CornerNeighbours =
    [
        (1, new Vector2i(0, 1)), (2, new Vector2i(0, -1)),
        (4, new Vector2i(1, 0)), (8, new Vector2i(-1, 0)),
        (16, new Vector2i(1, 1)), (32, new Vector2i(1, -1)),
        (64, new Vector2i(-1, -1)), (128, new Vector2i(-1, 1)),
    ];

    /// <summary>Same CornerFill flags as IconSmoothSystem, in world-grid SE/NE/NW/SW order.</summary>
    public static int[] CornerStates(int mask) =>
    [
        ((mask & 4) != 0 ? 1 : 0) | ((mask & 32) != 0 ? 2 : 0) | ((mask & 2) != 0 ? 4 : 0),
        ((mask & 1) != 0 ? 1 : 0) | ((mask & 16) != 0 ? 2 : 0) | ((mask & 4) != 0 ? 4 : 0),
        ((mask & 8) != 0 ? 1 : 0) | ((mask & 128) != 0 ? 2 : 0) | ((mask & 1) != 0 ? 4 : 0),
        ((mask & 2) != 0 ? 1 : 0) | ((mask & 64) != 0 ? 2 : 0) | ((mask & 8) != 0 ? 4 : 0),
    ];

    public static CMU3DModelPart[] CornerParts(CMU3DModelPrototype model, int mask)
    {
        if (model.CornerSurfaces.Length != 32 || model.Parts.Count < 4)
            return model.Parts.ToArray();
        var states = CornerStates(mask);
        int[] directions = [0, 2, 1, 3]; // SE/NE/NW/SW use RSI S/E/N/W.
        var parts = model.Parts.ToArray();
        for (var i = 0; i < 4; i++)
        {
            var part = model.Parts[i];
            parts[i] = new CMU3DModelPart
            {
                Min = part.Min, Max = part.Max, Color = part.Color, Label = part.Label,
                Shape = CMU3DPartShape.Box, SurfaceAxis = CMU3DSurfaceAxis.XY,
                Surface = model.CornerSurfaces[states[i] * 4 + directions[i]],
            };
        }
        return parts;
    }

    public static float RenderYaw(CMU3DModelPrototype model, float yaw, bool noRotation, bool snapCardinals)
    {
        var directions = model.SourceDirections is 4 or 8 ? model.SourceDirections : 1;
        if (model.UseEntityRotation)
            return yaw + model.YawOffset * MathF.PI / 180;
        if (noRotation)
            yaw = directions == 1 ? 0 : RoundDirection(yaw, directions);
        else if (directions == 1 && snapCardinals)
            yaw -= RoundDirection(yaw, 4);
        if (model.SourceCardinalFacings.Length == 4 && directions == 4)
        {
            var index = CardinalIndex(yaw);
            yaw += (model.SourceCardinalFacings[index] - index) * MathF.PI / 2;
        }
        if (model.SwapEastWest && directions == 4 && MathF.Abs(MathF.Sin(RoundDirection(yaw, 4))) > .5f)
            yaw += MathF.PI;
        return yaw + model.YawOffset * MathF.PI / 180;
    }

    public static Direction ReferenceDirection(CMU3DModelPrototype model, Direction direction)
    {
        if (model.ReferenceDirection is { } frame)
            return frame switch
            {
                1 => Direction.North, 2 => Direction.East, 3 => Direction.West,
                4 => Direction.SouthEast, 5 => Direction.SouthWest,
                6 => Direction.NorthEast, 7 => Direction.NorthWest, _ => Direction.South,
            };
        if (model.SourceDirections != 4)
            return direction;
        if (model.SwapEastWest)
            direction = direction switch { Direction.East => Direction.West, Direction.West => Direction.East, _ => direction };
        if (model.SourceCardinalFacings.Length != 4)
            return direction;
        var desired = direction switch { Direction.East => 1, Direction.North => 2, Direction.West => 3, _ => 0 };
        // Some vehicle sheets repeat only two physical views across four source directions.
        // Prefer the requested source slot on ties, then the nearest available physical view.
        var source = desired;
        var bestDistance = int.MaxValue;
        for (var step = 0; step < 4; step++)
        {
            var candidate = (desired + step) % 4;
            var facing = model.SourceCardinalFacings[candidate];
            if (facing is < 0 or > 3)
                continue;
            var distance = Math.Abs(facing - desired);
            distance = Math.Min(distance, 4 - distance);
            if (distance >= bestDistance)
                continue;
            source = candidate;
            bestDistance = distance;
        }
        return source switch { 1 => Direction.East, 2 => Direction.North, 3 => Direction.West, _ => Direction.South };
    }

    private static int CardinalIndex(float yaw) => ((int) MathF.Floor(yaw / (MathF.PI / 2) + .5f) % 4 + 4) % 4;

    public static bool IsApplianceBacking(CMU3DModelPrototype model, string key) =>
        key == "walls" || model.FaceAwayFromWindows && key == "windows";

    public static float FaceAwayFromWallYaw(float yaw, float gridYaw, int walls)
    {
        var local = yaw - gridYaw;
        foreach (var (flag, offset, _) in Cardinals)
        {
            if ((walls & flag) == 0)
                continue;
            var opposite = flag switch { 1 => 2, 2 => 1, 4 => 8, _ => 4 };
            var facing = MathF.Sin(local) * offset.X - MathF.Cos(local) * offset.Y;
            if (walls == flag || ((walls & opposite) == 0 && facing > .99999f))
                return gridYaw + MathF.Atan2(-offset.X, offset.Y);
        }
        return yaw;
    }

    public static float OpeningFixtureYaw(float yaw, float gridYaw, int walls, float fixtureYaw)
    {
        int Flag(float angle)
        {
            var turn = (angle - gridYaw) / (MathF.PI / 2);
            if (MathF.Abs(turn - MathF.Round(turn)) > .00001f)
                return 0;
            return CardinalIndex(angle - gridYaw) switch { 0 => 2, 1 => 4, 2 => 1, _ => 8 };
        }
        var front = Flag(yaw);
        var candidate = Flag(fixtureYaw);
        return front != 0 && candidate != 0 && (walls & front) != 0 && (walls & candidate) == 0 ? fixtureYaw : yaw;
    }

    public static float WallTargetYaw(float yaw, float gridYaw, bool onWall, int targets, int walls)
    {
        foreach (var (flag, offset, _) in Cardinals)
        {
            if (targets != flag)
                continue;
            if (onWall)
                return gridYaw + MathF.Atan2(offset.X, -offset.Y);
            var opposite = flag switch { 1 => 2, 2 => 1, 4 => 8, _ => 4 };
            // A room-tile fixture uses the mirrored inner-wall geometry and points its mounting axis at the wall.
            if ((walls & opposite) != 0)
                return gridYaw + MathF.Atan2(-offset.X, offset.Y);
        }
        return yaw;
    }

    public static Color PresentationTint(CMU3DModelPrototype model, Color liveTint)
    {
        var baked = model.BakedSpriteTint;
        return new Color(
            baked.R > 0 ? liveTint.R / baked.R : 1,
            baked.G > 0 ? liveTint.G / baked.G : 1,
            baked.B > 0 ? liveTint.B / baked.B : 1,
            baked.A > 0 ? liveTint.A / baked.A : 1);
    }

    /// <summary>Resolve an offset target only when it selects one clear grid side.</summary>
    public static int OffsetTargetMask(Vector2 delta, float gridYaw)
    {
        var c = MathF.Cos(gridYaw);
        var s = MathF.Sin(gridYaw);
        var local = new Vector2(c * delta.X + s * delta.Y, -s * delta.X + c * delta.Y);
        if (MathF.Abs(local.X) >= .25f && MathF.Abs(local.Y) <= .125f)
            return local.X > 0 ? 4 : 8;
        if (MathF.Abs(local.Y) >= .25f && MathF.Abs(local.X) <= .125f)
            return local.Y > 0 ? 1 : 2;
        return 0;
    }

    /// <summary>Snap mounting depth to the tile face while preserving authored spacing along the wall.</summary>
    public static Vector2 WallMountOffset(Vector2 position, Vector2 tileCenter, float yaw)
    {
        var normal = new Vector2(MathF.Sin(yaw), -MathF.Cos(yaw));
        return -Vector2.Dot(position - tileCenter, normal) * normal;
    }

    /// <summary>Only these source pairs define an exterior face beyond ordinary thin glazing.</summary>
    public static bool IsShutterExteriorTarget(string modelId, string prototype) =>
        modelId is "CMU3DHybrisaWindowShutter" or "CMU3DHybrisaWindowShutterOpen" &&
        prototype is "RMCDoubleDoorGlassHybrisa" or "CMAirlockGlassHybrisa";

    public static int? ShutterExteriorAxis(string prototype, int mask)
    {
        if (prototype == "RMCWindowPrisonCell")
        {
            if ((mask & 3) != 0 && (mask & 12) == 0)
                return 0;
            if ((mask & 12) != 0 && (mask & 3) == 0)
                return 1;
            return null;
        }
        return prototype is "RMCDoubleDoorGlassHybrisa" or "CMAirlockGlassHybrisa" ? 1 : null;
    }

    /// <summary>Negative/positive facade bits for an exact, co-pivot parallel prison shutter.</summary>
    public static int PrisonShutterSide(string prototype, string modelId, int? axis, float yaw,
        float windowYaw, Vector2 delta, bool inside = false)
    {
        if (prototype is not ("RMCShutterHybrisaWindow" or "RMCShutterHybrisaWindowOpen") ||
            modelId is not ("CMU3DHybrisaWindowShutter" or "CMU3DHybrisaWindowShutterOpen") ||
            inside || axis is not (0 or 1) || !float.IsFinite(yaw) || !float.IsFinite(windowYaw) ||
            !float.IsFinite(delta.X) || !float.IsFinite(delta.Y) || delta.LengthSquared() > .0000000001f)
            return 0;
        var normal = new Vector2(MathF.Sin(yaw - windowYaw), -MathF.Cos(yaw - windowYaw));
        if (MathF.Abs(axis == 0 ? normal.Y : normal.X) > .00001f)
            return 0;
        return (axis == 0 ? normal.X : normal.Y) > 0 ? 2 : 1;
    }

    /// <summary>
    /// Reserve shutter depth inside the exact wall-frame draft. The .305 backing
    /// face is .45 outer assembly minus .125 shutter and .02 gap, inferred from
    /// neighboring trim. Keep both facade profiles, glazing and the opposite half.
    /// </summary>
    public static bool TryPrisonRecessParts(IReadOnlyList<CMU3DModelPart> parts, int axis, int sides,
        out CMU3DModelPart[] recessed)
    {
        recessed = [];
        if (parts.Count == 0 || axis is not (0 or 1) || sides is not (1 or 2 or 3) ||
            parts.Any(p => !p.Valid || p.Shape != CMU3DPartShape.Box || p.Yaw != 0 || p.Pitch != 0 || p.Surface != null))
            return false;
        var glazing = -1f;
        var negative = 0f;
        var positive = 0f;
        foreach (var p in parts)
        {
            var low = axis == 0 ? p.Min.X : p.Min.Y;
            var high = axis == 0 ? p.Max.X : p.Max.Y;
            negative = MathF.Max(negative, -low);
            positive = MathF.Max(positive, high);
            if (p.Color.A < 1)
                glazing = MathF.Max(glazing, MathF.Max(MathF.Abs(low), MathF.Abs(high)));
        }
        // Unknown future shapes/depths must be authored and reviewed separately.
        if (MathF.Abs(glazing - .045f) > .00001f ||
            (sides & 1) != 0 && MathF.Abs(negative - .525f) > .00001f ||
            (sides & 2) != 0 && MathF.Abs(positive - .525f) > .00001f)
            return false;
        recessed = new CMU3DModelPart[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            var min = p.Min;
            var max = p.Max;
            if (axis == 0)
            {
                min.X = Map(min.X);
                max.X = Map(max.X);
            }
            else
            {
                min.Y = Map(min.Y);
                max.Y = Map(max.Y);
            }
            recessed[i] = new CMU3DModelPart
            {
                Min = min, Max = max, Label = p.Label, Shape = p.Shape, Color = p.Color,
                Yaw = p.Yaw, Pitch = p.Pitch, Surface = p.Surface, SurfaceAxis = p.SurfaceAxis,
                SurfaceFlipU = p.SurfaceFlipU, OmitWhenConnected = p.OmitWhenConnected,
            };
        }
        return true;

        float Map(float value)
        {
            var side = value > 0 ? 2 : 1;
            var depth = MathF.Abs(value);
            if (depth <= .045f || (sides & side) == 0)
                return value;
            var oldFace = value > 0 ? positive : negative;
            var mapped = .045f + (depth - .045f) * (.305f - .045f) / (oldFace - .045f);
            return value > 0 ? mapped : -mapped;
        }
    }

    /// <summary>Local wall-face bits (-X,+X,-Y,+Y) at the end of a parallel window opening.</summary>
    public static int PrisonWallJoinFace(Vector2 delta, float wallYaw, float gridYaw, int windowMask)
    {
        if (ShutterExteriorAxis("RMCWindowPrisonCell", windowMask) is not { } axis ||
            !float.IsFinite(delta.X) || !float.IsFinite(delta.Y) || !float.IsFinite(wallYaw) || !float.IsFinite(gridYaw))
            return 0;
        var c = MathF.Cos(gridYaw);
        var s = MathF.Sin(gridYaw);
        var local = new Vector2(c * delta.X + s * delta.Y, -s * delta.X + c * delta.Y);
        if (MathF.Abs(axis == 0 ? local.X : local.Y) > .00001f ||
            MathF.Abs(MathF.Abs(axis == 0 ? local.Y : local.X) - 1) > .00001f)
            return 0;
        c = MathF.Cos(wallYaw);
        s = MathF.Sin(wallYaw);
        local = new Vector2(c * delta.X + s * delta.Y, -s * delta.X + c * delta.Y);
        if (MathF.Abs(MathF.Abs(local.X) - 1) < .00001f && MathF.Abs(local.Y) < .00001f)
            return local.X > 0 ? 2 : 1;
        if (MathF.Abs(MathF.Abs(local.Y) - 1) < .00001f && MathF.Abs(local.X) < .00001f)
            return local.Y > 0 ? 8 : 4;
        return 0;
    }

    /// <summary>Inset only inferred relief on an internal source-connected wall/window joint.</summary>
    public static bool TryPrisonJoinedReliefParts(IReadOnlyList<CMU3DModelPart> parts, int faces,
        out CMU3DModelPart[] joined)
    {
        joined = [];
        if (parts.Count == 0 || faces is < 1 or > 15 || parts.Any(p => !p.Valid ||
                p.Shape != CMU3DPartShape.Box || p.Yaw != 0 || p.Pitch != 0 || p.Surface != null || p.Color.A != 1))
            return false;
        var cores = parts.Where(p => p.Label == "full tile wall core").ToArray();
        if (cores.Length != 1 || cores[0].Min != new Vector3(-.5f,-.5f,0) || cores[0].Max != new Vector3(.5f,.5f,2.8f))
            return false;
        var core = cores[0];
        var result = new CMU3DModelPart[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            var low = p.Min;
            var high = p.Max;
            if (p != core)
            {
                for (var bit = 0; bit < 4; bit++)
                {
                    if ((faces & (1 << bit)) == 0)
                        continue;
                    var x = bit < 2;
                    var sign = bit % 2 == 0 ? -1 : 1;
                    var a = sign * (x ? low.X : low.Y);
                    var b = sign * (x ? high.X : high.Y);
                    if (a > b) (a, b) = (b, a);
                    if (b <= .500001f)
                        continue;
                    if (b > .55f || low.Z < 0 || high.Z > 2.8f)
                        return false;
                    if (a >= .499999f)
                        a -= b - .49f;
                    b = .49f;
                    var lo = MathF.Min(sign * a, sign * b);
                    var hi = MathF.Max(sign * a, sign * b);
                    if (x) { low.X = lo; high.X = hi; }
                    else { low.Y = lo; high.Y = hi; }
                }
                // Every changed part removes occupied volume or moves inside the
                // already opaque core. It cannot introduce a neighbor collision.
                var subset = low.X >= p.Min.X && low.Y >= p.Min.Y && low.Z >= p.Min.Z &&
                             high.X <= p.Max.X && high.Y <= p.Max.Y && high.Z <= p.Max.Z;
                var buried = low.X >= core.Min.X && low.Y >= core.Min.Y && low.Z >= core.Min.Z &&
                             high.X <= core.Max.X && high.Y <= core.Max.Y && high.Z <= core.Max.Z;
                if (!subset && !buried)
                    return false;
            }
            result[i] = new CMU3DModelPart
            {
                Min = low, Max = high, Label = p.Label, Shape = p.Shape, Color = p.Color,
                Yaw = p.Yaw, Pitch = p.Pitch, Surface = p.Surface, SurfaceAxis = p.SurfaceAxis,
                SurfaceFlipU = p.SurfaceFlipU, OmitWhenConnected = p.OmitWhenConnected,
            };
        }
        joined = result;
        return true;
    }

    /// <summary>A stable authored envelope prevents the mount moving when its supporting door changes pose.</summary>
    public static bool TryShutterMountEnvelope(CMU3DModelPrototype model, CMU3DModelPrototype? alternate,
        out IReadOnlyList<CMU3DModelPart> parts)
    {
        parts = model.Parts;
        if (model.AlternateDoorModel != null && alternate == null)
            return false;
        if (alternate != null && (model.YawOffset != alternate.YawOffset ||
            model.SourceDirections != alternate.SourceDirections || model.UseEntityRotation != alternate.UseEntityRotation ||
            model.SwapEastWest != alternate.SwapEastWest || model.GroundOffset != alternate.GroundOffset ||
            !model.SourceCardinalFacings.SequenceEqual(alternate.SourceCardinalFacings)))
            return false;
        List<CMU3DModelPart> envelope = [];
        Add(model);
        if (alternate != null)
            Add(alternate);
        parts = envelope;
        return true;

        void Add(CMU3DModelPrototype item)
        {
            envelope.AddRange(item.Parts);
            foreach (var state in item.DoorSpriteStates.Values)
            foreach (var frame in state.Frames)
                envelope.AddRange(frame.Parts);
        }
    }

    /// <summary>Use the saved shutter facing to fit its rear just outside a parallel glazing assembly.</summary>
    public static bool TryWindowMountOffset(IReadOnlyList<CMU3DModelPart> parts, float yaw,
        IReadOnlyList<CMU3DModelPart> windowParts, float windowYaw, Vector2 delta, out Vector2 offset, bool inside = false,
        int? exteriorAxis = null)
    {
        offset = Vector2.Zero;
        if (parts.Count == 0 || windowParts.Count == 0)
            return false;
        var low = new Vector3(float.PositiveInfinity);
        var high = new Vector3(float.NegativeInfinity);
        foreach (var part in windowParts)
        {
            part.Bounds(out var min, out var max);
            low = Vector3.Min(low, min);
            high = Vector3.Max(high, max);
        }
        var size = high - low;
        var side = inside ? -1 : 1;
        var local = side * new Vector2(MathF.Sin(yaw - windowYaw), -MathF.Cos(yaw - windowYaw));
        if (exteriorAxis is { } axis)
        {
            if (inside || axis is not (0 or 1) || delta.LengthSquared() > .0000000001f ||
                MathF.Abs(axis == 0 ? local.Y : local.X) > .00001f)
                return false;
        }
        else if (MathF.Abs(size.X - size.Y) < .00001f || MathF.Abs(size.X > size.Y ? local.X : local.Y) > .00001f)
            return false;
        var normal = side * new Vector2(MathF.Sin(yaw), -MathF.Cos(yaw));
        var face = Vector2.Dot(delta, normal) + MathF.Max(low.X * local.X, high.X * local.X) + MathF.Max(low.Y * local.Y, high.Y * local.Y);
        var back = float.NegativeInfinity;
        foreach (var part in parts)
        {
            part.Bounds(out var min, out var max);
            back = MathF.Max(back, inside ? -min.Y : max.Y);
        }
        offset = MathF.Max(0, face + back + .02f) * normal;
        return true;
    }

    /// <summary>Conservative horizontal opening clearance; tall end trim defines the opening at every elevation.</summary>
    public static Vector2 PanelEndLimits(IReadOnlyList<CMU3DModelPart> parts, float yaw,
        IReadOnlyList<CMU3DModelPart> obstacles, float obstacleYaw, Vector2 delta)
    {
        var ends = new Vector2(parts.Min(p => p.Min.X), parts.Max(p => p.Max.X));
        var lowY = parts.Min(p => p.Min.Y);
        var highY = parts.Max(p => p.Max.Y);
        var turn = (obstacleYaw - yaw) / (MathF.PI / 2);
        if (MathF.Abs(turn - MathF.Round(turn)) > .00001f)
            return ends;
        var c = MathF.Cos(yaw);
        var s = MathF.Sin(yaw);
        var localDelta = new Vector2(c * delta.X + s * delta.Y, -s * delta.X + c * delta.Y);
        c = MathF.Cos(obstacleYaw - yaw);
        s = MathF.Sin(obstacleYaw - yaw);
        List<(Vector2 Low, Vector2 High)> bounds = [];
        foreach (var part in obstacles)
        {
            if (part.Shape != CMU3DPartShape.Box)
                continue;
            part.Bounds(out var min, out var max);
            var low = new Vector2(float.PositiveInfinity);
            var high = new Vector2(float.NegativeInfinity);
            foreach (var x in new[] { min.X, max.X })
            foreach (var y in new[] { min.Y, max.Y })
            {
                var corner = new Vector2(c * x - s * y, s * x + c * y) + localDelta;
                low = Vector2.Min(low, corner);
                high = Vector2.Max(high, corner);
            }
            bounds.Add((low, high));
        }
        // A wall across the panel's pivot is a front/rear obstruction, not two end supports.
        if (bounds.Count == 0 || bounds.Min(b => b.Low.X) <= 0 && bounds.Max(b => b.High.X) >= 0)
            return ends;
        foreach (var (low, high) in bounds)
        {
            if (MathF.Min(high.Y, highY) - MathF.Max(low.Y, lowY) <= .00001f)
                continue;
            if (low.X > 0) ends.Y = MathF.Min(ends.Y, low.X - .01f);
            if (high.X < 0) ends.X = MathF.Max(ends.X, high.X + .01f);
        }
        return ends;
    }

    /// <summary>Fit all frame members proportionally, keeping caps and rails instead of clipping them away.</summary>
    public static IReadOnlyList<CMU3DModelPart> FitPanelEnds(IReadOnlyList<CMU3DModelPart> parts, Vector2 ends)
    {
        if (parts.Count == 0 || parts.Any(p => p.Shape != CMU3DPartShape.Box || p.Yaw != 0 || p.Pitch != 0))
            return parts;
        var left = parts.Min(p => p.Min.X);
        var right = parts.Max(p => p.Max.X);
        if (!float.IsFinite(ends.X) || !float.IsFinite(ends.Y) ||
            ends.X < left || ends.X > left + .18f || ends.Y < right - .18f || ends.Y > right ||
            ends.Y - ends.X < (right - left) * .7f || MathF.Abs(ends.X - left) + MathF.Abs(ends.Y - right) < .00001f)
            return parts;
        var scale = (ends.Y - ends.X) / (right - left);
        return parts.Select(p => new CMU3DModelPart
        {
            Min = new Vector3(ends.X + (p.Min.X - left) * scale, p.Min.Y, p.Min.Z),
            Max = new Vector3(ends.X + (p.Max.X - left) * scale, p.Max.Y, p.Max.Z),
            Label = p.Label, Color = p.Color, Shape = p.Shape,
            Surface = p.Surface, SurfaceAxis = p.SurfaceAxis, SurfaceFlipU = p.SurfaceFlipU,
            OmitWhenConnected = p.OmitWhenConnected,
        }).ToArray();
    }

    /// <summary>Clear a rear wall toward the saved front, using only solid parts at the fixture's height and frontage.</summary>
    public static bool TryBackWallMountOffset(IReadOnlyList<CMU3DModelPart> parts, float yaw,
        IReadOnlyList<CMU3DModelPart> wallParts, float wallYaw, Vector2 delta, out Vector2 offset, bool roomSide = false,
        float heightOffset = 0)
    {
        offset = Vector2.Zero;
        if (roomSide)
            yaw += MathF.PI;
        var c = MathF.Cos(yaw);
        var s = MathF.Sin(yaw);
        var localDelta = new Vector2(c * delta.X + s * delta.Y, -s * delta.X + c * delta.Y);
        var turns = (wallYaw - yaw) / (MathF.PI / 2);
        if (parts.Count == 0 || wallParts.Count == 0 || localDelta.Y < -.00001f || localDelta.Y > 1.01f ||
            MathF.Abs(turns - MathF.Round(turns)) > .00001f)
            return false;
        var turn = ((int) MathF.Round(turns) % 4 + 4) % 4;
        var distance = 0f;
        foreach (var wall in wallParts)
        {
            if (wall.Shape != CMU3DPartShape.Box || wall.Surface != null)
                continue;
            wall.Bounds(out var min, out var max);
            for (var i = 0; i < turn; i++)
                (min, max) = (new Vector3(-max.Y, min.X, min.Z), new Vector3(-min.Y, max.X, max.Z));
            min += new Vector3(localDelta, 0);
            max += new Vector3(localDelta, 0);
            foreach (var part in parts)
            {
                part.Bounds(out var a, out var b);
                a.Z += heightOffset;
                b.Z += heightOffset;
                if (roomSide)
                    (a, b) = (new Vector3(-b.X, 1 + a.Y, a.Z), new Vector3(-a.X, 1 + b.Y, b.Z));
                if (MathF.Min(b.X, max.X) - MathF.Max(a.X, min.X) <= .00001f ||
                    MathF.Min(b.Z, max.Z) - MathF.Max(a.Z, min.Z) <= .00001f)
                    continue;
                distance = MathF.Max(distance, b.Y - min.Y + .01f);
            }
        }
        if (distance <= .00001f || distance > .75f)
            return false;
        offset = distance * new Vector2(s, -c);
        return true;
    }

    private static float RoundDirection(float yaw, int directions)
    {
        var step = MathF.Tau / directions;
        return MathF.Floor(yaw / step + .5f) * step;
    }

    public static CMU3DModelPart[] InsideWallParts(IReadOnlyList<CMU3DModelPart> parts)
    {
        return parts.Select(part => new CMU3DModelPart
        {
            Min = new Vector3(part.Min.X, -1 - part.Max.Y, part.Min.Z),
            Max = new Vector3(part.Max.X, -1 - part.Min.Y, part.Max.Z),
            Color = part.Color,
            Shape = part.Shape,
            Surface = part.Surface,
            SurfaceAxis = part.SurfaceAxis,
            SurfaceFlipU = part.Surface != null && part.SurfaceAxis == CMU3DSurfaceAxis.XZ ? !part.SurfaceFlipU : part.SurfaceFlipU,
            OmitWhenConnected = part.OmitWhenConnected,
            Label = part.Label,
        }).ToArray();
    }

    public static CMU3DModelPart[] ConnectedParts(IReadOnlyList<CMU3DModelPart> parts, int mask, string? supportLabel = null, float endInset = 0)
    {
        if (!string.IsNullOrEmpty(supportLabel))
        {
            List<CMU3DModelPart> joined = [];
            foreach (var part in parts)
            {
                if (!part.Valid || (part.OmitWhenConnected & mask) != 0)
                    continue;
                var min = part.Min;
                var max = part.Max;
                if (part.Label == supportLabel)
                {
                    if ((mask & 8) != 0) min.X = -.5f;
                    if ((mask & 4) != 0) max.X = .5f;
                    if ((mask & 2) != 0) min.Y = -.5f;
                    if ((mask & 1) != 0) max.Y = .5f;
                }
                joined.Add(new CMU3DModelPart { Min = min, Max = max, Color = part.Color, Label = part.Label,
                    Shape = part.Shape, OmitWhenConnected = part.OmitWhenConnected });
            }
            return joined.ToArray();
        }
        var half = mask is not (0 or 1 or 2 or 3 or 4 or 8 or 12);
        var turns = mask is 0 or 4 or 8 or 12 ? new[] { 0 } :
            mask is 1 or 2 or 3 ? new[] { 1 } :
            Cardinals.Where(value => (mask & value.Flag) != 0).Select(value => value.Turn).ToArray();
        List<CMU3DModelPart> output = [];
        HashSet<(Vector3, Vector3, Color)> seen = [];
        foreach (var turn in turns)
        foreach (var part in parts)
        {
            if (!part.Valid)
                continue;
            // A panel's end-cap flags rotate with its authored axis.
            var omit = part.OmitWhenConnected;
            for (var i = 0; i < turn; i++)
                omit = ((omit & 1) << 3) | ((omit & 2) << 1) | ((omit & 4) >> 2) | ((omit & 8) >> 2);
            if ((omit & mask) != 0)
                continue;
            var min = part.Min;
            var max = part.Max;
            if (endInset > 0)
            {
                var west = turn switch { 0 => 8, 1 => 2, 2 => 4, _ => 1 };
                var east = turn switch { 0 => 4, 1 => 1, 2 => 8, _ => 2 };
                if ((mask & west) != 0 && MathF.Abs(min.X - (-.5f + endInset)) < .00001f) min.X = -.5f;
                if ((mask & east) != 0 && MathF.Abs(max.X - (.5f - endInset)) < .00001f) max.X = .5f;
            }
            if (half)
            {
                min.X = MathF.Max(0, min.X);
                if (max.X <= min.X)
                    continue;
            }
            for (var i = 0; i < turn; i++)
                (min, max) = (new Vector3(-max.Y, min.X, min.Z), new Vector3(-min.Y, max.X, max.Z));
            if (seen.Add((min, max, part.Color)))
                output.Add(new CMU3DModelPart { Min = min, Max = max, Color = part.Color, Label = part.Label, Shape = part.Shape });
        }
        return output.ToArray();
    }
}
