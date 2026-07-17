using System.Numerics;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Physics;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.UserInterface;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaGearRackSystem : EntitySystem
{
    private const float RowEpsilon = 0.75f;
    private const float TileEpsilon = 0.25f;
    private const string RackFixtureId = "fix1";
    private const float RackFixtureHalfSize = 0.45f;
    private static readonly ProtoId<AccessLevelPrototype> YautjaSecureAccess = "CMUAccessYautjaSecure";
    private static readonly ProtoId<AccessLevelPrototype> YautjaElderAccess = "CMUAccessYautjaElder";
    private static readonly ProtoId<AccessLevelPrototype> YautjaAncientAccess = "CMUAccessYautjaAncient";
    private static readonly ProtoId<AccessLevelPrototype> YautjaBadBloodAccess = "CMUAccessYautjaBadBlood";
    private static readonly ProtoId<JobPrototype> HunterJob = "CMUYautjaHunter";
    private static readonly ProtoId<JobPrototype> YoungbloodJob = "CMUYautjaYoungblood";

    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FixtureSystem _fixtures = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaGearRackComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<YautjaGearRackComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<YautjaGearRackComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<YautjaGearRackComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<YautjaGearRackComponent, ActivatableUIOpenAttemptEvent>(
            OnOpenAttempt,
            before: [typeof(SharedCMAutomatedVendorSystem)]);
        SubscribeLocalEvent<YautjaGearRackComponent, BoundUserInterfaceCheckRangeEvent>(OnUiRangeCheck);
    }

    public override void Update(float frameTime)
    {
        RefreshAllRacks();
    }

    private void OnStartup(Entity<YautjaGearRackComponent> ent, ref ComponentStartup args)
    {
        RefreshRun(ent);
    }

    private void OnMapInit(Entity<YautjaGearRackComponent> ent, ref MapInitEvent args)
    {
        RefreshRun(ent);
    }

    private void OnShutdown(Entity<YautjaGearRackComponent> ent, ref ComponentShutdown args)
    {
        RefreshRun(ent);
    }

    private void OnMove(Entity<YautjaGearRackComponent> ent, ref MoveEvent args)
    {
        RefreshRun(ent);
    }

    private void RefreshRun(Entity<YautjaGearRackComponent> ent)
    {
        Timer.Spawn(0, RefreshAllRacks);
    }

    private void RefreshAllRacks()
    {
        var racks = new List<(EntityUid Uid, MapId Map, Vector2 Position, YautjaGearRackKind Kind)>();
        var query = EntityQueryEnumerator<YautjaGearRackComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var rack, out var xform))
        {
            racks.Add((uid, xform.MapID, _transform.GetWorldPosition(xform), rack.Kind));
        }

        if (racks.Count == 0)
            return;

        racks.Sort((a, b) =>
        {
            var map = ((int) a.Map).CompareTo((int) b.Map);
            if (map != 0)
                return map;

            var y = a.Position.Y.CompareTo(b.Position.Y);
            if (MathF.Abs(a.Position.Y - b.Position.Y) > RowEpsilon)
                return y;

            return a.Position.X.CompareTo(b.Position.X);
        });

        var rowStart = 0;
        while (rowStart < racks.Count)
        {
            var rowEnd = rowStart;
            while (rowEnd + 1 < racks.Count &&
                   racks[rowEnd + 1].Map == racks[rowStart].Map &&
                   MathF.Abs(racks[rowEnd + 1].Position.Y - racks[rowStart].Position.Y) <= RowEpsilon)
            {
                rowEnd++;
            }

            RefreshRow(racks, rowStart, rowEnd);
            rowStart = rowEnd + 1;
        }
    }

    private void RefreshRow(List<(EntityUid Uid, MapId Map, Vector2 Position, YautjaGearRackKind Kind)> racks, int rowStart, int rowEnd)
    {
        var segmentStart = rowStart;
        while (segmentStart <= rowEnd)
        {
            var segmentEnd = segmentStart;
            while (segmentEnd + 1 <= rowEnd &&
                   racks[segmentEnd].Kind == racks[segmentEnd + 1].Kind &&
                   IsAdjacent(racks[segmentEnd].Position.X, racks[segmentEnd + 1].Position.X))
            {
                segmentEnd++;
            }

            var length = segmentEnd - segmentStart + 1;
            var primary = racks[segmentStart].Uid;
            for (var i = segmentStart; i <= segmentEnd; i++)
            {
                var index = i - segmentStart;
                SetPrimaryVendor(racks[i].Uid, primary, index, length);
                SetActivatable(racks[i].Uid, i == segmentStart);
                SetInteractionFixture(racks[i].Uid, index, length);

                if (CanAutoConnect(racks[i].Uid))
                    SetState(racks[i].Uid, GetState(index, length));
            }

            segmentStart = segmentEnd + 1;
        }
    }

    private static bool IsAdjacent(float left, float right)
    {
        return MathF.Abs(right - left - 1f) <= TileEpsilon;
    }

    private static YautjaGearRackVisualState GetState(int index, int length)
    {
        if (index == 0)
            return YautjaGearRackVisualState.Left;

        if (index == length - 1)
            return YautjaGearRackVisualState.Right;

        if (length >= 4 && index == 1)
            return YautjaGearRackVisualState.LeftCentre;

        if (length >= 4 && index == length - 2)
            return YautjaGearRackVisualState.RightCentre;

        return YautjaGearRackVisualState.Centre;
    }

    private void SetState(EntityUid uid, YautjaGearRackVisualState state)
    {
        _appearance.SetData(uid, YautjaGearRackVisuals.State, state);
    }

    private void SetPrimaryVendor(EntityUid uid, EntityUid primary, int index, int length)
    {
        var rack = Comp<YautjaGearRackComponent>(uid);
        if (rack.PrimaryVendor == primary &&
            rack.SegmentIndex == index &&
            rack.RunLength == length)
            return;

        rack.PrimaryVendor = primary;
        rack.SegmentIndex = index;
        rack.RunLength = length;
    }

    private void SetActivatable(EntityUid uid, bool activatable)
    {
        if (!activatable)
        {
            RemCompDeferred<ActivatableUIComponent>(uid);
            RemCompDeferred<ActivatableUIRequiresAccessComponent>(uid);
            return;
        }

        var ui = EnsureComp<ActivatableUIComponent>(uid);
        ui.Key = CMAutomatedVendorUI.Key;
        Dirty(uid, ui);
        RemCompDeferred<ActivatableUIRequiresAccessComponent>(uid);
    }

    private bool CanAutoConnect(EntityUid uid)
    {
        var prototype = MetaData(uid).EntityPrototype;
        return prototype == null ||
               prototype.ID is "CMUYautjaLoadoutVendor" or
                   "CMUYautjaElderLoadoutVendor" or
                   "CMUYautjaYoungbloodLoadoutVendor" or
                   "CMUYautjaThrallLoadoutVendor" or
                   "CMUYautjaBloodedThrallLoadoutVendor" or
                   "CMUYautjaBadBloodLoadoutVendor" or
                   "CMUYautjaStrandedLoadoutVendor";
    }

    private void SetInteractionFixture(EntityUid uid, int index, int length)
    {
        if (!TryComp<FixturesComponent>(uid, out var manager) ||
            !TryComp<PhysicsComponent>(uid, out var body))
        {
            return;
        }

        var bounds = index == 0 && length > 1
            ? new Box2(-RackFixtureHalfSize, -RackFixtureHalfSize, length - 1f + RackFixtureHalfSize, RackFixtureHalfSize)
            : Box2.UnitCentered.Scale(RackFixtureHalfSize * 2f);
        var shape = CreateFixtureShape(bounds);
        var collisionLayer = GetCollisionLayer(index, length);

        if (manager.Fixtures.TryGetValue(RackFixtureId, out var fixture))
        {
            if (fixture.Shape.Equals(shape) &&
                fixture.CollisionLayer == collisionLayer)
            {
                return;
            }

            var density = fixture.Density;
            var hard = fixture.Hard;
            var collisionMask = fixture.CollisionMask;
            var friction = fixture.Friction;
            var restitution = fixture.Restitution;

            _fixtures.DestroyFixture(uid, RackFixtureId, false, body, manager);
            _fixtures.TryCreateFixture(
                uid,
                shape,
                RackFixtureId,
                density,
                hard,
                collisionLayer,
                collisionMask,
                friction,
                restitution,
                false,
                manager,
                body,
                Transform(uid));
            _fixtures.FixtureUpdate(uid, manager: manager, body: body);
            return;
        }

        _fixtures.TryCreateFixture(
            uid,
            shape,
            RackFixtureId,
            hard: true,
            collisionLayer: collisionLayer,
            collisionMask: (int) CollisionGroup.FullTileMask,
            manager: manager,
            body: body,
            xform: Transform(uid));
    }

    private static PolygonShape CreateFixtureShape(Box2 bounds)
    {
        var shape = new PolygonShape();
        shape.SetAsBox(bounds);
        return shape;
    }

    private static int GetCollisionLayer(int index, int length)
    {
        return index == 0 || length <= 1
            ? (int) CollisionGroup.WallLayer
            : (int) CollisionGroup.SpecialWallLayer;
    }

    private void OnOpenAttempt(Entity<YautjaGearRackComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var denial = ent.Comp.Kind switch
        {
            YautjaGearRackKind.Adult => DenyIfMissingAccessThenWrongRole(
                HasAccess(args.User, YautjaSecureAccess),
                HasJob(args.User, HunterJob)),
            YautjaGearRackKind.Youngblood => DenyIfMissingAccessThenWrongRole(
                HasAccess(args.User, YautjaSecureAccess),
                HasJob(args.User, YoungbloodJob) || HasJob(args.User, HunterJob)),
            YautjaGearRackKind.Elder => DenyIfMissingAccessThenWrongRole(
                HasAccess(args.User, YautjaElderAccess) || HasAccess(args.User, YautjaAncientAccess),
                HasJob(args.User, HunterJob)),
            YautjaGearRackKind.Thrall => HasComp<YautjaThrallComponent>(args.User)
                ? null
                : "cm-vending-machine-access-denied",
            YautjaGearRackKind.BloodedThrall => HasComp<YautjaTechAuthorizedComponent>(args.User)
                ? null
                : "cm-vending-machine-access-denied",
            YautjaGearRackKind.BadBlood => HasAccess(args.User, YautjaBadBloodAccess)
                ? null
                : "cm-vending-machine-access-denied",
            YautjaGearRackKind.Stranded => HasAccess(args.User, YautjaSecureAccess) &&
                                            !HasAccess(args.User, YautjaBadBloodAccess)
                ? null
                : "cm-vending-machine-access-denied",
            _ => null,
        };

        if (denial == null)
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString(denial), ent, args.User);
    }

    private static string? DenyIfMissingAccessThenWrongRole(bool hasAccess, bool hasRole)
    {
        if (!hasAccess)
            return "cm-vending-machine-access-denied";

        return hasRole ? null : "cmu-yautja-rack-wrong-role";
    }

    private bool HasAccess(EntityUid user, ProtoId<AccessLevelPrototype> access)
    {
        var tags = _accessReader.FindAccessTags(user);
        return tags.Contains(access);
    }

    private bool HasJob(EntityUid user, ProtoId<JobPrototype> job)
    {
        return _mind.TryGetMind(user, out var mindId, out _) &&
               _job.MindHasJobWithId(mindId, job.Id);
    }

    private void OnUiRangeCheck(Entity<YautjaGearRackComponent> ent, ref BoundUserInterfaceCheckRangeEvent args)
    {
        if (!Equals(args.UiKey, CMAutomatedVendorUI.Key) ||
            ent.Comp.PrimaryVendor != ent.Owner ||
            ent.Comp.RunLength <= 1)
        {
            return;
        }

        var range = args.Data.InteractionRange;
        var targetPos = _transform.GetWorldPosition(Transform(args.Target));
        var actorPos = _transform.GetWorldPosition(args.Actor.Comp);

        if (MathF.Abs(actorPos.Y - targetPos.Y) > range + 1f)
            return;

        var minX = targetPos.X - range;
        var maxX = targetPos.X + ent.Comp.RunLength - 1f + range;
        if (actorPos.X < minX || actorPos.X > maxX)
            return;

        args.Result = BoundUserInterfaceRangeResult.Pass;
    }
}
