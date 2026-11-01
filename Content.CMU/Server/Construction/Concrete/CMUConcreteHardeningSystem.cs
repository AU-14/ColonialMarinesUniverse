using Content.Shared._RMC14.Barricade.Components;
using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Construction.Concrete;

public sealed partial class CMUConcreteHardeningSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUConcreteHardeningComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUConcreteHardeningComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<CMUConcreteHardeningComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.HardenAt = _timing.CurTime + ent.Comp.Delay;
    }

    private void OnExamined(Entity<CMUConcreteHardeningComponent> ent, ref ExaminedEvent args)
    {
        var remaining = ent.Comp.HardenAt - _timing.CurTime;
        var minutes = Math.Max(1, (int) Math.Ceiling(remaining.TotalMinutes));
        args.PushMarkup(Loc.GetString("cmu-concrete-hardening-examine", ("minutes", minutes)));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUConcreteHardeningComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (time < comp.HardenAt || TerminatingOrDeleted(uid))
                continue;

            Harden((uid, comp, xform));
        }
    }

    private void Harden(Entity<CMUConcreteHardeningComponent, TransformComponent> ent)
    {
        var (uid, comp, xform) = ent;
        var coordinates = xform.Coordinates;

        var hardened = Spawn(comp.Hardened, coordinates);
        _transform.SetLocalRotation(hardened, xform.LocalRotation);

        // The wire cannot be carried over to the new barricade, so it drops where it was strung.
        if (TryComp<BarbedComponent>(uid, out var barbed) && barbed.IsBarbed)
            Spawn(barbed.Spawn, coordinates);

        QueueDel(uid);
    }
}
