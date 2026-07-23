using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Client.Clickable;
using Content.Client.Interactable.Components;
using Content.Shared.Mind;
using Content.Shared.Physics;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.UserInterface;
using Content.Shared._CMU14.Yautja;
using Content.Shared.VendingMachines;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Yautja;

public sealed partial class YautjaGearRackClientSystem : EntitySystem
{
    private const float RowEpsilon = 0.75f;
    private const float TileEpsilon = 0.25f;
    private const int MaxMergedRackLength = 5;
    private const string MergedRackState = "pred_vendor_merged";
    private const string RackFixtureId = "fix1";
    private const float RackFixtureHalfSize = 0.45f;

    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    private static readonly ProtoId<AccessLevelPrototype> YautjaSecureAccess = "CMUAccessYautjaSecure";
    private static readonly ProtoId<AccessLevelPrototype> YautjaElderAccess = "CMUAccessYautjaElder";
    private static readonly ProtoId<AccessLevelPrototype> YautjaAncientAccess = "CMUAccessYautjaAncient";
    private static readonly ProtoId<AccessLevelPrototype> YautjaBadBloodAccess = "CMUAccessYautjaBadBlood";
    private static readonly ProtoId<JobPrototype> HunterJob = "CMUYautjaHunter";
    private static readonly ProtoId<JobPrototype> YoungbloodJob = "CMUYautjaYoungblood";

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaGearRackComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    public override void Update(float frameTime)
    {
        RefreshAllRacks();
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
    }

    private static string? DenyIfMissingAccessThenWrongRole(bool hasAccess, bool hasRole)
    {
        if (!hasAccess)
            return "cm-vending-machine-access-denied";

        return hasRole ? null : "cmu-yautja-rack-wrong-role";
    }

    private bool HasAccess(EntityUid user, ProtoId<AccessLevelPrototype> access)
    {
        return _accessReader.FindAccessTags(user).Contains(access);
    }

    private bool HasJob(EntityUid user, ProtoId<JobPrototype> job)
    {
        return _mind.TryGetMind(user, out var mindId, out _) &&
               _job.MindHasJobWithId(mindId, job.Id);
    }

    private void RefreshAllRacks()
    {
        var racks = new List<(EntityUid Uid, MapId Map, Vector2 Position)>();
        var query = EntityQueryEnumerator<YautjaGearRackComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
            racks.Add((uid, xform.MapID, _transform.GetWorldPosition(xform)));

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

    private void RefreshRow(List<(EntityUid Uid, MapId Map, Vector2 Position)> racks, int rowStart, int rowEnd)
    {
        var segmentStart = rowStart;
        while (segmentStart <= rowEnd)
        {
            var segmentEnd = segmentStart;
            while (segmentEnd + 1 <= rowEnd &&
                   IsAdjacent(racks[segmentEnd].Position.X, racks[segmentEnd + 1].Position.X))
            {
                segmentEnd++;
            }

            var length = segmentEnd - segmentStart + 1;
            var useMergedVisual = length is >= 2 and <= MaxMergedRackLength;
            for (var i = segmentStart; i <= segmentEnd; i++)
            {
                var index = i - segmentStart;
                var isPrimary = index == 0;
                SetClickable(racks[i].Uid, !useMergedVisual || isPrimary, useMergedVisual ? length : 1);
                SetInteractionFixture(racks[i].Uid, index, length);
                SetRackVisual(racks[i].Uid, !useMergedVisual || isPrimary, index, length, useMergedVisual);
            }

            segmentStart = segmentEnd + 1;
        }
    }

    private static bool IsAdjacent(float left, float right)
    {
        return MathF.Abs(right - left - 1f) <= TileEpsilon;
    }

    private static string GetState(int index, int length)
    {
        if (index == 0)
            return "pred_vendor_left";

        if (index == length - 1)
            return "pred_vendor_right";

        if (length >= 4 && index == 1)
            return "pred_vendor_lcenter";

        if (length >= 4 && index == length - 2)
            return "pred_vendor_rcentre";

        return "pred_vendor_centre";
    }

    private void SetRackVisual(EntityUid uid, bool visible, int index, int length, bool useMergedVisual)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!visible)
        {
            _sprite.SetVisible((uid, sprite), false);
            return;
        }

        _sprite.SetVisible((uid, sprite), true);
        if (useMergedVisual)
        {
            var merged = new SpriteSpecifier.Rsi(
                new ResPath($"_CMU14/HunterShip/obj/items/hunter/pred_vendor_merged_{length}.rsi"),
                MergedRackState);

            if (!TrySetLayerSprite((uid, sprite), VendingMachineVisualLayers.Base, merged))
                return;

            HideUnshadedLayer((uid, sprite));
            _sprite.SetOffset((uid, sprite), new Vector2((length - 1) / 2f, 0.5f));
            return;
        }

        var state = GetState(index, length);
        var original = new SpriteSpecifier.Rsi(
            new ResPath("_CMU14/HunterShip/obj/items/hunter/pred_vendor.rsi"),
            state);
        if (!TrySetLayerSprite((uid, sprite), VendingMachineVisualLayers.Base, original))
            return;

        HideUnshadedLayer((uid, sprite));
        _sprite.SetOffset((uid, sprite), new Vector2(0f, 0.5f));
    }

    private void HideUnshadedLayer(Entity<SpriteComponent> sprite)
    {
        if (_sprite.LayerMapTryGet(sprite.AsNullable(), VendingMachineVisualLayers.BaseUnshaded, out var layer, false))
            _sprite.LayerSetVisible(sprite.AsNullable(), layer, false);
    }

    private bool TrySetLayerSprite(Entity<SpriteComponent> sprite, VendingMachineVisualLayers layer, SpriteSpecifier spec)
    {
        if (!_sprite.LayerMapTryGet(sprite.AsNullable(), layer, out _, false))
            return false;

        _sprite.LayerSetVisible(sprite.AsNullable(), layer, true);
        _sprite.LayerSetAutoAnimated(sprite.AsNullable(), layer, true);
        _sprite.LayerSetSprite(sprite.AsNullable(), layer, spec);
        return true;
    }

    private void SetClickable(EntityUid uid, bool clickable, int width)
    {
        if (!clickable)
        {
            RemCompDeferred<ClickableComponent>(uid);
            RemCompDeferred<InteractionOutlineComponent>(uid);
            return;
        }

        var clickableComp = EnsureComp<ClickableComponent>(uid);
        var bounds = width <= 1
            ? new Box2(-0.5f, -1f, 0.5f, 1f)
            : new Box2(-width / 2f, -1f, width / 2f, 1f);
        clickableComp.Bounds = new ClickableComponent.DirBoundData
        {
            All = bounds,
            North = bounds,
            South = bounds,
            East = bounds,
            West = bounds,
        };
        EnsureComp<InteractionOutlineComponent>(uid);
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
}
