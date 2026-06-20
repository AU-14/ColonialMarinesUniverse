using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Boots.SpikeBoots;

public sealed class SpikeBootsSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly RMCCameraShakeSystem _cameraShake = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private EntityQuery<XenoWeedsComponent> _weedsQuery;
    private EntityQuery<XenoConstructComponent> _xenoConstructQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;
    private EntityQuery<XenoComponent> _xenoQuery;
    private EntityQuery<XenoRestingComponent> _xenoRestingQuery;
    private EntityQuery<XenoParasiteComponent> _parasiteQuery;
    private EntityQuery<BeingFiremanCarriedComponent> _carriedQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    private readonly List<EntityUid> _expiredCooldowns = new();

    private static readonly TimeSpan MarineDamageCooldown = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan XenoDamageCooldown = TimeSpan.FromSeconds(0.75);
    private const float MarineDamageAmount = 10f;
    private const float XenoDamageAmount = 60f;

    // Organic crunch when resin is crushed underfoot.
    private static readonly SoundSpecifier ResinCrunchSound =
        new SoundPathSpecifier("/Audio/Effects/bone_rattle.ogg",
            AudioParams.Default.WithVolume(-4f).WithMaxDistance(4f));

    // Heavy boot thud audible to nearby players when a downed target is stomped.
    private static readonly SoundSpecifier StepDamageSound =
        new SoundPathSpecifier("/Audio/Effects/Footsteps/largethud.ogg",
            AudioParams.Default.WithVolume(-3f).WithMaxDistance(4f));

    public override void Initialize()
    {
        base.Initialize();

        _weedsQuery = GetEntityQuery<XenoWeedsComponent>();
        _xenoConstructQuery = GetEntityQuery<XenoConstructComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        _xenoQuery = GetEntityQuery<XenoComponent>();
        _xenoRestingQuery = GetEntityQuery<XenoRestingComponent>();
        _parasiteQuery = GetEntityQuery<XenoParasiteComponent>();
        _carriedQuery = GetEntityQuery<BeingFiremanCarriedComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<SpikeBootsComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<SpikeBootsComponent, GotUnequippedEvent>(OnUnequipped);
        // Speed is supplied by ClothingSpeedModifierComponent in YAML.
        // RefreshMovementSpeedModifiers must be called explicitly on equip/unequip
        // to make the inventory relay pick up the new value.
    }

    private void OnEquipped(Entity<SpikeBootsComponent> boots, ref GotEquippedEvent args)
    {
        if (args.Slot != "shoes")
            return;

        EnsureComp<SpikeBootsWearerComponent>(args.Equipee);
        _movementSpeed.RefreshMovementSpeedModifiers(args.Equipee);
    }

    private void OnUnequipped(Entity<SpikeBootsComponent> boots, ref GotUnequippedEvent args)
    {
        if (args.Slot != "shoes")
            return;

        RemComp<SpikeBootsWearerComponent>(args.Equipee);
        _movementSpeed.RefreshMovementSpeedModifiers(args.Equipee);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var enumerator = EntityQueryEnumerator<SpikeBootsWearerComponent, TransformComponent>();

        while (enumerator.MoveNext(out var uid, out var wearer, out var xform))
        {
            if (xform.GridUid is not { } gridUid)
                continue;

            // Don't trigger while the wearer is down, being carried, or devoured.
            if (_standing.IsDown(uid) || _carriedQuery.HasComp(uid) || _container.IsEntityInContainer(uid))
                continue;

            if (!_gridQuery.TryGetComponent(gridUid, out var gridComp))
                continue;

            var tilePos = _map.TileIndicesFor(gridUid, gridComp, xform.Coordinates);

            // Only fire effects on tile entry — enforces "must leave tile" requirement.
            if (tilePos == wearer.LastTile)
                continue;

            wearer.LastTile = tilePos;

            var onTile = _lookup.GetLocalEntitiesIntersecting(gridUid, tilePos, gridComp: gridComp);

            // Per-tile-entry flags so effects fire once even if multiple targets are present.
            var resinEffectsShown = false;
            var damageEffectsShown = false;

            foreach (var target in onTile)
            {
                if (target == uid)
                    continue;

                // Destroy xeno weeds and weed nodes only — skip hive structures (cores, pylons,
                // clusters) which also carry XenoWeedsComponent but have XenoConstructComponent.
                if (_weedsQuery.HasComp(target))
                {
                    if (_xenoConstructQuery.HasComp(target))
                        continue;

                    QueueDel(target);

                    if (!resinEffectsShown)
                    {
                        _popup.PopupEntity(
                            "You feel the ground crack beneath your boots.",
                            uid, uid, PopupType.Small);

                        _audio.PlayPvs(ResinCrunchSound, uid);

                        resinEffectsShown = true;
                    }
                    continue;
                }

                if (!_mobStateQuery.HasComp(target))
                    continue;

                if (!IsPassable(target))
                    continue;

                var isXeno = _xenoQuery.HasComp(target);
                var cooldown = isXeno ? XenoDamageCooldown : MarineDamageCooldown;
                var damage = isXeno ? XenoDamageAmount : MarineDamageAmount;

                // Per-target cooldown (tile-exit gate is the primary lock;
                // this guards against rapid tile-hop cheese).
                if (wearer.TargetCooldowns.TryGetValue(target, out var cooldownEnd) && now < cooldownEnd)
                    continue;

                wearer.TargetCooldowns[target] = now + cooldown;

                var dmg = new DamageSpecifier();
                dmg.DamageDict["Piercing"] = FixedPoint2.New(damage);
                _damageable.TryChangeDamage(target, dmg, ignoreResistances: false);

                if (!damageEffectsShown)
                {
                    _popup.PopupEntity(
                        "You feel your boots ripping through flesh.",
                        uid, uid, PopupType.SmallCaution);

                    _audio.PlayPvs(StepDamageSound, uid);

                    // Tiny single-kick screenshake felt only by the wearer.
                    _cameraShake.ShakeCamera(uid, 1, 2);

                    damageEffectsShown = true;
                }
            }

            // Prune stale cooldown entries on tile change (rare). System-level list avoids allocation.
            if (wearer.TargetCooldowns.Count > 0)
            {
                foreach (var (entity, expiry) in wearer.TargetCooldowns)
                {
                    if (now >= expiry || Deleted(entity))
                        _expiredCooldowns.Add(entity);
                }
                foreach (var entity in _expiredCooldowns)
                    wearer.TargetCooldowns.Remove(entity);
                _expiredCooldowns.Clear();
            }
        }
    }

    /// <summary>
    /// Returns true if the entity can be physically walked through.
    /// </summary>
    private bool IsPassable(EntityUid target)
    {
        // Prone, critical, unconscious, sleeping — StandingState.Standing = false.
        if (_standing.IsDown(target))
            return true;

        // Xeno resting: cancels AttemptMobCollideEvent independently of StandingState.
        if (_xenoRestingQuery.HasComp(target))
            return true;

        // Parasites use SmallMobLayer/SmallMobMask which does not interact with
        // bipedal mob physics layers — they are always physically passable.
        if (_parasiteQuery.HasComp(target))
            return true;

        return false;
    }
}
