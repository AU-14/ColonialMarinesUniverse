using System.Numerics;
using Content.Server.Cargo.Systems;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Cargo;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
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
    [Dependency] private SharedRMCFlamerSystem _flamer = default!;

    private const float DamagePitchVariation = 0.05f;

    public override void Initialize()
    {
        base.Initialize();
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

        var price = _pricing.GetEstimatedPrice(proto);
        args.Price += price * ent.Comp.UnspawnedCount;
    }

    public override List<EntityUid>? Shoot(
        Entity<GunComponent> gun,
        List<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out bool userImpulse,
        EntityUid? user = null,
        bool throwItems = false,
        List<int>? predictedProjectiles = null,
        ICommonSession? userSession = null)
    {
        userImpulse = true;

        if (user != null)
        {
            var selfEvent = new SelfBeforeGunShotEvent(user.Value, gun, ammo);
            RaiseLocalEvent(user.Value, selfEvent);
            if (selfEvent.Cancelled)
            {
                userImpulse = false;
                return null;
            }
        }

        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates);
        var toMap = TransformSystem.ToMapCoordinates(toCoordinates).Position;
        var mapDirection = toMap - fromMap.Position;
        var mapAngle = mapDirection.ToAngle();
        var angle = GetRecoilAngle(gun, mapAngle);

        var fromEnt = Maps.TryFindGridAt(fromMap, out var gridUid, out _)
            ? TransformSystem.WithEntityId(fromCoordinates, gridUid)
            : new EntityCoordinates(_map.GetMapOrInvalid(fromMap.MapId), fromMap.Position);

        toMap = fromMap.Position + angle.ToVec() * mapDirection.Length();
        mapDirection = toMap - fromMap.Position;
        var gunVelocity = Physics.GetMapLinearVelocity(fromEnt);
        var shotProjectiles = new List<EntityUid>(ammo.Count);

        foreach (var (ent, shootable) in ammo)
        {
            if (throwItems && ent != null)
            {
                ShootOrThrow(ent.Value, mapDirection, gunVelocity, gun, user);
                continue;
            }

            switch (shootable)
            {
                case CartridgeAmmoComponent cartridge:
                    var cartridgeUid = ent!.Value;
                    if (!cartridge.Spent)
                    {
                        var projectile = Spawn(cartridge.Prototype, fromEnt);
                        CreateAndFireProjectiles(projectile, cartridge);

                        RaiseLocalEvent(cartridgeUid, new AmmoShotEvent
                        {
                            FiredProjectiles = shotProjectiles,
                        });

                        SetCartridgeSpent(cartridgeUid, cartridge, true);

                        if (cartridge.DeleteOnSpawn)
                            Del(cartridgeUid);
                    }
                    else
                    {
                        userImpulse = false;
                        Audio.PlayPredicted(gun.Comp.SoundEmpty, gun, user);
                    }

                    if (!cartridge.DeleteOnSpawn &&
                        !Containers.IsEntityInContainer(cartridgeUid))
                    {
                        EjectCartridge(cartridgeUid, angle);
                    }

                    Dirty(cartridgeUid, cartridge);
                    break;
                case AmmoComponent newAmmo:
                    if (ent != null)
                        CreateAndFireProjectiles(ent.Value, newAmmo);
                    break;
                case HitscanAmmoComponent:
                    if (ent == null)
                        break;

                    var hitscanEvent = new HitscanTraceEvent
                    {
                        FromCoordinates = fromCoordinates,
                        ShotDirection = mapDirection.Normalized(),
                        Gun = gun,
                        Shooter = user,
                        Target = gun.Comp.Target,
                    };
                    RaiseLocalEvent(ent.Value, ref hitscanEvent);

                    Del(ent.Value);
                    PlayGunshotSound(gun.Comp.SoundGunshotModified, gun, user);
                    break;
                case RMCFlamerAmmoProviderComponent flamer when ent != null:
                    _flamer.ShootFlamer((ent.Value, flamer), gun, user, fromCoordinates, toCoordinates);
                    break;
                case RMCSprayAmmoProviderComponent spray when ent != null:
                    _flamer.ShootSpray((ent.Value, spray), gun, user, fromCoordinates, toCoordinates);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        RaiseLocalEvent(gun, new AmmoShotEvent
        {
            FiredProjectiles = shotProjectiles,
        });

        Logs.Add(
            LogType.RMCGunShot,
            LogImpact.Low,
            $"{ToPrettyString(user)} shot {ToPrettyString(gun)} with {shotProjectiles.Count} projectiles aiming at {TransformSystem.ToMapCoordinates(toCoordinates)}.");

        return shotProjectiles;

        void CreateAndFireProjectiles(EntityUid ammoEnt, AmmoComponent ammoComp)
        {
            if (TryComp<ProjectileSpreadComponent>(ammoEnt, out var ammoSpread))
            {
                var spreadEvent = new GunGetAmmoSpreadEvent(ammoSpread.Spread);
                RaiseLocalEvent(gun, ref spreadEvent);

                var angles = LinearSpread(
                    mapAngle - spreadEvent.Spread / 2,
                    mapAngle + spreadEvent.Spread / 2,
                    ammoSpread.Count);

                FireProjectile(ammoEnt, angles[0].ToVec());

                for (var i = 1; i < ammoSpread.Count; i++)
                {
                    var projectile = Spawn(ammoSpread.Proto, fromEnt);
                    FireProjectile(projectile, angles[i].ToVec());
                }
            }
            else
            {
                FireProjectile(ammoEnt, mapDirection);
            }

            MuzzleFlash(gun, ammoComp, mapDirection.ToAngle(), user);
            PlayGunshotSound(gun.Comp.SoundGunshotModified, gun, user);
        }

        void FireProjectile(EntityUid projectile, Vector2 direction)
        {
            MarkPredicted(projectile, shotProjectiles.Count);
            ShootOrThrow(projectile, direction, gunVelocity, gun, user);
            shotProjectiles.Add(projectile);
        }

        void MarkPredicted(EntityUid projectile, int index)
        {
            if (!GunPrediction ||
                predictedProjectiles == null ||
                userSession == null ||
                index >= predictedProjectiles.Count)
            {
                return;
            }

            var predicted = new PredictedProjectileServerComponent
            {
                Shooter = userSession,
                ClientId = predictedProjectiles[index],
                ClientEnt = user,
            };
            AddComp(projectile, predicted, true);
            Dirty(projectile, predicted);
        }
    }

    private void ShootOrThrow(
        EntityUid uid,
        Vector2 mapDirection,
        Vector2 gunVelocity,
        Entity<GunComponent> gun,
        EntityUid? user)
    {
        if (gun.Comp.Target is { } target && !TerminatingOrDeleted(target))
        {
            var targeted = EnsureComp<TargetedProjectileComponent>(uid);
            targeted.Target = target;
            Dirty(uid, targeted);
        }

        if (!HasComp<ProjectileComponent>(uid))
        {
            RemoveShootable(uid);
            ThrowingSystem.TryThrow(
                uid,
                mapDirection,
                gun.Comp.ProjectileSpeedModified,
                user,
                recoil: false,
                rotate: false);
            return;
        }

        ShootProjectile(uid, mapDirection, gunVelocity, gun, user, gun.Comp.ProjectileSpeedModified);
    }

    private static Angle[] LinearSpread(Angle start, Angle end, int intervals)
    {
        var angles = new Angle[intervals];
        DebugTools.Assert(intervals > 1);

        for (var i = 0; i < intervals; i++)
            angles[i] = new Angle(start + (end - start) * i / (intervals - 1));

        return angles;
    }

    protected override void CreateEffect(
        EntityUid gunUid,
        MuzzleFlashEvent message,
        EntityUid? user = null,
        EntityUid? player = null,
        Vector2 offset = default,
        Vector2 originOffset = default)
    {
        var filter = Filter.Pvs(gunUid, entityManager: EntityManager);

        if (TryComp<ActorComponent>(user, out var actor))
            filter.RemovePlayer(actor.PlayerSession);

        if (GunPrediction && TryComp(player, out actor))
            filter.RemovePlayer(actor.PlayerSession);

        RaiseNetworkEvent(message, filter);
    }

    public override void PlayImpactSound(
        EntityUid otherEntity,
        DamageSpecifier? modifiedDamage,
        SoundSpecifier? weaponSound,
        bool forceWeaponSound)
    {
        DebugTools.Assert(!Deleted(otherEntity), "Impact sound entity was deleted");

        var playedSound = false;

        if (!forceWeaponSound &&
            modifiedDamage != null &&
            modifiedDamage.GetTotal() > 0 &&
            TryComp<RangedDamageSoundComponent>(otherEntity, out var rangedSound))
        {
            var type = SharedMeleeWeaponSystem.GetHighestDamageSound(modifiedDamage, ProtoMan);

            if (type != null &&
                rangedSound.SoundTypes?.TryGetValue(type, out var damageSoundType) == true)
            {
                var soundParams = damageSoundType?.Params ?? AudioParams.Default;
                Audio.PlayPvs(
                    damageSoundType,
                    otherEntity,
                    soundParams.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
            else if (type != null &&
                     rangedSound.SoundGroups?.TryGetValue(type, out var damageSoundGroup) == true)
            {
                var soundParams = damageSoundGroup?.Params ?? AudioParams.Default;
                Audio.PlayPvs(
                    damageSoundGroup,
                    otherEntity,
                    soundParams.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
        }

        if (!playedSound && weaponSound != null)
            Audio.PlayPvs(weaponSound, otherEntity);
    }
}
