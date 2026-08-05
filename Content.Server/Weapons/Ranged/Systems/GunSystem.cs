using System.Numerics;
using Content.Server.Cargo.Systems;
using Content.Shared.Cargo;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem : SharedGunSystem
{
    [Dependency] private PricingSystem _pricing = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeAutoFire();
        SubscribeLocalEvent<BallisticAmmoProviderComponent, PriceCalculationEvent>(OnBallisticPrice);
    }

    private void OnBallisticPrice(Entity<BallisticAmmoProviderComponent> ent, ref PriceCalculationEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.Proto) || ent.Comp.UnspawnedCount == 0)
            return;

        if (!ProtoMan.TryIndex<EntityPrototype>(ent.Comp.Proto, out var proto))
        {
            Log.Error($"Unable to find fill prototype for price on {ent.Comp.Proto} on {ToPrettyString(ent)}");
            return;
        }

        // Probably good enough for most.
        var price = _pricing.GetEstimatedPrice(proto);
        args.Price += price * ent.Comp.UnspawnedCount;
    }

    protected override bool BeforeShoot(
        EntityUid? user,
        Entity<GunComponent> gun,
        List<(EntityUid? Entity, IShootable Shootable)> ammo)
    {
        if (user == null)
            return true;

        var selfEvent = new SelfBeforeGunShotEvent(user.Value, gun, ammo);
        RaiseLocalEvent(user.Value, selfEvent);
        return !selfEvent.Cancelled;
    }

    public override List<EntityUid> Shoot(Entity<GunComponent> gun, List<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates fromCoordinates, EntityCoordinates toCoordinates, out bool userImpulse, EntityUid? user = null,
        bool throwItems = false, Angle? recoilAngle = null,
        IReadOnlyList<int>? predictedProjectiles = null,
        ICommonSession? userSession = null,
        ISet<int>? acceptedPredictedProjectiles = null)
    {
        userImpulse = true;

        if (recoilAngle == null && !BeforeShoot(user, gun, ammo))
        {
            userImpulse = false;
            return [];
        }

        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates);
        var toMap = TransformSystem.ToMapCoordinates(toCoordinates).Position;
        var mapDirection = toMap - fromMap.Position;
        var mapAngle = mapDirection.ToAngle();
        var angle = recoilAngle ?? GetRecoilAngle(gun, Timing.CurTime, mapAngle);

        // If applicable, this ensures the projectile is parented to grid on spawn, instead of the map.
        var fromEnt = Maps.TryFindGridAt(fromMap, out var gridUid, out _)
            ? TransformSystem.WithEntityId(fromCoordinates, gridUid)
            : new EntityCoordinates(_map.GetMapOrInvalid(fromMap.MapId), fromMap.Position);

        // Update shot based on the recoil
        toMap = fromMap.Position + angle.ToVec() * mapDirection.Length();
        mapDirection = toMap - fromMap.Position;
        var gunVelocity = Physics.GetMapLinearVelocity(fromEnt);

        // I must be high because this was getting tripped even when true.
        // DebugTools.Assert(direction != Vector2.Zero);
        var shotProjectiles = new List<EntityUid>(ammo.Count);

        foreach (var (ent, shootable) in ammo)
        {
            // pneumatic cannon doesn't shoot bullets it just throws them, ignore ammo handling
            if (throwItems && ent != null)
            {
                ShootOrThrow(ent.Value, mapDirection, gunVelocity, gun, user);
                continue;
            }

            // TODO: Clean this up in a gun refactor at some point - too much copy pasting
            switch (shootable)
            {
                // Cartridge shoots something else
                case CartridgeAmmoComponent cartridge:
                    if (!cartridge.Spent)
                    {
                        var uid = Spawn(cartridge.Prototype, fromEnt);
                        CreateAndFireProjectiles(uid, cartridge);

                        RaiseLocalEvent(ent!.Value, new AmmoShotEvent()
                        {
                            FiredProjectiles = shotProjectiles,
                        });

                        SetCartridgeSpent(ent.Value, cartridge, true);

                        if (cartridge.DeleteOnSpawn)
                            Del(ent.Value);
                    }
                    else
                    {
                        userImpulse = false;
                        Audio.PlayPredicted(gun.Comp.SoundEmpty, gun, user);
                    }

                    // Something like ballistic might want to leave it in the container still
                    if (!cartridge.DeleteOnSpawn && !Containers.IsEntityInContainer(ent!.Value))
                        EjectCartridge(ent.Value, angle);

                    Dirty(ent!.Value, cartridge);
                    break;
                // Ammo shoots itself
                case AmmoComponent newAmmo:
                    if (ent == null)
                        break;
                    CreateAndFireProjectiles(ent.Value, newAmmo);

                    break;
                case HitscanAmmoComponent:
                    if (ent == null)
                        break;

                    var hitscanEv = new HitscanTraceEvent
                    {
                        FromCoordinates = fromCoordinates,
                        ShotDirection = mapDirection.Normalized(),
                        Gun = gun,
                        Shooter = user,
                        Target = gun.Comp.Target,
                    };
                    RaiseLocalEvent(ent.Value, ref hitscanEv);

                    Del(ent);

                    Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
                    break;
                case RMCFlamerAmmoProviderComponent flamer:
                    if (ent != null)
                        RMCFlamer.ShootFlamer((ent.Value, flamer), gun, user, fromCoordinates, toCoordinates);
                    break;
                case RMCSprayAmmoProviderComponent spray:
                    if (ent != null)
                        RMCFlamer.ShootSpray((ent.Value, spray), gun, user, fromCoordinates, toCoordinates);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        CorrelatePredictedProjectiles(
            shotProjectiles,
            predictedProjectiles,
            userSession,
            user,
            acceptedPredictedProjectiles);

        RaiseLocalEvent(gun, new AmmoShotEvent()
        {
            FiredProjectiles = shotProjectiles,
        });

        return shotProjectiles;

        void CreateAndFireProjectiles(EntityUid ammoEnt, AmmoComponent ammoComp)
        {
            if (TryComp<ProjectileSpreadComponent>(ammoEnt, out var ammoSpreadComp))
            {
                var spreadEvent = new GunGetAmmoSpreadEvent(ammoSpreadComp.Spread);
                RaiseLocalEvent(gun, ref spreadEvent);

                var angles = LinearSpread(mapAngle - spreadEvent.Spread / 2,
                    mapAngle + spreadEvent.Spread / 2, ammoSpreadComp.Count);

                ShootOrThrow(ammoEnt, angles[0].ToVec(), gunVelocity, gun, user);
                shotProjectiles.Add(ammoEnt);

                for (var i = 1; i < ammoSpreadComp.Count; i++)
                {
                    var newuid = Spawn(ammoSpreadComp.Proto, fromEnt);
                    ShootOrThrow(newuid, angles[i].ToVec(), gunVelocity, gun, user);
                    shotProjectiles.Add(newuid);
                }
            }
            else
            {
                ShootOrThrow(ammoEnt, mapDirection, gunVelocity, gun, user);
                shotProjectiles.Add(ammoEnt);
            }

            MuzzleFlash(gun, ammoComp, mapDirection.ToAngle(), user);
            Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
        }
    }

    private void CorrelatePredictedProjectiles(
        List<EntityUid> projectiles,
        IReadOnlyList<int>? predictedProjectiles,
        ICommonSession? userSession,
        EntityUid? user,
        ISet<int>? acceptedPredictedProjectiles)
    {
        if (predictedProjectiles == null || userSession == null || user == null)
            return;

        var predictionIndex = 0;
        foreach (var projectile in projectiles)
        {
            if (!HasComp<ProjectileComponent>(projectile))
                continue;

            if (predictionIndex >= predictedProjectiles.Count)
                break;

            var clientId = predictedProjectiles[predictionIndex++];
            if (acceptedPredictedProjectiles != null && !acceptedPredictedProjectiles.Add(clientId))
                continue;

            var predicted = new PredictedProjectileServerComponent
            {
                Shooter = userSession,
                ClientId = clientId,
                ClientEnt = user,
            };
            AddComp(projectile, predicted, true);
            Dirty(projectile, predicted);
        }
    }

    private void ShootOrThrow(EntityUid uid, Vector2 mapDirection, Vector2 gunVelocity, Entity<GunComponent> gun, EntityUid? user)
    {
        if (gun.Comp.Target is { } target && !TerminatingOrDeleted(target))
        {
            var targeted = EnsureComp<TargetedProjectileComponent>(uid);
            targeted.Target = target;
            Dirty(uid, targeted);
        }

        // Do a throw
        if (!HasComp<ProjectileComponent>(uid))
        {
            RemoveShootable(uid);
            // TODO: Someone can probably yeet this a billion miles so need to pre-validate input somewhere up the call stack.
            ThrowingSystem.TryThrow(uid, mapDirection, gun.Comp.ProjectileSpeedModified, user);
            return;
        }

        ShootProjectile(uid, mapDirection, gunVelocity, gun, user, gun.Comp.ProjectileSpeedModified);
    }

    protected override void CreateEffect(
        EntityUid gunUid,
        MuzzleFlashEvent message,
        EntityUid? tracked = null,
        EntityUid? player = null,
        Vector2 offset = default,
        Vector2 originOffset = default)
    {
        var filter = Filter.Pvs(gunUid, entityManager: EntityManager);

        if (TryComp<ActorComponent>(tracked, out var actor))
            filter.RemovePlayer(actor.PlayerSession);

        if (TryComp<ActorComponent>(player, out actor))
            filter.RemovePlayer(actor.PlayerSession);

        RaiseNetworkEvent(message, filter);
    }

}
