using Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;
using Content.Shared.Damage;
using Content.Shared.Eye;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Content.Shared.Actions;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.Watch;

namespace Content.Server._CMU14.Xenomorphs.Pathogen.Overmind;

public sealed class CMUXenoOvermindSystem : EntitySystem
{
    [Dependency] private readonly CMUXenoOvermindAppearanceSystem _appearance = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly SharedXenoWatchSystem _xenoWatch = default!;
    [Dependency] private readonly QueenEyeSystem _queenEye = default!;

    private static readonly ProtoId<TagPrototype> DoorBumpOpenerTag = "DoorBumpOpener";
    private static readonly EntProtoId EyeProto = "CMU14XenoOvermindEye";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoOvermindComponent, ComponentStartup>(OnOvermindInit);
        SubscribeLocalEvent<CMUXenoOvermindComponent, CMUXenoOvermindChangeFormActionEvent>(OnChangeForm);
        SubscribeLocalEvent<CMUXenoOvermindComponent, CMUXenoOvermindFormChangedEvent>(OnFormChanged);
        SubscribeLocalEvent<CMUXenoOvermindComponent, ComponentShutdown>(OnOvermindShutdown);
        SubscribeLocalEvent<CMUXenoOvermindComponent, GetVisMaskEvent>(OnGetVisMask);
        SubscribeLocalEvent<CMUXenoOvermindComponent, XenoUnwatchEvent>(OnUnwatch);
        SubscribeLocalEvent<CMUXenoOvermindComponent, XenoWatchEvent>(OnWatch);
    }

    private void OnGetVisMask(Entity<CMUXenoOvermindComponent> ent, ref GetVisMaskEvent args)
    {
        if (ent.Comp.Eye != null)
            args.VisibilityMask |= (int) VisibilityFlags.Xeno;
    }

    private void OnOvermindInit(Entity<CMUXenoOvermindComponent> ent, ref ComponentStartup args)
    {
        EnterEyeForm(ent);

        // Grant unlimited construction range (same mechanism the Queen gets from an attached ovipositor).
        // This must be granted here too, not just in OnFormChanged, since the Overmind spawns already
        // in eye form and OnFormChanged is only raised on subsequent form toggles.
        EnsureComp<XenoAttachedOvipositorComponent>(ent.Owner);

        Timer.Spawn(0, () =>
        {
            if (TerminatingOrDeleted(ent))
                return;

            // Grant eye-form actions (starts in eye form)
            GrantFormActions(ent.Owner, ent.Comp.EyeFormActions, ent.Comp.EyeFormActionEntities);
            Dirty(ent);

            EnsurePathogenHive(ent.Owner);
        });
    }

    private void OnOvermindShutdown(Entity<CMUXenoOvermindComponent> ent, ref ComponentShutdown args)
    {
        RemoveEye(ent);
        RevokeFormActions(ent.Owner, ent.Comp.EyeFormActionEntities);
        RevokeFormActions(ent.Owner, ent.Comp.PhysicalFormActionEntities);
    }

    private void OnFormChanged(Entity<CMUXenoOvermindComponent> ent, ref CMUXenoOvermindFormChangedEvent args)
    {
        if (args.Incorporeal)
        {
            EnterEyeForm(ent);

            // Swap actions: remove physical, grant eye
            RevokeFormActions(ent.Owner, ent.Comp.PhysicalFormActionEntities);
            GrantFormActions(ent.Owner, ent.Comp.EyeFormActions, ent.Comp.EyeFormActionEntities);

            // Grant unlimited construction range (same mechanism the Queen gets from an attached ovipositor)
            EnsureComp<XenoAttachedOvipositorComponent>(ent.Owner);

            // Fire ovipositor-equivalent event so construction range unlocks hive-wide
            var oviEv = new XenoOvipositorChangedEvent(true, ent.Owner, _hive.GetHive(ent.Owner)?.Owner);
            RaiseLocalEvent(ent.Owner, ref oviEv, true);
        }
        else
        {
            EnterPhysicalForm(ent);

            // Swap actions: remove eye, grant physical
            RevokeFormActions(ent.Owner, ent.Comp.EyeFormActionEntities);
            GrantFormActions(ent.Owner, ent.Comp.PhysicalFormActions, ent.Comp.PhysicalFormActionEntities);

            // Revoke unlimited construction range
            RemCompDeferred<XenoAttachedOvipositorComponent>(ent.Owner);

            // Revoke construction range unlock
            var oviEv = new XenoOvipositorChangedEvent(false, ent.Owner, _hive.GetHive(ent.Owner)?.Owner);
            RaiseLocalEvent(ent.Owner, ref oviEv, true);
        }

        Dirty(ent);
    }

    private void GrantFormActions(EntityUid uid, List<EntProtoId> actionIds, Dictionary<EntProtoId, EntityUid> tracker)
    {
        foreach (var actionId in actionIds)
        {
            if (tracker.ContainsKey(actionId))
                continue;

            if (_actions.AddAction(uid, actionId) is { } spawned)
                tracker[actionId] = spawned;
        }
    }

    private void RevokeFormActions(EntityUid uid, Dictionary<EntProtoId, EntityUid> tracker)
    {
        foreach (var (_, actionEntity) in tracker)
        {
            _actions.RemoveAction(uid, actionEntity);
        }
        tracker.Clear();
    }

    private void EnsurePathogenHive(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return;

        if (_hive.GetHive(uid) != null)
            return;

        var hives = EntityQueryEnumerator<HiveComponent, MetaDataComponent>();
        while (hives.MoveNext(out var hiveUid, out _, out var meta))
        {
            if (meta.EntityPrototype?.ID != "CMUPathogenHive")
                continue;

            Log.Debug($"EnsurePathogenHive: assigning Overmind {ToPrettyString(uid)} to {ToPrettyString(hiveUid)}");
            _hive.SetHive(uid, hiveUid);

            // Fire ovipositor-equivalent event now that the hive is actually known, so hive-wide
            // construction-range unlock listeners get a valid hive rather than null.
            var oviEv = new XenoOvipositorChangedEvent(true, uid, hiveUid);
            RaiseLocalEvent(uid, ref oviEv, true);
            return;
        }

        Log.Debug($"EnsurePathogenHive: CMUPathogenHive not found for {ToPrettyString(uid)}, retrying next tick");
        Timer.Spawn(0, () => EnsurePathogenHive(uid));
    }

    private void OnChangeForm(Entity<CMUXenoOvermindComponent> ent, ref CMUXenoOvermindChangeFormActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(ent, out CMUXenoOvermindAppearanceComponent? appearance))
            return;

        if (appearance.Incorporeal)
        {
            if (TryComp(ent, out DamageableComponent? dmg) && dmg.TotalDamage > 0)
                return;
        }

        if (!_appearance.TryBeginFormChange((ent.Owner, appearance)))
            return;

        args.Handled = true;
    }

    private void EnterEyeForm(Entity<CMUXenoOvermindComponent> ent)
    {
        SetIncorporealPhysics(ent.Owner, true);

        if (_net.IsClient)
            return;

        var eye = SpawnAtPosition(EyeProto, Transform(ent.Owner).Coordinates);
        ent.Comp.Eye = eye;
        Dirty(ent);

        // So the shared watch system's move-input handler (which fires on the relay
        // target, i.e. this eye entity) can resolve the controlling player's
        // ActorComponent via the body and auto-unwatch on movement.
        _queenEye.SetQueen(eye, ent.Owner);

        var eyeComp = EnsureComp<EyeComponent>(ent.Owner);
        _eye.SetDrawFov(ent.Owner, false, eyeComp);
        _eye.SetDrawLight(ent.Owner, false);
        _eye.SetPvsScale(ent.Owner, 1.5f);
        _eye.SetTarget(ent.Owner, eye, eyeComp);
        _eye.RefreshVisibilityMask(ent.Owner);
        _mover.SetRelay(ent.Owner, eye);
    }

    private void EnterPhysicalForm(Entity<CMUXenoOvermindComponent> ent)
    {
        RemoveEye(ent);
        SetIncorporealPhysics(ent.Owner, false);

        if (TryComp(ent.Owner, out EyeComponent? eyeComp))
        {
            _eye.SetDrawFov(ent.Owner, true, eyeComp);
            _eye.SetDrawLight(ent.Owner, true);
            _eye.SetPvsScale(ent.Owner, 1f);
            _eye.SetTarget(ent.Owner, null, eyeComp);
            _eye.RefreshVisibilityMask(ent.Owner);
        }

        RemComp<RelayInputMoverComponent>(ent.Owner);
    }

    private void OnWatch(Entity<CMUXenoOvermindComponent> ent, ref XenoWatchEvent args)
    {
        // Mirror XenoWatchingComponent onto the Overmind's own spawned eye entity, same as
        // QueenEyeSystem.OnQueenEyeActionWatch does for a normal Queen's spawned Queen Eye.
        // MoveInputEvent fires on the relay target (the eye), not the body, so XenoWatchingComponent
        // must live there for OnXenoMoveInput to catch movement and auto-unwatch.
        if (ent.Comp.Eye is not { } eye)
            return;

        _xenoWatch.SetWatching(eye, args.Watching);
    }

    private void OnUnwatch(Entity<CMUXenoOvermindComponent> ent, ref XenoUnwatchEvent args)
    {
        if (ent.Comp.Eye is not { } eye)
            return;

        RemCompDeferred<XenoWatchingComponent>(eye);

        if (TryComp(ent.Owner, out EyeComponent? eyeComp))
            _eye.SetTarget(ent.Owner, eye, eyeComp);
    }

    private void RemoveEye(Entity<CMUXenoOvermindComponent> ent)
    {
        if (ent.Comp.Eye is not { } eye)
            return;

        if (_net.IsServer)
            QueueDel(eye);

        ent.Comp.Eye = null;
        Dirty(ent);
    }

    private void SetIncorporealPhysics(EntityUid uid, bool incorporeal)
    {
        if (!TryComp(uid, out PhysicsComponent? physics)
                || !TryComp(uid, out FixturesComponent? fixtures))
            return;

        var hard = !incorporeal;
        var toRebuild = new List<(string Id, Fixture Fixture)>(fixtures.Fixtures.Count);

        foreach (var (id, fixture) in fixtures.Fixtures)
            toRebuild.Add((id, fixture));

        foreach (var (id, fixture) in toRebuild)
        {
            if (fixture.Hard == hard)
                continue;

            var shape = fixture.Shape;
            var density = fixture.Density;
            var layer = fixture.CollisionLayer;
            var mask = fixture.CollisionMask;
            var friction = fixture.Friction;
            var restitution = fixture.Restitution;

            _fixtures.DestroyFixture(uid, id, fixture, updates: false, body: physics, manager: fixtures);
            _fixtures.TryCreateFixture(uid, shape, id, density, hard, layer, mask, friction, restitution,
                updates: false, manager: fixtures, body: physics);
        }

        _fixtures.FixtureUpdate(uid, manager: fixtures, body: physics);
        _physics.SetCanCollide(uid, !incorporeal, body: physics);

        if (incorporeal)
            _tag.RemoveTag(uid, DoorBumpOpenerTag);
        else
            _tag.AddTag(uid, DoorBumpOpenerTag);
    }
}
