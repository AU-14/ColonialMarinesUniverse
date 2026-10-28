using System.IO;
using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Areas;
using Content.Shared.CMU14.Photography;
using Content.Shared.CMU14.Traits.Epilepsy;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Examine;
using Content.Shared.Ghost.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Wieldable.Components;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.Server.CMU14.Photography;

/// <summary>
/// Photo cameras: a click fires the shutter and flash and asks the photographer's client to capture that square of
/// its own view; the PNG it sends back becomes a print that comes out into the photographer's hand a moment later.
/// Pictures stay on the server for the round and are sent to clients that ask for them by id.
/// </summary>
/// <remarks>
/// The picture comes from the photographer's client, so a modified client could send any image of the right
/// size. Captures are only accepted for a request this system made, within its timeout, at exactly the frame's
/// pixel size, and every print is admin logged with its photographer.
/// </remarks>
public sealed partial class CMUPhotoSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private EpilepsySystem _epilepsy = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TransformSystem _transform = default!;

    private readonly Dictionary<int, Pending> _pending = new();
    private readonly Dictionary<int, byte[]> _photos = new();
    private readonly Queue<int> _order = new();
    private readonly List<Print> _printing = new();
    private int _nextRequest = 1;
    private int _nextPhoto = 1;

    private readonly record struct Pending(
        ICommonSession Session,
        EntityUid User,
        EntityUid Camera,
        EntityCoordinates Centre,
        int Size,
        TimeSpan Expires,
        TimeSpan PrintAt,
        List<string> Contents,
        List<CMUPhotoGhost> Ghosts);

    /// <summary>A picture that has been taken and is on its way out of the camera.</summary>
    private readonly record struct Print(
        int Id,
        EntityUid User,
        EntityUid Camera,
        int Size,
        TimeSpan At,
        List<string> Contents,
        List<CMUPhotoGhost> Ghosts,
        byte[] Thumbnail);

    /// <summary>Captures still waiting on the photographer's client.</summary>
    public int PendingCaptures => _pending.Count;

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
        _printing.Clear();
        _photos.Clear();
        _order.Clear();
        // Photo ids keep counting across rounds: clients cache pictures by id, and a reused id would show
        // last round's picture.
    }

    private void OnAfterInteract(Entity<CMUPhotoCameraComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !TryComp(args.User, out ActorComponent? actor))
            return;

        if (ent.Comp.RequiresPhotoMode && !ent.Comp.PhotoMode)
            return;

        args.Handled = true;
        if (ent.Comp.RequiresWielded && !(TryComp(ent, out WieldableComponent? wieldable) && wieldable.Wielded))
        {
            _popup.PopupEntity(Loc.GetString("cmu-photo-camera-not-wielded", ("camera", ent.Owner)), args.User, args.User);
            return;
        }

        if (_timing.CurTime < ent.Comp.NextPhoto)
            return;

        if (ent.Comp.Shots <= 0)
        {
            ent.Comp.NextPhoto = _timing.CurTime + ent.Comp.EmptyCooldown;
            _audio.PlayPvs(ent.Comp.EmptySound, ent);
            _popup.PopupEntity(Loc.GetString("cmu-photo-camera-no-film"), args.User, args.User);
            return;
        }

        var centre = SnapToTile(args.ClickLocation);
        var userPos = _transform.GetMapCoordinates(args.User);
        var target = _transform.ToMapCoordinates(centre);
        if (userPos.MapId != target.MapId || (userPos.Position - target.Position).Length() > ent.Comp.Range)
        {
            _popup.PopupEntity(Loc.GetString("cmu-photo-camera-too-far"), args.User, args.User);
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

    /// <summary>
    /// Fires the shutter and flash and asks the photographer's client for the picture. The client takes it once
    /// the flash is lighting its view, so a dark room comes out lit as far as the photographer can see into it.
    /// Returns the request id.
    /// </summary>
    public int RequestCapture(ICommonSession session, EntityUid user, Entity<CMUPhotoCameraComponent> camera, EntityCoordinates centre)
    {
        var request = _nextRequest++;
        var now = _timing.CurTime;
        var size = camera.Comp.Size;
        _pending[request] = new Pending(session, user, camera, centre, size,
            now + CMUPhotoLimits.CaptureTimeout, now + camera.Comp.PrintDelay,
            Describe(user, centre, size), FindGhosts(user, centre, size));
        _audio.PlayPvs(camera.Comp.ShutterSound, camera);

        NetEntity? flash = null;
        if (camera.Comp.Flash is { } flashProto)
        {
            var light = SpawnAttachedTo(flashProto, new EntityCoordinates(user, default));
            var radius = FlashRadius(camera.Comp, user, centre, size);
            _light.SetRadius(light, radius);
            flash = GetNetEntity(light);

            // Anyone with epilepsy who sees the flash might seize. The photographer is behind it.
            foreach (var witness in _lookup.GetEntitiesInRange<EpilepsyComponent>(Transform(user).Coordinates, radius))
            {
                if (witness.Owner != user && _examine.InRangeUnOccluded(user, witness.Owner, radius))
                    _epilepsy.TryFlashSeizure((witness.Owner, witness.Comp));
            }
        }

        RaiseNetworkEvent(new CMUPhotoCaptureRequestEvent(request, GetNetCoordinates(centre), size, flash), session.Channel);
        return request;
    }

    /// <summary>
    /// The flash reaches past the frame's far corner, so the whole picture is lit, but no further than it must.
    /// Walls still stop it: the light casts shadows like any other.
    /// </summary>
    private float FlashRadius(CMUPhotoCameraComponent camera, EntityUid user, EntityCoordinates centre, int size)
    {
        var from = _transform.GetMapCoordinates(user).Position;
        var to = _transform.ToMapCoordinates(centre).Position;
        // Half the frame's diagonal, plus a tile so the far edge is not lit at the very tail of the falloff.
        var reach = (to - from).Length() + size * 0.71f + 1.5f;
        return Math.Clamp(reach, camera.FlashMinRadius, camera.FlashMaxRadius);
    }

    /// <summary>
    /// Ghosts in the frame that the photographer could have seen had they been visible, where they stood and how
    /// strongly each shows. Admin ghosts, which can touch the world, are left out.
    /// </summary>
    public List<CMUPhotoGhost> FindGhosts(EntityUid user, EntityCoordinates centre, int size)
    {
        var ghosts = new List<CMUPhotoGhost>();
        var centreMap = _transform.ToMapCoordinates(centre);
        var half = size / 2f;
        var query = EntityQueryEnumerator<GhostComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var ghost, out var xform))
        {
            if (ghosts.Count >= CMUPhotoLimits.Ghosts)
                break;

            if (ghost.CanGhostInteract || xform.MapID != centreMap.MapId)
                continue;

            if (MetaData(uid).EntityPrototype is not { } proto)
                continue;

            // In the frame's own axes, which are the grid's.
            var local = _transform.WithEntityId(xform.Coordinates, centre.EntityId).Position - centre.Position;
            if (Math.Abs(local.X) > half || Math.Abs(local.Y) > half)
                continue;

            if (!_examine.InRangeUnOccluded(user, uid, CMUPhotoLimits.DescribeRange))
                continue;

            ghosts.Add(new CMUPhotoGhost
            {
                Prototype = proto.ID,
                Offset = local,
                Opacity = _random.NextFloat(CMUPhotoLimits.GhostMinOpacity, CMUPhotoLimits.GhostMaxOpacity),
            });
        }

        return ghosts;
    }

    private void OnCaptureResult(CMUPhotoCaptureResultEvent ev, EntitySessionEventArgs args)
    {
        TryAcceptCapture(args.SenderSession, ev.Request, ev.Png, out _);
    }

    /// <summary>
    /// Keeps a capture and sends the print on its way out of the camera. Rejects captures nobody asked this
    /// session for, late ones, oversized ones and ones that are not a PNG of exactly the frame's size.
    /// </summary>
    /// <param name="photo">The print, when it came out straight away; otherwise it is printed in a later tick.</param>
    public bool TryAcceptCapture(ICommonSession session, int request, byte[] png, out EntityUid photo)
    {
        photo = default;
        if (!_pending.TryGetValue(request, out var pending) || pending.Session != session)
            return false;

        _pending.Remove(request);
        if (_timing.CurTime > pending.Expires || Deleted(pending.User))
            return false;

        if (png.Length == 0)
        {
            // Nothing was captured, so no film was used.
            if (TryComp(pending.Camera, out CMUPhotoCameraComponent? camera) && camera.Shots < camera.MaxShots)
            {
                camera.Shots++;
                Dirty(pending.Camera, camera);
            }

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

        var print = new Print(id, pending.User, pending.Camera, pending.Size, pending.PrintAt, pending.Contents,
            pending.Ghosts, Thumbnail(png));
        if (_timing.CurTime >= print.At)
            photo = PrintOut(print);
        else
            _printing.Add(print);

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        for (var i = _printing.Count - 1; i >= 0; i--)
        {
            if (now < _printing[i].At)
                continue;

            var print = _printing[i];
            _printing.RemoveAt(i);
            PrintOut(print);
        }
    }

    /// <summary>The print comes out of the camera: into the photographer's hand, or at their feet.</summary>
    private EntityUid PrintOut(Print print)
    {
        // With the photographer gone, the print drops out of the camera wherever it is.
        var holder = Exists(print.User) ? print.User : print.Camera;
        if (!Exists(holder))
            return default;

        var photo = Spawn("CMUPhoto", Transform(holder).Coordinates);
        var comp = EnsureComp<CMUPhotoComponent>(photo);
        comp.Id = print.Id;
        comp.Size = print.Size;
        comp.Photographer = Exists(print.User) ? MetaData(print.User).EntityName : string.Empty;
        comp.Contents = print.Contents;
        comp.Ghosts = print.Ghosts;
        comp.Thumbnail = print.Thumbnail;
        comp.Printed = _timing.CurTime;
        Dirty(photo, comp);

        if (TryComp(print.Camera, out CMUPhotoCameraComponent? camera))
            _audio.PlayPvs(camera.PrintSound, print.Camera);

        if (holder == print.User)
            _hands.PickupOrDrop(print.User, photo);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(print.User):player} took photo {print.Id} ({print.Size}x{print.Size}) printed as {ToPrettyString(photo)}");
        return photo;
    }

    /// <summary>The picture shrunk for the print's sprite; empty if it cannot be read, which leaves the window dark.</summary>
    private byte[] Thumbnail(byte[] png)
    {
        try
        {
            using var image = Image.Load<Rgba32>(png);
            image.Mutate(context => context.Resize(new ResizeOptions
            {
                Size = new Size(CMUPhotoLimits.ThumbnailPixels, CMUPhotoLimits.ThumbnailPixels),
                Sampler = KnownResamplers.Box,
                Mode = ResizeMode.Stretch,
            }));

            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            return stream.ToArray();
        }
        catch (Exception e)
        {
            Log.Warning($"Could not shrink a photo for its thumbnail: {e.Message}");
            return Array.Empty<byte>();
        }
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

            // Ghosts show on the print itself, never in what the photographer saw.
            if (HasComp<GhostComponent>(uid))
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

            // Walls, windows and floors frame every picture; listing them says nothing. Area markers fill every
            // tile but are never seen.
            if (HasComp<OccluderComponent>(uid)
                || HasComp<AreaComponent>(uid)
                || (!HasComp<ItemComponent>(uid) && !Transform(uid).Anchored))
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
