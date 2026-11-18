using System.Numerics;
using Content.Shared._RMC14.Stealth;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ECS-712 (D17): the Medic's triage sight, the cheap version. A small mark over each clanmate and clan beast
/// within <see cref="BadBloodBloodRiteComponent.TriageRange"/>: green while well, amber once hurt, red while critical,
/// so the Medic knows where to run. Drawn for a local player who has the component. It is not shown on the bracer
/// map, and a cloaked clanmate shows no mark.
/// </summary>
public sealed class BadBloodTriageOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _players = default!;

    private readonly TransformSystem _transform;
    private readonly ContainerSystem _container;
    private readonly MobStateSystem _mobState;
    private readonly MobThresholdSystem _thresholds;
    private readonly Content.Shared.Damage.Systems.DamageableSystem _damageable;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private static readonly Color Well = Color.FromHex("#3cd65a");
    private static readonly Color Hurt = Color.FromHex("#e0a020");
    private static readonly Color Critical = Color.FromHex("#e02020");

    private const float MarkRadius = 0.12f;
    private const float MarkHeight = 0.9f;

    public BadBloodTriageOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entity.System<TransformSystem>();
        _container = _entity.System<ContainerSystem>();
        _mobState = _entity.System<MobStateSystem>();
        _thresholds = _entity.System<MobThresholdSystem>();
        _damageable = _entity.System<Content.Shared.Damage.Systems.DamageableSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_players.LocalEntity is not { } player ||
            !_entity.TryGetComponent(player, out BadBloodBloodRiteComponent? triage) ||
            !_entity.TryGetComponent(player, out TransformComponent? playerXform))
        {
            return;
        }

        var handle = args.WorldHandle;
        var center = _transform.GetWorldPosition(playerXform);
        var range = triage.TriageRange * triage.TriageRange;
        var map = playerXform.MapID;

        var clan = _entity.EntityQueryEnumerator<YautjaBadBloodComponent, MobStateComponent, TransformComponent>();
        while (clan.MoveNext(out var uid, out _, out var state, out var xform))
        {
            Mark(uid, player, state, xform, map, center, range, triage.TriageHurtFraction, handle);
        }

        var beasts = _entity.EntityQueryEnumerator<BadBloodBeastComponent, MobStateComponent, TransformComponent>();
        while (beasts.MoveNext(out var uid, out _, out var state, out var xform))
        {
            Mark(uid, player, state, xform, map, center, range, triage.TriageHurtFraction, handle);
        }
    }

    private void Mark(EntityUid uid,
        EntityUid player,
        MobStateComponent state,
        TransformComponent xform,
        Robust.Shared.Map.MapId map,
        Vector2 center,
        float range,
        float hurtFraction,
        DrawingHandleWorld handle)
    {
        if (uid == player || xform.MapID != map || _mobState.IsDead(uid, state))
            return;

        var position = _transform.GetWorldPosition(xform);
        if ((position - center).LengthSquared() > range)
            return;

        if (_container.IsEntityOrParentInContainer(uid, xform: xform) ||
            _entity.HasComponent<EntityActiveInvisibleComponent>(uid))
        {
            return;
        }

        Color color;
        if (_mobState.IsCritical(uid, state))
        {
            color = Critical;
        }
        else if (_entity.HasComponent<DamageableComponent>(uid) &&
                 _thresholds.TryGetThresholdForState(uid, MobState.Critical, out var crit) &&
                 _damageable.GetTotalDamage(uid) >= crit.Value * hurtFraction)
        {
            color = Hurt;
        }
        else
        {
            color = Well;
        }

        handle.DrawCircle(position + new Vector2(0, MarkHeight), MarkRadius, color);
    }
}

/// <summary>
/// CMU14 ECS-712: keeps <see cref="BadBloodTriageOverlay"/> on the overlay manager.
/// </summary>
public sealed class BadBloodTriageSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        _overlays.AddOverlay(new BadBloodTriageOverlay());
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<BadBloodTriageOverlay>();
    }
}
