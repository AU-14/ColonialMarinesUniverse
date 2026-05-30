using Content.Shared._RMC14.Deafness;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Containers;
using Content.Shared.Coordinates;
using Content.Shared.Jittering;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Screech;

public sealed partial class XenoScreechSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private ExamineSystemShared _examineSystem = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedDeafnessSystem _deaf = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private XenoSystem _xeno = default!;
    [Dependency] private RMCCameraShakeSystem _cameraShake = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoScreechComponent, XenoScreechActionEvent>(OnXenoScreechAction);
        SubscribeLocalEvent<ScreechScatterComponent, GunRefreshModifiersEvent>(OnScreechScatterRefresh);
        SubscribeLocalEvent<ScreechBlindComponent, ComponentStartup>(OnScreechBlindStartup);
        SubscribeLocalEvent<ScreechBlindComponent, ComponentShutdown>(OnScreechBlindShutdown);
    }

    private void OnXenoScreechAction(Entity<XenoScreechComponent> xeno, ref XenoScreechActionEvent args)
    {
        if (args.Handled)
            return;

        var attempt = new XenoScreechAttemptEvent();
        RaiseLocalEvent(xeno, ref attempt);

        if (attempt.Cancelled)
            return;

        if (!_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        if (!TryComp(xeno, out TransformComponent? xform))
            return;

        args.Handled = true;

        if (_net.IsServer)
            _audio.PlayPvs(xeno.Comp.Sound, xeno);

        _mobs.Clear();
        _entityLookup.GetEntitiesInRange(xform.Coordinates, xeno.Comp.Range, _mobs);

        foreach (var receiver in _mobs)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, receiver))
                continue;

            if (!ApplyScreechEffects(xeno, receiver, xeno.Comp.SlowTime, xeno.Comp.BlindTime))
                continue;

            _cameraShake.ShakeCamera(receiver, xeno.Comp.ScreenShakeShakes, xeno.Comp.ScreenShakeStrength);
            Deafen(xeno, receiver, xeno.Comp.DeafTime);
        }

        if (_net.IsServer)
            SpawnAttachedTo(xeno.Comp.Effect, xeno.Owner.ToCoordinates());
    }

    private bool ApplyScreechEffects(EntityUid xeno, EntityUid receiver, TimeSpan slowTime, TimeSpan blindTime)
    {
        if (_mobState.IsDead(receiver))
            return false;

        if (!_examineSystem.InRangeUnOccluded(xeno, receiver))
            return false;

        _slow.TrySuperSlowdown(receiver, slowTime, ignoreDurationModifier: true);
        _jitter.DoJitter(receiver, slowTime, true, 3f, 8f);
        var blind = EnsureComp<ScreechBlindComponent>(receiver);
        blind.EndsAt = _timing.CurTime + blindTime;
        Dirty(receiver, blind);
        return true;
    }

    private void Deafen(EntityUid xeno, EntityUid receiver, TimeSpan time)
    {
        if (_mobState.IsDead(receiver))
            return;

        if (!_examineSystem.InRangeUnOccluded(xeno, receiver))
            return;

        _deaf.TryDeafen(receiver, time, false);
    }

    private void OnScreechScatterRefresh(Entity<ScreechScatterComponent> ent, ref GunRefreshModifiersEvent args)
    {
        args.MinAngle += ent.Comp.AngleIncrease;
        args.MaxAngle += ent.Comp.AngleIncrease;
    }

    private void OnScreechBlindStartup(Entity<ScreechBlindComponent> ent, ref ComponentStartup args)
    {
        AddScatterToHeldGuns(ent);
    }

    private void OnScreechBlindShutdown(Entity<ScreechBlindComponent> ent, ref ComponentShutdown args)
    {
        RemoveScatterFromHeldGuns(ent);
    }

    private void AddScatterToHeldGuns(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return;

        foreach (var name in hands.Hands.Keys)
        {
            if (!_container.TryGetContainer(user, name, out var container))
                continue;

            foreach (var held in container.ContainedEntities)
            {
                if (!HasComp<GunComponent>(held))
                    continue;

                EnsureComp<ScreechScatterComponent>(held);
                _gun.RefreshModifiers(held);
            }
        }
    }

    private void RemoveScatterFromHeldGuns(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return;

        foreach (var name in hands.Hands.Keys)
        {
            if (!_container.TryGetContainer(user, name, out var container))
                continue;

            foreach (var held in container.ContainedEntities)
            {
                if (!RemComp<ScreechScatterComponent>(held))
                    continue;

                _gun.RefreshModifiers(held);
            }
        }
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<ScreechBlindComponent>();

        while (query.MoveNext(out var uid, out var blind))
        {
            if (blind.EndsAt != null && time < blind.EndsAt)
                continue;

            RemCompDeferred<ScreechBlindComponent>(uid);
        }
    }
}
