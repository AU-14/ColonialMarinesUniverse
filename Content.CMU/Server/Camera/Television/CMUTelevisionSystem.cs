using Content.Server.Camera;
using Content.Shared._RMC14.Camera;
using Content.Shared.CCVar;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Camera.Television;

/// <summary>
/// Shared channels and watch-from-afar for <see cref="CMUTelevisionComponent"/> sets. The camera session system is
/// per viewer, so the set remembers its channel and pushes it onto every viewer's session.
/// </summary>
public sealed partial class CMUTelevisionSystem : EntitySystem
{
    private static readonly TimeSpan VisibilityInterval = TimeSpan.FromSeconds(0.25);

    [Dependency] private ActivatableUISystem _activatableUI = default!;
    [Dependency] private CameraSessionSystem _cameraSessions = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _userInterface = default!;

    private readonly List<EntityUid> _actors = new();
    private bool _checkingButtons;
    private TimeSpan _nextVisibilityCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUTelevisionComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<HandsComponent, InRangeOverrideEvent>(OnInRangeOverride);
    }

    /// <summary>
    /// An empty-handed click reaches a set from anywhere it can be seen, so it can be switched on and watched.
    /// Anything held, like a tool, still needs the set in reach.
    /// </summary>
    private void OnInRangeOverride(Entity<HandsComponent> ent, ref InRangeOverrideEvent args)
    {
        if (args.Handled || _checkingButtons || !TryComp(args.Target, out CMUTelevisionComponent? television))
            return;

        if (_hands.TryGetActiveItem(ent.AsNullable(), out _) || !CanSee((args.Target, television), ent))
            return;

        args.Handled = true;
        args.InRange = true;
    }

    /// <summary>
    /// Call after a viewer deliberately changed channel or band: the set takes it and every other viewer follows.
    /// </summary>
    public void ChannelChanged(EntityUid tv, CameraViewerSession source)
    {
        if (!TryComp(tv, out CMUTelevisionComponent? television))
            return;

        television.Network = source.ActiveNetwork;
        television.Camera = source.SelectedCamera;

        foreach (var session in _cameraSessions.GetSessions(tv))
        {
            if (session.Id != source.Id)
                Apply(television, session);
        }
    }

    /// <summary>
    /// Tunes a viewer's session to the set's channel, for someone who just turned to watch it.
    /// </summary>
    public void Apply(EntityUid tv, CameraViewerSession session)
    {
        if (TryComp(tv, out CMUTelevisionComponent? television))
            Apply(television, session);
    }

    /// <summary>
    /// Anyone who can see a set can watch it, but changing the channel takes being next to it.
    /// Anything that isn't a <see cref="CMUTelevisionComponent"/> set, like a security monitor, is unaffected.
    /// </summary>
    public bool CanPressButtons(EntityUid tv, EntityUid user)
    {
        if (!HasComp<CMUTelevisionComponent>(tv))
            return true;

        // Skip the watch-from-afar reach so this asks for real reach.
        _checkingButtons = true;
        var inReach = _interaction.InRangeUnobstructed(user, tv);
        _checkingButtons = false;
        if (inReach)
            return true;

        _popup.PopupEntity(Loc.GetString("cmu-tv-too-far", ("tv", tv)), user, user);
        return false;
    }

    /// <summary>
    /// Sets tuned to <paramref name="camera"/>, whether or not anyone is watching them.
    /// </summary>
    public IEnumerable<EntityUid> GetTunedTo(EntityUid camera)
    {
        var query = EntityQueryEnumerator<CMUTelevisionComponent>();
        while (query.MoveNext(out var uid, out var television))
        {
            if (television.Camera == camera)
                yield return uid;
        }
    }

    private void Apply(CMUTelevisionComponent television, CameraViewerSession session)
    {
        if (television.Camera is { } camera && TerminatingOrDeleted(camera))
            television.Camera = null;

        if (television.Network is { } network && TerminatingOrDeleted(network))
            television.Network = null;

        if (television.Network is { } band && session.ActiveNetwork != band)
            _cameraSessions.SelectNetwork(session.Id, band);

        if (session.SelectedCamera != television.Camera)
            _cameraSessions.SelectCamera(session.Id, television.Camera);
    }

    private void OnGetVerbs(Entity<CMUTelevisionComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || !TryComp(ent, out ActivatableUIComponent? activatable))
            return;

        if (activatable.Key is not { } key || !_userInterface.HasUi(ent, key))
            return;

        var watching = _userInterface.IsUiOpen(ent.Owner, key, args.User);
        if (!watching && !CanSee(ent, args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(watching ? "cmu-tv-verb-stop-watching" : "cmu-tv-verb-watch"),
            Act = () => _activatableUI.InteractUI(user, ent, activatable),
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextVisibilityCheck)
            return;

        _nextVisibilityCheck = _timing.CurTime + VisibilityInterval;

        // The screen stays open as long as the set is on screen and nothing blocks the view of it.
        var query = EntityQueryEnumerator<CMUTelevisionComponent, UserInterfaceComponent>();
        while (query.MoveNext(out var uid, out var television, out var ui))
        {
            _actors.Clear();
            _actors.AddRange(_userInterface.GetActors((uid, ui), RMCCameraUiKey.Key));
            foreach (var actor in _actors)
            {
                if (!CanSee((uid, television), actor))
                    _userInterface.CloseUi((uid, ui), RMCCameraUiKey.Key, actor);
            }
        }
    }

    private bool CanSee(Entity<CMUTelevisionComponent> tv, EntityUid user)
    {
        var userCoords = _transform.GetMapCoordinates(user);
        var tvCoords = _transform.GetMapCoordinates(tv);
        if (userCoords.MapId != tvCoords.MapId)
            return false;

        var halfWidth = _configuration.GetCVar(CCVars.ViewportMaximumWidth) / 2f;
        var halfHeight = tv.Comp.ViewHalfHeight;
        var eyeCenter = userCoords.Position;
        var rotation = Angle.Zero;
        if (TryComp(user, out EyeComponent? eye))
        {
            halfWidth *= eye.Zoom.X;
            halfHeight *= eye.Zoom.Y;
            rotation = eye.Rotation;
            eyeCenter += eye.Offset;
        }

        // Half a tile of slack so a set at the very edge of the screen still counts.
        var offset = (-rotation).RotateVec(tvCoords.Position - eyeCenter);
        if (MathF.Abs(offset.X) > halfWidth + 0.5f || MathF.Abs(offset.Y) > halfHeight + 0.5f)
            return false;

        // By coordinates: the entity overload raises InRangeOverrideEvent, which would come back here.
        return _examine.InRangeUnOccluded(userCoords, tvCoords, 0f, uid => uid == tv.Owner);
    }
}
