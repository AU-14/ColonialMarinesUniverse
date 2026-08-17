using Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;
using Content.Shared.Damage;
using Content.Shared.Eye;
using Content.Shared.Mind;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Content.Shared._CMU14.Xenomorphs;
using Content.Shared.Actions;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.GameObjects;

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

    private static readonly ProtoId<TagPrototype> DoorBumpOpenerTag = "DoorBumpOpener";
    private static readonly EntProtoId EyeProto = "CMU14XenoOvermindEye";

    private static readonly string[] EyeOnlyActions =
    [
        "CMU14ActionXenoPathogenHeal",
        "CMU14ActionXenoPathogenWordQueen",
        "CMU14ActionXenoPathogenPheromones",
        "CMU14ActionXenoPathogenWatch",
        "CMU14ActionXenoPathogenRest",
    ];

    private static readonly string[] PhysicalOnlyActions =
    [
        "CMU14ActionXenoPathogenParalyzingSlash",
        "CMU14ActionXenoPathogenBlightWave",
    ];

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoOvermindComponent, ComponentStartup>(OnOvermindInit);
        SubscribeLocalEvent<CMUXenoOvermindComponent, CMUXenoOvermindChangeFormActionEvent>(OnChangeForm);
        SubscribeLocalEvent<CMUXenoOvermindComponent, CMUXenoOvermindFormChangedEvent>(OnFormChanged);
        SubscribeLocalEvent<CMUXenoOvermindComponent, ComponentShutdown>(OnOvermindShutdown);
        // Mirror queen eye: handle visibility mask so the player can see xeno-layer entities
        SubscribeLocalEvent<CMUXenoOvermindComponent, GetVisMaskEvent>(OnGetVisMask);
    }

    private void OnGetVisMask(Entity<CMUXenoOvermindComponent> ent, ref GetVisMaskEvent args)
    {
        // While in eye form, grant the xeno visibility layer so the eye entity is visible to the player
        if (ent.Comp.Eye != null)
            args.VisibilityMask |= (int) VisibilityFlags.Xeno;
    }

    private void OnOvermindInit(Entity<CMUXenoOvermindComponent> ent, ref ComponentStartup args)
    {
        EnterEyeForm(ent);
        // Actions are granted after MapInit so defer the update
        Timer.Spawn(0, () =>
        {
            if (!TerminatingOrDeleted(ent))
                UpdateFormActions(ent.Owner, incorporeal: true);
        });
    }

    private void OnOvermindShutdown(Entity<CMUXenoOvermindComponent> ent, ref ComponentShutdown args)
    {
        RemoveEye(ent);
    }

    private void OnFormChanged(Entity<CMUXenoOvermindComponent> ent, ref CMUXenoOvermindFormChangedEvent args)
    {
        if (args.Incorporeal)
            EnterEyeForm(ent);
        else
            EnterPhysicalForm(ent);

        UpdateFormActions(ent.Owner, args.Incorporeal);
    }

    private void EnterEyeForm(Entity<CMUXenoOvermindComponent> ent)
    {
        SetIncorporealPhysics(ent.Owner, true);

        if (_net.IsClient)
            return;

        var eye = SpawnAtPosition(EyeProto, Transform(ent.Owner).Coordinates);
        ent.Comp.Eye = eye;
        Dirty(ent);

        var eyeComp = EnsureComp<EyeComponent>(ent.Owner);

        _eye.SetDrawFov(ent.Owner, false, eyeComp);
        _eye.SetDrawLight(ent.Owner, false, eyeComp);
        _eye.SetPvsScale(ent.Owner, 1.5f, eyeComp);
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
            _eye.SetDrawLight(ent.Owner, true, eyeComp);
            _eye.SetPvsScale(ent.Owner, 1f, eyeComp);
            _eye.SetTarget(ent.Owner, null, eyeComp);
            _eye.RefreshVisibilityMask(ent.Owner);
        }

        RemComp<RelayInputMoverComponent>(ent.Owner);
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

    private void UpdateFormActions(EntityUid uid, bool incorporeal)
    {
        foreach (var (actionId, _) in _actions.GetActions(uid))
        {
            var prototype = MetaData(actionId).EntityPrototype?.ID;
            if (prototype == null)
                continue;

            foreach (var eyeAction in EyeOnlyActions)
            {
                if (prototype == eyeAction)
                {
                    _actions.SetEnabled(actionId, incorporeal);
                    break;
                }
            }

            foreach (var physAction in PhysicalOnlyActions)
            {
                if (prototype == physAction)
                {
                    _actions.SetEnabled(actionId, !incorporeal);
                    break;
                }
            }
        }
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
