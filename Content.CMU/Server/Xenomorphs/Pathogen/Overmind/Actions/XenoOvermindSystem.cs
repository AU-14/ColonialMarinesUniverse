using Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Xenomorphs.Pathogen.Overmind;

public sealed class CMUXenoOvermindSystem : EntitySystem
{
    [Dependency] private readonly CMUXenoOvermindAppearanceSystem _appearance = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> DoorBumpOpenerTag = "DoorBumpOpener";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoOvermindComponent, ComponentStartup>(OnOvermindInit);
        SubscribeLocalEvent<CMUXenoOvermindComponent, CMUXenoOvermindChangeFormActionEvent>(OnChangeForm);
        SubscribeLocalEvent<CMUXenoOvermindComponent, CMUXenoOvermindFormChangedEvent>(OnFormChanged);
    }

    private void OnOvermindInit(Entity<CMUXenoOvermindComponent> ent, ref ComponentStartup args)
    {
        SetIncorporealPhysics(ent.Owner, incorporeal: true);
        SetEyeState(ent.Owner, incorporeal: true);
    }

    private void OnFormChanged(Entity<CMUXenoOvermindComponent> ent, ref CMUXenoOvermindFormChangedEvent args)
    {
        SetIncorporealPhysics(ent.Owner, args.Incorporeal);
        SetEyeState(ent.Owner, args.Incorporeal);
    }

    private void SetEyeState(EntityUid uid, bool incorporeal)
    {
        _eye.SetDrawFov(uid, !incorporeal);
        _eye.SetDrawLight(uid, !incorporeal);
        _eye.SetPvsScale(uid, incorporeal ? 1.5f : 1f);
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
        {
            toRebuild.Add((id, fixture));
        }

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

            _fixtures.TryCreateFixture(
                uid,
                shape,
                id,
                density,
                hard,
                layer,
                mask,
                friction,
                restitution,
                updates: false,
                manager: fixtures,
                body: physics);
        }

        _fixtures.FixtureUpdate(uid, manager: fixtures, body: physics);
        _physics.SetCanCollide(uid, !incorporeal, body: physics);

        if (incorporeal)
            _tag.RemoveTag(uid, DoorBumpOpenerTag);
        else
            _tag.AddTag(uid, DoorBumpOpenerTag);
    }
}
