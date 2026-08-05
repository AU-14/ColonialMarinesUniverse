using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Random;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Audio;
using Content.Shared.CombatMode;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private CMUZLevelShootingSystem _zLevelShooting = default!;
    [Dependency] private RechargeBasicEntityAmmoSystem _recharge = default!;
    [Dependency] private SharedCombatModeSystem _combatMode = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] protected DamageableSystem Damageable = default!;
    [Dependency] protected ExamineSystemShared Examine = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected ISharedAdminLogManager Logs = default!;
    [Dependency] protected SharedActionsSystem Actions = default!;
    [Dependency] protected SharedAppearanceSystem Appearance = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected SharedContainerSystem Containers = default!;
    [Dependency] protected SharedMapSystem Maps = default!;
    [Dependency] protected SharedPhysicsSystem Physics = default!;
    [Dependency] protected SharedPointLightSystem Lights = default!;
    [Dependency] protected SharedPopupSystem PopupSystem = default!;
    [Dependency] protected SharedProjectileSystem Projectiles = default!;
    [Dependency] protected SharedRMCFlamerSystem RMCFlamer = default!;
    [Dependency] protected SharedTransformSystem TransformSystem = default!;
    [Dependency] protected TagSystem TagSystem = default!;
    [Dependency] protected ThrowingSystem ThrowingSystem = default!;

    /// <summary>
    /// Default projectile speed
    /// </summary>
    public const float ProjectileSpeed = 40f;

    /// <summary>
    ///     Name of the container slot used as the gun's chamber
    /// </summary>
    public const string ChamberSlot = "gun_chamber";

    /// <summary>
    ///     Name of the container slot used as the gun's magazine
    /// </summary>
    public const string MagazineSlot = "gun_magazine";

    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    private const float InteractNextFire = 0.3f;
    private const double SafetyNextFire = 0.5;
    private const float EjectOffset = 0.4f;
    private const float DamagePitchVariation = 0.05f;
    private const int MaxPredictedProjectilesPerRequest = 256;
    protected const string AmmoExamineColor = "yellow";
    protected const string FireRateExamineColor = "yellow";
    public const string ModeExamineColor = "cyan";

    public override void Initialize()
    {
        SubscribeAllEvent<RequestShootEvent>(OnShootRequest);
        SubscribeAllEvent<RequestStopShootEvent>(OnStopShootRequest);
        SubscribeLocalEvent<GunComponent, MeleeHitEvent>(OnGunMelee);

        // Ammo providers
        InitializeBallistic();
        InitializeBattery();
        InitializeCartridge();
        InitializeChamberMagazine();
        InitializeCustomAmmoCounter();
        InitializeMagazine();
        InitializeRevolver();
        InitializeBasicEntity();
        InitializeClothing();
        InitializeContainer();
        InitializeSolution();

        // Interactions
        SubscribeLocalEvent<GunComponent, GetVerbsEvent<AlternativeVerb>>(OnAltVerb);
        SubscribeLocalEvent<GunComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<GunComponent, CycleModeEvent>(OnCycleMode);
        SubscribeLocalEvent<GunComponent, HandSelectedEvent>(OnGunSelected);
        SubscribeLocalEvent<GunComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<GunComponent> gun, ref MapInitEvent args)
    {
#if DEBUG
        if (gun.Comp.NextFire > Timing.CurTime)
            Log.Warning($"Initializing a map that contains an entity that is on cooldown. Entity: {ToPrettyString(gun)}");

        DebugTools.Assert((gun.Comp.AvailableModes & gun.Comp.SelectedMode) != 0x0);
#endif

        RefreshModifiers((gun, gun));
    }

    private void OnGunMelee(Entity<GunComponent> ent, ref MeleeHitEvent args)
    {
        if (!ent.Comp.MeleeCooldownOnShoot)
            return;

        if (!TryComp<MeleeWeaponComponent>(ent, out var melee))
            return;

        if (melee.NextAttack > ent.Comp.NextFire)
        {
            ent.Comp.NextFire = melee.NextAttack;
            DirtyField(ent.AsNullable(), nameof(GunComponent.NextFire));
        }
    }

    private void OnShootRequest(RequestShootEvent msg, EntitySessionEventArgs args)
    {
        // The input system has already simulated the shot so it can include the
        // predicted projectile IDs in this request. Prediction replays still run here.
        if (_netManager.IsClient && Timing.IsFirstTimePredicted)
            return;

        var requestedPredictions = msg.Shot is { Count: > 0 } requested
            ? requested.GetRange(0, Math.Min(requested.Count, MaxPredictedProjectilesPerRequest))
            : null;
        var acceptedPredictions = new HashSet<int>();
        try
        {
            var user = args.SenderSession.AttachedEntity;

            if (user == null ||
                !_combatMode.IsInCombatMode(user) ||
                !TryGetGun(user.Value, out var gun))
            {
                return;
            }

            if (gun.Owner != GetEntity(msg.Gun))
                return;

            var shootCoordinates = GetCoordinates(msg.Coordinates);
            var shootMapCoordinates = TransformSystem.ToMapCoordinates(shootCoordinates);
            if (shootMapCoordinates.MapId == MapId.Nullspace || Transform(gun).MapID != shootMapCoordinates.MapId)
                return;

            var target = GetRmcShootTarget(shootCoordinates, GetEntity(msg.Target));
            if (target is { } resolvedTarget &&
                (!TryComp(resolvedTarget, out TransformComponent? targetTransform) ||
                 targetTransform.MapID != shootMapCoordinates.MapId))
            {
                target = null;
            }

            gun.Comp.ShootCoordinates = shootCoordinates;
            gun.Comp.Target = target;
            if (ShouldRearmSemiAuto(
                    msg.Continuous,
                    gun.Comp.SelectedMode,
                    gun.Comp.AvailableModes,
                    HasComp<GunClickToFireComponent>(gun)))
            {
                ResetShotCounter(gun, gun.Comp);
            }

            AttemptShootInternal(
                user.Value,
                gun,
                requestedPredictions,
                args.SenderSession,
                acceptedPredictions);
        }
        finally
        {
            if (_netManager.IsServer)
                SendPredictedProjectileCleanup(requestedPredictions, acceptedPredictions, args.SenderSession);
        }
    }

    private void SendPredictedProjectileCleanup(
        IReadOnlyList<int>? requested,
        IReadOnlySet<int> accepted,
        ICommonSession session)
    {
        if (requested == null)
            return;

        var cleanup = new PredictedProjectileCleanupEvent();
        var added = new HashSet<int>();
        foreach (var projectile in requested)
        {
            if (!accepted.Contains(projectile) && added.Add(projectile))
                cleanup.Projectiles.Add(projectile);
        }

        if (cleanup.Projectiles.Count > 0)
            RaiseNetworkEvent(cleanup, session);
    }

    private void OnStopShootRequest(RequestStopShootEvent ev, EntitySessionEventArgs args)
    {
        var gunUid = GetEntity(ev.Gun);

        if (args.SenderSession.AttachedEntity == null ||
            !TryComp<GunComponent>(gunUid, out var gun) ||
            !TryGetGun(args.SenderSession.AttachedEntity.Value, out var userGun))
        {
            return;
        }

        if (userGun != (gunUid, gun))
            return;

        StopShooting(userGun);
    }

    public bool CanShoot(GunComponent component)
    {
        if (component.NextFire > Timing.CurTime)
            return false;

        return true;
    }

    /// <summary>
    ///     Tries to get an entity with <see cref="GunComponent"/> from the specified entity's hands, or from the entity itself.
    /// </summary>
    /// <param name="entity">Entity that is holding the gun, or is the gun</param>
    /// <param name="gun">Gun entity to return</param>
    /// <returns>True if gun was found</returns>
    public bool TryGetGun(EntityUid entity, out Entity<GunComponent> gun)
    {
        gun = default;

        if (TryGetRmcPriorityGun(entity, out gun))
            return true;

        if (_hands.GetActiveItem(entity) is { } held &&
            TryComp(held, out GunComponent? gunComp))
        {
            gun = (held, gunComp);
            return true;
        }

        // Last resort is check if the entity itself is a gun.
        if (TryComp(entity, out gunComp))
        {
            gun = (entity, gunComp);
            return true;
        }

        return TryGetRmcFallbackGun(entity, out gun);
    }

    public static bool ShouldRearmSemiAuto(
        bool holdToFire,
        SelectiveFire selectedMode,
        SelectiveFire availableModes,
        bool clickToFire)
    {
        return holdToFire &&
               selectedMode == SelectiveFire.SemiAuto &&
               !clickToFire &&
               (availableModes & SelectiveFire.FullAuto) == 0;
    }

    private void StopShooting(Entity<GunComponent> ent)
    {
        if (ent.Comp.ShotCounter == 0)
            return;

        ent.Comp.ShotCounter = 0;
        ent.Comp.ShootCoordinates = null;
        ent.Comp.Target = null;
        DirtyField(ent.AsNullable(), nameof(GunComponent.ShotCounter));
    }

    /// <summary>
    /// Attempts to shoot at the target coordinates. Resets the shot counter after every shot.
    /// </summary>
    public bool AttemptShoot(EntityUid user, Entity<GunComponent> gun, EntityCoordinates toCoordinates, EntityUid? target = null)
    {
        gun.Comp.ShootCoordinates = toCoordinates;
        gun.Comp.Target = target;
        var result = AttemptShootInternal(user, gun) != null;
        gun.Comp.ShotCounter = 0;
        DirtyField(gun.AsNullable(), nameof(GunComponent.ShotCounter));
        return result;
    }

    /// <summary>
    /// Shoots by assuming the gun is the user at default coordinates.
    /// </summary>
    public bool AttemptShoot(Entity<GunComponent> gun)
    {
        var coordinates = new EntityCoordinates(gun, gun.Comp.DefaultDirection);
        gun.Comp.ShootCoordinates = coordinates;
        var result = AttemptShootInternal(gun, gun) != null;
        gun.Comp.ShotCounter = 0;
        return result;
    }

    internal List<EntityUid>? AttemptShootRequest(
        EntityUid user,
        Entity<GunComponent> gun,
        EntityCoordinates toCoordinates,
        EntityUid? target = null,
        IReadOnlyList<int>? predictedProjectiles = null,
        ICommonSession? userSession = null)
    {
        gun.Comp.ShootCoordinates = toCoordinates;
        gun.Comp.Target = target;
        return AttemptShootInternal(user, gun, predictedProjectiles, userSession);
    }

    private List<EntityUid>? AttemptShootInternal(
        EntityUid user,
        Entity<GunComponent> gun,
        IReadOnlyList<int>? predictedProjectiles = null,
        ICommonSession? userSession = null,
        ISet<int>? acceptedPredictedProjectiles = null)
    {
        if (gun.Comp.FireRateModified <= 0f ||
            !_actionBlockerSystem.CanAttack(user))
        {
            return null;
        }

        var toCoordinates = gun.Comp.ShootCoordinates;

        if (toCoordinates == null)
            return null;

        var curTime = Timing.CurTime;

        // check if anything wants to prevent shooting
        var prevention = new ShotAttemptedEvent
        {
            User = user,
            Used = gun
        };
        RaiseLocalEvent(gun, ref prevention);
        if (prevention.Cancelled)
            return null;

        RaiseLocalEvent(user, ref prevention);
        if (prevention.Cancelled)
            return null;

        // Need to do this to play the clicking sound for empty automatic weapons
        // but not play anything for burst fire.
        if (gun.Comp.NextFire > curTime)
            return null;

        var fireRate = TimeSpan.FromSeconds(1f / gun.Comp.FireRateModified);

        if (gun.Comp.SelectedMode == SelectiveFire.Burst || gun.Comp.BurstActivated)
            fireRate = TimeSpan.FromSeconds(1f / gun.Comp.BurstFireRate);

        // First shot
        // Previously we checked shotcounter but in some cases all the bullets got dumped at once
        // curTime - fireRate is insufficient because if you time it just right you can get a 3rd shot out slightly quicker.
        if (gun.Comp.NextFire < curTime - fireRate || gun.Comp.ShotCounter == 0 && gun.Comp.NextFire < curTime)
            gun.Comp.NextFire = curTime;

        var shots = 0;
        var lastFire = gun.Comp.NextFire;

        while (gun.Comp.NextFire <= curTime)
        {
            gun.Comp.NextFire += fireRate;
            shots++;
        }

        // NextFire has been touched regardless so need to dirty the gun.
        DirtyField(gun.AsNullable(), nameof(GunComponent.NextFire));

        // Get how many shots we're actually allowed to make, due to clip size or otherwise.
        // Don't do this in the loop so we still reset NextFire.
        if (!gun.Comp.BurstActivated)
        {
            switch (gun.Comp.SelectedMode)
            {
                case SelectiveFire.SemiAuto:
                    shots = Math.Min(shots, 1 - gun.Comp.ShotCounter);
                    break;
                case SelectiveFire.Burst:
                    shots = Math.Min(shots, gun.Comp.ShotsPerBurstModified - gun.Comp.ShotCounter);
                    break;
                case SelectiveFire.FullAuto:
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"No implemented shooting behavior for {gun.Comp.SelectedMode}!");
            }
        }
        else
        {
            shots = Math.Min(shots, gun.Comp.ShotsPerBurstModified - gun.Comp.ShotCounter);
        }

        var originEntity = HasComp<GunUseGunOriginComponent>(gun) ? gun.Owner : user;
        var fromCoordinates = Transform(originEntity).Coordinates;

        var shotOriginEv = new BeforeAttemptShootEvent(fromCoordinates, gun.Comp.ShootOriginOffset);
        RaiseLocalEvent(user, ref shotOriginEv);

        if (shotOriginEv.Handled)
            fromCoordinates = shotOriginEv.Origin;

        var attemptEv = new AttemptShootEvent(user, null, fromCoordinates, toCoordinates);
        RaiseLocalEvent(gun, ref attemptEv);

        if (attemptEv.Cancelled)
        {
            if (attemptEv.Message != null)
            {
                PopupSystem.PopupEntity(attemptEv.Message, gun, user);
            }
            gun.Comp.BurstActivated = false;
            gun.Comp.BurstShotsCount = 0;
            gun.Comp.NextFire = attemptEv.ResetCooldown
                ? curTime
                : TimeSpan.FromSeconds(Math.Max(lastFire.TotalSeconds + SafetyNextFire, gun.Comp.NextFire.TotalSeconds));
            return null;
        }

        fromCoordinates = attemptEv.FromCoordinates;
        toCoordinates = attemptEv.ToCoordinates;
        if (toCoordinates == null)
            return null;

        var sourceFromCoordinates = fromCoordinates;
        var sourceToCoordinates = toCoordinates.Value;
        if (!_zLevelShooting.TryAdjustShotCoordinates(
                user,
                fromCoordinates,
                toCoordinates.Value,
                out fromCoordinates,
                out var adjustedToCoordinates,
                sourceCollisionMask: 0))
        {
            gun.Comp.NextFire = lastFire;
            DirtyField(gun.AsNullable(), nameof(GunComponent.NextFire));
            return null;
        }

        toCoordinates = adjustedToCoordinates;

        // Remove ammo
        var ev = new TakeAmmoEvent(shots, [], fromCoordinates, user);

        // Listen it just makes the other code around it easier if shots == 0 to do this.
        if (shots > 0)
            RaiseLocalEvent(gun, ev);

        DebugTools.Assert(ev.Ammo.Count <= shots);
        DebugTools.Assert(shots >= 0);
        UpdateAmmoCount(gun);

        // Even if we don't actually shoot update the ShotCounter. This is to avoid spamming empty sounds
        // where the gun may be SemiAuto or Burst.
        gun.Comp.ShotCounter += shots;
        DirtyField(gun.AsNullable(), nameof(GunComponent.ShotCounter));

        if (ev.Ammo.Count <= 0)
        {
            // triggers effects on the gun if it's empty
            var emptyGunShotEvent = new OnEmptyGunShotEvent(user);
            RaiseLocalEvent(gun, ref emptyGunShotEvent);

            gun.Comp.BurstActivated = false;
            gun.Comp.BurstShotsCount = 0;
            gun.Comp.NextFire += TimeSpan.FromSeconds(gun.Comp.BurstCooldown);

            // Play empty gun sounds if relevant
            // If they're firing an existing clip then don't play anything.
            if (shots > 0)
            {
                PopupSystem.PopupCursor(ev.Reason ?? Loc.GetString("gun-magazine-fired-empty"), user);

                // Don't spam safety sounds at gun fire rate, play it at a reduced rate.
                // May cause prediction issues? Needs more tweaking
                gun.Comp.NextFire = TimeSpan.FromSeconds(Math.Max(lastFire.TotalSeconds + SafetyNextFire, gun.Comp.NextFire.TotalSeconds));
                Audio.PlayPredicted(gun.Comp.SoundEmpty, gun, user);
                return null;
            }

            return null;
        }

        var sourceCollisionMask = GetShotCollisionMask(ev.Ammo);
        if (_zLevelShooting.IsCrossZSourcePathBlocked(
                user,
                sourceFromCoordinates,
                fromCoordinates,
                sourceCollisionMask))
        {
            fromCoordinates = sourceFromCoordinates;
            toCoordinates = sourceToCoordinates;
            RestoreDirectAmmoCoordinates(ev.Ammo, sourceFromCoordinates);
        }

        _zLevelShooting.TryGetProjectileVisualOffset(
            user,
            sourceFromCoordinates,
            fromCoordinates,
            out var projectileVisualOffset);

        // Handle burstfire
        if (gun.Comp.SelectedMode == SelectiveFire.Burst)
        {
            gun.Comp.BurstActivated = true;
        }
        if (gun.Comp.BurstActivated)
        {
            gun.Comp.BurstShotsCount += shots;
            if (gun.Comp.BurstShotsCount >= gun.Comp.ShotsPerBurstModified)
            {
                gun.Comp.NextFire += TimeSpan.FromSeconds(gun.Comp.BurstCooldown);
                gun.Comp.BurstActivated = false;
                gun.Comp.BurstShotsCount = 0;
            }
        }

        // Shoot confirmed - sounds also played here in case it's invalid (e.g. cartridge already spent).
        var shotProjectiles = new List<EntityUid>();
        var userImpulse = false;
        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates);
        var toMap = TransformSystem.ToMapCoordinates(toCoordinates.Value);
        var recoilAngle = GetRecoilAngle(
            gun,
            curTime,
            (toMap.Position - fromMap.Position).ToAngle());

        // Reserve the deterministic spread sample before server-only shoot
        // cancellation hooks. The predicting client has already advanced this
        // state, so a cancelled authoritative shot must do the same or the next
        // rapid shot will use a different spread sequence.
        if (BeforeShoot(user, gun, ev.Ammo))
        {
            if (Timing.IsFirstTimePredicted)
            {
                var correlateProjectiles = _netManager.IsServer &&
                                           _config.GetCVar(RMCCVars.RMCGunPrediction) &&
                                           !HasComp<GunIgnorePredictionComponent>(gun) &&
                                           predictedProjectiles != null &&
                                           userSession != null;
                shotProjectiles = Shoot(
                    gun,
                    ev.Ammo,
                    fromCoordinates,
                    toCoordinates.Value,
                    out userImpulse,
                    user,
                    throwItems: attemptEv.ThrowItems,
                    recoilAngle: recoilAngle,
                    predictedProjectiles: correlateProjectiles ? predictedProjectiles : null,
                    userSession: correlateProjectiles ? userSession : null,
                    acceptedPredictedProjectiles: correlateProjectiles ? acceptedPredictedProjectiles : null);
            }

            // Taking physical ammo calls EnsureShootable and resets cartridge state on every replay,
            // but Shoot only runs once. Mirror the authoritative state changes without replaying effects.
            if (_netManager.IsClient && !Timing.IsFirstTimePredicted)
            {
                foreach (var (ammoEntity, shootable) in ev.Ammo)
                {
                    if (ammoEntity is not { } ammoUid)
                        continue;

                    if (shootable is CartridgeAmmoComponent { Spent: false } cartridge)
                    {
                        SetCartridgeSpent(ammoUid, cartridge, true);
                        if (cartridge.DeleteOnSpawn)
                            PredictedQueueDel(ammoUid);
                    }
                    else if (shootable is AmmoComponent and not CartridgeAmmoComponent &&
                             !HasComp<ProjectileComponent>(ammoUid))
                    {
                        RemoveShootable(ammoUid);
                    }
                }

                if (!attemptEv.ThrowItems)
                {
                    ReplayPhysicalProjectiles(
                        gun,
                        ev.Ammo,
                        fromCoordinates,
                        toCoordinates.Value,
                        user,
                        recoilAngle);
                }
            }

            // This state change must run during prediction replays. Shoot itself
            // is first-time-only because it creates entities and presentation,
            // but skipping replayed fuel debits lets near-empty flamers produce
            // phantom client shots until the next authoritative state arrives.
            if (!attemptEv.ThrowItems)
            {
                foreach (var (ammoEntity, shootable) in ev.Ammo)
                {
                    if (ammoEntity is { } flamerUid &&
                        shootable is RMCFlamerAmmoProviderComponent flamer)
                    {
                        RMCFlamer.ConsumeFlamerFuel(
                            (flamerUid, flamer),
                            fromCoordinates,
                            toCoordinates.Value);
                    }
                    else if (ammoEntity is { } sprayUid &&
                             shootable is RMCSprayAmmoProviderComponent spray)
                    {
                        RMCFlamer.ConsumeSprayFuel((sprayUid, spray));
                    }
                }
            }
        }

        _zLevelShooting.ApplyProjectileVisualOffset(shotProjectiles, projectileVisualOffset);

        var shotEv = new GunShotEvent(user, ev.Ammo, fromCoordinates, toCoordinates.Value);
        RaiseLocalEvent(gun, ref shotEv);

        foreach (var (ammoEntity, _) in ev.Ammo)
        {
            if (ammoEntity is { } uid && IsClientSide(uid) && !shotProjectiles.Contains(uid))
                Del(uid);
        }

        if (!userImpulse || !TryComp<PhysicsComponent>(user, out var userPhysics))
            return shotProjectiles;

        var shooterEv = new ShooterImpulseEvent();
        RaiseLocalEvent(user, ref shooterEv);

        if (shooterEv.Push)
            CauseImpulse(fromCoordinates, toCoordinates.Value, (user, userPhysics));
        return shotProjectiles;
    }

    public void Shoot(
        Entity<GunComponent> gun,
        EntityUid ammo,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out bool userImpulse,
        EntityUid? user = null,
        bool throwItems = false)
    {
        var shootable = EnsureShootable(ammo);
        Shoot(gun, new List<(EntityUid? Entity, IShootable Shootable)>(1) { (ammo, shootable) }, fromCoordinates, toCoordinates, out userImpulse, user, throwItems);
    }

    public abstract List<EntityUid> Shoot(
        Entity<GunComponent> gun,
        List<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out bool userImpulse,
        EntityUid? user = null,
        bool throwItems = false,
        Angle? recoilAngle = null,
        IReadOnlyList<int>? predictedProjectiles = null,
        ICommonSession? userSession = null,
        ISet<int>? acceptedPredictedProjectiles = null);

    protected virtual bool BeforeShoot(
        EntityUid? user,
        Entity<GunComponent> gun,
        List<(EntityUid? Entity, IShootable Shootable)> ammo)
    {
        return true;
    }

    /// <summary>
    /// Restores simulation state for server-owned projectile ammunition after a client prediction rollback.
    /// </summary>
    protected virtual void ReplayPhysicalProjectiles(
        Entity<GunComponent> gun,
        IReadOnlyList<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        EntityUid? user,
        Angle recoilAngle)
    {
    }

    protected Angle GetRecoilAngle(Entity<GunComponent> gun, TimeSpan curTime, Angle direction)
    {
        var timeSinceLastFire = (curTime - gun.Comp.LastFire).TotalSeconds;
        var newTheta = MathHelper.Clamp(
            gun.Comp.CurrentAngle.Theta +
            gun.Comp.AngleIncreaseModified.Theta -
            gun.Comp.AngleDecayModified.Theta * timeSinceLastFire,
            gun.Comp.MinAngleModified.Theta,
            gun.Comp.MaxAngleModified.Theta);
        gun.Comp.CurrentAngle = new Angle(newTheta);
        gun.Comp.LastFire = gun.Comp.NextFire;
        DirtyField(gun.AsNullable(), nameof(GunComponent.CurrentAngle));
        DirtyField(gun.AsNullable(), nameof(GunComponent.LastFire));

        var seed = (long) gun.Comp.SpreadSequence << 32 | (uint) GetNetEntity(gun).Id;
        gun.Comp.SpreadSequence++;
        DirtyField(gun.AsNullable(), nameof(GunComponent.SpreadSequence));
        var random = new Xoroshiro64S(seed).NextFloat(-0.5f, 0.5f);
        var spread = gun.Comp.CurrentAngle.Theta * random;
        var angle = new Angle(direction.Theta + spread);
        DebugTools.Assert(spread <= gun.Comp.MaxAngleModified.Theta);
        return angle;
    }

    protected static Angle[] LinearSpread(Angle start, Angle end, int intervals)
    {
        var angles = new Angle[intervals];
        DebugTools.Assert(intervals > 1);

        for (var i = 0; i < intervals; i++)
        {
            angles[i] = new Angle(start + (end - start) * i / (intervals - 1));
        }

        return angles;
    }

    public void ShootProjectile(EntityUid uid, Vector2 direction, Vector2 gunVelocity, EntityUid? gunUid, EntityUid? user = null, float speed = ProjectileSpeed)
    {
        var physics = EnsureComp<PhysicsComponent>(uid);
        Physics.SetBodyStatus(uid, physics, BodyStatus.InAir);

        var targetMapVelocity = gunVelocity + direction.Normalized() * speed;
        var currentMapVelocity = Physics.GetMapLinearVelocity(uid, physics);
        var finalLinear = physics.LinearVelocity + targetMapVelocity - currentMapVelocity;
        Physics.SetLinearVelocity(uid, finalLinear, body: physics);

        var projectile = EnsureComp<ProjectileComponent>(uid);
        projectile.Weapon = gunUid;
        var shooter = user ?? gunUid;
        if (shooter != null)
            Projectiles.SetShooter(uid, projectile, shooter.Value);

        TransformSystem.SetWorldRotation(uid, direction.ToWorldAngle() + projectile.Angle);
    }

    private int GetShotCollisionMask(IReadOnlyList<(EntityUid? Entity, IShootable Shootable)> ammo)
    {
        var collisionMask = 0;
        foreach (var (entity, shootable) in ammo)
        {
            if (shootable is CartridgeAmmoComponent cartridge)
            {
                collisionMask |= GetPrototypeShotCollisionMask(cartridge.Prototype);
                continue;
            }

            if (shootable is RMCFlamerAmmoProviderComponent or RMCSprayAmmoProviderComponent)
            {
                collisionMask |= (int) (CollisionGroup.FullTileMask | CollisionGroup.BarricadeImpassable);
                continue;
            }

            if (entity is not { } uid || Deleted(uid))
                continue;

            if (TryComp<HitscanBasicRaycastComponent>(uid, out var hitscan))
                collisionMask |= (int) hitscan.CollisionMask;

            if (!TryComp<FixturesComponent>(uid, out var fixtures))
                continue;

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                collisionMask |= fixture.CollisionMask;
            }
        }

        return collisionMask == 0
            ? CMUZLevelShootingSystem.DefaultSourceCollisionMask
            : collisionMask;
    }

    private int GetPrototypeShotCollisionMask(EntProtoId prototypeId)
    {
        if (!ProtoMan.TryIndex<EntityPrototype>(prototypeId, out var prototype))
            return 0;

        var collisionMask = 0;
        if (prototype.TryComp<HitscanBasicRaycastComponent>(out var hitscan, Factory))
            collisionMask |= (int) hitscan.CollisionMask;

        if (prototype.TryComp<FixturesComponent>(out var fixtures, Factory))
        {
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                collisionMask |= fixture.CollisionMask;
            }
        }

        return collisionMask;
    }

    private void RestoreDirectAmmoCoordinates(
        IReadOnlyList<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates coordinates)
    {
        foreach (var (entity, shootable) in ammo)
        {
            if (entity is not { } uid ||
                Deleted(uid) ||
                Containers.IsEntityInContainer(uid) ||
                shootable is CartridgeAmmoComponent ||
                shootable is RMCFlamerAmmoProviderComponent ||
                shootable is RMCSprayAmmoProviderComponent)
            {
                continue;
            }

            TransformSystem.SetCoordinates(uid, Transform(uid), coordinates);
        }
    }

    /// <summary>
    /// Call this whenever the ammo count for a gun changes.
    /// </summary>
    public virtual void UpdateAmmoCount(EntityUid uid, bool prediction = true, int artificialIncrease = 0) { }

    protected void SetCartridgeSpent(EntityUid uid, CartridgeAmmoComponent cartridge, bool spent)
    {
        if (cartridge.Spent != spent)
            DirtyField(uid, cartridge, nameof(CartridgeAmmoComponent.Spent));

        cartridge.Spent = spent;
        Appearance.SetData(uid, AmmoVisuals.Spent, spent);

        if (!cartridge.MarkSpentAsTrash)
            return;

        if (spent)
            TagSystem.AddTag(uid, TrashTag);
        else
            TagSystem.RemoveTag(uid, TrashTag);
    }

    /// <summary>
    /// Drops a single cartridge / shell
    /// </summary>
    protected void EjectCartridge(
        EntityUid entity,
        Angle? angle = null,
        bool playSound = true)
    {
        // TODO: Sound limit version.
        var offsetPos = Random.NextVector2(EjectOffset);
        var xform = Transform(entity);

        var coordinates = xform.Coordinates;
        coordinates = coordinates.Offset(offsetPos);

        TransformSystem.SetCoordinates(entity, xform, coordinates, rotation: Random.NextAngle());
        TransformSystem.AttachToGridOrMap(entity, xform);

        // decides direction the casing ejects and only when not cycling
        if (angle != null)
        {
            var ejectAngle = angle.Value;
            ejectAngle += 3.7f; // 212 degrees; casings should eject slightly to the right and behind of a gun
            ThrowingSystem.TryThrow(entity, ejectAngle.ToVec().Normalized() / 100, 5f);
        }
        if (playSound && TryComp<CartridgeAmmoComponent>(entity, out var cartridge))
        {
            var audioParams = cartridge.EjectSound?.Params ?? AudioParams.Default;
            audioParams = audioParams.AddVolume(-1f).WithVariation(SharedContentAudioSystem.DefaultVariation);
            Audio.PlayPvs(cartridge.EjectSound, entity, audioParams);
        }
    }

    public IShootable EnsureShootable(EntityUid uid)
    {
        if (TryComp<CartridgeAmmoComponent>(uid, out var cartridge))
            return cartridge;

        if (TryComp<HitscanAmmoComponent>(uid, out var hitscanAmmo))
            return hitscanAmmo;

        return EnsureComp<AmmoComponent>(uid);
    }

    protected void RemoveShootable(EntityUid uid)
    {
        RemCompDeferred<CartridgeAmmoComponent>(uid);
        RemCompDeferred<AmmoComponent>(uid);
    }

    protected void MuzzleFlash(EntityUid gun, AmmoComponent component, Angle worldAngle, EntityUid? user = null)
    {
        var attemptEv = new GunMuzzleFlashAttemptEvent();
        RaiseLocalEvent(gun, ref attemptEv);
        if (attemptEv.Cancelled)
            return;

        var sprite = component.MuzzleFlash;

        if (sprite == null)
            return;

        var muzzleFlashOffset = component.MuzzleFlashOffset;
        var muzzleFlashOriginOffset = Vector2.Zero;
        if (TryComp(gun, out GunComponent? gunComp))
        {
            var beforeEv = new RMCBeforeMuzzleFlashEvent(gun, gunComp.ShootOriginOffset);
            RaiseLocalEvent(gun, ref beforeEv);
            gun = beforeEv.Weapon;
            muzzleFlashOriginOffset = beforeEv.Offset;
        }

        var ev = new MuzzleFlashEvent(
            GetNetEntity(gun),
            sprite,
            worldAngle,
            muzzleFlashOffset,
            muzzleFlashOriginOffset);
        CreateEffect(gun, ev, gun, user, muzzleFlashOffset, muzzleFlashOriginOffset);
    }

    public void CauseImpulse(EntityCoordinates fromCoordinates, EntityCoordinates toCoordinates, Entity<PhysicsComponent> user)
    {
        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates).Position;
        var toMap = TransformSystem.ToMapCoordinates(toCoordinates).Position;
        var shotDirection = (toMap - fromMap).Normalized();

        const float impulseStrength = 25.0f;
        var impulseVector = shotDirection * impulseStrength;
        Physics.ApplyLinearImpulse(user, -impulseVector, body: user.Comp);
    }

    public void RefreshModifiers(Entity<GunComponent?> gun)
    {
        if (!Resolve(gun, ref gun.Comp))
            return;

        var comp = gun.Comp;
        var ev = new GunRefreshModifiersEvent(
            (gun, comp),
            comp.SoundGunshot,
            comp.CameraRecoilScalar,
            comp.AngleIncrease,
            comp.AngleDecay,
            comp.MaxAngle,
            comp.MinAngle,
            comp.ShotsPerBurst,
            comp.FireRate,
            comp.ProjectileSpeed
        );

        RaiseLocalEvent(gun, ref ev);

        if (comp.SoundGunshotModified != ev.SoundGunshot)
        {
            comp.SoundGunshotModified = ev.SoundGunshot;
            DirtyField(gun, nameof(GunComponent.SoundGunshotModified));
        }

        if (!MathHelper.CloseTo(comp.CameraRecoilScalarModified, ev.CameraRecoilScalar))
        {
            comp.CameraRecoilScalarModified = ev.CameraRecoilScalar;
            DirtyField(gun, nameof(GunComponent.CameraRecoilScalarModified));
        }

        if (!comp.AngleIncreaseModified.EqualsApprox(ev.AngleIncrease))
        {
            comp.AngleIncreaseModified = ev.AngleIncrease;
            DirtyField(gun, nameof(GunComponent.AngleIncreaseModified));
        }

        if (!comp.AngleDecayModified.EqualsApprox(ev.AngleDecay))
        {
            comp.AngleDecayModified = ev.AngleDecay;
            DirtyField(gun, nameof(GunComponent.AngleDecayModified));
        }

        if (!comp.MaxAngleModified.EqualsApprox(ev.MaxAngle))
        {
            comp.MaxAngleModified = ev.MaxAngle;
            DirtyField(gun, nameof(GunComponent.MaxAngleModified));
        }

        if (!comp.MinAngleModified.EqualsApprox(ev.MinAngle))
        {
            comp.MinAngleModified = ev.MinAngle;
            DirtyField(gun, nameof(GunComponent.MinAngleModified));
        }

        if (comp.ShotsPerBurstModified != ev.ShotsPerBurst)
        {
            comp.ShotsPerBurstModified = ev.ShotsPerBurst;
            DirtyField(gun, nameof(GunComponent.ShotsPerBurstModified));
        }

        if (!MathHelper.CloseTo(comp.FireRateModified, ev.FireRate))
        {
            comp.FireRateModified = ev.FireRate;
            DirtyField(gun, nameof(GunComponent.FireRateModified));
        }

        if (!MathHelper.CloseTo(comp.ProjectileSpeedModified, ev.ProjectileSpeed))
        {
            comp.ProjectileSpeedModified = ev.ProjectileSpeed;
            DirtyField(gun, nameof(GunComponent.ProjectileSpeedModified));
        }
    }

    protected abstract void CreateEffect(
        EntityUid gunUid,
        MuzzleFlashEvent message,
        EntityUid? tracked = null,
        EntityUid? player = null,
        Vector2 offset = default,
        Vector2 originOffset = default);

    public void PlayImpactSound(
        EntityUid otherEntity,
        DamageSpecifier? modifiedDamage,
        SoundSpecifier? weaponSound,
        bool forceWeaponSound,
        ICommonSession? excludedSession = null)
    {
        DebugTools.Assert(!Deleted(otherEntity), "Impact sound entity was deleted");

        var filter = _netManager.IsClient
            ? Filter.Local()
            : Filter.Pvs(otherEntity, entityManager: EntityManager);
        if (excludedSession != null)
            filter.RemovePlayer(excludedSession);

        if (filter.Count == 0)
            return;

        var (sound, varyPitch) = GetImpactSound(
            otherEntity,
            modifiedDamage,
            weaponSound,
            forceWeaponSound);
        if (sound == null)
            return;

        var audioParams = varyPitch
            ? sound.Params.WithVariation(DamagePitchVariation)
            : sound.Params;
        Audio.PlayEntity(sound, filter, otherEntity, true, audioParams);
    }

    public (SoundSpecifier? Sound, bool VaryPitch) GetImpactSound(
        EntityUid otherEntity,
        DamageSpecifier? modifiedDamage,
        SoundSpecifier? weaponSound,
        bool forceWeaponSound)
    {
        // Like projectiles and melee: prefer the target's material/damage sound,
        // then fall back to the projectile's configured impact sound.
        if (!forceWeaponSound &&
            modifiedDamage != null &&
            modifiedDamage.GetTotal() > 0 &&
            TryComp(otherEntity, out RangedDamageSoundComponent? rangedSound))
        {
            var type = SharedMeleeWeaponSystem.GetHighestDamageSound(modifiedDamage, ProtoMan);
            if (type != null &&
                rangedSound.SoundTypes?.TryGetValue(type, out var damageSoundType) == true)
            {
                return (damageSoundType, true);
            }

            if (type != null &&
                rangedSound.SoundGroups?.TryGetValue(type, out var damageSoundGroup) == true)
            {
                return (damageSoundGroup, true);
            }
        }

        return (weaponSound, false);
    }

    public void PlayResolvedImpactSound(
        EntityCoordinates coordinates,
        SoundSpecifier? sound,
        bool varyPitch)
    {
        if (sound == null)
            return;

        var filter = _netManager.IsClient
            ? Filter.Local()
            : Filter.Pvs(coordinates, entityMan: EntityManager);
        var audioParams = varyPitch
            ? sound.Params.WithVariation(DamagePitchVariation)
            : sound.Params;
        Audio.PlayStatic(sound, filter, coordinates, true, audioParams);
    }

    /// <summary>
    /// Used for animated effects on the client.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class HitscanEvent : EntityEventArgs
    {
        public List<(NetCoordinates coordinates, Angle angle, SpriteSpecifier Sprite, float Distance)> Sprites = [];
    }

    /// <summary>
    /// Get the ammo count for a given EntityUid. Can be a firearm or magazine.
    /// </summary>
    public int GetAmmoCount(EntityUid uid)
    {
        var ammoEv = new GetAmmoCountEvent();
        RaiseLocalEvent(uid, ref ammoEv);
        return ammoEv.Count;
    }

    /// <summary>
    /// Get the ammo capacity for a given EntityUid. Can be a firearm or magazine.
    /// </summary>
    public int GetAmmoCapacity(EntityUid uid)
    {
        var ammoEv = new GetAmmoCountEvent();
        RaiseLocalEvent(uid, ref ammoEv);
        return ammoEv.Capacity;
    }

    public override void Update(float frameTime)
    {
        UpdateBattery(frameTime);
        UpdateBallistic(frameTime);
    }
}

