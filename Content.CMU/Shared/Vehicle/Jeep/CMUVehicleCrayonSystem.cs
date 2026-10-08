using Content.Shared._RMC14.Vehicle;
using Content.Shared.Charges.Systems;
using Content.Shared.Crayon;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using System.Numerics;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Vehicle.Jeep;

public abstract class SharedCMUVehicleCrayonSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public const int FrameSize = 96;
    public const float PixelsPerMeter = 32f;

    /// <summary>
    /// Camera-facing panel per direction, and the screen's right and down axes in vehicle space for
    /// top faces and for that panel.
    /// </summary>
    public static readonly Direction[] MapDirections = { Direction.South, Direction.North, Direction.East, Direction.West };

    private static readonly Dictionary<Direction, (string Normal, Vector3 Right, Vector3 TopDown)> Axes = new()
    {
        [Direction.South] = ("+f", new Vector3(0, -1, 0), new Vector3(1, 0, 0)),
        [Direction.North] = ("-f", new Vector3(0, 1, 0), new Vector3(-1, 0, 0)),
        [Direction.East] = ("+r", new Vector3(1, 0, 0), new Vector3(0, 1, 0)),
        [Direction.West] = ("-r", new Vector3(-1, 0, 0), new Vector3(0, -1, 0)),
    };

    private static readonly Vector3 FaceDown = new(0, 0, -1);

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleCrayonComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<CMUVehicleCrayonComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp(args.Used, out CrayonComponent? crayon))
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        if (_charges.IsEmpty(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-crayon-empty"), ent, args.User);
            return;
        }

        var direction = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(_transform.GetWorldRotation(ent));
        var offset = _transform.ToMapCoordinates(args.ClickLocation).Position - _transform.GetWorldPosition(ent);
        var x = (int) MathF.Floor(FrameSize / 2f + offset.X * PixelsPerMeter);
        var y = (int) MathF.Floor(FrameSize / 2f - offset.Y * PixelsPerMeter);
        if (!TryGetSurface(ent.Comp.Map, direction, x, y, out var voxel, out var front, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-crayon-miss"), ent, args.User);
            return;
        }

        var axes = Axes[direction];
        var drawing = new CMUCrayonDrawing
        {
            Decal = crayon.SelectedState,
            Color = crayon.Color,
            Anchor = voxel,
            Normal = front ? axes.Normal : "up",
            Right = axes.Right,
            Down = front ? FaceDown : axes.TopDown,
        };

        ent.Comp.Drawings.Add(drawing);
        if (ent.Comp.Drawings.Count > ent.Comp.MaxDrawings)
            ent.Comp.Drawings.RemoveAt(0);

        Dirty(ent);
        _charges.TryUseCharge(args.Used);
        _audio.PlayPvs(crayon.UseSound, ent);
    }

    /// <summary>
    /// The body voxel a pixel of a direction's frame shows, whether that is the camera-facing panel
    /// (else a top face), and whether it is drawn over the riders.
    /// </summary>
    public abstract bool TryGetSurface(ResPath map, Direction direction, int x, int y, out Vector3 voxel, out bool front, out bool overRiders);

    /// <summary>
    /// Decodes a crayon map pixel (R = f + 128, G = r + 128, B = 2z + front, A = 0 off the body, 128 over riders).
    /// </summary>
    protected static bool DecodeSurface(byte r, byte g, byte b, byte a, out Vector3 voxel, out bool front, out bool overRiders)
    {
        voxel = new Vector3(r - 128, g - 128, b / 2);
        front = b % 2 == 1;
        overRiders = a == 128;
        return a != 0;
    }

    protected static (int X, int Y) MapPixel(Direction direction, int x, int y)
    {
        var index = Array.IndexOf(MapDirections, direction);
        return (x + index % 2 * FrameSize, y + index / 2 * FrameSize);
    }

    /// <summary>
    /// Where a drawing paints one pixel of the decal, or null if this surface point isn't on its panel.
    /// </summary>
    public static Vector2i? DecalPixel(CMUCrayonDrawing drawing, Vector3 voxel, bool front, Direction direction)
    {
        var here = front ? Axes[direction].Normal : "up";
        if (here != drawing.Normal)
            return null;

        var d = voxel - drawing.Anchor;
        var depth = drawing.Normal switch
        {
            "up" => d.Z,
            "+f" or "-f" => d.X,
            _ => d.Y,
        };

        if (MathF.Abs(depth) > 2)
            return null;

        var u = 16 + (int) MathF.Round(Vector3.Dot(d, drawing.Right));
        var w = 16 + (int) MathF.Round(Vector3.Dot(d, drawing.Down));
        return u is >= 0 and < 32 && w is >= 0 and < 32 ? new Vector2i(u, w) : null;
    }
}
