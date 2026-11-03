using System.Numerics;
using Content.Server.Decals;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja;

public sealed partial class CMUYautjaEffectsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private BloodstreamSystem _blood = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<CMUYautjaCasterAimEvent>(OnAim);
        SubscribeLocalEvent<YautjaStoredGearComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<YautjaCasterProjectileRefundComponent, ProjectileHitEvent>(OnPlasmaHit);
    }

    private void OnAim(CMUYautjaCasterAimEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryGetEntity(ev.Caster, out var caster) ||
            _hands.GetActiveItem(user) != caster || !HasComp<YautjaCasterComponent>(caster) ||
            !float.IsFinite(ev.Position.X) || !float.IsFinite(ev.Position.Y))
            return;

        var origin = _transform.GetMapCoordinates(user);
        if (origin.MapId != ev.Position.MapId)
            return;

        var aim = EnsureComp<CMUYautjaCasterAimComponent>(user);
        if (aim.ExpiresAt <= _timing.CurTime)
            aim.StartedAt = _timing.CurTime;
        // A client may update a sight ten times a second, but cannot direct another player's caster.
        if (aim.ExpiresAt - _timing.CurTime > TimeSpan.FromSeconds(.31))
            return;
        var delta = ev.Position.Position - origin.Position;
        aim.Position = origin.Position + delta / MathF.Max(1, delta.Length() / 20);
        aim.Caster = caster.Value;
        aim.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(.4);
        Dirty(user, aim);
    }

    private void OnShot(Entity<YautjaStoredGearComponent> caster, ref GunShotEvent args)
    {
        if (!HasComp<YautjaCasterComponent>(caster))
            return;
        var effect = Emit(CMUYautjaEffect.PlasmaMuzzle, Transform(args.User).Coordinates, .594f, args.User);
        effect.Comp.Destination = _transform.ToMapCoordinates(args.ToCoordinates).Position;
        foreach (var (uid, _) in args.Ammo)
        {
            if (TryComp<CMUYautjaPlasmaVisualComponent>(uid, out var visual))
            {
                effect.Comp.PlasmaMode = visual.Mode;
                break;
            }
        }
        Dirty(effect);
    }

    private void OnPlasmaHit(Entity<YautjaCasterProjectileRefundComponent> projectile, ref ProjectileHitEvent args)
    {
        // A projectile may intersect several fixtures; its impact is a single visual event.
        if (!projectile.Comp.Fired || HasComp<CMUYautjaPlasmaImpactPlayedComponent>(projectile))
            return;
        AddComp<CMUYautjaPlasmaImpactPlayedComponent>(projectile);
        var effect = Emit(CMUYautjaEffect.PlasmaImpact, Transform(projectile).Coordinates, 2.646f);
        if (TryComp<CMUYautjaPlasmaVisualComponent>(projectile, out var visual))
            effect.Comp.PlasmaMode = visual.Mode;
        Dirty(effect);
    }

    public Entity<CMUYautjaEffectComponent> Emit(CMUYautjaEffect kind, EntityCoordinates at, float seconds,
        EntityUid? target = null)
    {
        var uid = Spawn(null, at);
        _transform.AttachToGridOrMap(uid);
        var effect = AddComp<CMUYautjaEffectComponent>(uid);
        effect.Kind = kind;
        effect.Target = target;
        effect.StartedAt = _timing.CurTime;
        effect.EndsAt = _timing.CurTime + TimeSpan.FromSeconds(seconds);
        Dirty(uid, effect);
        return (uid, effect);
    }

    public void Recall(CMUYautjaEffect kind, EntityUid item, EntityUid user, MapCoordinates? from = null)
    {
        var origin = from ?? _transform.GetMapCoordinates(item);
        if (origin.MapId != _transform.GetMapCoordinates(user).MapId)
            return;
        var effect = Emit(kind, Transform(user).Coordinates,
            kind == CMUYautjaEffect.FalconRecall ? CMUYautjaPresentation.FalconDuration : CMUYautjaPresentation.RecallDuration,
            user);
        effect.Comp.Destination = origin.Position;
        if (kind != CMUYautjaEffect.FalconRecall)
            effect.Comp.Item = item;
        else if (TryComp<YautjaFalconDroneDeployedComponent>(item, out var deployed))
            effect.Comp.Item = deployed.DroneItem;
        effect.Comp.BadBloodDrone = MetaData(item).EntityPrototype?.ID == "CMUYautjaFalconDroneBadBloodDeployed";
        Dirty(effect);
    }

    public void Motion(EntityUid target, CMUYautjaEffect kind, float seconds, EntityUid? controller = null)
    {
        var motion = EnsureComp<CMUYautjaMotionComponent>(target);
        motion.Kind = kind;
        motion.Controller = controller;
        motion.StartedAt = _timing.CurTime;
        motion.EndsAt = _timing.CurTime + TimeSpan.FromSeconds(seconds);
        Dirty(target, motion);
    }

    public void Land(EntityUid target)
    {
        if (TerminatingOrDeleted(target) || !TryComp<CMUYautjaMotionComponent>(target, out var motion) ||
            motion.Kind != CMUYautjaEffect.Leap)
            return;
        Motion(target, CMUYautjaEffect.Landing, .48f);
        Emit(CMUYautjaEffect.Landing, Transform(target).Coordinates, 1.824f);
    }

    public void StartButcher(EntityUid victim, EntityUid hunter, YautjaButcherProcedure procedure, int stage,
        bool xeno, TimeSpan duration)
    {
        var visual = EnsureComp<CMUYautjaButcherVisualComponent>(victim);
        visual.ActiveStage = stage;
        visual.Procedure = procedure;
        visual.Hunter = hunter;
        visual.Xeno = xeno;
        visual.Blood = _blood.GetBloodReferenceSolution(victim) is { } blood
            ? blood.GetColor(_prototypes)
            : Color.FromHex(xeno ? "#B3D52B" : "#A01423");
        visual.StartedAt = _timing.CurTime;
        visual.EndsAt = _timing.CurTime + duration;
        Dirty(victim, visual);
    }

    public void EndButcher(EntityUid victim, EntityUid hunter, TimeSpan startedAt, bool completed, int stage)
    {
        if (!TryComp<CMUYautjaButcherVisualComponent>(victim, out var visual) ||
            visual.Hunter != hunter || visual.StartedAt != startedAt)
            return;

        visual.ActiveStage = 0;
        visual.Hunter = null;
        if (completed)
        {
            if (visual.Procedure == YautjaButcherProcedure.Skin)
                visual.CompletedStage = Math.Max(visual.CompletedStage, stage);
            // Real cleanable decals remain after the victim is moved or deleted.
            for (var i = 0; i < Math.Max(1, stage); i++)
            {
                var offset = new Vector2(MathF.Cos(i * 2.4f), MathF.Sin(i * 2.4f)) * (.12f + i * .07f);
                _decals.TryAddDecal($"CMUBloodSplatter{1 + i % 3 * 2}",
                    Transform(victim).Coordinates.Offset(offset - new Vector2(.5f)), out _,
                    color: visual.Blood, cleanable: true);
            }
        }
        Dirty(victim, visual);
    }

    public override void Update(float frameTime)
    {
        var effects = EntityQueryEnumerator<CMUYautjaEffectComponent>();
        while (effects.MoveNext(out var uid, out var effect))
        {
            if (_timing.CurTime >= effect.EndsAt)
                QueueDel(uid);
        }

        var motions = EntityQueryEnumerator<CMUYautjaMotionComponent>();
        while (motions.MoveNext(out var uid, out var motion))
        {
            // Land events end a leap; collision timing need not match its nominal flight duration.
            var deadline = motion.Kind == CMUYautjaEffect.Leap ? motion.EndsAt + TimeSpan.FromSeconds(3) : motion.EndsAt;
            if (_timing.CurTime >= deadline && motion.Kind != CMUYautjaEffect.FalconDeploy)
                RemCompDeferred<CMUYautjaMotionComponent>(uid);
        }
        var aims = EntityQueryEnumerator<CMUYautjaCasterAimComponent>();
        while (aims.MoveNext(out var uid, out var aim))
        {
            if (_timing.CurTime >= aim.ExpiresAt || _hands.GetActiveItem(uid) != aim.Caster)
                RemCompDeferred<CMUYautjaCasterAimComponent>(uid);
        }
    }
}

[RegisterComponent]
public sealed partial class CMUYautjaPlasmaImpactPlayedComponent : Component;
