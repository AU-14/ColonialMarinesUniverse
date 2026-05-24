using Content.Server.Administration.Logs;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Camera;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Effects;
using Content.Shared.Mobs.Components;
using Content.Shared.Throwing;
using Content.Shared.Humanoid;
using Content.Shared.Wires;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Damage.Systems
{
    public sealed partial class DamageOtherOnHitSystem : SharedDamageOtherOnHitSystem
    {
        private static readonly ProtoId<ReagentPrototype> YautjaBloodReagent = "CMUYautjaBlood";

        [Dependency] private IAdminLogManager _adminLogger = default!;
        [Dependency] private GunSystem _guns = default!;
        [Dependency] private DamageableSystem _damageable = default!;
        [Dependency] private DamageExamineSystem _damageExamine = default!;
        [Dependency] private RMCReagentSystem _reagent = default!;
        [Dependency] private SharedCameraRecoilSystem _sharedCameraRecoil = default!;
        [Dependency] private SharedColorFlashEffectSystem _color = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<DamageOtherOnHitComponent, ThrowDoHitEvent>(OnDoHit);
            SubscribeLocalEvent<DamageOtherOnHitComponent, DamageExamineEvent>(OnDamageExamine);
            SubscribeLocalEvent<DamageOtherOnHitComponent, AttemptPacifiedThrowEvent>(OnAttemptPacifiedThrow);
        }

        private void OnDoHit(EntityUid uid, DamageOtherOnHitComponent component, ThrowDoHitEvent args)
        {
            if (TerminatingOrDeleted(args.Target))
                return;

            var damage = GetThrownHitDamage(uid, args.Target, component.Damage);
            var dmg = _damageable.TryChangeDamage(args.Target, damage * _damageable.UniversalThrownDamageModifier, component.IgnoreResistances, origin: args.Component.Thrower, tool: uid);

            // Log damage only for mobs. Useful for when people throw spears at each other, but also avoids log-spam when explosions send glass shards flying.
            if (dmg != null && HasComp<MobStateComponent>(args.Target))
                _adminLogger.Add(LogType.ThrowHit, $"{ToPrettyString(args.Target):target} received {dmg.GetTotal():damage} damage from collision");

            if (dmg is { Empty: false })
            {
                _color.RaiseEffect(GetDamageEffectColor(args.Target), new List<EntityUid>() { args.Target }, Filter.Pvs(args.Target, entityManager: EntityManager));
            }

            _guns.PlayImpactSound(args.Target, dmg, null, false);
            if (TryComp<PhysicsComponent>(uid, out var body) && body.LinearVelocity.LengthSquared() > 0f)
            {
                var direction = body.LinearVelocity.Normalized();
                _sharedCameraRecoil.KickCamera(args.Target, direction);
            }
        }

        private void OnDamageExamine(EntityUid uid, DamageOtherOnHitComponent component, ref DamageExamineEvent args)
        {
            var damage = component.Damage;
            if (TryComp(uid, out YautjaTechItemComponent? tech))
                damage *= tech.DamageMultiplier;

            _damageExamine.AddDamageExamine(args.Message, _damageable.ApplyUniversalAllModifiers(damage * _damageable.UniversalThrownDamageModifier), Loc.GetString("damage-throw"));
        }

        /// <summary>
        /// Prevent players with the Pacified status effect from throwing things that deal damage.
        /// </summary>
        private void OnAttemptPacifiedThrow(Entity<DamageOtherOnHitComponent> ent, ref AttemptPacifiedThrowEvent args)
        {
            args.Cancel("pacified-cannot-throw");
        }

        private DamageSpecifier GetThrownHitDamage(EntityUid uid, EntityUid target, DamageSpecifier damage)
        {
            if (TryComp(uid, out YautjaSmartDiscComponent? disc) &&
                HasComp<HumanoidAppearanceComponent>(target) &&
                !HasComp<YautjaComponent>(target))
            {
                return damage * disc.HumanDamageMultiplier;
            }

            return damage;
        }

        private Color GetDamageEffectColor(EntityUid target)
        {
            if (TryComp(target, out BloodstreamComponent? bloodstream) &&
                bloodstream.BloodReagent == YautjaBloodReagent &&
                _reagent.TryIndex(bloodstream.BloodReagent, out var reagent))
            {
                return reagent.SubstanceColor;
            }

            return Color.Red;
        }
    }
}
