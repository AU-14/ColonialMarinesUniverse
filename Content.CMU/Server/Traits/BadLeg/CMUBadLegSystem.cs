using System.Numerics;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Traits.BadLeg;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Traits.BadLeg;

/// <summary>
///     Walking on a bad leg without a cane builds up strain. They are warned as it builds, and when
///     it maxes out the leg gives out and they fall. Resting the leg or walking with a cane eases it.
/// </summary>
public sealed class CMUBadLegSystem : EntitySystem
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>
    ///     Moves bigger than this between checks are teleports or being dragged, not walking.
    /// </summary>
    private const float MaxStepDistance = 5f;

    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, MapCoordinates> _lastPosition = new();
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUBadLegComponent, ComponentShutdown>((uid, _, _) => _lastPosition.Remove(uid));
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;
        var seconds = (float) UpdateInterval.TotalSeconds;

        var query = EntityQueryEnumerator<CMUBadLegComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var leg, out var xform))
        {
            var position = _transform.GetMapCoordinates(uid, xform);
            var walked = 0f;
            if (_lastPosition.TryGetValue(uid, out var last) && last.MapId == position.MapId)
                walked = Vector2.Distance(last.Position, position.Position);

            _lastPosition[uid] = position;

            // Only their own steps count: not riding, lying down, being carried or out cold.
            var onFoot = walked is > 0.05f and <= MaxStepDistance &&
                         xform.ParentUid == xform.GridUid &&
                         !_standing.IsDown(uid) &&
                         !(TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled) &&
                         _mobState.IsAlive(uid);

            if (onFoot && !HasCane(uid))
            {
                leg.Strain += walked * leg.StrainPerTile;
            }
            else
            {
                leg.Strain = MathF.Max(0f, leg.Strain - leg.RecoveryPerSecond * seconds);
                if (leg.Strain < leg.MaxStrain * 0.4f)
                    leg.WarningsShown = 0;

                continue;
            }

            if (leg.Strain >= leg.MaxStrain)
            {
                GiveOut((uid, leg));
                continue;
            }

            if (leg.WarningsShown < 2 && leg.Strain >= leg.MaxStrain * 0.8f)
            {
                leg.WarningsShown = 2;
                _popup.PopupEntity(Loc.GetString("cmu-bad-leg-warning-severe"), uid, uid, PopupType.MediumCaution);
            }
            else if (leg.WarningsShown < 1 && leg.Strain >= leg.MaxStrain * 0.5f)
            {
                leg.WarningsShown = 1;
                _popup.PopupEntity(Loc.GetString("cmu-bad-leg-warning"), uid, uid, PopupType.SmallCaution);
            }
        }
    }

    private bool HasCane(EntityUid uid)
    {
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (HasComp<MobilityAidComponent>(held))
                return true;
        }

        return false;
    }

    private void GiveOut(Entity<CMUBadLegComponent> ent)
    {
        ent.Comp.Strain = ent.Comp.MaxStrain * 0.25f;
        ent.Comp.WarningsShown = 0;

        _stun.TryKnockdown(ent.Owner, ent.Comp.KnockdownTime, refresh: true);
        _popup.PopupEntity(Loc.GetString("cmu-bad-leg-give-out"), ent, ent, PopupType.LargeCaution);
        _popup.PopupEntity(
            Loc.GetString("cmu-bad-leg-give-out-others", ("user", Identity.Entity(ent, EntityManager))),
            ent,
            Filter.PvsExcept(ent),
            true,
            PopupType.Small);
    }
}
