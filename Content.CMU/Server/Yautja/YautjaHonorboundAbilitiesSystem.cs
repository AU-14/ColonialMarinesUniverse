using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Armor.Magnetic;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared._RMC14.Deafness;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids.Screech;
using Content.Shared.Actions;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Coordinates;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaHonorboundAbilitiesSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private RMCCameraShakeSystem _cameraShake = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private RMCMagneticSystem _magnetic = default!;
    [Dependency] private RMCObstacleSlammingSystem _obstacleSlamming = default!;
    [Dependency] private RMCPullingSystem _pulling = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private XenoScreechSystem _screech = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private ThrownItemSystem _thrownItem = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private YautjaCloakSystem _cloak = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, YautjaHonorRoarActionEvent>(OnHonorRoar);
        SubscribeLocalEvent<YautjaComponent, YautjaHuntingLeapActionEvent>(OnHuntingLeap);
        SubscribeLocalEvent<YautjaHuntingLeapingComponent, ThrowDoHitEvent>(OnLeapHit);
        SubscribeLocalEvent<YautjaHuntingLeapingComponent, LandEvent>(OnLeapLand);
    }

    public void GrantActions(Entity<YautjaComponent> ent)
    {
        if (HasComp<YautjaBadBloodComponent>(ent))
            return;

        _actions.AddAction(ent.Owner, ref ent.Comp.HonorRoarAction, ent.Comp.HonorRoarActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.HuntingLeapAction, ent.Comp.HuntingLeapActionId);
    }

    private void OnHonorRoar(Entity<YautjaComponent> hunter, ref YautjaHonorRoarActionEvent args)
    {
        if (args.Handled || args.Performer != hunter.Owner || HasComp<YautjaBadBloodComponent>(hunter))
            return;

        args.Handled = true;
        _audio.PlayPvs(hunter.Comp.HonorRoarSound, hunter);

        _mobs.Clear();
        _entityLookup.GetEntitiesInRange(Transform(hunter).Coordinates, hunter.Comp.HonorRoarRange, _mobs);
        foreach (var target in _mobs)
        {
            if (!CanAttack(hunter.Owner, target.Owner) ||
                !_screech.ApplyScreechEffects(hunter.Owner,
                    target.Owner,
                    hunter.Comp.HonorRoarDuration,
                    hunter.Comp.HonorRoarDuration))
            {
                continue;
            }

            _cameraShake.ShakeCamera(target, 8, 4);
            _screech.Deafen(hunter.Owner, target.Owner, hunter.Comp.HonorRoarDuration);
        }

        SpawnAttachedTo("CMEffectScreech", hunter.Owner.ToCoordinates());
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(hunter.Owner):player} used their honor roar");
    }

    private void OnHuntingLeap(Entity<YautjaComponent> hunter, ref YautjaHuntingLeapActionEvent args)
    {
        if (args.Handled ||
            args.Performer != hunter.Owner ||
            HasComp<YautjaBadBloodComponent>(hunter) ||
            !CanAttack(hunter.Owner, args.Target) ||
            HasComp<YautjaHuntingLeapingComponent>(hunter) ||
            !_melee.TryGetWeapon(hunter.Owner, out var weapon, out _) ||
            !TryGetLeapVector(hunter.Owner, args.Target, hunter.Comp.HuntingLeapRange, out var direction))
        {
            return;
        }

        args.Handled = true;
        _cloak.ForceDecloak(hunter.Owner);
        _pulling.TryStopAllPullsFromAndOn(hunter.Owner);

        var active = EnsureComp<YautjaHuntingLeapingComponent>(hunter);
        active.Target = args.Target;
        active.Weapon = weapon;
        active.Resolved = false;

        _obstacleSlamming.MakeImmune(hunter.Owner, 0.5f);
        _throwing.TryThrow(hunter.Owner,
            direction.Normalized() * hunter.Comp.HuntingLeapRange,
            hunter.Comp.HuntingLeapSpeed,
            animated: false);

        if (_transform.GetMapCoordinates(hunter).InRange(_transform.GetMapCoordinates(args.Target), 1.25f))
            ResolveLeapStrike((hunter.Owner, active));

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(hunter.Owner):hunter} hunting-leaped at {ToPrettyString(args.Target):target}");
    }

    private void OnLeapHit(Entity<YautjaHuntingLeapingComponent> hunter, ref ThrowDoHitEvent args)
    {
        if (args.Target == hunter.Comp.Target)
            ResolveLeapStrike(hunter);
    }

    private void OnLeapLand(Entity<YautjaHuntingLeapingComponent> hunter, ref LandEvent args)
    {
        if (!TerminatingOrDeleted(hunter.Comp.Target) &&
            _transform.GetMapCoordinates(hunter).InRange(_transform.GetMapCoordinates(hunter.Comp.Target), 1.5f))
        {
            ResolveLeapStrike(hunter);
        }

        RemCompDeferred<YautjaHuntingLeapingComponent>(hunter);
    }

    private void ResolveLeapStrike(Entity<YautjaHuntingLeapingComponent> hunter)
    {
        if (hunter.Comp.Resolved || !CanAttack(hunter.Owner, hunter.Comp.Target))
            return;

        hunter.Comp.Resolved = true;

        if (TryComp(hunter.Owner, out ThrownItemComponent? thrown) &&
            TryComp(hunter.Owner, out PhysicsComponent? physics))
        {
            _thrownItem.LandComponent(hunter.Owner, thrown, physics, true);
            _thrownItem.StopThrow(hunter.Owner, thrown);
        }

        if (TryComp(hunter.Comp.Weapon, out MeleeWeaponComponent? weapon))
        {
            weapon.NextAttack = _timing.CurTime;
            Dirty(hunter.Comp.Weapon, weapon);
            _melee.AttemptLightAttack(hunter.Owner,
                hunter.Comp.Weapon,
                weapon,
                hunter.Comp.Target,
                requireCombatMode: false,
                predicted: false,
                animationOverride: weapon.Animation);
        }

        ForceDropActiveWeapon(hunter.Comp.Target);
        RemCompDeferred<YautjaHuntingLeapingComponent>(hunter);
    }

    private void ForceDropActiveWeapon(EntityUid target)
    {
        if (!_hands.TryGetActiveItem(target, out var held))
            return;

        _magnetic.UnlinkForForcedDrop(held.Value);
        _hands.TryDrop(target, held.Value, checkActionBlocker: false);
    }

    private bool CanAttack(EntityUid hunter, EntityUid target)
    {
        return hunter != target &&
               !TerminatingOrDeleted(target) &&
               !HasComp<YautjaComponent>(target) &&
               !HasComp<YautjaThrallComponent>(target) &&
               TryComp(target, out MobStateComponent? state) &&
               !_mobState.IsDead(target, state);
    }

    private bool TryGetLeapVector(EntityUid hunter, EntityUid target, float range, out Vector2 direction)
    {
        var origin = _transform.GetMapCoordinates(hunter);
        var destination = _transform.GetMapCoordinates(target);
        direction = destination.Position - origin.Position;
        return origin.MapId == destination.MapId &&
               direction.LengthSquared() > 0 &&
               direction.LengthSquared() <= range * range;
    }
}
