using Content.Shared._RMC14.Vehicle;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Charges.Systems;
using Content.Shared.Crayon;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Vehicle.Jeep;

public abstract class SharedCMUVehicleCrayonSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public const int FrameSize = 96;
    public const float PixelsPerMeter = 32f;

    /// <summary>
    /// The most points a stroke batch may carry, and the longest line between two of them, in pixels.
    /// </summary>
    public const int MaxStrokePoints = 64;
    private const int MaxStrokeStep = 48;

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
        SubscribeLocalEvent<CrayonComponent, GetVerbsEvent<AlternativeVerb>>(OnCrayonVerbs);
        SubscribeNetworkEvent<CMUCrayonStrokeEvent>(OnStroke);
    }

    private void OnCrayonVerbs(Entity<CrayonComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_hands.IsHolding(args.User, ent))
            return;

        var free = HasComp<CMUCrayonFreeDrawComponent>(ent);
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(free ? "cmu-vehicle-crayon-mode-symbols" : "cmu-vehicle-crayon-mode-free"),
            Act = () =>
            {
                if (free)
                    RemComp<CMUCrayonFreeDrawComponent>(ent);
                else
                    EnsureComp<CMUCrayonFreeDrawComponent>(ent);

                _popup.PopupClient(Loc.GetString(free ? "cmu-vehicle-crayon-mode-symbols-set" : "cmu-vehicle-crayon-mode-free-set"), user, user);
            },
        });
    }

    private void OnInteractUsing(Entity<CMUVehicleCrayonComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp(args.Used, out CrayonComponent? crayon))
            return;

        args.Handled = true;
        // A free drawing crayon draws by dragging, which the client sends as strokes.
        if (_net.IsClient || HasComp<CMUCrayonFreeDrawComponent>(args.Used))
            return;

        if (_charges.IsEmpty(args.Used))
        {
            if (crayon.DeleteEmpty)
                UseUpCrayon(args.Used, args.User);
            else
                _popup.PopupEntity(Loc.GetString("cmu-vehicle-crayon-empty"), ent, args.User);

            return;
        }

        if (!TryGetClickedSurface(ent, args.ClickLocation, out var direction, out var voxel, out var front, out var panel) ||
            IsPanelOpen(CompOrNull<CMUJeepComponent>(ent.Owner), ent.Comp, panel))
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
        _adminLog.Add(LogType.CrayonDraw, LogImpact.Low,
            $"{ToPrettyString(args.User):user} drew a {crayon.Color:color} {crayon.SelectedState} on {ToPrettyString(ent):entity}");

        // As on the floor: a used up crayon is spent, otherwise its typed text moves on to the next decal.
        if (crayon.DeleteEmpty && _charges.IsEmpty(args.Used))
            UseUpCrayon(args.Used, args.User);
        else
            _ui.ServerSendUiMessage(args.Used, CrayonUiKey.Key, new CrayonUsedMessage(crayon.SelectedState));
    }

    /// <summary>
    /// Draws a batch of a stroke: lines between its points, traced onto the body under them as the
    /// vehicle is drawn from where it faces, a charge used up for every so many new dots.
    /// </summary>
    private void OnStroke(CMUCrayonStrokeEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryGetEntity(msg.Vehicle, out var vehicle) ||
            !TryComp(vehicle, out CMUVehicleCrayonComponent? comp) ||
            _hands.GetActiveItem(user) is not { } held ||
            !TryComp(held, out CrayonComponent? crayon) ||
            !TryComp(held, out CMUCrayonFreeDrawComponent? free) ||
            msg.Points.Count == 0 ||
            msg.Points.Count > MaxStrokePoints ||
            !_actionBlocker.CanInteract(user, vehicle) ||
            !_interaction.InRangeUnobstructed(user, vehicle.Value))
        {
            return;
        }

        Draw((vehicle.Value, comp), user, (held, crayon, free), msg.Points, msg.Start);
    }

    /// <summary>
    /// Draws a batch of a stroke with a crayon; returns how many dots it added.
    /// </summary>
    public int Draw(
        Entity<CMUVehicleCrayonComponent> vehicle,
        EntityUid user,
        Entity<CrayonComponent, CMUCrayonFreeDrawComponent> crayon,
        IReadOnlyList<Vector2> points,
        bool start)
    {
        var (held, crayonComp, free) = crayon;
        var comp = vehicle.Comp;
        if (_charges.IsEmpty(held))
        {
            if (crayonComp.DeleteEmpty)
                UseUpCrayon(held, user);
            else
                _popup.PopupEntity(Loc.GetString("cmu-vehicle-crayon-empty"), vehicle, user);

            return 0;
        }

        var jeep = CompOrNull<CMUJeepComponent>(vehicle);
        var (position, rotation) = _transform.GetWorldPositionRotation(vehicle);
        var direction = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(rotation);
        var turn = direction.ToAngle() - rotation;
        var index = new Dictionary<(Vector3, byte), int>();
        for (var i = 0; i < comp.Dots.Count; i++)
        {
            index[(comp.Dots[i].Voxel, comp.Dots[i].Face)] = i;
        }

        var added = new List<CMUCrayonDot>();
        var drawn = new HashSet<(Vector3, byte)>();
        var painted = new HashSet<int>();
        Vector2i? last = null;
        foreach (var point in points)
        {
            var offset = turn.RotateVec(point - position);
            var pixel = new Vector2i(
                (int) MathF.Floor(FrameSize / 2f + offset.X * PixelsPerMeter),
                (int) MathF.Floor(FrameSize / 2f - offset.Y * PixelsPerMeter));
            var from = last ?? pixel;
            last = pixel;
            if ((pixel - from).Length > MaxStrokeStep)
                continue;

            foreach (var (x, y) in Line(from, pixel))
            {
                if (!TryGetSurface(comp.Map, direction, x, y, out var voxel, out var front, out _, out var panel) ||
                    IsPanelOpen(jeep, comp, panel))
                {
                    continue;
                }

                var dot = new CMUCrayonDot
                {
                    Voxel = voxel,
                    Face = FaceOf(front, direction),
                    Color = crayonComp.Color,
                };

                var key = (dot.Voxel, dot.Face);
                if (!drawn.Add(key))
                    continue;

                if (index.TryGetValue(key, out var at))
                {
                    if (comp.Dots[at].Color == dot.Color)
                        continue;

                    painted.Add(at);
                }

                added.Add(dot);
            }
        }

        if (added.Count == 0)
            return 0;

        // Painted over dots are taken out; the new ones go on top.
        var n = 0;
        comp.Dots.RemoveAll(_ => painted.Contains(n++));
        comp.Dots.AddRange(added);
        if (comp.Dots.Count > comp.MaxDots)
            comp.Dots.RemoveRange(0, comp.Dots.Count - comp.MaxDots);

        Dirty(vehicle);
        if (start)
        {
            _audio.PlayPvs(crayonComp.UseSound, vehicle);
            _adminLog.Add(LogType.CrayonDraw, LogImpact.Low,
                $"{ToPrettyString(user):user} drew freely in {crayonComp.Color:color} on {ToPrettyString(vehicle):entity}");
        }

        free.Drawn += added.Count;
        while (free.Drawn >= comp.DotsPerCharge && !_charges.IsEmpty(held))
        {
            free.Drawn -= comp.DotsPerCharge;
            _charges.TryUseCharge(held);
        }

        if (crayonComp.DeleteEmpty && _charges.IsEmpty(held))
            UseUpCrayon(held, user);

        return added.Count;
    }

    /// <summary>
    /// The pixels of a line, both ends included.
    /// </summary>
    private static IEnumerable<(int X, int Y)> Line(Vector2i from, Vector2i to)
    {
        int x = from.X, y = from.Y;
        int dx = Math.Abs(to.X - x), dy = -Math.Abs(to.Y - y);
        int sx = x < to.X ? 1 : -1, sy = y < to.Y ? 1 : -1;
        var err = dx + dy;
        while (true)
        {
            yield return (x, y);
            if (x == to.X && y == to.Y)
                yield break;

            var e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    /// <summary>
    /// The face a surface point is on: 0 a top face, else the camera-facing panel of the direction.
    /// </summary>
    public static byte FaceOf(bool front, Direction direction)
    {
        return front ? (byte) (Array.IndexOf(MapDirections, direction) + 1) : (byte) 0;
    }

    private void UseUpCrayon(EntityUid crayon, EntityUid user)
    {
        _popup.PopupEntity(Loc.GetString("crayon-interact-used-up-text", ("owner", crayon)), user, user);
        QueueDel(crayon);
    }

    /// <summary>
    /// The body voxel under a click on the vehicle, read from the frame it is drawn with.
    /// </summary>
    public bool TryGetClickedSurface(Entity<CMUVehicleCrayonComponent> ent, EntityCoordinates click, out Direction direction, out Vector3 voxel, out bool front, out int panel)
    {
        // The frame is drawn turned by whatever is left over past its cardinal, which a jeep can rest at.
        var (position, rotation) = _transform.GetWorldPositionRotation(ent);
        direction = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(rotation);
        var offset = (direction.ToAngle() - rotation).RotateVec(_transform.ToMapCoordinates(click).Position - position);
        var x = (int) MathF.Floor(FrameSize / 2f + offset.X * PixelsPerMeter);
        var y = (int) MathF.Floor(FrameSize / 2f - offset.Y * PixelsPerMeter);
        return TryGetSurface(ent.Comp.Map, direction, x, y, out voxel, out front, out _, out panel);
    }

    /// <summary>
    /// The body voxel a pixel of a direction's frame shows, whether that is the camera-facing panel
    /// (else a top face), whether it is drawn over the riders and the hinged panel it is on.
    /// </summary>
    public abstract bool TryGetSurface(ResPath map, Direction direction, int x, int y, out Vector3 voxel, out bool front, out bool overRiders, out int panel);

    /// <summary>
    /// Decodes a crayon map pixel (R = f + 128, G = r + 128, B = 2z + front, A = 0 off the body, else
    /// 255 - panel under riders or 128 - panel over them).
    /// </summary>
    protected static bool DecodeSurface(byte r, byte g, byte b, byte a, out Vector3 voxel, out bool front, out bool overRiders, out int panel)
    {
        voxel = new Vector3(r - 128, g - 128, b / 2);
        front = b % 2 == 1;
        overRiders = a <= 128;
        panel = (overRiders ? 128 : 255) - a;
        return a != 0;
    }

    /// <summary>
    /// Whether a panel is swung away from the shut position the map has it in.
    /// </summary>
    protected static bool IsPanelOpen(CMUJeepComponent? jeep, CMUVehicleCrayonComponent crayon, int panel)
    {
        return jeep != null &&
               panel > 0 &&
               panel <= crayon.Panels.Count &&
               jeep.OpenPanels.Contains(crayon.Panels[panel - 1]);
    }

    /// <summary>
    /// One bit per panel that is open, to tell when paint baked with the panels as they were is stale.
    /// </summary>
    protected static int OpenPanels(CMUJeepComponent? jeep, CMUVehicleCrayonComponent crayon)
    {
        var mask = 0;
        for (var panel = 1; panel <= crayon.Panels.Count; panel++)
        {
            if (IsPanelOpen(jeep, crayon, panel))
                mask |= 1 << panel;
        }

        return mask;
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