/// <summary>
///     Raised directed on the gun before firing to see if the shot should go through.
/// </summary>
/// <remarks>
///     Handling this in server exclusively will lead to mispredicts.
/// </remarks>
/// <param name="User">The user that attempted to fire this gun.</param>
/// <param name="Cancelled">Set this to true if the shot should be cancelled.</param>
/// <param name="ThrowItems">Set this to true if the ammo shouldn't actually be fired, just thrown.</param>
[ByRefEvent]
public record struct AttemptShootEvent(
    EntityUid User,
    string? Message,
    EntityCoordinates FromCoordinates,
    EntityCoordinates? ToCoordinates,
    bool Cancelled = false,
    bool ThrowItems = false,
    bool ResetCooldown = false);

/// <summary>
///     Raised directed on the gun after firing.
/// </summary>
/// <param name="User">The user that fired this gun.</param>
[ByRefEvent]
public record struct GunShotEvent(
    EntityUid User,
    List<(EntityUid? Uid, IShootable Shootable)> Ammo,
    EntityCoordinates FromCoordinates,
    EntityCoordinates ToCoordinates);

/// <summary>
/// Raised on an entity after firing a gun to see if any components or systems would allow this entity to be pushed
/// by the gun they're firing. If true, GunSystem will create an impulse on our entity.
/// </summary>
[ByRefEvent]
public record struct ShooterImpulseEvent()
{
    public bool Push;
};

public enum EffectLayers : byte
{
    Unshaded,
}

[Serializable, NetSerializable]
public enum AmmoVisuals : byte
{
    Spent,
    AmmoCount,
    AmmoMax,
    HasAmmo, // used for generic visualizers. c# stuff can just check ammocount != 0
    IsFull, // used for generic visualizers. c# stuff can just check ammocount == ammomax
    MagLoaded,
    BoltClosed,
}
