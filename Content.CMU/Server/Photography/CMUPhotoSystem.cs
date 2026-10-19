using Content.Server.Administration.Logs;
using Content.Shared.CMU14.Photography;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Item;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;

namespace Content.Server.CMU14.Photography;

/// <summary>
/// Photo cameras: a click asks the photographer's client to capture that square of its own view, and the PNG it
/// sends back becomes a print in the photographer's hand. Pictures stay on the server for the round and are sent to
/// clients that ask for them by id.
/// </summary>
/// <remarks>
/// The picture comes from the photographer's client, so a modified client could send any image of the right
/// size. Captures are only accepted for a request this system made, within its timeout, at exactly the frame's
/// pixel size, and every print is admin logged with its photographer.
/// </remarks>
public sealed partial class CMUPhotoSystem : EntitySystem
{
    private static readonly SoundSpecifier SnapSound = new SoundPathSpecifier("/Audio/Misc/camera_snap.ogg");

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TransformSystem _transform = default!;

    private readonly Dictionary<int, Pending> _pending = new();
    private readonly Dictionary<int, byte[]> _photos = new();
    private readonly Queue<int> _order = new();
    private int _nextRequest = 1;
    private int _nextPhoto = 1;

    private readonly record struct Pending(
        ICommonSession Session,
        EntityUid User,
        EntityUid Camera,
        EntityCoordinates Centre,
        int Size,
        TimeSpan Expires,
        List<string> Contents);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUPhotoCameraComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CMUPhotoCameraComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeNetworkEvent<CMUPhotoCaptureResultEvent>(OnCaptureResult);
        SubscribeNetworkEvent<CMUPhotoImageRequestEvent>(OnImageRequest);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _pending.Clear();
        _photos.Clear();
        _order.Clear();
        // Photo ids keep counting across rounds: clients cache pictures by id, and a reused id would show
        // last round's picture.
    }

    private void OnAfterInteract(Entity<CMUPhotoCameraComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !TryComp(args.User, out ActorComponent? actor))
            return;

        args.Handled = true;
        if (_timing.CurTime < ent.Comp.NextPhoto)
            return;

        var centre = SnapToTile(args.ClickLocation);
        var userPos = _transform.GetMapCoordinates(args.User);
        var target = _transform.ToMapCoordinates(centre);
        if (userPos.MapId != target.MapId || (userPos.Position - target.Position).Length() > ent.Comp.Range)
        {
            _popup.PopupEntity(Loc.GetString("cmu-photo-camera-too-far"), args.User, args.User);
            return;
        }

        if (ent.Comp.Shots <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-photo-camera-no-film"), args.User, args.User);
            return;
        }

        ent.Comp.NextPhoto = _timing.CurTime + ent.Comp.Cooldown;
        ent.Comp.Shots--;
        Dirty(ent);
        RequestCapture(actor.PlayerSession, args.User, ent, centre);
    }

    /// <summary>A fresh roll replaces whatever is left of the loaded one.</summary>
    private void OnInteractUsing(Entity<CMUPhotoCameraComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<CMUCameraFilmComponent>(args.Used))
            return;

        args.Handled = true;
        if (ent.Comp.Shots >= ent.Comp.MaxShots)
        {
            _popup.PopupEntity(Loc.GetString("cmu-photo-camera-film-full"), args.User, args.User);
            return;
        }

        ent.Comp.Shots = ent.Comp.MaxShots;
        Dirty(ent);
        QueueDel(args.Used);
        _popup.PopupEntity(Loc.GetString("cmu-photo-camera-film-loaded", ("shots", ent.Comp.Shots)), args.User, args.User);
    }

    /// <summary>Asks the photographer's client for the picture. Returns the request id.</summary>
    public int RequestCapture(ICommonSession session, EntityUid user, Entity<CMUPhotoCameraComponent> camera, EntityCoordinates centre)
    {
        var request = _nextRequest++;
        _pending[request] = new Pending(session, user, camera, centre, camera.Comp.Size,
            _timing.CurTime + CMUPhotoLimits.CaptureTimeout, Describe(user, centre, camera.Comp.Size));
        _audio.PlayPvs(SnapSound, camera);
        RaiseNetworkEvent(new CMUPhotoCaptureRequestEvent(request, GetNetCoordinates(centre), camera.Comp.Size), session.Channel);
        return request;
    }

    private void OnCaptureResult(CMUPhotoCaptureResultEvent ev, EntitySessionEventArgs args)
    {
        TryAcceptCapture(args.SenderSession, ev.Request, ev.Png, out _);
    }

    /// <summary>
    /// Turns a capture into a print at the photographed tile. Rejects captures nobody asked this session for,
    /// late ones, oversized ones and ones that are not a PNG of exactly the frame's size.
    /// </summary>
    public bool TryAcceptCapture(ICommonSession session, int request, byte[] png, out EntityUid photo)
    {
        photo = default;
        if (!_pending.Remove(request, out var pending) || pending.Session != session)
            return false;

        if (_timing.CurTime > pending.Expires || Deleted(pending.User))
            return false;

        if (png.Length == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-photo-camera-not-in-view"), pending.User, pending.User);
            return false;
        }

        var expected = pending.Size * CMUPhotoLimits.TilePixels;
        if (png.Length > CMUPhotoLimits.MaxBytes || !IsPngOfSize(png, expected))
        {
            Log.Warning($"Rejected a photo from {session.Name}: {png.Length} bytes, not a {expected}px PNG");
            return false;
        }

        var id = _nextPhoto++;
        _photos[id] = png;
        _order.Enqueue(id);
        while (_order.Count > CMUPhotoLimits.PhotosPerRound)
            _photos.Remove(_order.Dequeue());

        // The print comes out of the camera: into the photographer's hand, or at their feet.
        photo = Spawn("CMUPhoto", Transform(pending.User).Coordinates);
        var comp = EnsureComp<CMUPhotoComponent>(photo);
        comp.Id = id;
        comp.Size = pending.Size;
        comp.Photographer = MetaData(pending.User).EntityName;
        comp.Contents = pending.Contents;
        Dirty(photo, comp);
        _hands.PickupOrDrop(pending.User, photo);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(pending.User):player} took photo {id} ({pending.Size}x{pending.Size}) printed as {ToPrettyString(photo)}");
        return true;
    }

    /// <summary>
    /// What the photographer can see in the frame at the moment of the click: people by their visible identity
    /// and condition, then items and fittings grouped by name. Walls and things out of sight are left out.
    /// </summary>
    public List<string> Describe(EntityUid user, EntityCoordinates centre, int size)
    {
        var map = _transform.ToMapCoordinates(centre);
        var box = Box2.CenteredAround(map.Position, new System.Numerics.Vector2(size, size));
        var people = new List<string>();
        var objects = new Dictionary<string, int>();

        foreach (var uid in _lookup.GetEntitiesIntersecting(map.MapId, box, LookupFlags.Uncontained))
        {
            if (uid == HeldItem(user) || !_examine.InRangeUnOccluded(user, uid, CMUPhotoLimits.DescribeRange))
                continue;

            if (HasComp<MobStateComponent>(uid))
            {
                if (people.Count >= CMUPhotoLimits.DescribedPeople)
                    continue;

                var name = Identity.Name(uid, EntityManager, user).Name;
                if (_mobState.IsDead(uid))
                    name = Loc.GetString("cmu-photo-person-dead", ("name", name));
                else if (_mobState.IsCritical(uid))
                    name = Loc.GetString("cmu-photo-person-critical", ("name", name));

                people.Add(name);
                continue;
            }

            // Walls, windows and floors frame every picture; listing them says nothing.
            if (HasComp<OccluderComponent>(uid) || (!HasComp<ItemComponent>(uid) && !Transform(uid).Anchored))
                continue;

            var thing = MetaData(uid).EntityName;
            if (thing.Length == 0)
                continue;

            objects[thing] = objects.GetValueOrDefault(thing) + 1;
        }

        var sorted = new List<KeyValuePair<string, int>>(objects);
        sorted.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));

        var contents = people;
        for (var i = 0; i < sorted.Count && i < CMUPhotoLimits.DescribedObjects; i++)
        {
            var (thing, count) = sorted[i];
            contents.Add(count == 1 ? thing : Loc.GetString("cmu-photo-object-count", ("count", count), ("name", thing)));
        }

        return contents;
    }

    private EntityUid? HeldItem(EntityUid user)
    {
        return _hands.TryGetActiveItem(user, out var item) ? item : null;
    }

    private static bool IsPngOfSize(byte[] png, int size)
    {
        // The signature first: Identify would also accept other formats.
        if (png.Length < 8 || png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47)
            return false;

        try
        {
            var info = Image.Identify(png);
            return info.Width == size && info.Height == size;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void OnImageRequest(CMUPhotoImageRequestEvent ev, EntitySessionEventArgs args)
    {
        if (_photos.TryGetValue(ev.Photo, out var png))
            RaiseNetworkEvent(new CMUPhotoImageEvent(ev.Photo, png), args.SenderSession.Channel);
    }

    public bool HasPhoto(int id)
    {
        return _photos.ContainsKey(id);
    }

    /// <summary>The first print the user is holding, for attaching to a posting.</summary>
    public bool TryGetHeldPhoto(EntityUid user, out int id)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (TryComp(held, out CMUPhotoComponent? photo) && _photos.ContainsKey(photo.Id))
            {
                id = photo.Id;
                return true;
            }
        }

        id = 0;
        return false;
    }

    private EntityCoordinates SnapToTile(EntityCoordinates coordinates)
    {
        if (_transform.GetGrid(coordinates) is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return coordinates;

        var tile = _map.TileIndicesFor(gridUid, grid, coordinates);
        return _map.GridTileToLocal(gridUid, grid, tile);
    }
}
